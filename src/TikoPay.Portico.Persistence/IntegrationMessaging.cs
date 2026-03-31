using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace TikoPay.Portico.Persistence;

public static class PorticoIntegrationMessageTypes
{
    public const string CitadelPaymentMatched = "citadel.payment.matched";
    public const string CitadelPaymentStarted = "citadel.payment.started";
    public const string CitadelPaymentSucceeded = "citadel.payment.succeeded";
    public const string CitadelPaymentFailed = "citadel.payment.failed";
    public const string CitadelPaymentExpired = "citadel.payment.expired";

    public const string PorticoPaymentIntentCreated = "portico.payment-intent.created";
    public const string PorticoPaymentIntentCancelled = "portico.payment-intent.cancelled";
    public const string PorticoPaymentIntentExpired = "portico.payment-intent.expired";
}

public sealed class IntegrationInboxMessage
{
    public Guid Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string LastError { get; set; } = string.Empty;
}

public sealed class IntegrationOutboxMessage
{
    public Guid Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string LastError { get; set; } = string.Empty;
}

public sealed record ReceivedIntegrationMessage(
    string MessageId,
    string MessageType,
    string RoutingKey,
    string? CorrelationId,
    string PayloadJson,
    DateTimeOffset OccurredAt);

public interface IIntegrationInboxService
{
    Task<bool> TryStoreAsync(ReceivedIntegrationMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<IntegrationInboxMessage>> GetPendingAsync(int take, CancellationToken cancellationToken);
    Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken);
}

public interface IIntegrationOutboxService
{
    void Enqueue<TPayload>(
        string messageType,
        string aggregateId,
        string? correlationId,
        TPayload payload,
        DateTimeOffset occurredAt,
        string? routingKey = null);

    Task<IReadOnlyCollection<IntegrationOutboxMessage>> GetPendingAsync(int take, CancellationToken cancellationToken);
    Task MarkPublishedAsync(Guid messageId, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken);
}

public sealed class IntegrationInboxService(PorticoDbContext dbContext) : IIntegrationInboxService
{
    public async Task<bool> TryStoreAsync(ReceivedIntegrationMessage message, CancellationToken cancellationToken)
    {
        var exists = await dbContext.IntegrationInboxMessages
            .AsNoTracking()
            .AnyAsync(item => item.MessageId == message.MessageId, cancellationToken);

        if (exists)
        {
            return false;
        }

        dbContext.IntegrationInboxMessages.Add(new IntegrationInboxMessage
        {
            Id = Guid.NewGuid(),
            MessageId = message.MessageId,
            MessageType = message.MessageType,
            RoutingKey = message.RoutingKey,
            CorrelationId = message.CorrelationId ?? string.Empty,
            PayloadJson = message.PayloadJson,
            OccurredAt = message.OccurredAt,
            ReceivedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<IntegrationInboxMessage>> GetPendingAsync(int take, CancellationToken cancellationToken)
    {
        return await dbContext.IntegrationInboxMessages
            .Where(item => item.ProcessedAt == null)
            .OrderBy(item => item.ReceivedAt)
            .Take(take)
            .ToArrayAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await dbContext.IntegrationInboxMessages
            .FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken);

        if (message is null)
        {
            return;
        }

        message.ProcessedAt = DateTimeOffset.UtcNow;
        message.LastError = string.Empty;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        var message = await dbContext.IntegrationInboxMessages
            .FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken);

        if (message is null)
        {
            return;
        }

        message.LastError = TrimError(error);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string TrimError(string error) => error.Length <= 2048 ? error : error[..2048];
}

public sealed class IntegrationOutboxService(PorticoDbContext dbContext) : IIntegrationOutboxService
{
    public void Enqueue<TPayload>(
        string messageType,
        string aggregateId,
        string? correlationId,
        TPayload payload,
        DateTimeOffset occurredAt,
        string? routingKey = null)
    {
        dbContext.IntegrationOutboxMessages.Add(new IntegrationOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid().ToString("N"),
            MessageType = messageType,
            RoutingKey = string.IsNullOrWhiteSpace(routingKey) ? messageType : routingKey,
            AggregateId = aggregateId,
            CorrelationId = correlationId ?? string.Empty,
            PayloadJson = JsonSerializer.Serialize(payload),
            OccurredAt = occurredAt,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<IReadOnlyCollection<IntegrationOutboxMessage>> GetPendingAsync(int take, CancellationToken cancellationToken)
    {
        return await dbContext.IntegrationOutboxMessages
            .Where(item => item.PublishedAt == null)
            .OrderBy(item => item.CreatedAt)
            .Take(take)
            .ToArrayAsync(cancellationToken);
    }

    public async Task MarkPublishedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await dbContext.IntegrationOutboxMessages
            .FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken);

        if (message is null)
        {
            return;
        }

        message.PublishedAt = DateTimeOffset.UtcNow;
        message.LastError = string.Empty;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        var message = await dbContext.IntegrationOutboxMessages
            .FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken);

        if (message is null)
        {
            return;
        }

        message.LastError = TrimError(error);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string TrimError(string error) => error.Length <= 2048 ? error : error[..2048];
}
