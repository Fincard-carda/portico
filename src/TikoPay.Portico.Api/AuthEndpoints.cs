using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TikoPay.Portico.Contracts;
using TikoPay.Portico.IdentityAccess;

namespace TikoPay.Portico.Api;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapPorticoAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .RequireAuthorization(PorticoPolicies.ReadAccess);

        group.MapPost("/session/exchange", (HttpContext context) =>
        {
            var response = CreateSessionResponse(context.User);
            return response is null ? Results.Unauthorized() : Results.Ok(response);
        }).WithName("ExchangePorticoSession");

        group.MapGet("/me", (HttpContext context) =>
        {
            var response = CreateSessionResponse(context.User);
            return response is null ? Results.Unauthorized() : Results.Ok(response);
        }).WithName("GetPorticoSession");

        endpoints.MapGet("/api/authorization/policies", [Authorize(Policy = PorticoPolicies.AdminAccess)] () =>
            Results.Ok(new
            {
                policies = new[]
                {
                    PorticoPolicies.ReadAccess,
                    PorticoPolicies.CashierAccess,
                    PorticoPolicies.AdminAccess,
                    PorticoPolicies.SuperUserAccess
                }
            }))
            .WithName("GetAuthorizationPolicies");

        return endpoints;
    }

    private static SessionExchangeResponse? CreateSessionResponse(ClaimsPrincipal principal)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return null;
        }

        var phoneNumber = principal.GetPhoneNumber();
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        var accessContexts = principal.GetMerchantAccessContexts()
            .Select(context => new MerchantAccessContextDto(
                context.MerchantId,
                context.Role,
                context.BranchIds,
                context.TerminalIds,
                context.Scope))
            .ToArray();

        return new SessionExchangeResponse(
            new PorticoUserDto(userId, phoneNumber, principal.GetDisplayName()),
            accessContexts,
            principal.GetPorticoRoles());
    }
}
