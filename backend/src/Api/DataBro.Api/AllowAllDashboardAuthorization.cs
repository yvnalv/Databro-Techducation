using Hangfire.Dashboard;

namespace DataBro.Api;

/// <summary>
/// Allows every request to reach the Hangfire dashboard. Used <b>only</b> in Development (Program.cs
/// mounts the dashboard nowhere else): Hangfire's default filter permits localhost only, which the
/// dev docker port map trips over. Never wire this into a non-development environment — the dashboard
/// can trigger and delete jobs.
/// </summary>
internal sealed class AllowAllDashboardAuthorization : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
