namespace TikoPay.Portico.PaymentIntents;

public enum PaymentChannel
{
    Qr = 1,
    DeepLink = 2
}

public enum PaymentIntentStatus
{
    Pending = 1,
    Active = 2,
    Cancelled = 3,
    Expired = 4,
    Completed = 5
}

public sealed class PaymentIntent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid MerchantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid TerminalId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string IntentReference { get; set; } = string.Empty;
    public string? MerchantReference { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? Description { get; set; }
    public PaymentChannel Channel { get; set; } = PaymentChannel.Qr;
    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.Pending;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
