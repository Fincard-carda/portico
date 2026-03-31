using Microsoft.AspNetCore.SignalR;
using TikoPay.Portico.BuildingBlocks;
using TikoPay.Portico.Realtime.Hubs;

namespace TikoPay.Portico.Realtime;

internal static class RealtimeInternalEndpoints
{
    public static IEndpointRouteBuilder MapRealtimeInternalEndpoints(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
    {
        var dispatchOptions = configuration.GetSection(RealtimeDispatchOptions.SectionName).Get<RealtimeDispatchOptions>()
            ?? new RealtimeDispatchOptions();

        endpoints.MapPost("/internal/realtime/events", async (
            HttpContext httpContext,
            IHubContext<PaymentsHub> hubContext,
            PorticoRealtimeEvent realtimeEvent,
            CancellationToken cancellationToken) =>
        {
            var providedKey = httpContext.Request.Headers["X-Portico-Internal-Key"].FirstOrDefault();
            if (!string.Equals(providedKey, dispatchOptions.InternalApiKey, StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }

            await hubContext.Clients.Group($"merchant:{realtimeEvent.MerchantId}")
                .SendAsync(realtimeEvent.EventName, realtimeEvent.Payload, cancellationToken);

            if (realtimeEvent.BranchId is { } branchId)
            {
                await hubContext.Clients.Group($"branch:{branchId}")
                    .SendAsync(realtimeEvent.EventName, realtimeEvent.Payload, cancellationToken);
            }

            if (realtimeEvent.TerminalId is { } terminalId)
            {
                await hubContext.Clients.Group($"terminal:{terminalId}")
                    .SendAsync(realtimeEvent.EventName, realtimeEvent.Payload, cancellationToken);
            }

            return Results.Accepted();
        }).WithName("DispatchRealtimeEvent");

        return endpoints;
    }
}
