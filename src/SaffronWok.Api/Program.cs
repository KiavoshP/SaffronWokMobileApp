using System.Threading.RateLimiting;
using Npgsql;
using SaffronWok.Api.Verification;
using SaffronWok.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("phone-verification", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1)
        }));
});

var connectionString = builder.Configuration.GetConnectionString("Verification");
byte[]? key = null;
if (!string.IsNullOrWhiteSpace(connectionString))
{
    key = Convert.FromBase64String(builder.Configuration["PhoneVerification:SigningKey"] ?? "");
    if (key.Length < 32)
        throw new InvalidOperationException("PhoneVerification:SigningKey must be at least 32 random bytes encoded as Base64.");
    builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
}

var app = builder.Build();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok());

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/events", () => Results.Ok(DevelopmentCatalog.Events));
    app.MapGet("/api/events/{eventId}/menu", (string eventId) =>
        DevelopmentCatalog.Menus.TryGetValue(eventId, out var menu)
            ? Results.Ok(menu)
            : Results.NotFound());
}

if (key is not null)
{
    var database = app.Services.GetRequiredService<NpgsqlDataSource>();
    if (app.Environment.IsDevelopment())
    {
        using var schema = typeof(PhoneVerificationEndpoints).Assembly
            .GetManifestResourceStream("SaffronWok.Api.Verification.schema.sql")!;
        using var reader = new StreamReader(schema);
        await using var create = database.CreateCommand(await reader.ReadToEndAsync());
        await create.ExecuteNonQueryAsync();
    }

    var endpoints = new PhoneVerificationEndpoints(database, key,
        app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("PhoneVerification:EnableLocalTestCode"));
    var verification = app.MapGroup("/api/phone-verification").RequireRateLimiting("phone-verification");
    verification.AddEndpointFilter(async (context, next) =>
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return await next(context);
    });
    verification.MapPost("/request", endpoints.RequestAsync);
    verification.MapPost("/confirm", endpoints.ConfirmAsync);
}

app.Run();
