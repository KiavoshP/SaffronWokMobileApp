using System.Threading.RateLimiting;
using Npgsql;
using SaffronWok.Api.Accounts;
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

var workerToken = builder.Configuration["SmsQueue:WorkerToken"];
if (workerToken is not null && (key is null || Convert.FromBase64String(workerToken).Length < 32))
    throw new InvalidOperationException("SMS queue configuration requires the verification database and a Base64 worker token of at least 32 random bytes.");

var app = builder.Build();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok());

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/bootstrap", (string? eventId) =>
    {
        eventId ??= DevelopmentCatalog.Events.FirstOrDefault(item => item.IsServing)?.Id;
        var menu = eventId is null ? null : DevelopmentCatalog.Menus.GetValueOrDefault(eventId);
        return Results.Ok(new DiscoveryBootstrap(DevelopmentCatalog.Events, menu, DevelopmentCatalog.Specials));
    });
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

    var allowLocalTest = app.Environment.IsDevelopment() &&
        app.Configuration.GetValue<bool>("PhoneVerification:EnableLocalTestCode");
    new CustomerEndpoints(database, allowLocalTest).Map(app);
    var smsQueue = workerToken is null ? null : new PhoneVerificationSmsQueue(database, key, workerToken);
    smsQueue?.Map(app);
    var endpoints = new PhoneVerificationEndpoints(database, key, smsQueue, allowLocalTest);
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
