using DataBro.Modules.Identity.Application;
using Microsoft.Extensions.Logging;

namespace DataBro.Modules.Identity.Infrastructure.Auth;

/// <summary>
/// Placeholder email sender. Logs the confirmation token instead of sending mail, until a real
/// transport (e.g. Resend) is wired. Never log tokens in production.
/// </summary>
public sealed class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task SendEmailConfirmationAsync(string email, Guid userId, string token, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Email confirmation for {Email} (user {UserId}). Token: {Token}", email, userId, token);
        return Task.CompletedTask;
    }
}
