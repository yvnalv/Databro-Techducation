using DataBro.Modules.Learning.Domain;
using Xunit;

namespace DataBro.Modules.Learning.Tests;

/// <summary>
/// Streak arithmetic (LN-15 … LN-19).
///
/// Two rules carry the whole feature and neither is obvious from the field names: a streak decays
/// with the passage of time rather than with writes, so the read and the write disagree on purpose;
/// and a day is a local day, which is why nothing here takes a timestamp.
/// </summary>
public class LearnerStreakTests
{
    private static readonly DateOnly Mon = new(2026, 8, 17);
    private static DateOnly Day(int offset) => Mon.AddDays(offset);

    private static LearnerStreak New() => LearnerStreak.Start(Guid.NewGuid(), Guid.NewGuid());

    /// <summary>Records activity on each given day offset from Monday.</summary>
    private static LearnerStreak After(params int[] offsets)
    {
        var streak = New();
        foreach (var offset in offsets) streak.RecordActivity(Day(offset));
        return streak;
    }

    // ---- Advancing ----

    [Fact]
    public void A_first_completion_starts_a_streak_of_one()
    {
        var streak = After(0);

        Assert.Equal(1, streak.Current);
        Assert.Equal(1, streak.Longest);
        Assert.Equal(Mon, streak.LastActiveOn);
    }

    [Fact]
    public void Consecutive_days_accumulate()
    {
        Assert.Equal(3, After(0, 1, 2).Current);
    }

    [Fact]
    public void A_missed_day_restarts_the_count_at_one()
    {
        // Thursday after a Wednesday off is a new run, not a continuation.
        var streak = After(0, 1, 3);

        Assert.Equal(1, streak.Current);
    }

    [Fact]
    public void The_longest_run_survives_a_broken_one()
    {
        // The point of storing Longest separately: breaking a streak is not a reason to erase the
        // fact that someone once studied three days running.
        var streak = After(0, 1, 2, 5);

        Assert.Equal(1, streak.Current);
        Assert.Equal(3, streak.Longest);
    }

    // ---- Not advancing ----

    [Fact]
    public void A_second_completion_on_the_same_day_does_not_count_twice()
    {
        var streak = After(0);

        Assert.False(streak.RecordActivity(Mon));
        Assert.Equal(1, streak.Current);
    }

    [Fact]
    public void A_completion_dated_before_the_last_one_is_ignored()
    {
        // A replayed request or a clock that stepped backwards. Rewinding here would punish a
        // learner for something the system did.
        var streak = After(0, 1);

        Assert.False(streak.RecordActivity(Day(0)));
        Assert.Equal(2, streak.Current);
        Assert.Equal(Day(1), streak.LastActiveOn);
    }

    [Fact]
    public void Advancing_is_reported_only_when_the_streak_actually_moved()
    {
        var streak = New();

        Assert.True(streak.RecordActivity(Day(0)));
        Assert.False(streak.RecordActivity(Day(0)));
        Assert.True(streak.RecordActivity(Day(1)));
    }

    // ---- Decay: the read and the stored value disagree on purpose ----

    [Fact]
    public void Studying_yesterday_still_counts_today()
    {
        // Mid-streak, not lapsed. Someone who studied last night and opens the app in the morning
        // has not lost anything, and telling them they have is how a streak feature loses a user.
        var streak = After(0, 1);

        Assert.Equal(2, streak.CurrentAsOf(Day(2)));
    }

    [Fact]
    public void A_two_day_gap_reads_as_zero_even_though_nothing_wrote_to_the_row()
    {
        // The stored counter is untouched — the learner has not been back to touch it. Only the
        // read applies the passage of time.
        var streak = After(0, 1, 2);

        Assert.Equal(3, streak.Current);
        Assert.Equal(0, streak.CurrentAsOf(Day(4)));
        Assert.Equal(3, streak.Longest);
    }

    [Fact]
    public void A_streak_that_lapsed_and_resumed_reads_from_the_new_run()
    {
        var streak = After(0, 1, 2);
        Assert.Equal(0, streak.CurrentAsOf(Day(9)));

        streak.RecordActivity(Day(9));

        Assert.Equal(1, streak.CurrentAsOf(Day(9)));
        Assert.Equal(3, streak.Longest);
    }

    [Fact]
    public void A_learner_who_has_never_finished_anything_reads_as_zero()
    {
        var streak = New();

        Assert.Equal(0, streak.CurrentAsOf(Mon));
        Assert.Null(streak.LastActiveOn);
        Assert.False(streak.IsActiveOn(Mon));
    }

    [Fact]
    public void Today_is_reported_as_already_counted()
    {
        var streak = After(0);

        Assert.True(streak.IsActiveOn(Mon));
        Assert.False(streak.IsActiveOn(Day(1)));
    }
}
