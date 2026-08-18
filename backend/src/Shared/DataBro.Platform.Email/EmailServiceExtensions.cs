using DataBro.Platform.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataBro.Platform.Email;

/// <summary>Registers the transactional email transport selected by configuration.</summary>
public static class EmailServiceExtensions
{
    public static IServiceCollection AddPlatformEmail(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        var provider = configuration[$"{EmailOptions.SectionName}:Provider"]?.Trim().ToLowerInvariant()
            ?? "log";

        switch (provider)
        {
            case "smtp":
                services.TryAddScoped<IEmailSender, SmtpEmailSender>();
                break;

            case "log":
                services.TryAddScoped<IEmailSender, LoggingEmailSender>();
                break;

            default:
                // An unrecognised provider is a configuration mistake, and falling back to "log"
                // would hide it: mail would appear to work in production while going nowhere.
                throw new InvalidOperationException(
                    $"Unknown email provider '{provider}'. Expected 'log' or 'smtp'.");
        }

        if (provider == "log" && !environment.IsDevelopment())
        {
            // Loud, and deliberately not an exception: refusing to start would take an otherwise
            // healthy deployment offline over email. The log line is what a deploy checklist greps.
            services.AddSingleton<IHostedService>(sp =>
                new EmailProviderWarning(
                    sp.GetRequiredService<ILogger<EmailProviderWarning>>(), environment.EnvironmentName));
        }

        return services;
    }
}

/// <summary>
/// Says once, at startup, that email is going to the log in an environment that is not development —
/// which also means verification tokens are being written there.
/// </summary>
internal sealed class EmailProviderWarning(ILogger<EmailProviderWarning> logger, string environmentName)
    : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        logger.LogWarning(
            "Email provider is 'log' in environment {Environment}. No mail will be delivered, and " +
            "verification tokens are being written to the log. Set Email:Provider to 'smtp'.",
            environmentName);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
