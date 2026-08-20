using DataBro.Modules.Learning.Application;
using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Abstractions;
using Xunit;

namespace DataBro.Modules.Learning.Tests;

/// <summary>
/// Which day an instant belongs to (LN-15).
///
/// The reason this is tested at all: the failure it guards against is silent. If the configured zone
/// is not applied — a typo, or a runtime image with no tzdata — nothing errors. Streaks just quietly
/// undercount for everyone who studies in the evening, and the only symptom is learners saying the
/// number "feels wrong".
/// </summary>
public class StreakTimeZoneTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    /// <summary>Enough of the port to construct the service; these tests never touch storage.</summary>
    private sealed class NoStreaks : ILearnerStreakRepository
    {
        public Task<LearnerStreak?> GetAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<LearnerStreak?>(null);

        public Task AddAsync(LearnerStreak streak, CancellationToken ct = default) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private static StreakService WithZone(string zone, DateTimeOffset utcNow) =>
        new(new NoStreaks(), new FixedClock(utcNow), new StreakOptions { TimeZone = zone });

    [Fact]
    public void Late_evening_in_Jakarta_is_still_that_evening_and_not_the_same_UTC_day_as_the_night_before()
    {
        // The exact case that makes UTC days wrong. 23:00 Monday WIB and 01:00 Tuesday WIB are two
        // days of study, but 16:00 and 18:00 on the *same* Monday in UTC — so a UTC-day streak would
        // count them once.
        var mondayNight = WithZone("Asia/Jakarta", new DateTimeOffset(2026, 8, 17, 16, 0, 0, TimeSpan.Zero));
        var tuesdayMorning = WithZone("Asia/Jakarta", new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 8, 17), mondayNight.Today());
        Assert.Equal(new DateOnly(2026, 8, 18), tuesdayMorning.Today());
        Assert.NotEqual(mondayNight.Today(), tuesdayMorning.Today());
    }

    [Fact]
    public void The_configured_zone_is_actually_applied_and_not_ignored()
    {
        // Two zones, one instant, two different days. If the zone were being dropped on the floor —
        // a missing tzdata, a binding that never fired — both would read as the UTC day and this
        // would fail.
        var instant = new DateTimeOffset(2026, 8, 20, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 8, 20), WithZone("UTC", instant).Today());
        Assert.Equal(new DateOnly(2026, 8, 21), WithZone("Asia/Jakarta", instant).Today());
        Assert.Equal(new DateOnly(2026, 8, 20), WithZone("America/New_York", instant).Today());
    }

    [Fact]
    public void An_unrecognised_zone_falls_back_to_UTC_rather_than_failing_a_lesson_completion()
    {
        // A bad zone id is a configuration mistake. The right blast radius for it is a slightly wrong
        // streak, not a 500 on the request that records a learner's work.
        var service = WithZone("Not/AZone", new DateTimeOffset(2026, 8, 20, 22, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 8, 20), service.Today());
    }
}
