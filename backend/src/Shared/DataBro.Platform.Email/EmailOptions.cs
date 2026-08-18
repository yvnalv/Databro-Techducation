namespace DataBro.Platform.Email;

/// <summary>
/// Transport configuration. Bound from <c>Email</c> in configuration.
///
/// <para>
/// <b>No credentials have defaults.</b> A password that falls back to something is a password that
/// works by accident in an environment nobody meant to configure.
/// </para>
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// <c>log</c> (default) or <c>smtp</c>. Selection is configuration, never a compile-time choice
    /// (CLAUDE.md rule 14) — the same reason `ILlmProvider` will be config-selected later.
    /// </summary>
    public string Provider { get; set; } = "log";

    /// <summary>Envelope sender. Required once a real transport is selected.</summary>
    public string FromAddress { get; set; } = "no-reply@databro.local";

    public string FromName { get; set; } = "DataBro";

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";

    /// <summary>1025 is Mailpit's submission port — the local default, not a production one.</summary>
    public int Port { get; set; } = 1025;

    /// <summary>
    /// STARTTLS. Off by default because the default host is a local capture server with no
    /// certificate; every deployed environment sets it on.
    /// </summary>
    public bool UseTls { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}
