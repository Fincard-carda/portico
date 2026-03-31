using Microsoft.EntityFrameworkCore;
using TikoPay.Portico.IdentityAccess;
using TikoPay.Portico.MerchantDirectory;
using TikoPay.Portico.PaymentIntents;
using TikoPay.Portico.PaymentTracking;
using TikoPay.Portico.Reporting;

namespace TikoPay.Portico.Persistence;

public static class PorticoDevelopmentSeeder
{
    public static async Task SeedAsync(PorticoDbContext dbContext)
    {
        await dbContext.Database.MigrateAsync();

        if (await dbContext.Merchants.AnyAsync())
        {
            return;
        }

        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var merchantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var branchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var terminalId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var merchant = new Merchant
        {
            Id = merchantId,
            TenantId = tenantId,
            MerchantCode = "demo-merchant",
            LegalName = "Demo Merchant Ltd.",
            DisplayName = "Demo Restaurant",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var branch = new Branch
        {
            Id = branchId,
            MerchantId = merchantId,
            BranchCode = "central",
            Name = "Central Branch",
            Timezone = "Europe/Istanbul",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var terminal = new Terminal
        {
            Id = terminalId,
            BranchId = branchId,
            TerminalCode = "web-01",
            Name = "Front Desk Web Terminal",
            TerminalType = "Web",
            Status = "Active",
            LastSeenAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var user = new MerchantUser
        {
            Id = userId,
            TenantId = tenantId,
            ExternalUserId = userId,
            PhoneNumber = "+905551234567",
            Email = "merchant.demo@tikopay.local",
            DisplayName = "Demo Merchant User",
            Status = MerchantUserStatus.Active,
            LastLoginAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var membership = new MerchantMembership
        {
            Id = Guid.NewGuid(),
            MerchantUserId = userId,
            MerchantId = merchantId,
            Role = PorticoRoles.SuperUser,
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var branchAssignment = new UserBranchAssignment
        {
            Id = Guid.NewGuid(),
            MerchantUserId = userId,
            BranchId = branchId,
            AssignmentType = "Assigned",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var terminalAssignment = new UserTerminalAssignment
        {
            Id = Guid.NewGuid(),
            MerchantUserId = userId,
            TerminalId = terminalId,
            AssignmentType = "Assigned",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var activeIntent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MerchantId = merchantId,
            BranchId = branchId,
            TerminalId = terminalId,
            CreatedByUserId = userId,
            IntentReference = "pi_active_001",
            MerchantReference = "order-1001",
            AmountMinor = 125000,
            Currency = "TRY",
            Description = "Dinner payment",
            Channel = PaymentChannel.Qr,
            Status = PaymentIntentStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        var successfulIntent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MerchantId = merchantId,
            BranchId = branchId,
            TerminalId = terminalId,
            CreatedByUserId = userId,
            IntentReference = "pi_paid_001",
            MerchantReference = "order-1000",
            AmountMinor = 98000,
            Currency = "TRY",
            Description = "Lunch payment",
            Channel = PaymentChannel.DeepLink,
            Status = PaymentIntentStatus.Completed,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };

        var failedIntent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MerchantId = merchantId,
            BranchId = branchId,
            TerminalId = terminalId,
            CreatedByUserId = userId,
            IntentReference = "pi_failed_001",
            MerchantReference = "order-999",
            AmountMinor = 76000,
            Currency = "TRY",
            Description = "Coffee order",
            Channel = PaymentChannel.Qr,
            Status = PaymentIntentStatus.Completed,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-60),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-4),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-4)
        };

        var successPayment = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            PaymentIntentId = successfulIntent.Id,
            CitadelPaymentId = "citadel-payment-001",
            CitadelSessionId = "citadel-session-001",
            Status = PaymentRecordStatus.Succeeded,
            ProcessedAmountMinor = successfulIntent.AmountMinor,
            ProcessedAt = DateTimeOffset.UtcNow.AddHours(-2).AddMinutes(2),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2).AddMinutes(2)
        };

        var failedPayment = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            PaymentIntentId = failedIntent.Id,
            CitadelPaymentId = "citadel-payment-002",
            CitadelSessionId = "citadel-session-002",
            Status = PaymentRecordStatus.Failed,
            FailureCode = "citadel.psp.mock_declined",
            FailureReason = "Mock PSP declined the transaction.",
            ProcessedAmountMinor = failedIntent.AmountMinor,
            ProcessedAt = DateTimeOffset.UtcNow.AddHours(-4).AddMinutes(1),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-4),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-4).AddMinutes(1)
        };

        var summary = new DashboardSummaryProjection
        {
            MerchantId = merchantId,
            BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SuccessCount = 1,
            FailedCount = 1,
            PendingCount = 1,
            TotalSuccessAmountMinor = successfulIntent.AmountMinor,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AddRange(
            merchant,
            branch,
            terminal,
            user,
            membership,
            branchAssignment,
            terminalAssignment,
            activeIntent,
            successfulIntent,
            failedIntent,
            successPayment,
            failedPayment,
            summary);

        await dbContext.SaveChangesAsync();
    }
}
