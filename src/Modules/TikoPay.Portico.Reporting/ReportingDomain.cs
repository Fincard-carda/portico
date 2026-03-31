namespace TikoPay.Portico.Reporting;

public sealed class DashboardSummaryProjection
{
    public Guid MerchantId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingCount { get; set; }
    public long TotalSuccessAmountMinor { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
