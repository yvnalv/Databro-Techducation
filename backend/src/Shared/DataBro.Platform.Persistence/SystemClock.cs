using DataBro.Platform.Abstractions;

namespace DataBro.Platform.Persistence;

/// <summary>Default <see cref="IClock"/> backed by the system clock (UTC).</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
