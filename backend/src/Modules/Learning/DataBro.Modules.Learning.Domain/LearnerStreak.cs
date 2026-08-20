using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Domain;

/// <summary>
/// How many days in a row a learner has finished something.
///
/// <para>
/// Its own aggregate root beside <see cref="Enrollment"/> and <see cref="Bookmark"/>: learner-owned,
/// one row per person, written whenever they complete a lesson. Nothing else has a reason to load it.
/// </para>
/// <para>
/// <b>Stored rather than derived.</b> The current streak could be computed from recent
/// <c>lesson_progress</c> rows cheaply enough, but the longest-ever streak cannot — that is a scan of
/// a learner's whole history on every dashboard load. Storing both keeps the read trivial, and the
/// progress rows remain the source of truth it can be rebuilt from if this ever drifts.
/// </para>
/// <para>
/// <b>The day is a <see cref="DateOnly"/> in a chosen timezone, never a UTC instant</b> — see
/// <see cref="RecordActivity"/>. That choice is the whole difficulty of a streak feature.
/// </para>
/// </summary>
public sealed class LearnerStreak : AggregateRoot
{
    public Guid UserId { get; private set; }

    /// <summary>Consecutive days up to and including <see cref="LastActiveOn"/>.</summary>
    public int Current { get; private set; }

    /// <summary>
    /// The best run ever. Never decreases — a broken streak is a fact about now, not a reason to
    /// erase what someone already did.
    /// </summary>
    public int Longest { get; private set; }

    /// <summary>The last local day with activity. Null only before the first ever completion.</summary>
    public DateOnly? LastActiveOn { get; private set; }

    private LearnerStreak() { } // EF

    public static LearnerStreak Start(Guid id, Guid userId) =>
        new() { Id = id, UserId = userId, Current = 0, Longest = 0 };

    /// <summary>
    /// Records that the learner finished something on <paramref name="localDay"/>.
    ///
    /// <para>
    /// Takes a <see cref="DateOnly"/> rather than a timestamp on purpose: the caller has already
    /// decided which day an instant belongs to, and that decision needs a timezone the domain has no
    /// business knowing. Passing an instant here would push a timezone lookup into the aggregate and
    /// make this logic untestable without one.
    /// </para>
    /// <para>
    /// Returns true only when the streak actually advanced, so a caller can react to a milestone
    /// once rather than on every completion of the day.
    /// </para>
    /// </summary>
    public bool RecordActivity(DateOnly localDay)
    {
        // Same day again: finishing a fifth lesson before bed is not a fifth day.
        if (LastActiveOn == localDay) return false;

        // Out-of-order arrival — a backfill, a clock skew, a replayed request. Ignored rather than
        // treated as a gap, because rewinding a streak on a late-arriving old event would punish a
        // learner for something the system did.
        if (LastActiveOn is { } last && localDay < last) return false;

        Current = LastActiveOn is { } previous && localDay == previous.AddDays(1)
            ? Current + 1
            : 1; // First ever, or the run was broken by at least one missed day.

        LastActiveOn = localDay;
        if (Current > Longest) Longest = Current;

        return true;
    }

    /// <summary>
    /// The streak as of <paramref name="today"/>, which is not always the stored
    /// <see cref="Current"/>.
    ///
    /// <para>
    /// A streak decays with time rather than with writes: someone who last studied three days ago has
    /// a stored <c>Current</c> of 5 and an actual streak of 0, and nothing will have updated the row
    /// because they have not been back. Reading it raw would tell a learner they are on a 5-day run
    /// while they are not — so the read applies the passage of time and the write does not.
    /// </para>
    /// <para>
    /// Yesterday still counts: a learner who studied yesterday and has not yet studied today is
    /// mid-streak, not lapsed. Only a gap of two or more days breaks it.
    /// </para>
    /// </summary>
    public int CurrentAsOf(DateOnly today)
    {
        if (LastActiveOn is not { } last) return 0;

        var gap = today.DayNumber - last.DayNumber;

        // A future LastActiveOn means a clock moved backwards; treat the stored value as current
        // rather than inventing a negative gap.
        if (gap <= 0) return Current;

        return gap == 1 ? Current : 0;
    }

    /// <summary>Whether the learner has already been counted today — what a UI needs to say "done today".</summary>
    public bool IsActiveOn(DateOnly day) => LastActiveOn == day;
}
