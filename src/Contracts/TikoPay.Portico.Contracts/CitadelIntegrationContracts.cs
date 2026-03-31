namespace TikoPay.Portico.Contracts;

public sealed record CitadelPaymentExecutionMatched(
    string CitadelPaymentId,
    string CitadelSessionId,
    Guid MerchantId,
    Guid BranchId,
    Guid TerminalId,
    Guid PaymentIntentId,
    string MerchantReference,
    DateTimeOffset OccurredAt);

public sealed record CitadelPaymentExecutionStarted(
    string CitadelPaymentId,
    Guid PaymentIntentId,
    DateTimeOffset OccurredAt);

public sealed record CitadelPaymentExecutionSucceeded(
    string CitadelPaymentId,
    Guid PaymentIntentId,
    long? AmountMinor,
    string? Currency,
    DateTimeOffset OccurredAt);

public sealed record CitadelPaymentExecutionFailed(
    string CitadelPaymentId,
    Guid PaymentIntentId,
    string FailureCode,
    string FailureReason,
    DateTimeOffset OccurredAt);

public sealed record CitadelPaymentExecutionExpired(
    string CitadelPaymentId,
    Guid PaymentIntentId,
    DateTimeOffset OccurredAt);
