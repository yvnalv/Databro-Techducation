using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef` can build the model without running the host. The connection
/// string is only used when actually hitting the database (e.g. `database update`); for local dev it
/// defaults to the docker-compose Postgres (see docker-compose.yml / .env), overridable via
/// the DATABRO_POSTGRES env var.
/// </summary>
public sealed class ContentDbContextFactory : IDesignTimeDbContextFactory<ContentDbContext>
{
    public ContentDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DATABRO_POSTGRES")
            ?? "Host=localhost;Port=5439;Database=databro;Username=databro;Password=databro_dev_pw";

        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql(connection, npg => npg.MigrationsHistoryTable("__ef_migrations_history", ContentDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ContentDbContext(options);
    }
}
