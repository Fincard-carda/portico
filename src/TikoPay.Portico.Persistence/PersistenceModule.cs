using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TikoPay.Portico.IdentityAccess;
using TikoPay.Portico.MerchantDirectory;
using TikoPay.Portico.PaymentIntents;
using TikoPay.Portico.PaymentTracking;
using TikoPay.Portico.Reporting;

namespace TikoPay.Portico.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPorticoPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PorticoDb")
            ?? "Host=localhost;Port=5432;Database=portico;Username=portico;Password=ChangeMe!";

        services.AddDbContext<PorticoDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IDashboardProjectionService, DashboardProjectionService>();
        services.AddScoped<IPaymentIntentLifecycleService, PaymentIntentLifecycleService>();
        services.AddScoped<ICitadelPaymentEventProcessor, CitadelPaymentEventProcessor>();

        return services;
    }
}

public sealed class PorticoDbContext(DbContextOptions<PorticoDbContext> options) : DbContext(options)
{
    public DbSet<MerchantUser> MerchantUsers => Set<MerchantUser>();
    public DbSet<MerchantMembership> MerchantMemberships => Set<MerchantMembership>();
    public DbSet<UserBranchAssignment> UserBranchAssignments => Set<UserBranchAssignment>();
    public DbSet<UserTerminalAssignment> UserTerminalAssignments => Set<UserTerminalAssignment>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<PaymentStatusHistory> PaymentStatusHistory => Set<PaymentStatusHistory>();
    public DbSet<DashboardSummaryProjection> DashboardSummaryProjections => Set<DashboardSummaryProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MerchantUser>(entity =>
        {
            entity.ToTable("merchant_users");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PhoneNumber).HasMaxLength(32);
            entity.Property(item => item.Email).HasMaxLength(256);
            entity.Property(item => item.DisplayName).HasMaxLength(128);
        });

        modelBuilder.Entity<MerchantMembership>(entity =>
        {
            entity.ToTable("merchant_memberships");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Role).HasMaxLength(32);
            entity.Property(item => item.Status).HasMaxLength(32);
            entity.HasIndex(item => new { item.MerchantUserId, item.MerchantId }).IsUnique();
        });

        modelBuilder.Entity<UserBranchAssignment>(entity =>
        {
            entity.ToTable("user_branch_assignments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AssignmentType).HasMaxLength(32);
            entity.HasIndex(item => new { item.MerchantUserId, item.BranchId }).IsUnique();
        });

        modelBuilder.Entity<UserTerminalAssignment>(entity =>
        {
            entity.ToTable("user_terminal_assignments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AssignmentType).HasMaxLength(32);
            entity.HasIndex(item => new { item.MerchantUserId, item.TerminalId }).IsUnique();
        });

        modelBuilder.Entity<Merchant>(entity =>
        {
            entity.ToTable("merchants");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MerchantCode).HasMaxLength(64);
            entity.Property(item => item.LegalName).HasMaxLength(256);
            entity.Property(item => item.DisplayName).HasMaxLength(256);
            entity.Property(item => item.Status).HasMaxLength(32);
            entity.HasIndex(item => new { item.TenantId, item.MerchantCode }).IsUnique();
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("branches");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.BranchCode).HasMaxLength(64);
            entity.Property(item => item.Name).HasMaxLength(128);
            entity.Property(item => item.Timezone).HasMaxLength(64);
            entity.Property(item => item.Status).HasMaxLength(32);
            entity.HasIndex(item => new { item.MerchantId, item.BranchCode }).IsUnique();
        });

        modelBuilder.Entity<Terminal>(entity =>
        {
            entity.ToTable("terminals");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TerminalCode).HasMaxLength(64);
            entity.Property(item => item.Name).HasMaxLength(128);
            entity.Property(item => item.TerminalType).HasMaxLength(64);
            entity.Property(item => item.Status).HasMaxLength(32);
            entity.HasIndex(item => new { item.BranchId, item.TerminalCode }).IsUnique();
        });

        modelBuilder.Entity<PaymentIntent>(entity =>
        {
            entity.ToTable("payment_intents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.IntentReference).HasMaxLength(64);
            entity.Property(item => item.MerchantReference).HasMaxLength(128);
            entity.Property(item => item.Currency).HasMaxLength(8);
            entity.Property(item => item.Description).HasMaxLength(512);
            entity.HasIndex(item => item.IntentReference).IsUnique();
        });

        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.ToTable("payment_records");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CitadelPaymentId).HasMaxLength(128);
            entity.Property(item => item.CitadelSessionId).HasMaxLength(128);
            entity.Property(item => item.FailureCode).HasMaxLength(64);
            entity.Property(item => item.FailureReason).HasMaxLength(512);
        });

        modelBuilder.Entity<PaymentStatusHistory>(entity =>
        {
            entity.ToTable("payment_status_history");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OldStatus).HasMaxLength(32);
            entity.Property(item => item.NewStatus).HasMaxLength(32);
            entity.Property(item => item.Source).HasMaxLength(64);
            entity.Property(item => item.CorrelationId).HasMaxLength(128);
        });

        modelBuilder.Entity<DashboardSummaryProjection>(entity =>
        {
            entity.ToTable("dashboard_summary_projections");
            entity.HasKey(item => new { item.MerchantId, item.BusinessDate });
        });
    }
}
