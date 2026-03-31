using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TikoPay.Portico.BuildingBlocks;

public sealed class RealtimeDispatchOptions
{
    public const string SectionName = "RealtimeDispatch";

    public string BaseUrl { get; set; } = "https://localhost:7244";
    public string InternalApiKey { get; set; } = "portico-local-internal-key";
}

public sealed record PorticoRealtimeEvent(
    string EventName,
    Guid MerchantId,
    Guid? BranchId,
    Guid? TerminalId,
    object Payload);

public interface IPorticoRealtimeNotifier
{
    Task NotifyAsync(PorticoRealtimeEvent realtimeEvent, CancellationToken cancellationToken);
}

public sealed class PorticoRealtimeNotifier(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<PorticoRealtimeNotifier> logger) : IPorticoRealtimeNotifier
{
    private readonly RealtimeDispatchOptions _options =
        configuration.GetSection(RealtimeDispatchOptions.SectionName).Get<RealtimeDispatchOptions>()
        ?? new RealtimeDispatchOptions();

    public async Task NotifyAsync(PorticoRealtimeEvent realtimeEvent, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/internal/realtime/events")
            {
                Content = JsonContent.Create(realtimeEvent)
            };
            request.Headers.Add("X-Portico-Internal-Key", _options.InternalApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to dispatch realtime event {EventName} for merchant {MerchantId}",
                realtimeEvent.EventName,
                realtimeEvent.MerchantId);
        }
    }
}

public static class RealtimeDispatchServiceCollectionExtensions
{
    public static IServiceCollection AddPorticoRealtimeDispatch(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RealtimeDispatchOptions>(configuration.GetSection(RealtimeDispatchOptions.SectionName));
        services.AddHttpClient<IPorticoRealtimeNotifier, PorticoRealtimeNotifier>();
        return services;
    }
}
