namespace DataBro.Modules.Learning.Domain;

/// <summary>
/// A course's own publish state (ADR-0013), independent of its lessons'. A published course shows
/// only its published lessons rather than refusing to publish until every one is finished.
/// </summary>
public enum CourseStatus
{
    Draft,
    Published,

    /// <summary>Taken down after publication. Distinct from <see cref="Draft"/>: it has been live.</summary>
    Unpublished,
}

/// <summary>
/// Difficulty as a learner reads it, not as a number to sort by. Three levels because a scale with
/// more than that invites arguments about the middle and tells a learner nothing extra.
/// </summary>
public enum Difficulty
{
    Beginner,
    Intermediate,
    Advanced,
}
