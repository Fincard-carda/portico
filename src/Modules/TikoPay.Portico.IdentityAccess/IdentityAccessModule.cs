using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace TikoPay.Portico.IdentityAccess;

public sealed class TesseraFederationOptions
{
    public const string SectionName = "TesseraFederation";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Tessera.Auth";
    public string Audience { get; set; } = "Tessera.App";
    public bool RequireHttpsMetadata { get; set; } = true;
}

public sealed class BootstrapAccessOptions
{
    public const string SectionName = "BootstrapAccess";

    public List<BootstrapMerchantAccess> Users { get; set; } = [];
}

public sealed class BootstrapMerchantAccess
{
    public Guid UserId { get; set; }
    public Guid MerchantId { get; set; }
    public string Role { get; set; } = PorticoRoles.ReadOnly;
    public string Scope { get; set; } = "merchant";
    public List<Guid> BranchIds { get; set; } = [];
    public List<Guid> TerminalIds { get; set; } = [];
}

public sealed record MerchantAccessContext(
    Guid MerchantId,
    string Role,
    IReadOnlyCollection<Guid> BranchIds,
    IReadOnlyCollection<Guid> TerminalIds,
    string Scope);

public static class TesseraClaimNames
{
    public const string PhoneNumber = "phone_number";
    public const string TenantId = "tenant_id";
}

public static class PorticoClaimNames
{
    public const string MerchantId = "portico_merchant_id";
    public const string BranchId = "portico_branch_id";
    public const string TerminalId = "portico_terminal_id";
    public const string Scope = "portico_scope";
}

public static class PorticoRoles
{
    public const string SuperUser = "SuperUser";
    public const string Admin = "Admin";
    public const string Cashier = "Cashier";
    public const string ReadOnly = "ReadOnly";
}

public static class PorticoPolicies
{
    public const string ReadAccess = "PorticoReadAccess";
    public const string CashierAccess = "PorticoCashierAccess";
    public const string AdminAccess = "PorticoAdminAccess";
    public const string SuperUserAccess = "PorticoSuperUserAccess";
}

public interface IMerchantAccessProvider
{
    ValueTask<IReadOnlyCollection<MerchantAccessContext>> GetAccessContextsAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class BootstrapMerchantAccessProvider(IOptions<BootstrapAccessOptions> options) : IMerchantAccessProvider
{
    private readonly BootstrapAccessOptions _options = options.Value;

    public ValueTask<IReadOnlyCollection<MerchantAccessContext>> GetAccessContextsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var contexts = _options.Users
            .Where(entry => entry.UserId == userId)
            .Select(entry => new MerchantAccessContext(
                entry.MerchantId,
                entry.Role,
                entry.BranchIds,
                entry.TerminalIds,
                entry.Scope))
            .ToArray();

        return ValueTask.FromResult<IReadOnlyCollection<MerchantAccessContext>>(contexts);
    }
}

public sealed class BootstrapMerchantClaimsTransformation(IMerchantAccessProvider accessProvider) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        if (identity.Claims.Any(claim => claim.Type == ClaimTypes.Role))
        {
            return principal;
        }

        if (!principal.TryGetUserId(out var userId))
        {
            return principal;
        }

        var contexts = await accessProvider.GetAccessContextsAsync(userId, CancellationToken.None);

        foreach (var context in contexts)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, context.Role));
            identity.AddClaim(new Claim(PorticoClaimNames.MerchantId, context.MerchantId.ToString()));
            identity.AddClaim(new Claim(PorticoClaimNames.Scope, context.Scope));

            foreach (var branchId in context.BranchIds.Distinct())
            {
                identity.AddClaim(new Claim(PorticoClaimNames.BranchId, branchId.ToString()));
            }

            foreach (var terminalId in context.TerminalIds.Distinct())
            {
                identity.AddClaim(new Claim(PorticoClaimNames.TerminalId, terminalId.ToString()));
            }
        }

        return principal;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var rawValue = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(rawValue, out userId);
    }

    public static string? GetPhoneNumber(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(TesseraClaimNames.PhoneNumber);

    public static string? GetDisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue("name") ?? principal.Identity?.Name;

    public static IReadOnlyCollection<string> GetPorticoRoles(this ClaimsPrincipal principal) =>
        principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyCollection<Guid> GetBranchIds(this ClaimsPrincipal principal) =>
        principal.FindAll(PorticoClaimNames.BranchId)
            .Select(claim => Guid.TryParse(claim.Value, out var value) ? value : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();

    public static IReadOnlyCollection<Guid> GetTerminalIds(this ClaimsPrincipal principal) =>
        principal.FindAll(PorticoClaimNames.TerminalId)
            .Select(claim => Guid.TryParse(claim.Value, out var value) ? value : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();

    public static IReadOnlyCollection<MerchantAccessContext> GetMerchantAccessContexts(this ClaimsPrincipal principal)
    {
        var roles = principal.GetPorticoRoles();
        var branchIds = principal.GetBranchIds();
        var terminalIds = principal.GetTerminalIds();

        return principal.FindAll(PorticoClaimNames.MerchantId)
            .Select(claim => Guid.TryParse(claim.Value, out var merchantId) ? merchantId : Guid.Empty)
            .Where(merchantId => merchantId != Guid.Empty)
            .Distinct()
            .Select((merchantId, index) => new MerchantAccessContext(
                merchantId,
                roles.ElementAtOrDefault(index) ?? roles.FirstOrDefault() ?? PorticoRoles.ReadOnly,
                branchIds,
                terminalIds,
                principal.FindAll(PorticoClaimNames.Scope).ElementAtOrDefault(index)?.Value ?? "merchant"))
            .ToArray();
    }
}

public static class PorticoIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddPorticoIdentityAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TesseraFederationOptions>(configuration.GetSection(TesseraFederationOptions.SectionName));
        services.Configure<BootstrapAccessOptions>(configuration.GetSection(BootstrapAccessOptions.SectionName));

        var federationOptions = configuration.GetSection(TesseraFederationOptions.SectionName).Get<TesseraFederationOptions>()
            ?? new TesseraFederationOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = federationOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = federationOptions.Issuer,
                    ValidAudience = federationOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(federationOptions.Secret)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(PorticoPolicies.ReadAccess, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(PorticoRoles.ReadOnly, PorticoRoles.Cashier, PorticoRoles.Admin, PorticoRoles.SuperUser));

            options.AddPolicy(PorticoPolicies.CashierAccess, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(PorticoRoles.Cashier, PorticoRoles.Admin, PorticoRoles.SuperUser));

            options.AddPolicy(PorticoPolicies.AdminAccess, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(PorticoRoles.Admin, PorticoRoles.SuperUser));

            options.AddPolicy(PorticoPolicies.SuperUserAccess, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(PorticoRoles.SuperUser));
        });

        services.AddSingleton<IMerchantAccessProvider, BootstrapMerchantAccessProvider>();
        services.AddTransient<IClaimsTransformation, BootstrapMerchantClaimsTransformation>();

        return services;
    }
}
