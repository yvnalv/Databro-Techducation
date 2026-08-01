using DataBro.Api;
using DataBro.Modules.Content.Api;
using DataBro.Modules.Identity.Api;
using DataBro.Modules.Media.Api;
using DataBro.Modules.Search.Api;
using Hangfire;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

// ---- Module registration (composition root) ----
// Each module owns its own DI wiring; the host only composes them.
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddContentModule(builder.Configuration)
    .AddMediaModule(builder.Configuration)
    .AddSearchModule(builder.Configuration);

// ---- Background jobs (Hangfire) ----
// The host owns the job server and its PostgreSQL storage; modules register their own recurring
// jobs (see ContentJobsInitializer). Disabled as a unit for integration tests, which drive the job
// method directly rather than through a real server.
var hangfireEnabled = builder.Configuration.GetValue("Hangfire:EnableServer", true);
if (hangfireEnabled)
{
    var postgres = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing connection string 'Postgres'.");

    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(postgres)));
    builder.Services.AddHangfireServer();
}

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Dashboard is a development convenience only; never mount it in other environments, where it would
// expose job internals. Localhost-only guard is lifted in dev so it works through the docker port map.
if (hangfireEnabled && app.Environment.IsDevelopment())
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new AllowAllDashboardAuthorization()],
    });

// ---- Platform endpoints ----
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .WithTags("Platform");

// ---- Module endpoints ----
app.MapIdentityModule()
   .MapContentModule()
   .MapMediaModule()
   .MapSearchModule();

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>).
public partial class Program;
