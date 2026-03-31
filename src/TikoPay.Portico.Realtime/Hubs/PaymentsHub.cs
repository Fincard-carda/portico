using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TikoPay.Portico.IdentityAccess;

namespace TikoPay.Portico.Realtime.Hubs;

[Authorize]
public sealed class PaymentsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        foreach (var context in Context.User?.GetMerchantAccessContexts() ?? [])
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"merchant:{context.MerchantId}");

            foreach (var branchId in context.BranchIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"branch:{branchId}");
            }

            foreach (var terminalId in context.TerminalIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"terminal:{terminalId}");
            }
        }

        await base.OnConnectedAsync();
    }
}
