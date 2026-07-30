namespace DataBro.Platform.Abstractions;

/// <summary>Abstracts the current time so domain/application logic is testable and deterministic.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
