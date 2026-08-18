using DataBro.Modules.Identity.Application;
using DataBro.Modules.Identity.Infrastructure.Auth;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace DataBro.Modules.Content.Tests;

/// <summary>
/// The transactional email seam: what Identity composes, and what the transport does with it.
///
/// No SMTP here — the sender is a capture double. What is worth pinning is the shape of the message,
/// because the two failure modes are silent: a token mangled in a URL, and a display name that
/// escapes into markup.
/// </summary>
public class EmailTests
{
    private sealed class CapturingSender : IEmailSender
    {
        public EmailMessage? Sent { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent = message;
            return Task.CompletedTask;
        }
    }

    private static (CapturingSender Sender, IIdentityEmails Emails) Build(string locale = "en")
    {
        var sender = new CapturingSender();

        var emails = new IdentityEmails(
            sender,
            Options.Create(new IdentityEmailOptions
            {
                AppBaseUrl = "http://localhost:3001",
                DefaultLocale = locale,
            }));

        return (sender, emails);
    }

    [Fact]
    public async Task A_confirmation_token_is_url_encoded()
    {
        // ASP.NET Core Identity's tokens are base64 and routinely contain `+` and `/`. An unencoded
        // `+` arrives at the server as a space, so the confirmation fails for a fraction of users
        // and works for everyone who tests it once.
        var (sender, emails) = Build();
        var userId = Guid.NewGuid();

        await emails.SendEmailConfirmationAsync("learner@databro.test", "Learner", userId, "abc+def/ghi==");

        Assert.NotNull(sender.Sent);
        Assert.Contains("token=abc%2Bdef%2Fghi%3D%3D", sender.Sent!.TextBody);
        Assert.DoesNotContain("token=abc+def/ghi", sender.Sent.TextBody);
        Assert.Contains($"userId={userId}", sender.Sent.TextBody);
    }

    [Fact]
    public async Task A_display_name_is_escaped_into_the_html_body()
    {
        // A display name is user input and this puts it into markup. An email client is a HTML
        // renderer like any other.
        var (sender, emails) = Build();

        await emails.SendEmailConfirmationAsync(
            "learner@databro.test", "<script>alert(1)</script>", Guid.NewGuid(), "token");

        Assert.DoesNotContain("<script>", sender.Sent!.HtmlBody);
        Assert.Contains("&lt;script&gt;", sender.Sent.HtmlBody);
    }

    [Fact]
    public async Task Both_a_text_and_an_html_body_are_always_produced()
    {
        // HTML-only is a spam signal, and a text client cannot click a button — so the link has to
        // appear in full in the text part rather than only behind the HTML anchor.
        var (sender, emails) = Build();

        await emails.SendEmailConfirmationAsync("learner@databro.test", "Learner", Guid.NewGuid(), "tok");

        Assert.False(string.IsNullOrWhiteSpace(sender.Sent!.TextBody));
        Assert.False(string.IsNullOrWhiteSpace(sender.Sent.HtmlBody));
        Assert.Contains("http://localhost:3001/verify-email", sender.Sent.TextBody);
        Assert.DoesNotContain("<", sender.Sent.TextBody);
    }

    [Fact]
    public async Task The_email_is_written_in_the_configured_locale()
    {
        // Rule 19 governs UI chrome, and an email is chrome that arrives by post.
        var (english, enEmails) = Build("en");
        var (indonesian, idEmails) = Build("id");

        await enEmails.SendEmailConfirmationAsync("a@databro.test", "A", Guid.NewGuid(), "t");
        await idEmails.SendEmailConfirmationAsync("b@databro.test", "B", Guid.NewGuid(), "t");

        Assert.Equal("Confirm your email address", english.Sent!.Subject);
        Assert.Equal("Konfirmasi alamat email Anda", indonesian.Sent!.Subject);
        Assert.Contains("lang=\"id\"", indonesian.Sent.HtmlBody);
    }

    [Fact]
    public void An_unknown_provider_fails_loudly_rather_than_falling_back()
    {
        // Falling back to 'log' would hide a typo in production: mail would appear to work while
        // going nowhere.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Email:Provider"] = "carrier-pigeon" })
            .Build();

        var environment = new TestEnvironment();

        Assert.Throws<InvalidOperationException>(
            () => { services.AddPlatformEmail(configuration, environment); });
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
