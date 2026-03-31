namespace TikoPay.Portico.Contracts;

public sealed record BranchDto(
    Guid Id,
    Guid MerchantId,
    string BranchCode,
    string Name,
    string Status);

public sealed record TerminalDto(
    Guid Id,
    Guid BranchId,
    string TerminalCode,
    string Name,
    string Status);

public sealed record MerchantUserDto(
    Guid Id,
    string DisplayName,
    string PhoneNumber,
    string? Email,
    string Status,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<Guid> AssignedBranchIds,
    IReadOnlyCollection<Guid> AssignedTerminalIds);

public sealed record CreatePaymentIntentRequest(
    Guid MerchantId,
    Guid BranchId,
    Guid TerminalId,
    long AmountMinor,
    string Currency,
    string? Description,
    string? MerchantReference,
    string Channel,
    int ExpiresInSeconds);

public sealed record PaymentIntentDto(
    Guid Id,
    Guid MerchantId,
    Guid BranchId,
    Guid TerminalId,
    long AmountMinor,
    string Currency,
    string Status,
    string Channel,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    string IntentReference,
    string? MerchantReference,
    string? Description);

public sealed record PaymentIntentDetailDto(
    Guid Id,
    Guid MerchantId,
    Guid BranchId,
    Guid TerminalId,
    Guid CreatedByUserId,
    long AmountMinor,
    string Currency,
    string Status,
    string Channel,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string IntentReference,
    string? MerchantReference,
    string? Description);

public sealed record CreatePaymentIntentResponse(
    Guid IntentId,
    string IntentReference,
    string Status,
    DateTimeOffset ExpiresAt,
    string QrToken,
    string DeepLinkUrl);

public sealed record ResolveCustomerPaymentIntentResponse(
    Guid IntentId,
    string IntentReference,
    string MerchantDisplayName,
    long AmountMinor,
    string Currency,
    string Status,
    DateTimeOffset ExpiresAt,
    string? Description);

public sealed record PaymentDto(
    Guid PaymentId,
    Guid IntentId,
    Guid MerchantId,
    Guid BranchId,
    Guid TerminalId,
    long AmountMinor,
    string Currency,
    string Status,
    string? FailureCode,
    DateTimeOffset? ProcessedAt);

public sealed record PaymentDetailDto(
    Guid PaymentId,
    Guid IntentId,
    Guid MerchantId,
    Guid BranchId,
    Guid TerminalId,
    long AmountMinor,
    string Currency,
    string Status,
    string? CitadelPaymentId,
    string? CitadelSessionId,
    string? FailureCode,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);

public sealed record DashboardSummaryDto(
    int SuccessCount,
    int FailedCount,
    int PendingCount,
    long TotalSuccessAmountMinor,
    long AverageTicketMinor);
