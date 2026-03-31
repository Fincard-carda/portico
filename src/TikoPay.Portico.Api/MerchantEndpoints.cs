using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TikoPay.Portico.BuildingBlocks;
using TikoPay.Portico.Contracts;
using TikoPay.Portico.IdentityAccess;
using TikoPay.Portico.PaymentIntents;
using TikoPay.Portico.Persistence;
using TikoPay.Portico.PaymentTracking;

namespace TikoPay.Portico.Api;

internal static class MerchantEndpoints
{
    public static IEndpointRouteBuilder MapMerchantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/branches", [Authorize(Policy = PorticoPolicies.ReadAccess)] async (
            HttpContext context,
            PorticoDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var merchantId = GetSelectedMerchantId(context.User);
            if (merchantId is null)
            {
                return Results.Forbid();
            }

            var accessContexts = GetMerchantAccessContexts(context.User, merchantId.Value);
            if (accessContexts.Count == 0)
            {
                return Results.Forbid();
            }

            var hasFullMerchantAccess = HasFullMerchantAccess(accessContexts);
            var scopedBranchIds = GetScopedBranchIds(accessContexts);

            var query = dbContext.Branches
                .AsNoTracking()
                .Where(branch => branch.MerchantId == merchantId.Value);

            if (!hasFullMerchantAccess && scopedBranchIds.Count > 0)
            {
                query = query.Where(branch => scopedBranchIds.Contains(branch.Id));
            }

            var branches = await query
                .OrderBy(branch => branch.Name)
                .Select(branch => new BranchDto(
                    branch.Id,
                    branch.MerchantId,
                    branch.BranchCode,
                    branch.Name,
                    branch.Status))
                .ToArrayAsync(cancellationToken);

