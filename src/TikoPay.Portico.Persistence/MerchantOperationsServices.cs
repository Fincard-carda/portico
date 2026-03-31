using Microsoft.EntityFrameworkCore;
using TikoPay.Portico.BuildingBlocks;
using TikoPay.Portico.Contracts;
using TikoPay.Portico.PaymentIntents;
using TikoPay.Portico.PaymentTracking;
using TikoPay.Portico.Reporting;

namespace TikoPay.Portico.Persistence;

public sealed record ExpiredPaymentIntentResult(
    Guid IntentId,
    Guid MerchantId,
    Guid BranchId,
    Guid TerminalId,
    string IntentReference);

public interface IDashboardProjectionService
{
    Task<DashboardSummaryProjection> RefreshSummaryAsync(Guid merchantId, CancellationToken cancellationToken);
}

public interface IPaymentIntentLifecycleService
{
    Task<IReadOnlyCollection<ExpiredPaymentIntentResult>> ExpireDueIntentsAsync(CancellationToken cancellationToken);
}

public interface ICitadelPaymentEventProcessor
{
    Task HandleAsync(CitadelPaymentExecutionMatched paymentMatched, CancellationToken cancellationToken);
    Task HandleAsync(CitadelPaymentExecutionStarted paymentStarted, CancellationToken cancellationToken);
    Task HandleAsync(CitadelPaymentExecutionSucceeded paymentSucceeded, CancellationToken cancellationToken);
    Task HandleAsync(CitadelPaymentExecutionFailed paymentFailed, CancellationToken cancellationToken);
    Task HandleAsync(CitadelPaymentExecutionExpired paymentExpired, CancellationToken cancellationToken);
}

public sealed class DashboardProjectionService(PorticoDbContext dbContext) : IDashboardProjectionService
{
    public async Task<DashboardSummaryProjection> RefreshSummaryAsync(Guid merchantId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var successPayments = await (
            from payment in dbContext.PaymentRecords.AsNoTracking()
            join intent in dbContext.PaymentIntents.AsNoTracking() on payment.PaymentIntentId equals intent.Id
            where intent.MerchantId == merchantId && payment.Status == PaymentRecordStatus.Succeeded
            select payment.ProcessedAmountMinor ?? intent.AmountMinor)
            .ToArrayAsync(cancellationToken);

        var failedCount = await (
            from payment in dbContext.PaymentRecords.AsNoTracking()
            join intent in dbContext.PaymentIntents.AsNoTracking() on payment.PaymentIntentId equals intent.Id
            where intent.MerchantId == merchantId && payment.Status == PaymentRecordStatus.Failed
            select payment.Id)
            .CountAsync(cancellationToken);

        var pendingCount = await dbContext.PaymentIntents
            .AsNoTracking()
            .Where(intent => intent.MerchantId == merchantId &&
                             (intent.Status == PaymentIntentStatus.Pending || intent.Status == PaymentIntentStatus.Active))
            .CountAsync(cancellationToken);

        var summary = await dbContext.DashboardSummaryProjections
            .FirstOrDefaultAsync(item => item.MerchantId == merchantId && item.BusinessDate == today, cancellationToken);

        if (summary is null)
        {
            summary = new DashboardSummaryProjection
            {
                MerchantId = merchantId,
                BusinessDate = today
            };

            dbContext.DashboardSummaryProjections.Add(summary);
        }

        summary.SuccessCount = successPayments.Length;
        summary.FailedCount = failedCount;
        summary.PendingCount = pendingCount;
        summary.TotalSuccessAmountMinor = successPayments.Sum();
        summary.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return summary;
    }
}

public sealed class PaymentIntentLifecycleService(PorticoDbContext dbContext) : IPaymentIntentLifecycleService
{
    public async Task<IReadOnlyCollection<ExpiredPaymentIntentResult>> ExpireDueIntentsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var expiredIntents = await dbContext.PaymentIntents
            .Where(intent => intent.ExpiresAt <= now &&
                             (intent.Status == PaymentIntentStatus.Pending || intent.Status == PaymentIntentStatus.Active))
            .ToArrayAsync(cancellationToken);

        if (expiredIntents.Length == 0)
        {
            return [];
        }

        foreach (var intent in expiredIntents)
        {
            intent.Status = PaymentIntentStatus.Expired;
            intent.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return expiredIntents
            .Select(intent => new ExpiredPaymentIntentResult(
                intent.Id,
                intent.MerchantId,
                intent.BranchId,
                intent.TerminalId,
                intent.IntentReference))
            .ToArray();
    }
}

