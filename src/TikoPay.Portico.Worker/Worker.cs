using TikoPay.Portico.BuildingBlocks;
using TikoPay.Portico.Contracts;
using TikoPay.Portico.Persistence;
using System.Text.Json;

namespace TikoPay.Portico.Worker;

public sealed class Worker(
    IServiceScopeFactory scopeFactory,
    IPorticoMessageBus messageBus,
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
                var consumedMessages = await messageBus.PullCitadelMessagesIntoInboxAsync(stoppingToken);
                var publishedMessages = await messageBus.PublishPendingOutboxAsync(stoppingToken);

                await using var scope = scopeFactory.CreateAsyncScope();
                var lifecycleService = scope.ServiceProvider.GetRequiredService<IPaymentIntentLifecycleService>();
                var inboxService = scope.ServiceProvider.GetRequiredService<IIntegrationInboxService>();
                var citadelPaymentProcessor = scope.ServiceProvider.GetRequiredService<ICitadelPaymentEventProcessor>();
                var projectionService = scope.ServiceProvider.GetRequiredService<IDashboardProjectionService>();
                var realtimeNotifier = scope.ServiceProvider.GetRequiredService<IPorticoRealtimeNotifier>();

                var expiredIntents = await lifecycleService.ExpireDueIntentsAsync(stoppingToken);
                var processedInboxMessages = await ProcessInboxMessagesAsync(
                    inboxService,
                    citadelPaymentProcessor,
                    stoppingToken);

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
                    "{ServiceName} heartbeat at {HeartbeatAt}. Expired intents: {ExpiredCount}. Inbox consumed: {ConsumedCount}. Inbox processed: {ProcessedCount}. Outbox published: {PublishedCount}",
                    "TikoPay.Portico.Worker",
                    DateTimeOffset.UtcNow,
                    expiredIntents.Count,
                    consumedMessages,
                    processedInboxMessages,
                    publishedMessages);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "{ServiceName} iteration failed", "TikoPay.Portico.Worker");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private static async Task<int> ProcessInboxMessagesAsync(
        IIntegrationInboxService inboxService,
        ICitadelPaymentEventProcessor citadelPaymentProcessor,
        CancellationToken cancellationToken)
    {
        var pendingMessages = await inboxService.GetPendingAsync(25, cancellationToken);
        var processedCount = 0;

        foreach (var message in pendingMessages)
        {
            try
            {
                switch (message.MessageType)
                {
                    case PorticoIntegrationMessageTypes.CitadelPaymentMatched:
                        await citadelPaymentProcessor.HandleAsync(
                            Deserialize<CitadelPaymentExecutionMatched>(message.PayloadJson),
                            cancellationToken);
                        break;
                    case PorticoIntegrationMessageTypes.CitadelPaymentStarted:
                        await citadelPaymentProcessor.HandleAsync(
                            Deserialize<CitadelPaymentExecutionStarted>(message.PayloadJson),
                            cancellationToken);
                        break;
                    case PorticoIntegrationMessageTypes.CitadelPaymentSucceeded:
                        await citadelPaymentProcessor.HandleAsync(
                            Deserialize<CitadelPaymentExecutionSucceeded>(message.PayloadJson),
                            cancellationToken);
                        break;
                    case PorticoIntegrationMessageTypes.CitadelPaymentFailed:
                        await citadelPaymentProcessor.HandleAsync(
                            Deserialize<CitadelPaymentExecutionFailed>(message.PayloadJson),
                            cancellationToken);
                        break;
                    case PorticoIntegrationMessageTypes.CitadelPaymentExpired:
                        await citadelPaymentProcessor.HandleAsync(
                            Deserialize<CitadelPaymentExecutionExpired>(message.PayloadJson),
                            cancellationToken);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported integration message type '{message.MessageType}'.");
                }

                await inboxService.MarkProcessedAsync(message.Id, cancellationToken);
                processedCount++;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                await inboxService.MarkFailedAsync(message.Id, ex.Message, cancellationToken);
                throw;
            }
        }

        return processedCount;
    }

    private static TMessage Deserialize<TMessage>(string payloadJson)
    {
        return JsonSerializer.Deserialize<TMessage>(payloadJson)
            ?? throw new InvalidOperationException($"Failed to deserialize integration payload '{typeof(TMessage).Name}'.");
    }
}
