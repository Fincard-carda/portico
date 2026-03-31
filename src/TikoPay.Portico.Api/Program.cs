using TikoPay.Portico.Api;
using TikoPay.Portico.IdentityAccess;
using TikoPay.Portico.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddPorticoIdentityAccess(builder.Configuration);
builder.Services.AddPorticoPersistence(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new ServiceDescriptor(
    Name: "TikoPay.Portico.Api",
    Status: "ok",
    Environment: app.Environment.EnvironmentName,
    UtcNow: DateTimeOffset.UtcNow)))
    .WithName("GetApiRoot");

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .WithName("GetLiveness");

app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }))
    .WithName("GetReadiness");

app.MapHealthChecks("/health");

app.MapGet("/api/system/info", () => Results.Ok(new ServiceDescriptor(
    Name: "TikoPay.Portico.Api",
    Status: "bootstrap",
    Environment: app.Environment.EnvironmentName,
    UtcNow: DateTimeOffset.UtcNow)))
    .WithName("GetSystemInfo");

app.MapPorticoAuthEndpoints();
app.MapMerchantEndpoints();
app.MapPublicPaymentEndpoints();
app.MapInternalCitadelEndpoints(builder.Configuration);

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PorticoDbContext>();
    await PorticoDevelopmentSeeder.SeedAsync(dbContext);
}

app.Run();

internal sealed record ServiceDescriptor(
    string Name,
    string Status,
    string Environment,
    DateTimeOffset UtcNow);
