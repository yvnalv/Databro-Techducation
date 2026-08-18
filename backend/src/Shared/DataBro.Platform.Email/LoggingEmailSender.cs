using DataBro.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace DataBro.Platform.Email;

/// <summary>
/// Writes the message to the log instead of sending it. The default provider.
///
/// <para>
/// Distinct from the no-op it replaces in one way that matters: it logs the <b>whole</b> message,
/// including the body. The old sender logged only a confirmation token, which meant any email added
/// later would have vanished silently in development.
/// </para>
/// <para>
/// The body of a transactional email routinely contains a single-use credential — a verification or
/// password-reset token. That is exactly why this is development-only and why the startup path warns
/// when it is selected outside development.
/// </para>
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Email (not sent - 'log' provider)\n  To: {To}\n  Subject: {Subject}\n{Body}",
            message.To,
            message.Subject,
            message.TextBody);

        return Task.CompletedTask;
    }
}
