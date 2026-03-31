using TikoPay.Portico.Contracts;
using TikoPay.Portico.Persistence;

namespace TikoPay.Portico.Api;

internal static class InternalCitadelEndpoints
{
    public static IEndpointRouteBuilder MapInternalCitadelEndpoints(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
    {
        var group = endpoints.MapGroup("/internal/citadel");

        group.MapPost("/payments/matched", async (
            HttpContext context,
            CitadelPaymentExecutionMatched request,
            ICitadelPaymentEventProcessor processor,
            CancellationToken cancellationToken) =>
        {
            if (!IsAuthorized(context.Request, configuration))
            {
                return Results.Unauthorized();
            }

            await processor.HandleAsync(request, cancellationToken);
            return Results.Accepted();
        }).WithName("IngestCitadelPaymentMatched");

        group.MapPost("/payments/started", async (
            HttpContext context,
            CitadelPaymentExecutionStarted request,
            ICitadelPaymentEventProcessor processor,
            CancellationToken cancellationToken) =>
        {
            if (!IsAuthorized(context.Request, configuration))
            {
                return Results.Unauthorized();
            }

            await processor.HandleAsync(request, cancellationToken);
            return Results.Accepted();
        }).WithName("IngestCitadelPaymentStarted");

        group.MapPost("/payments/succeeded", async (
            HttpContext context,
            CitadelPaymentExecutionSucceeded request,
            ICitadelPaymentEventProcessor processor,
            CancellationToken cancellationToken) =>
        {
            if (!IsAuthorized(context.Request, configuration))
            {
                return Results.Unauthorized();
            }

            await processor.HandleAsync(request, cancellationToken);
            return Results.Accepted();
        }).WithName("IngestCitadelPaymentSucceeded");

        group.MapPost("/payments/failed", async (
            HttpContext context,
            CitadelPaymentExecutionFailed request,
            ICitadelPaymentEventProcessor processor,
            CancellationToken cancellationToken) =>
        {
            if (!IsAuthorized(context.Request, configuration))
            {
                return Results.Unauthorized();
            }

            await processor.HandleAsync(request, cancellationToken);
            return Results.Accepted();
        }).WithName("IngestCitadelPaymentFailed");

        group.MapPost("/payments/expired", async (
            HttpContext context,
            CitadelPaymentExecutionExpired request,
            ICitadelPaymentEventProcessor processor,
            CancellationToken cancellationToken) =>
        {
            if (!IsAuthorized(context.Request, configuration))
            {
                return Results.Unauthorized();
            }

            await processor.HandleAsync(request, cancellationToken);
            return Results.Accepted();
        }).WithName("IngestCitadelPaymentExpired");

        return endpoints;
    }

    private static bool IsAuthorized(HttpRequest request, IConfiguration configuration)
    {
        var expectedKey = configuration["CitadelIngress:InternalApiKey"];
        var providedKey = request.Headers["X-Portico-Internal-Key"].ToString();

        return !string.IsNullOrWhiteSpace(expectedKey) &&
               string.Equals(expectedKey, providedKey, StringComparison.Ordinal);
    }
}
