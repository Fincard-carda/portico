using TikoPay.Portico.Realtime.Hubs;
using TikoPay.Portico.Realtime;
using TikoPay.Portico.BuildingBlocks;
using TikoPay.Portico.IdentityAccess;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddSignalR();
builder.Services.AddPorticoIdentityAccess(builder.Configuration);
builder.Services.AddPorticoRealtimeDispatch(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new ServiceDescriptor(
    Name: "TikoPay.Portico.Realtime",
    Status: "ok",
    Environment: app.Environment.EnvironmentName,
    UtcNow: DateTimeOffset.UtcNow)))
    .WithName("GetRealtimeRoot");

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .WithName("GetRealtimeLiveness");

app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }))
    .WithName("GetRealtimeReadiness");

app.MapHealthChecks("/health");
app.MapHub<PaymentsHub>("/hubs/payments");
app.MapRealtimeInternalEndpoints(builder.Configuration);

app.Run();

internal sealed record ServiceDescriptor(
    string Name,
    string Status,
    string Environment,
    DateTimeOffset UtcNow);
