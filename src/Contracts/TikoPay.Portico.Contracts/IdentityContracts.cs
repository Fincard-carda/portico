namespace TikoPay.Portico.Contracts;

public sealed record MerchantAccessContextDto(
    Guid MerchantId,
    string Role,
    IReadOnlyCollection<Guid> BranchIds,
    IReadOnlyCollection<Guid> TerminalIds,
    string Scope);

public sealed record PorticoUserDto(
    Guid UserId,
    string PhoneNumber,
    string? DisplayName);

public sealed record SessionExchangeResponse(
    PorticoUserDto User,
    IReadOnlyCollection<MerchantAccessContextDto> AccessContexts,
    IReadOnlyCollection<string> Roles);
