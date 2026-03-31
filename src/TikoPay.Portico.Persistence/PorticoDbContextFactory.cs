using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TikoPay.Portico.Persistence;

public sealed class PorticoDbContextFactory : IDesignTimeDbContextFactory<PorticoDbContext>
{
    public PorticoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PorticoDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=portico;Username=portico;Password=Port1co_Dev!");

        return new PorticoDbContext(optionsBuilder.Options);
    }
}
