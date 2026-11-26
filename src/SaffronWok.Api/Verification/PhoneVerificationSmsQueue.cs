using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace SaffronWok.Api.Verification;

internal sealed class PhoneVerificationSmsQueue(NpgsqlDataSource database, byte[] signingKey, string workerToken)
{
    private const int MaxBatchSize = 50;
    private const int DefaultBatchSize = 10;

    private readonly byte[] encryptionKey = HMACSHA256.HashData(signingKey, "saffronwok:sms-queue:v1"u8);
    private readonly byte[] workerTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(workerToken));

    public void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/sms-queue");
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";
            if (!AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization.ToString(), out var auth) ||
                !auth.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || auth.Parameter is null ||
                !CryptographicOperations.FixedTimeEquals(workerTokenHash,
                    SHA256.HashData(Encoding.UTF8.GetBytes(auth.Parameter))))
            {
                http.Response.Headers.WWWAuthenticate = "Bearer";
                return Results.Unauthorized();
            }

            return await next(context);
        });
        group.MapPost("/claim", ClaimAsync);
        group.MapPost("/{id:guid}/ack", AcknowledgeAsync);
        group.MapPost("/claim-batch", ClaimBatchAsync);
        group.MapPost("/ack-batch", AcknowledgeBatchAsync);
    }

    public byte[] Encrypt(Guid id, string message)
    {
        var plaintext = Encoding.UTF8.GetBytes(message);
        var payload = new byte[12 + 16 + plaintext.Length];
        RandomNumberGenerator.Fill(payload.AsSpan(0, 12));
        using var cipher = new AesGcm(encryptionKey, 16);
        cipher.Encrypt(payload.AsSpan(0, 12), plaintext, payload.AsSpan(28),
            payload.AsSpan(12, 16), id.ToByteArray());
        return payload;
    }

    private string Decrypt(Guid id, byte[] payload)
    {
        var plaintext = new byte[payload.Length - 28];
        using var cipher = new AesGcm(encryptionKey, 16);
        cipher.Decrypt(payload.AsSpan(0, 12), payload.AsSpan(28), payload.AsSpan(12, 16),
            plaintext, id.ToByteArray());
        return Encoding.UTF8.GetString(plaintext);
    }

    private async Task<IResult> ClaimAsync(CancellationToken cancellation)
    {
        var messages = await ClaimMessagesAsync(1, cancellation);
        return messages.Count == 0 ? Results.NoContent() : Results.Ok(messages[0]);
    }

    private async Task<IResult> ClaimBatchAsync(int? limit, CancellationToken cancellation)
    {
        var count = limit ?? DefaultBatchSize;
        if (count is < 1 or > MaxBatchSize)
            return Results.BadRequest($"Limit must be between 1 and {MaxBatchSize}.");

        return Results.Ok(await ClaimMessagesAsync(count, cancellation));
    }

    private async Task<List<ClaimedSms>> ClaimMessagesAsync(int limit, CancellationToken cancellation)
    {
        await using var command = database.CreateCommand("""
            WITH expired AS (
                DELETE FROM phone_verification_sms_queue WHERE expires_at <= now()
            ), next_messages AS (
                SELECT q.id, c.phone FROM phone_verification_sms_queue q
                JOIN phone_verification_challenges c ON c.id = q.id
                WHERE q.expires_at > now() AND (q.leased_until IS NULL OR q.leased_until <= now())
                    AND c.consumed_at IS NULL AND c.attempts_remaining > 0
                ORDER BY q.created_at, q.id
                LIMIT @limit
                FOR UPDATE OF q SKIP LOCKED
            ), claimed AS (
                UPDATE phone_verification_sms_queue q
                SET lease_id = gen_random_uuid(), leased_until = least(now() + interval '30 seconds', q.expires_at)
                FROM next_messages n WHERE q.id = n.id
                RETURNING q.id, q.lease_id, n.phone, q.encrypted_text, q.leased_until, q.expires_at, q.created_at
            )
            SELECT id, lease_id, phone, encrypted_text, leased_until, expires_at
            FROM claimed ORDER BY created_at, id
            """);
        command.Parameters.AddWithValue("limit", limit);

        var messages = new List<ClaimedSms>(limit);
        await using var reader = await command.ExecuteReaderAsync(cancellation);
        while (await reader.ReadAsync(cancellation))
        {
            var id = reader.GetGuid(0);
            messages.Add(new ClaimedSms(id, reader.GetGuid(1), reader.GetString(2),
                Decrypt(id, reader.GetFieldValue<byte[]>(3)), reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return messages;
    }

    private async Task<IResult> AcknowledgeAsync(Guid id, SmsAcknowledgement request, CancellationToken cancellation)
    {
        var acknowledged = await AcknowledgeMessagesAsync([new SmsLease(id, request.LeaseId)], cancellation);
        return acknowledged.Contains(id) ? Results.NoContent() : Results.Conflict();
    }

    private async Task<IResult> AcknowledgeBatchAsync(SmsLease[] leases, CancellationToken cancellation)
    {
        if (leases.Length is < 1 or > MaxBatchSize ||
            leases.Any(lease => lease is null || lease.Id == Guid.Empty || lease.LeaseId == Guid.Empty) ||
            leases.Select(lease => lease.Id).Distinct().Count() != leases.Length)
        {
            return Results.BadRequest($"Supply 1 to {MaxBatchSize} unique message IDs and their lease IDs.");
        }

        var acknowledged = await AcknowledgeMessagesAsync(leases, cancellation);
        return Results.Ok(leases.Select(lease => new { lease.Id, Acknowledged = acknowledged.Contains(lease.Id) }));
    }

    private async Task<HashSet<Guid>> AcknowledgeMessagesAsync(SmsLease[] leases, CancellationToken cancellation)
    {
        await using var command = database.CreateCommand("""
            WITH matching AS (
                SELECT q.id, q.lease_id
                FROM phone_verification_sms_queue q
                JOIN unnest(@ids, @leases) AS requested(id, lease_id)
                    ON q.id = requested.id AND q.lease_id = requested.lease_id
                WHERE q.leased_until > clock_timestamp()
                ORDER BY q.id
                FOR UPDATE OF q
            )
            DELETE FROM phone_verification_sms_queue q USING matching m
            WHERE q.id = m.id AND q.lease_id = m.lease_id AND q.leased_until > clock_timestamp()
            RETURNING q.id
            """);
        command.Parameters.AddWithValue("ids", leases.Select(lease => lease.Id).ToArray());
        command.Parameters.AddWithValue("leases", leases.Select(lease => lease.LeaseId).ToArray());

        var acknowledged = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellation);
        while (await reader.ReadAsync(cancellation))
            acknowledged.Add(reader.GetGuid(0));

        return acknowledged;
    }

    private sealed record ClaimedSms(Guid Id, Guid LeaseId, string PhoneNumber, string Text,
        DateTimeOffset LeaseExpiresAt, DateTimeOffset ExpiresAt);

    internal sealed record SmsAcknowledgement(Guid LeaseId);
    internal sealed record SmsLease(Guid Id, Guid LeaseId);
}
