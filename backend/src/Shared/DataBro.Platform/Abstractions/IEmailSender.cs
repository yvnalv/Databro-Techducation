namespace DataBro.Platform.Abstractions;

/// <summary>
/// One outbound email, provider-agnostic.
/// </summary>
/// <param name="HtmlBody">
/// The rendered body. Composed by the calling module from its own templates — the transport never
/// knows what an email is *about*, only how to put it on the wire.
/// </param>
/// <param name="TextBody">
/// The plain-text alternative. <b>Required, not optional.</b> A multipart message with only HTML is
/// a well-known spam signal, and plenty of clients still render text first; making it a required
/// parameter is what stops it being the field everyone forgets.
/// </param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody);

/// <summary>
/// Sends transactional email, behind an abstraction so no business logic knows the provider
/// (CLAUDE.md rule 14).
///
/// <para>
/// Deliberately dumb: a to-address, a subject and two bodies. Templating, localisation and the
/// decision of *what* to say belong to the module raising the message — Identity knows what a
/// verification email is, and the transport must not.
/// </para>
/// <para>
/// <b>Failure is surfaced, not swallowed.</b> An implementation throws when it cannot send. Callers
/// that must not fail because of email decide that for themselves; a sender that silently returns
/// success would make the outbox — whose entire job is retrying what failed — unable to tell that
/// anything had.
/// </para>
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
