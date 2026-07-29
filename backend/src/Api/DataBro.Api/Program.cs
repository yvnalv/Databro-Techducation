using DataBro.Modules.Content.Api;
using DataBro.Modules.Identity.Api;
using DataBro.Modules.Media.Api;
using DataBro.Modules.Search.Api;

var builder = WebApplication.CreateBuilder(args);

// ---- Module registration (composition root) ----
// Each module owns its own DI wiring; the host only composes them.
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddContentModule(builder.Configuration)
    .AddMediaModule(builder.Configuration)
    .AddSearchModule(builder.Configuration);

var app = builder.Build();

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