            return Results.Ok(branches);
        }).WithName("GetBranches");

        endpoints.MapGet("/api/terminals", [Authorize(Policy = PorticoPolicies.ReadAccess)] async (
            HttpContext context,
            PorticoDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var merchantId = GetSelectedMerchantId(context.User);
            if (merchantId is null)
            {
                return Results.Forbid();
            }

            var accessContexts = GetMerchantAccessContexts(context.User, merchantId.Value);
            if (accessContexts.Count == 0)
            {
                return Results.Forbid();
            }

            var hasFullMerchantAccess = HasFullMerchantAccess(accessContexts);
            var scopedBranchIds = GetScopedBranchIds(accessContexts);
            var scopedTerminalIds = GetScopedTerminalIds(accessContexts);

            var branchIds = await dbContext.Branches
                .AsNoTracking()
                .Where(branch => branch.MerchantId == merchantId.Value)
                .Select(branch => branch.Id)
                .ToArrayAsync(cancellationToken);

            var query = dbContext.Terminals
                .AsNoTracking()
                .Where(terminal => branchIds.Contains(terminal.BranchId));

            if (!hasFullMerchantAccess)
            {
                if (scopedTerminalIds.Count > 0)
                {
                    query = query.Where(terminal => scopedTerminalIds.Contains(terminal.Id));
                }
                else if (scopedBranchIds.Count > 0)
                {
                    query = query.Where(terminal => scopedBranchIds.Contains(terminal.BranchId));
                }
            }

            var terminals = await query
                .OrderBy(terminal => terminal.Name)
                .Select(terminal => new TerminalDto(
                    terminal.Id,
                    terminal.BranchId,
                    terminal.TerminalCode,
                    terminal.Name,
                    terminal.Status))
                .ToArrayAsync(cancellationToken);

            return Results.Ok(terminals);
        }).WithName("GetTerminals");

        endpoints.MapGet("/api/users", [Authorize(Policy = PorticoPolicies.AdminAccess)] async (
            HttpContext context,
            PorticoDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var merchantId = GetSelectedMerchantId(context.User);
            if (merchantId is null)
            {
                return Results.Forbid();
            }

            var memberships = await dbContext.MerchantMemberships
                .AsNoTracking()
                .Where(item => item.MerchantId == merchantId.Value)
                .ToArrayAsync(cancellationToken);

            var userIds = memberships.Select(item => item.MerchantUserId).Distinct().ToArray();

            var users = await dbContext.MerchantUsers
                .AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToArrayAsync(cancellationToken);

            var branchAssignments = await dbContext.UserBranchAssignments
                .AsNoTracking()
                .Where(item => userIds.Contains(item.MerchantUserId))
                .ToArrayAsync(cancellationToken);

            var terminalAssignments = await dbContext.UserTerminalAssignments
                .AsNoTracking()
                .Where(item => userIds.Contains(item.MerchantUserId))
                .ToArrayAsync(cancellationToken);

            var response = users
                .OrderBy(user => user.DisplayName)
                .Select(user => new MerchantUserDto(
                    user.Id,
                    user.DisplayName,
                    user.PhoneNumber,
                    user.Email,
                    user.Status.ToString(),
                    memberships.Where(item => item.MerchantUserId == user.Id).Select(item => item.Role).Distinct().ToArray(),
                    branchAssignments.Where(item => item.MerchantUserId == user.Id).Select(item => item.BranchId).Distinct().ToArray(),
                    terminalAssignments.Where(item => item.MerchantUserId == user.Id).Select(item => item.TerminalId).Distinct().ToArray()))
                .ToArray();

            return Results.Ok(response);
        }).WithName("GetMerchantUsers");

        endpoints.MapGet("/api/payment-intents", [Authorize(Policy = PorticoPolicies.ReadAccess)] async (
            HttpContext context,
            PorticoDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var merchantId = GetSelectedMerchantId(context.User);
            if (merchantId is null)
            {
                return Results.Forbid();
            }

            var accessContexts = GetMerchantAccessContexts(context.User, merchantId.Value);
            if (accessContexts.Count == 0)
            {
                return Results.Forbid();
            }

            var intents = await ApplyPaymentIntentScope(
                    dbContext.PaymentIntents
                        .AsNoTracking()
                        .Where(intent => intent.MerchantId == merchantId.Value),
                    accessContexts)
                .AsNoTracking()
                .OrderByDescending(intent => intent.CreatedAt)
                .Select(intent => new PaymentIntentDto(
                    intent.Id,
                    intent.MerchantId,
                    intent.BranchId,
                    intent.TerminalId,
                    intent.AmountMinor,
                    intent.Currency,
                    intent.Status.ToString(),
                    intent.Channel.ToString(),
                    intent.ExpiresAt,
                    intent.CreatedAt,
                    intent.IntentReference,
                    intent.MerchantReference,
                    intent.Description))
                .ToArrayAsync(cancellationToken);

            return Results.Ok(intents);
        }).WithName("GetPaymentIntents");

        endpoints.MapGet("/api/payment-intents/{intentId:guid}", [Authorize(Policy = PorticoPolicies.ReadAccess)] async (
            HttpContext context,
            Guid intentId,
            PorticoDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var merchantId = GetSelectedMerchantId(context.User);
            if (merchantId is null)
            {
                return Results.Forbid();
            }

            var accessContexts = GetMerchantAccessContexts(context.User, merchantId.Value);
            if (accessContexts.Count == 0)
            {
                return Results.Forbid();
            }

            var intent = await ApplyPaymentIntentScope(
                    dbContext.PaymentIntents
                        .AsNoTracking()
                        .Where(item => item.Id == intentId && item.MerchantId == merchantId.Value),
                    accessContexts)
                .Select(item => new PaymentIntentDetailDto(
                    item.Id,
                    item.MerchantId,
                    item.BranchId,
                    item.TerminalId,
                    item.CreatedByUserId,
                    item.AmountMinor,
                    item.Currency,
                    item.Status.ToString(),
                    item.Channel.ToString(),
                    item.ExpiresAt,
                    item.CreatedAt,
                    item.UpdatedAt,
                    item.IntentReference,
                    item.MerchantReference,
                    item.Description))
                .FirstOrDefaultAsync(cancellationToken);

            return intent is null ? Results.NotFound() : Results.Ok(intent);
        }).WithName("GetPaymentIntentById");

        endpoints.MapPost("/api/payment-intents", [Authorize(Policy = PorticoPolicies.CashierAccess)] async (
            HttpContext context,
            CreatePaymentIntentRequest request,
            PorticoDbContext dbContext,
            IDashboardProjectionService projectionService,
            IIntegrationOutboxService integrationOutboxService,
            IPorticoRealtimeNotifier realtimeNotifier,
            CancellationToken cancellationToken) =>
        {
            if (!context.User.TryGetUserId(out var userId))
            {
                return Results.Unauthorized();
            }

            var accessContexts = GetMerchantAccessContexts(context.User, request.MerchantId);
            if (accessContexts.Count == 0)
            {
                return Results.Forbid();
            }

            var branch = await dbContext.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.BranchId && item.MerchantId == request.MerchantId, cancellationToken);

            if (branch is null)
            {
                return Results.BadRequest(new { message = "Selected branch does not belong to the merchant." });
            }

            var terminal = await dbContext.Terminals
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.TerminalId && item.BranchId == request.BranchId, cancellationToken);

            if (terminal is null)
            {
                return Results.BadRequest(new { message = "Selected terminal does not belong to the branch." });
            }

            if (!IsAuthorizedForPaymentScope(accessContexts, request.BranchId, request.TerminalId))
            {
                return Results.Forbid();
            }

            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(request.ExpiresInSeconds, 60, 3600));
            var paymentIntent = new PaymentIntent
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                MerchantId = request.MerchantId,
                BranchId = request.BranchId,
                TerminalId = request.TerminalId,
                CreatedByUserId = userId,
                IntentReference = $"pi_{Guid.NewGuid():N}"[..15],
                MerchantReference = request.MerchantReference,
                AmountMinor = request.AmountMinor,
                Currency = request.Currency,
                Description = request.Description,
                Channel = Enum.TryParse<PaymentChannel>(request.Channel, true, out var channel)
                    ? channel
                    : PaymentChannel.Qr,
                Status = PaymentIntentStatus.Active,
                ExpiresAt = expiresAt,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            dbContext.PaymentIntents.Add(paymentIntent);
            integrationOutboxService.Enqueue(
                PorticoIntegrationMessageTypes.PorticoPaymentIntentCreated,
                paymentIntent.Id.ToString(),
                paymentIntent.IntentReference,
                new
                {
                    intentId = paymentIntent.Id,
                    intentReference = paymentIntent.IntentReference,
                    merchantId = paymentIntent.MerchantId,
                    branchId = paymentIntent.BranchId,
                    terminalId = paymentIntent.TerminalId,
                    amountMinor = paymentIntent.AmountMinor,
                    currency = paymentIntent.Currency,
                    channel = paymentIntent.Channel.ToString(),
                    status = paymentIntent.Status.ToString(),
                    expiresAt = paymentIntent.ExpiresAt,
                    occurredAt = paymentIntent.CreatedAt
                },
                paymentIntent.CreatedAt);
            await dbContext.SaveChangesAsync(cancellationToken);

            var summary = await projectionService.RefreshSummaryAsync(paymentIntent.MerchantId, cancellationToken);

            var response = new CreatePaymentIntentResponse(
                paymentIntent.Id,
                paymentIntent.IntentReference,
                paymentIntent.Status.ToString(),
                paymentIntent.ExpiresAt,
                paymentIntent.IntentReference,
                $"tikoapp://pay?intent={paymentIntent.IntentReference}");

            await realtimeNotifier.NotifyAsync(
                new PorticoRealtimeEvent(
                    "paymentIntentUpdated",
                    paymentIntent.MerchantId,
                    paymentIntent.BranchId,
                    paymentIntent.TerminalId,
                    response),
                cancellationToken);

            await realtimeNotifier.NotifyAsync(
                new PorticoRealtimeEvent(
                    "dashboardSummaryChanged",
                    paymentIntent.MerchantId,
                    paymentIntent.BranchId,
                    paymentIntent.TerminalId,
                    new DashboardSummaryDto(
                        summary.SuccessCount,
                        summary.FailedCount,
                        summary.PendingCount,
                        summary.TotalSuccessAmountMinor,
                        summary.SuccessCount == 0 ? 0 : summary.TotalSuccessAmountMinor / summary.SuccessCount)),
                cancellationToken);

            return Results.Ok(response);
        }).WithName("CreatePaymentIntent");

        endpoints.MapPost("/api/payment-intents/{intentId:guid}/cancel", [Authorize(Policy = PorticoPolicies.CashierAccess)] async (
            HttpContext context,
            Guid intentId,
            PorticoDbContext dbContext,
            IDashboardProjectionService projectionService,
            IIntegrationOutboxService integrationOutboxService,
            IPorticoRealtimeNotifier realtimeNotifier,
            CancellationToken cancellationToken) =>
        {
            var merchantId = GetSelectedMerchantId(context.User);
            if (merchantId is null)
            {
                return Results.Forbid();
            }

            var intent = await dbContext.PaymentIntents
                .FirstOrDefaultAsync(item => item.Id == intentId && item.MerchantId == merchantId.Value, cancellationToken);

            if (intent is null)
            {
                return Results.NotFound();
            }

            var accessContexts = GetMerchantAccessContexts(context.User, intent.MerchantId);
            if (accessContexts.Count == 0 || !IsAuthorizedForPaymentScope(accessContexts, intent.BranchId, intent.TerminalId))
            {
                return Results.Forbid();
            }

            if (intent.Status is PaymentIntentStatus.Completed or PaymentIntentStatus.Expired or PaymentIntentStatus.Cancelled)
            {
                return Results.Conflict(new
                {
                    message = $"Payment intent cannot be cancelled from status {intent.Status}."
                });
            }

            intent.Status = PaymentIntentStatus.Cancelled;
            intent.CancelledAt = DateTimeOffset.UtcNow;
            intent.UpdatedAt = DateTimeOffset.UtcNow;

            integrationOutboxService.Enqueue(
                PorticoIntegrationMessageTypes.PorticoPaymentIntentCancelled,
                intent.Id.ToString(),
                intent.IntentReference,
                new
                {
                    intentId = intent.Id,
                    intentReference = intent.IntentReference,
                    merchantId = intent.MerchantId,
                    branchId = intent.BranchId,
                    terminalId = intent.TerminalId,
                    status = intent.Status.ToString(),
                    cancelledAt = intent.CancelledAt,
                    occurredAt = intent.UpdatedAt
                },
                intent.UpdatedAt);
            await dbContext.SaveChangesAsync(cancellationToken);

            var summary = await projectionService.RefreshSummaryAsync(intent.MerchantId, cancellationToken);

            await realtimeNotifier.NotifyAsync(
                new PorticoRealtimeEvent(
                    "paymentIntentUpdated",
                    intent.MerchantId,
                    intent.BranchId,
                    intent.TerminalId,
                    new
                    {
                        intentId = intent.Id,
                        intentReference = intent.IntentReference,
                        status = intent.Status.ToString(),
                        cancelledAt = intent.CancelledAt
                    }),
                cancellationToken);

            await realtimeNotifier.NotifyAsync(
                new PorticoRealtimeEvent(
                    "dashboardSummaryChanged",
                    intent.MerchantId,
                    intent.BranchId,
                    intent.TerminalId,
                    new DashboardSummaryDto(
                        summary.SuccessCount,
                        summary.FailedCount,
                        summary.PendingCount,
                        summary.TotalSuccessAmountMinor,
                        summary.SuccessCount == 0 ? 0 : summary.TotalSuccessAmountMinor / summary.SuccessCount)),
                cancellationToken);

            return Results.Ok(new
            {
                intentId = intent.Id,
                status = intent.Status.ToString(),
                cancelledAt = intent.CancelledAt
            });
        }).WithName("CancelPaymentIntent");

        endpoints.MapGet("/api/payments", [Authorize(Policy = PorticoPolicies.ReadAccess)] async (
            HttpContext context,
            PorticoDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var merchantId = GetSelectedMerchantId(context.User);
            if (merchantId is null)
            {
                return Results.Forbid();
            }

            var accessContexts = GetMerchantAccessContexts(context.User, merchantId.Value);
            if (accessContexts.Count == 0)
            {
                return Results.Forbid();
            }

            var scopedIntents = ApplyPaymentIntentScope(
                dbContext.PaymentIntents
                    .AsNoTracking()
                    .Where(intent => intent.MerchantId == merchantId.Value),
                accessContexts);

            var payments = await (
                from payment in dbContext.PaymentRecords.AsNoTracking()
                join intent in scopedIntents on payment.PaymentIntentId equals intent.Id
                orderby payment.CreatedAt descending
                select new PaymentDto(
                    payment.Id,
                    intent.Id,
                    intent.MerchantId,
                    intent.BranchId,
                    intent.TerminalId,
                    intent.AmountMinor,
                    intent.Currency,
                    payment.Status.ToString(),
                    payment.FailureCode,
                    payment.ProcessedAt))
                .ToArrayAsync(cancellationToken);

            return Results.Ok(payments);
        }).WithName("GetPayments");

        endpoints.MapGet("/api/payments/{paymentId:guid}", [Authorize(Policy = PorticoPolicies.ReadAccess)] async (
            HttpContext context,
            Guid paymentId,
            PorticoDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var merchantId = GetSelectedMerchantId(context.User);
            if (merchantId is null)
            {
                return Results.Forbid();
            }

            var accessContexts = GetMerchantAccessContexts(context.User, merchantId.Value);
            if (accessContexts.Count == 0)
            {
                return Results.Forbid();
            }

            var scopedIntents = ApplyPaymentIntentScope(
                dbContext.PaymentIntents
                    .AsNoTracking()
                    .Where(intent => intent.MerchantId == merchantId.Value),
                accessContexts);

            var payment = await (
                from record in dbContext.PaymentRecords.AsNoTracking()
                join intent in scopedIntents on record.PaymentIntentId equals intent.Id
                where record.Id == paymentId
                select new PaymentDetailDto(
                    record.Id,
                    intent.Id,
                    intent.MerchantId,
                    intent.BranchId,
                    intent.TerminalId,
                    record.ProcessedAmountMinor ?? intent.AmountMinor,
                    intent.Currency,
                    record.Status.ToString(),
                    record.CitadelPaymentId,
                    record.CitadelSessionId,
                    record.FailureCode,
                    record.FailureReason,
                    record.CreatedAt,
                    record.ProcessedAt))
                .FirstOrDefaultAsync(cancellationToken);

            return payment is null ? Results.NotFound() : Results.Ok(payment);
        }).WithName("GetPaymentById");

        endpoints.MapGet("/api/dashboard/summary", [Authorize(Policy = PorticoPolicies.ReadAccess)] async (
            HttpContext context,
            PorticoDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var merchantId = GetSelectedMerchantId(context.User);
            if (merchantId is null)
            {
                return Results.Forbid();
            }

            var accessContexts = GetMerchantAccessContexts(context.User, merchantId.Value);
            if (accessContexts.Count == 0)
            {
                return Results.Forbid();
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (!HasFullMerchantAccess(accessContexts))
            {
                var scopedIntents = ApplyPaymentIntentScope(
                    dbContext.PaymentIntents
                        .AsNoTracking()
                        .Where(intent => intent.MerchantId == merchantId.Value),
                    accessContexts);

                var successPayments = await (
                    from payment in dbContext.PaymentRecords.AsNoTracking()
                    join intent in scopedIntents on payment.PaymentIntentId equals intent.Id
                    where payment.Status == PaymentRecordStatus.Succeeded
                    select payment.ProcessedAmountMinor ?? intent.AmountMinor)
                    .ToArrayAsync(cancellationToken);

                var failedCount = await (
                    from payment in dbContext.PaymentRecords.AsNoTracking()
                    join intent in scopedIntents on payment.PaymentIntentId equals intent.Id
                    where payment.Status == PaymentRecordStatus.Failed
                    select payment.Id)
                    .CountAsync(cancellationToken);

                var pendingCount = await scopedIntents
                    .Where(intent => intent.Status == PaymentIntentStatus.Pending || intent.Status == PaymentIntentStatus.Active)
                    .CountAsync(cancellationToken);

                var totalSuccessAmountMinor = successPayments.Sum();
                var averageScoped = successPayments.Length == 0
                    ? 0
                    : totalSuccessAmountMinor / successPayments.Length;

                return Results.Ok(new DashboardSummaryDto(
                    successPayments.Length,
                    failedCount,
                    pendingCount,
                    totalSuccessAmountMinor,
                    averageScoped));
            }

            var summary = await dbContext.DashboardSummaryProjections
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.MerchantId == merchantId.Value && item.BusinessDate == today, cancellationToken);

            if (summary is null)
            {
                return Results.Ok(new DashboardSummaryDto(0, 0, 0, 0, 0));
            }

            var average = summary.SuccessCount == 0
                ? 0
                : summary.TotalSuccessAmountMinor / summary.SuccessCount;

            return Results.Ok(new DashboardSummaryDto(
                summary.SuccessCount,
                summary.FailedCount,
                summary.PendingCount,
                summary.TotalSuccessAmountMinor,
                average));
        }).WithName("GetDashboardSummary");

        return endpoints;
    }

    private static Guid? GetSelectedMerchantId(ClaimsPrincipal principal)
    {
        return principal.GetMerchantAccessContexts().Select(item => (Guid?)item.MerchantId).FirstOrDefault();
    }

    private static IReadOnlyCollection<MerchantAccessContext> GetMerchantAccessContexts(ClaimsPrincipal principal, Guid merchantId)
    {
        return principal.GetMerchantAccessContexts()
            .Where(item => item.MerchantId == merchantId)
            .ToArray();
    }

    private static bool HasFullMerchantAccess(IReadOnlyCollection<MerchantAccessContext> accessContexts)
    {
        return accessContexts.Any(item =>
            string.Equals(item.Scope, "merchant", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Role, PorticoRoles.Admin, StringComparison.Ordinal) ||
            string.Equals(item.Role, PorticoRoles.SuperUser, StringComparison.Ordinal));
    }

    private static IReadOnlyCollection<Guid> GetScopedBranchIds(IReadOnlyCollection<MerchantAccessContext> accessContexts)
    {
        return accessContexts
            .SelectMany(item => item.BranchIds)
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyCollection<Guid> GetScopedTerminalIds(IReadOnlyCollection<MerchantAccessContext> accessContexts)
    {
        return accessContexts
            .SelectMany(item => item.TerminalIds)
            .Distinct()
            .ToArray();
    }

    private static IQueryable<PaymentIntent> ApplyPaymentIntentScope(
        IQueryable<PaymentIntent> query,
        IReadOnlyCollection<MerchantAccessContext> accessContexts)
    {
        if (HasFullMerchantAccess(accessContexts))
        {
            return query;
        }

        var terminalIds = GetScopedTerminalIds(accessContexts);
        if (terminalIds.Count > 0)
        {
            return query.Where(intent => terminalIds.Contains(intent.TerminalId));
        }

        var branchIds = GetScopedBranchIds(accessContexts);
        if (branchIds.Count > 0)
        {
            return query.Where(intent => branchIds.Contains(intent.BranchId));
        }

        return query.Where(_ => false);
    }

    private static bool IsAuthorizedForPaymentScope(
        IReadOnlyCollection<MerchantAccessContext> accessContexts,
        Guid branchId,
        Guid terminalId)
    {
        if (HasFullMerchantAccess(accessContexts))
        {
            return true;
        }

        return accessContexts.Any(item =>
        {
            var branchAllowed = item.BranchIds.Count == 0 || item.BranchIds.Contains(branchId);
            var terminalAllowed = item.TerminalIds.Count == 0 || item.TerminalIds.Contains(terminalId);

            return string.Equals(item.Scope, "branch", StringComparison.OrdinalIgnoreCase)
                ? branchAllowed
                : branchAllowed && terminalAllowed;
        });
    }
}
