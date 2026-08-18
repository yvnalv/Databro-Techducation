using System.Net;
using System.Net.Mail;
using DataBro.Platform.Abstractions;
using Microsoft.Extensions.Options;

namespace DataBro.Platform.Email;

/// <summary>
/// Sends over SMTP. Points at Mailpit in local development and at a relay in deployed environments.
///
/// <para>
/// Every message goes out as <b>multipart/alternative</b> — text and HTML both. A client that
/// prefers text gets readable text rather than tag soup, and an HTML-only message is a well-known
/// spam signal.
/// </para>
/// <para>
/// Failures are allowed to throw. A transport that swallowed them would report success for mail that
/// never left, and the retry machinery that will eventually sit in front of this can only retry what
/// it is told failed.
/// </para>
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false,
        };

        mail.To.Add(message.To);

        // Text is the body and HTML is the alternative view, in that order: SMTP clients are
        // specified to render the *last* alternative they understand, so HTML still wins where it
        // is supported while text remains the fallback rather than an afterthought.
        mail.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(message.HtmlBody, null, "text/html"));

        using var client = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
        {
            EnableSsl = _options.Smtp.UseTls,
        };

        if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
        {
            client.Credentials = new NetworkCredential(_options.Smtp.Username, _options.Smtp.Password);
        }

        // SmtpClient has no cancellable send, so cancellation is honoured at the boundary rather
        // than mid-flight. Better than ignoring the token: a cancelled request does not wait on a
        // dead relay's timeout.
        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(mail, ct);
    }
}
