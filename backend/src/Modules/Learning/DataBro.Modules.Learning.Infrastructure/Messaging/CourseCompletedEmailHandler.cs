using DataBro.Modules.Learning.Application;
using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace DataBro.Modules.Learning.Infrastructure.Messaging;

/// <summary>
/// Congratulates a learner who has finished a course.
///
/// <para>
/// The outbox's first real consumer, and the reason it exists: this must happen if the completion
/// happened, but it need not happen in the same request. A learner should not wait on an SMTP round
/// trip to see their progress bar reach 100%, and a mail server being down must not roll back the
/// fact that they finished.
/// </para>
/// <para>
/// <b>Idempotent in the way that matters here.</b> At-least-once delivery means this can run twice,
/// and there is no dedupe key on an email — so the honest position is that a duplicate congratulation
/// is a nuisance, not damage. A certificate, when it arrives, will need a real key and will get one.
/// </para>
/// </summary>
public sealed class CourseCompletedEmailHandler(
    IUserContacts contacts,
    ICourseRepository courses,
    IEmailSender email,
    ILogger<CourseCompletedEmailHandler> logger)
    : IIntegrationEventHandler<CourseCompletedDomainEvent>
{
    public async Task HandleAsync(CourseCompletedDomainEvent completed, CancellationToken ct = default)
    {
        var contact = await contacts.GetContactAsync(completed.UserId, ct);
        if (contact is null)
        {
            // A deleted account is an ordinary outcome, not a failure. Throwing would retry eight
            // times and then dead-letter a message that was never deliverable.
            logger.LogInformation(
                "No contact for user {UserId}; skipping completion email.", completed.UserId);
            return;
        }

        var course = await courses.GetByIdAsync(completed.CourseId, ct);
        var title = course?.Title ?? "your course";

        var subject = $"You finished {title}";
        var greeting = $"Hi {contact.DisplayName},";
        var lead = $"You have completed {title}. That is worth a moment.";
        var closing = "Your progress is saved, and the course stays open if you want to revisit it.";

        var html = $"""
            <!doctype html>
            <html lang="en">
              <body style="margin:0;padding:24px;background:#f6f7f9;font-family:system-ui,-apple-system,'Segoe UI',sans-serif;color:#1c2530;">
                <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;padding:32px;">
                  <p style="margin:0 0 16px;font-size:16px;">{System.Net.WebUtility.HtmlEncode(greeting)}</p>
                  <p style="margin:0 0 24px;line-height:1.6;">{System.Net.WebUtility.HtmlEncode(lead)}</p>
                  <p style="margin:0;font-size:13px;color:#5b6672;line-height:1.6;">{System.Net.WebUtility.HtmlEncode(closing)}</p>
                </div>
              </body>
            </html>
            """;

        var text = $"""
            {greeting}

            {lead}

            {closing}
            """;

        await email.SendAsync(new EmailMessage(contact.Email, subject, html, text), ct);
    }
}
