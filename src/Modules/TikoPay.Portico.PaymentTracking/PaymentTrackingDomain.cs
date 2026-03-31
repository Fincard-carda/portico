namespace TikoPay.Portico.PaymentTracking;

public enum PaymentRecordStatus
{
    Pending = 1,
    Matched = 2,
    Started = 3,
    Succeeded = 4,
    Failed = 5,
    Expired = 6
}

public sealed class PaymentRecord
{
    public Guid Id { get; set; }
    public Guid PaymentIntentId { get; set; }
    public string? CitadelPaymentId { get; set; }
    public string? CitadelSessionId { get; set; }
    public PaymentRecordStatus Status { get; set; } = PaymentRecordStatus.Pending;
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public long? ProcessedAmountMinor { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PaymentStatusHistory
{
    public Guid Id { get; set; }
    public Guid PaymentRecordId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}