public sealed class CitadelPaymentEventProcessor(
    PorticoDbContext dbContext,
    IDashboardProjectionService projectionService,
    IPorticoRealtimeNotifier realtimeNotifier) : ICitadelPaymentEventProcessor
{
    public async Task HandleAsync(CitadelPaymentExecutionMatched paymentMatched, CancellationToken cancellationToken)
    {
        var intent = await dbContext.PaymentIntents
            .FirstOrDefaultAsync(item => item.Id == paymentMatched.PaymentIntentId, cancellationToken);

        if (intent is null)
        {
            return;
        }

        var paymentRecord = await dbContext.PaymentRecords
            .FirstOrDefaultAsync(item => item.PaymentIntentId == paymentMatched.PaymentIntentId, cancellationToken);

        if (paymentRecord is null)
        {
            paymentRecord = new PaymentRecord
            {
                Id = Guid.NewGuid(),
                PaymentIntentId = intent.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            dbContext.PaymentRecords.Add(paymentRecord);
        }

        var oldStatus = paymentRecord.Status.ToString();
        paymentRecord.Status = PaymentRecordStatus.Matched;
        paymentRecord.CitadelPaymentId = paymentMatched.CitadelPaymentId;
        paymentRecord.CitadelSessionId = paymentMatched.CitadelSessionId;
        paymentRecord.UpdatedAt = DateTimeOffset.UtcNow;

        AppendStatusHistory(paymentRecord.Id, oldStatus, PaymentRecordStatus.Matched.ToString(), paymentMatched.CitadelPaymentId, paymentMatched.OccurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        await NotifyPaymentStatusChangedAsync(
            intent,
            paymentRecord,
            new
            {
                paymentId = paymentRecord.Id,
                intentId = intent.Id,
                newStatus = PaymentRecordStatus.Matched.ToString(),
                citadelPaymentId = paymentRecord.CitadelPaymentId
            },
            cancellationToken);
    }

    public async Task HandleAsync(CitadelPaymentExecutionStarted paymentStarted, CancellationToken cancellationToken)
    {
        var intent = await dbContext.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == paymentStarted.PaymentIntentId, cancellationToken);

        if (intent is null)
        {
            return;
        }

        var paymentRecord = await dbContext.PaymentRecords
            .FirstOrDefaultAsync(item => item.PaymentIntentId == paymentStarted.PaymentIntentId, cancellationToken);

        if (paymentRecord is null)
        {
            return;
        }

        var oldStatus = paymentRecord.Status.ToString();
        paymentRecord.Status = PaymentRecordStatus.Started;
        paymentRecord.UpdatedAt = DateTimeOffset.UtcNow;

        AppendStatusHistory(paymentRecord.Id, oldStatus, PaymentRecordStatus.Started.ToString(), paymentStarted.CitadelPaymentId, paymentStarted.OccurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        await NotifyPaymentStatusChangedAsync(
            intent,
            paymentRecord,
            new
            {
                paymentId = paymentRecord.Id,
                intentId = intent.Id,
                newStatus = PaymentRecordStatus.Started.ToString(),
                occurredAt = paymentStarted.OccurredAt
            },
            cancellationToken);
    }

    public async Task HandleAsync(CitadelPaymentExecutionSucceeded paymentSucceeded, CancellationToken cancellationToken)
    {
        var paymentRecord = await dbContext.PaymentRecords
            .FirstOrDefaultAsync(item => item.PaymentIntentId == paymentSucceeded.PaymentIntentId, cancellationToken);

        if (paymentRecord is null)
        {
            return;
        }

        var oldStatus = paymentRecord.Status.ToString();
        paymentRecord.Status = PaymentRecordStatus.Succeeded;
        paymentRecord.CitadelPaymentId = paymentSucceeded.CitadelPaymentId;
        paymentRecord.ProcessedAmountMinor = paymentSucceeded.AmountMinor ?? paymentRecord.ProcessedAmountMinor;
        paymentRecord.ProcessedAt = paymentSucceeded.OccurredAt;
        paymentRecord.UpdatedAt = DateTimeOffset.UtcNow;

        var intent = await dbContext.PaymentIntents
            .FirstOrDefaultAsync(item => item.Id == paymentSucceeded.PaymentIntentId, cancellationToken);

        if (intent is null)
        {
            return;
        }

        intent.Status = PaymentIntentStatus.Completed;
        intent.UpdatedAt = DateTimeOffset.UtcNow;

        AppendStatusHistory(paymentRecord.Id, oldStatus, PaymentRecordStatus.Succeeded.ToString(), paymentSucceeded.CitadelPaymentId, paymentSucceeded.OccurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        var summary = await projectionService.RefreshSummaryAsync(intent.MerchantId, cancellationToken);

        await realtimeNotifier.NotifyAsync(
            new PorticoRealtimeEvent(
                "paymentStatusChanged",
                intent.MerchantId,
                intent.BranchId,
                intent.TerminalId,
                new
                {
                    paymentId = paymentRecord.Id,
                    intentId = intent.Id,
                    newStatus = PaymentRecordStatus.Succeeded.ToString(),
                    processedAt = paymentRecord.ProcessedAt
                }),
            cancellationToken);

        await NotifyDashboardSummaryChangedAsync(intent, summary, cancellationToken);
    }

    public async Task HandleAsync(CitadelPaymentExecutionFailed paymentFailed, CancellationToken cancellationToken)
    {
        var paymentRecord = await dbContext.PaymentRecords
            .FirstOrDefaultAsync(item => item.PaymentIntentId == paymentFailed.PaymentIntentId, cancellationToken);

        if (paymentRecord is null)
        {
            return;
        }

        var oldStatus = paymentRecord.Status.ToString();
        paymentRecord.Status = PaymentRecordStatus.Failed;
        paymentRecord.CitadelPaymentId = paymentFailed.CitadelPaymentId;
        paymentRecord.FailureCode = paymentFailed.FailureCode;
        paymentRecord.FailureReason = paymentFailed.FailureReason;
        paymentRecord.ProcessedAt = paymentFailed.OccurredAt;
        paymentRecord.UpdatedAt = DateTimeOffset.UtcNow;

        var intent = await dbContext.PaymentIntents
            .FirstOrDefaultAsync(item => item.Id == paymentFailed.PaymentIntentId, cancellationToken);

        if (intent is null)
        {
            return;
        }

        intent.Status = PaymentIntentStatus.Cancelled;
        intent.UpdatedAt = DateTimeOffset.UtcNow;

        AppendStatusHistory(paymentRecord.Id, oldStatus, PaymentRecordStatus.Failed.ToString(), paymentFailed.CitadelPaymentId, paymentFailed.OccurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        var summary = await projectionService.RefreshSummaryAsync(intent.MerchantId, cancellationToken);

        await realtimeNotifier.NotifyAsync(
            new PorticoRealtimeEvent(
                "paymentStatusChanged",
                intent.MerchantId,
                intent.BranchId,
                intent.TerminalId,
                new
                {
                    paymentId = paymentRecord.Id,
                    intentId = intent.Id,
                    newStatus = PaymentRecordStatus.Failed.ToString(),
                    failureCode = paymentRecord.FailureCode
                }),
            cancellationToken);

        await NotifyDashboardSummaryChangedAsync(intent, summary, cancellationToken);
    }

    public async Task HandleAsync(CitadelPaymentExecutionExpired paymentExpired, CancellationToken cancellationToken)
    {
        var paymentRecord = await dbContext.PaymentRecords
            .FirstOrDefaultAsync(item => item.PaymentIntentId == paymentExpired.PaymentIntentId, cancellationToken);

        if (paymentRecord is null)
        {
            return;
        }

        var intent = await dbContext.PaymentIntents
            .FirstOrDefaultAsync(item => item.Id == paymentExpired.PaymentIntentId, cancellationToken);

        if (intent is null)
        {
            return;
        }

        var oldStatus = paymentRecord.Status.ToString();
        paymentRecord.Status = PaymentRecordStatus.Expired;
        paymentRecord.UpdatedAt = DateTimeOffset.UtcNow;

        intent.Status = PaymentIntentStatus.Expired;
        intent.UpdatedAt = DateTimeOffset.UtcNow;

        AppendStatusHistory(paymentRecord.Id, oldStatus, PaymentRecordStatus.Expired.ToString(), paymentExpired.CitadelPaymentId, paymentExpired.OccurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        var summary = await projectionService.RefreshSummaryAsync(intent.MerchantId, cancellationToken);

        await NotifyPaymentStatusChangedAsync(
            intent,
            paymentRecord,
            new
            {
                paymentId = paymentRecord.Id,
                intentId = intent.Id,
                newStatus = PaymentRecordStatus.Expired.ToString()
            },
            cancellationToken);

        await NotifyDashboardSummaryChangedAsync(intent, summary, cancellationToken);
    }

    private void AppendStatusHistory(Guid paymentRecordId, string oldStatus, string newStatus, string? correlationId, DateTimeOffset occurredAt)
    {
        dbContext.PaymentStatusHistory.Add(new PaymentStatusHistory
        {
            Id = Guid.NewGuid(),
            PaymentRecordId = paymentRecordId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Source = "Citadel",
            CorrelationId = correlationId ?? string.Empty,
            OccurredAt = occurredAt
        });
    }

    private async Task NotifyPaymentStatusChangedAsync(
        PaymentIntent intent,
        PaymentRecord paymentRecord,
        object payload,
        CancellationToken cancellationToken)
    {
        await realtimeNotifier.NotifyAsync(
            new PorticoRealtimeEvent(
                "paymentStatusChanged",
                intent.MerchantId,
                intent.BranchId,
                intent.TerminalId,
                payload),
            cancellationToken);
    }

    private async Task NotifyDashboardSummaryChangedAsync(
        PaymentIntent intent,
        DashboardSummaryProjection summary,
        CancellationToken cancellationToken)
    {
        await realtimeNotifier.NotifyAsync(
            new PorticoRealtimeEvent(
                "dashboardSummaryChanged",
                intent.MerchantId,
                intent.BranchId,
                intent.TerminalId,
                new DashboardSummaryDto(
                    summary.SuccessCount,
                    summary.FailedCount,
                    summary.PendingCount,
                    summary.TotalSuccessAmountMinor,
                    summary.SuccessCount == 0 ? 0 : summary.TotalSuccessAmountMinor / summary.SuccessCount)),
            cancellationToken);
    }
}
