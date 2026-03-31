namespace TikoPay.Portico.MerchantDirectory;

public sealed class Merchant
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string MerchantCode { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Branch
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Timezone { get; set; } = "UTC";
    public string? AddressJson { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Terminal
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string TerminalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TerminalType { get; set; } = "Web";
    public string Status { get; set; } = "Active";
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
