using TikoPay.Portico.BuildingBlocks;
using TikoPay.Portico.Contracts;
using TikoPay.Portico.Persistence;

namespace TikoPay.Portico.Worker;

public sealed class Worker(
    IServiceScopeFactory scopeFactory,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "{ServiceName} started in bootstrap mode at {StartedAt}",
            "TikoPay.Portico.Worker",
            DateTimeOffset.UtcNow);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var lifecycleService = scope.ServiceProvider.GetRequiredService<IPaymentIntentLifecycleService>();
                var projectionService = scope.ServiceProvider.GetRequiredService<IDashboardProjectionService>();
                var realtimeNotifier = scope.ServiceProvider.GetRequiredService<IPorticoRealtimeNotifier>();

                var expiredIntents = await lifecycleService.ExpireDueIntentsAsync(stoppingToken);
                foreach (var expiredIntent in expiredIntents)
                {
                    var summary = await projectionService.RefreshSummaryAsync(expiredIntent.MerchantId, stoppingToken);

                    await realtimeNotifier.NotifyAsync(
                        new PorticoRealtimeEvent(
                            "paymentIntentUpdated",
                            expiredIntent.MerchantId,
                            expiredIntent.BranchId,
                            expiredIntent.TerminalId,
                            new
                            {
                                intentId = expiredIntent.IntentId,
                                intentReference = expiredIntent.IntentReference,
                                status = "Expired"
                            }),
                        stoppingToken);

                    await realtimeNotifier.NotifyAsync(
                        new PorticoRealtimeEvent(
                            "dashboardSummaryChanged",
                            expiredIntent.MerchantId,
                            expiredIntent.BranchId,
                            expiredIntent.TerminalId,
                            new DashboardSummaryDto(
                                summary.SuccessCount,
                                summary.FailedCount,
                                summary.PendingCount,
                                summary.TotalSuccessAmountMinor,
                                summary.SuccessCount == 0 ? 0 : summary.TotalSuccessAmountMinor / summary.SuccessCount)),
                        stoppingToken);
                }

                logger.LogInformation(
                    "{ServiceName} heartbeat at {HeartbeatAt}. Expired intents processed: {ExpiredCount}",
                    "TikoPay.Portico.Worker",
                    DateTimeOffset.UtcNow,
                    expiredIntents.Count);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "{ServiceName} iteration failed", "TikoPay.Portico.Worker");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
