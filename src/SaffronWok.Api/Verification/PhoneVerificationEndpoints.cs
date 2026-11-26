using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using SaffronWok.Shared;

namespace SaffronWok.Api.Verification;

internal sealed partial class PhoneVerificationEndpoints(NpgsqlDataSource database, byte[] signingKey,
    PhoneVerificationSmsQueue? smsQueue, bool localTestEnabled)
{
    private const string LocalTestCode = "021819";

    public async Task RequestAsync(HttpContext context, RequestPhoneCode request)
    {
        var phone = request.PhoneNumber?.Trim();
        if (phone is null || !PhonePattern().IsMatch(phone))
        {
            await Results.BadRequest("Enter a phone number in international format, such as +14045550123.")
                .ExecuteAsync(context);
            return;
        }

        var localTest = AllowsLocalTest(context, phone);
        if (!localTest && smsQueue is null)
        {
            await Results.StatusCode(503).ExecuteAsync(context);
            return;
        }

        var id = Guid.NewGuid();
        var code = GenerateCode();
        var encryptedText = localTest ? null : smsQueue!.Encrypt(id,
            $"Your SaffronWok verification code is {code}. It expires in 5 minutes.");
        await using var command = database.CreateCommand("""
            WITH challenge AS (
                INSERT INTO phone_verification_challenges
                    (id, phone, code_hash, is_local_test, expires_at, attempts_remaining)
                VALUES (@id, @phone, @hash, @local_test, now() + interval '5 minutes', 5)
                ON CONFLICT (phone) DO UPDATE
                SET id = excluded.id, code_hash = excluded.code_hash, is_local_test = excluded.is_local_test,
                    issued_at = now(), expires_at = excluded.expires_at,
                    attempts_remaining = 5, consumed_at = NULL
                WHERE phone_verification_challenges.issued_at <= now() - interval '1 minute'
                RETURNING id, expires_at
            ), queued AS (
                INSERT INTO phone_verification_sms_queue (id, encrypted_text, expires_at)
                SELECT id, @text, expires_at FROM challenge WHERE @text IS NOT NULL
            )
            SELECT count(*) FROM challenge
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("phone", phone);
        command.Parameters.AddWithValue("hash", HashCode(id, code));
        command.Parameters.AddWithValue("local_test", localTest);
        command.Parameters.AddWithValue("text", NpgsqlTypes.NpgsqlDbType.Bytea,
            (object?)encryptedText ?? DBNull.Value);

        var cancellation = context.RequestAborted;
        if ((long)(await command.ExecuteScalarAsync(cancellation))! == 0)
        {
            await Results.StatusCode(429).ExecuteAsync(context);
            return;
        }

        await Results.Ok(new PhoneCodeChallenge(id)).ExecuteAsync(context);
    }

    public async Task ConfirmAsync(HttpContext context, ConfirmPhoneCode request)
    {
        if (request.Code is null || !CodePattern().IsMatch(request.Code))
        {
            await Results.BadRequest("Enter six digits.").ExecuteAsync(context);
            return;
        }

        var cancellation = context.RequestAborted;
        await using var connection = await database.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);
        await using var lookup = new NpgsqlCommand("""
            SELECT phone, code_hash, is_local_test FROM phone_verification_challenges
            WHERE id = @id AND expires_at > clock_timestamp() AND consumed_at IS NULL
                AND attempts_remaining > 0
            FOR UPDATE
            """, connection, transaction);
        lookup.Parameters.AddWithValue("id", request.ChallengeId);

        string? phone = null;
        byte[]? expectedHash = null;
        bool localTest = false;
        await using (var reader = await lookup.ExecuteReaderAsync(cancellation))
        {
            if (await reader.ReadAsync(cancellation))
            {
                phone = reader.GetString(0);
                expectedHash = reader.GetFieldValue<byte[]>(1);
                localTest = reader.GetBoolean(2);
            }
        }

        if (phone is null || (localTest && !AllowsLocalTest(context, phone)))
        {
            await transaction.RollbackAsync(cancellation);
            await InvalidCode().ExecuteAsync(context);
            return;
        }

        var valid = localTest && request.Code == LocalTestCode ||
            request.Code != LocalTestCode &&
            CryptographicOperations.FixedTimeEquals(expectedHash!, HashCode(request.ChallengeId, request.Code));
        await using var update = new NpgsqlCommand("""
            UPDATE phone_verification_challenges
            SET attempts_remaining = attempts_remaining - 1,
                consumed_at = CASE WHEN @valid THEN now() ELSE consumed_at END
            WHERE id = @id
            """, connection, transaction);
        update.Parameters.AddWithValue("valid", valid);
        update.Parameters.AddWithValue("id", request.ChallengeId);
        await update.ExecuteNonQueryAsync(cancellation);

        if (!valid)
        {
            await transaction.CommitAsync(cancellation);
            await InvalidCode().ExecuteAsync(context);
            return;
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiry = DateTimeOffset.UtcNow.AddMinutes(10);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO phone_verification_receipts (token_hash, phone, is_local_test, expires_at)
            VALUES (@hash, @phone, @local_test, @expiry)
            """, connection, transaction);
        insert.Parameters.AddWithValue("hash", SHA256.HashData(Encoding.ASCII.GetBytes(token)));
        insert.Parameters.AddWithValue("phone", phone);
        insert.Parameters.AddWithValue("local_test", localTest);
        insert.Parameters.AddWithValue("expiry", expiry);
        await insert.ExecuteNonQueryAsync(cancellation);
        await transaction.CommitAsync(cancellation);

        await Results.Ok(new PhoneVerificationReceipt(token, expiry)).ExecuteAsync(context);
    }

    private bool AllowsLocalTest(HttpContext context, string phone) =>
        localTestEnabled && context.Connection.RemoteIpAddress is { } address &&
        IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address) &&
        TestPhonePattern().IsMatch(phone);

    private byte[] HashCode(Guid id, string code) =>
        HMACSHA256.HashData(signingKey, Encoding.ASCII.GetBytes($"{id:N}:{code}"));

    private static string GenerateCode()
    {
        string code;
        do
        {
            code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6",
                System.Globalization.CultureInfo.InvariantCulture);
        } while (code == LocalTestCode);
        return code;
    }

    private static IResult InvalidCode() => Results.BadRequest("The code is invalid or expired.");

    [GeneratedRegex(@"\A\+[1-9][0-9]{1,14}\z")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\A[0-9]{6}\z")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"\A\+1[2-9][0-9]{2}55501[0-9]{2}\z")]
    private static partial Regex TestPhonePattern();
}
