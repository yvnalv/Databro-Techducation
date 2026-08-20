using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Abstractions;

namespace DataBro.Modules.Learning.Application;

/// <summary>
/// Which timezone a "day" belongs to.
///
/// <para>
/// <b>The hardest part of a streak feature, and there is no free answer.</b> UTC days are the easy
/// choice and are wrong for this audience: a learner in WIB (UTC+7) studying at 23:00 Monday and
/// again at 01:00 Tuesday has studied on two local days, but both instants fall on the same UTC day
/// — so their streak would silently count one. The error always undercounts, and always for the
/// people furthest from UTC, which for DataBro is nearly everyone.
/// </para>
/// <para>
/// So the platform picks a zone. That is also a simplification — a learner outside it sees days roll
/// over at an odd hour — but it is <i>correct for the audience the product is built for</i> rather
/// than correct for nobody. The upgrade is a per-user timezone once Identity has a profile to hang it
/// on (S-2); this then becomes one argument instead of one setting, and the domain does not change,
/// because <see cref="LearnerStreak.RecordActivity"/> already takes a day rather than an instant.
/// </para>
/// </summary>
public sealed class StreakOptions
{
    public const string SectionName = "Learning:Streaks";

    /// <summary>An IANA zone id. Defaults to the launch audience rather than to UTC.</summary>
    public string TimeZone { get; set; } = "Asia/Jakarta";
}

/// <summary>
/// Reads and advances a learner's streak.
///
/// <para>
/// Takes the learner's id explicitly, like the other learner-owned services: on a surface where the
/// id is the authorization boundary, an implicit one is what gets forgotten in a branch.
/// </para>
/// </summary>
public sealed class StreakService(
    ILearnerStreakRepository streaks,
    IClock clock,
    StreakOptions options)
{

    /// <summary>Today, in the configured zone.</summary>
    public DateOnly Today()
    {
        var zone = ResolveZone();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, zone).DateTime);
    }

    /// <summary>
    /// Records a qualifying activity and returns the streak afterwards.
    ///
    /// <para>
    /// Called when a lesson is completed — the platform's existing "you did the work" signal, and
    /// since S-6 it already implies passing any quiz the lesson carries. Opening a lesson deliberately
    /// does not count: a streak that rewards visiting rewards the wrong thing.
    /// </para>
    /// </summary>
    public async Task<StreakDto> RecordActivityAsync(Guid userId, CancellationToken ct = default)
    {
        var streak = await streaks.GetAsync(userId, ct);

        if (streak is null)
        {
            streak = LearnerStreak.Start(Guid.NewGuid(), userId);
            await streaks.AddAsync(streak, ct);
        }

        var today = Today();

        if (streak.RecordActivity(today))
            await streaks.SaveChangesAsync(ct);

        return Compose(streak, today);
    }

    public async Task<StreakDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var streak = await streaks.GetAsync(userId, ct);
        var today = Today();

        return streak is null
            ? new StreakDto(0, 0, null, false)
            : Compose(streak, today);
    }

    private static StreakDto Compose(LearnerStreak streak, DateOnly today) =>
        new(streak.CurrentAsOf(today), streak.Longest, streak.LastActiveOn, streak.IsActiveOn(today));

    /// <summary>
    /// Resolves the configured zone, falling back to UTC rather than throwing.
    ///
    /// <para>
    /// A bad zone id is a configuration mistake, and the right blast radius for it is a slightly
    /// wrong streak — not a 500 on lesson completion, which is the request that would carry it.
    /// </para>
    /// </summary>
    private TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
