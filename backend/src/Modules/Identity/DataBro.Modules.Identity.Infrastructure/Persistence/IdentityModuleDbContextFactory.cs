using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataBro.Modules.Identity.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef` can build the Identity model without the host.</summary>
public sealed class IdentityModuleDbContextFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DATABRO_POSTGRES")
            ?? "Host=localhost;Port=5439;Database=databro;Username=databro;Password=databro_dev_pw";

        var options = new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql(connection, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", IdentityModuleDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new IdentityModuleDbContext(options);
    }
}
