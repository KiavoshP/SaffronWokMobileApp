using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using SaffronWok.Shared;

namespace SaffronWok.Api.Accounts;

internal sealed class CustomerEndpoints(NpgsqlDataSource database, bool allowLocalTest)
{
    private readonly string dummyPasswordHash = BCrypt.Net.BCrypt.HashPassword(
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), 12);

    public void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/customer");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapPost("/register", RegisterAsync).RequireRateLimiting("phone-verification");
        group.MapPost("/login", LoginAsync).RequireRateLimiting("phone-verification");
        group.MapGet("/me", ProfileAsync);
        group.MapPost("/logout", LogoutAsync);
    }

    private async Task<IResult> RegisterAsync(RegisterCustomer request, CancellationToken cancellation)
    {
        var name = request.FullName?.Trim();
        if (name is null || name.Length is < 1 or > 100 || name.Any(char.IsControl) ||
            !ValidPassword(request.Password) || !TryHashToken(request.VerificationReceipt, out var receipt))
            return Results.BadRequest("Provide your name, a valid password, and a current phone verification receipt.");

        var id = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12);
        var (token, hash) = NewSessionToken();
        await using var command = database.CreateCommand("""
            WITH verified AS (
                UPDATE phone_verification_receipts SET consumed_at = now()
                WHERE token_hash = @receipt AND consumed_at IS NULL AND expires_at > clock_timestamp()
                    AND (@allow_test OR NOT is_local_test)
                RETURNING phone, is_local_test
            ), customer AS (
                INSERT INTO customers (id, phone, full_name, password_hash, is_local_test)
                SELECT @id, phone, @name, @password, is_local_test FROM verified
                ON CONFLICT (phone) DO NOTHING
                RETURNING id, phone
            ), session AS (
                INSERT INTO customer_sessions (token_hash, customer_id, expires_at)
                SELECT @hash, id, now() + interval '1 hour' FROM customer
            )
            SELECT phone FROM customer
            """);
        command.Parameters.AddWithValue("receipt", receipt);
        command.Parameters.AddWithValue("allow_test", allowLocalTest);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("password", passwordHash);
        command.Parameters.AddWithValue("hash", hash);

        if (await command.ExecuteScalarAsync(cancellation) is not string phone)
            return Results.BadRequest("Account could not be created. Verify your phone again or sign in.");

        return Results.Ok(new CustomerSession(token, new CustomerProfile(id, name, phone)));
    }

    private async Task<IResult> LoginAsync(SignInCustomer request, CancellationToken cancellation)
    {
        var phone = request.PhoneNumber?.Trim();
        if (!ValidPassword(request.Password) || phone is null || phone.Length is < 3 or > 16)
            return Results.Unauthorized();

        CustomerProfile? customer = null;
        var passwordHash = dummyPasswordHash;
        await using (var command = database.CreateCommand("""
            SELECT id, full_name, password_hash FROM customers
            WHERE phone = @phone AND (@allow_test OR NOT is_local_test)
            """))
        {
            command.Parameters.AddWithValue("phone", phone);
            command.Parameters.AddWithValue("allow_test", allowLocalTest);
            await using var reader = await command.ExecuteReaderAsync(cancellation);
            if (await reader.ReadAsync(cancellation))
            {
                customer = new CustomerProfile(reader.GetGuid(0), reader.GetString(1), phone);
                passwordHash = reader.GetString(2);
            }
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, passwordHash) || customer is null)
            return Results.Unauthorized();

        var (token, hash) = NewSessionToken();
        await using var insert = database.CreateCommand("""
            INSERT INTO customer_sessions (token_hash, customer_id, expires_at)
            VALUES (@hash, @id, now() + interval '1 hour')
            """);
        insert.Parameters.AddWithValue("hash", hash);
        insert.Parameters.AddWithValue("id", customer.Id);
        await insert.ExecuteNonQueryAsync(cancellation);
        return Results.Ok(new CustomerSession(token, customer));
    }

    private async Task<IResult> ProfileAsync(HttpContext context, CancellationToken cancellation)
    {
        if (!TryReadBearer(context, out var hash))
            return Results.Unauthorized();

        await using var command = database.CreateCommand("""
            UPDATE customer_sessions session
            SET expires_at = greatest(session.expires_at, clock_timestamp() + interval '1 hour')
            FROM customers customer
            WHERE session.token_hash = @hash AND customer.id = session.customer_id
                AND session.expires_at > clock_timestamp() AND (@allow_test OR NOT customer.is_local_test)
            RETURNING customer.id, customer.full_name, customer.phone
            """);
        command.Parameters.AddWithValue("hash", hash);
        command.Parameters.AddWithValue("allow_test", allowLocalTest);
        await using var reader = await command.ExecuteReaderAsync(cancellation);
        return await reader.ReadAsync(cancellation)
            ? Results.Ok(new CustomerProfile(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)))
            : Results.Unauthorized();
    }

    private async Task<IResult> LogoutAsync(HttpContext context, CancellationToken cancellation)
    {
        if (!TryReadBearer(context, out var hash))
            return Results.Unauthorized();

        await using var command = database.CreateCommand("DELETE FROM customer_sessions WHERE token_hash = @hash");
        command.Parameters.AddWithValue("hash", hash);
        await command.ExecuteNonQueryAsync(cancellation);
        return Results.NoContent();
    }

    private static bool ValidPassword(string? password) =>
        password is { Length: >= 6 and <= 72 } && !password.Contains('\0') && Encoding.UTF8.GetByteCount(password) <= 72;

    private static (string Token, byte[] Hash) NewSessionToken()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (token, SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    }

    private static bool TryReadBearer(HttpContext context, out byte[] hash)
    {
        if (AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization.ToString(), out var auth) &&
            auth.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            return TryHashToken(auth.Parameter, out hash);

        hash = [];
        return false;
    }

    private static bool TryHashToken(string? token, out byte[] hash)
    {
        if (token is { Length: 64 } && token.All(Uri.IsHexDigit))
        {
            hash = SHA256.HashData(Encoding.ASCII.GetBytes(token));
            return true;
        }

        hash = [];
        return false;
    }
}
