namespace TikoPay.Portico.IdentityAccess;

public enum MerchantUserStatus
{
    Pending = 1,
    Active = 2,
    Suspended = 3
}

public sealed class MerchantUser
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ExternalUserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public MerchantUserStatus Status { get; set; } = MerchantUserStatus.Pending;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class MerchantMembership
{
    public Guid Id { get; set; }
    public Guid MerchantUserId { get; set; }
    public Guid MerchantId { get; set; }
    public string Role { get; set; } = PorticoRoles.ReadOnly;
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class UserBranchAssignment
{
    public Guid Id { get; set; }
    public Guid MerchantUserId { get; set; }
    public Guid BranchId { get; set; }
    public string AssignmentType { get; set; } = "Assigned";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class UserTerminalAssignment
{
    public Guid Id { get; set; }
    public Guid MerchantUserId { get; set; }
    public Guid TerminalId { get; set; }
    public string AssignmentType { get; set; } = "Assigned";
    public DateTimeOffset CreatedAt { get; set; }
}
