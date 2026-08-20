using DataBro.Api;
using DataBro.Modules.Assessment.Api;
using DataBro.Modules.Content.Api;
using DataBro.Modules.Identity.Api;
using DataBro.Modules.Learning.Api;
using DataBro.Modules.Media.Api;
using DataBro.Modules.Search.Api;
using DataBro.Platform.Email;
using Hangfire;
using Hangfire.PostgreSql;

// Load `.env` into the process environment before configuration is built, so the host-run dev loop
// (`dotnet run` with infra in Docker) picks up the OAuth secrets the developer manages by hand
// (ADR-0019). Existing process variables win, so docker-compose's injected values stay authoritative;
// deployed environments ship no `.env`, so this finds nothing and does nothing.
var dotenv = DataBro.Api.DotEnvLoader.Find(AppContext.BaseDirectory);
if (dotenv is not null)
    DotNetEnv.Env.Load(dotenv, new DotNetEnv.LoadOptions(setEnvVars: true, clobberExistingVars: false));

var builder = WebApplication.CreateBuilder(args);

// One-time OAuth handoff code store (ADR-0019). Redis when a connection string is configured — which
// finally gives the provisioned-but-unused Redis a job (O-4) — and an in-memory cache otherwise, so
// tests and a Redis-less run still work. The store abstraction (IAuthCodeStore) does not care which.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConnection);
else
    builder.Services.AddDistributedMemoryCache();

// One transactional email transport for the whole host, selected by configuration (rule 14). The
// modules compose their own messages; only this line knows how they travel.
builder.Services.AddPlatformEmail(builder.Configuration, builder.Environment);

// ---- Module registration (composition root) ----
// Each module owns its own DI wiring; the host only composes them.
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddContentModule(builder.Configuration)
    .AddMediaModule(builder.Configuration)
    .AddLearningModule(builder.Configuration)
    .AddAssessmentModule(builder.Configuration)
    .AddSearchModule(builder.Configuration);

// ---- CORS ----
// The browser calls this API directly from the frontend apps, which are separate origins
// (docs/FRONTEND_ARCHITECTURE.md). Without a policy every client-side call is blocked at the
// preflight — which the public site never noticed, because its reads happen server-side during SSR,
// but which stops the authoring app dead at sign-in.
//
// Origins are configured, never wildcarded: `AllowAnyOrigin` would let any site on the internet call
// the API with a user's bearer token in a script it controls.
const string FrontendCorsPolicy = "databro-frontends";

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            // Authorization + Content-Type, and whatever a future client needs.
            .AllowAnyHeader();

        // Credentials are deliberately NOT allowed: auth travels as a bearer header, not a cookie,
        // so the API never needs to accept cross-origin cookies — and allowing them would widen the
        // CSRF surface for no benefit.
    }));

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

// Before authentication: a rejected preflight never carries credentials, and CORS headers must be
// present on the 401 responses too, or the browser reports a CORS error instead of the real status.
app.UseCors(FrontendCorsPolicy);

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
   .MapLearningModule()
   .MapAssessmentModule()
   // Cross-module search: composed here because only the host may know about both (ADR-0014).
   .MapSearch()
   .MapSearchModule();

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>).
public partial class Program;
