using DataBro.Modules.Learning.Domain;
using DataBro.Platform.SharedKernel;
using Xunit;

namespace DataBro.Modules.Learning.Tests;

/// <summary>
/// A path references courses it does not own (ADR-0013), so these pin the reference semantics rather
/// than any containment.
/// </summary>
public class LearningPathTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    private static LearningPath NewPath() =>
        LearningPath.CreateDraft(
            Guid.NewGuid(), Slug.Create("become-an-llm-engineer"), "Become an LLM Engineer", "A summary");

    [Fact]
    public void Courses_keep_the_order_they_were_added_in()
    {
        var path = NewPath();
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        path.AddCourse(a);
        path.AddCourse(b);
        path.AddCourse(c);

        Assert.Equal([a, b, c], path.CourseIds);
    }

    [Fact]
    public void Adding_the_same_course_twice_is_a_no_op()
    {
        // A builder UI dropping the same card twice is a slip, not a decision worth refusing.
        var path = NewPath();
        var course = Guid.NewGuid();

        path.AddCourse(course);
        path.AddCourse(course);

        Assert.Single(path.CourseIds);
    }

    [Fact]
    public void Removing_a_course_closes_the_gap()
    {
        var path = NewPath();
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        path.AddCourse(a);
        path.AddCourse(b);
        path.AddCourse(c);

        path.RemoveCourse(b);

        Assert.Equal([a, c], path.CourseIds);
    }

    [Fact]
    public void Reordering_puts_named_courses_first_and_keeps_the_rest_behind()
    {
        var path = NewPath();
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        path.AddCourse(a);
        path.AddCourse(b);
        path.AddCourse(c);

        Assert.True(path.ReorderCourses([c]).IsSuccess);

        Assert.Equal([c, a, b], path.CourseIds);
    }

    [Fact]
    public void A_reorder_naming_a_course_outside_the_path_is_refused()
    {
        var path = NewPath();
        path.AddCourse(Guid.NewGuid());

        Assert.True(path.ReorderCourses([Guid.NewGuid()]).IsFailure);
    }

    [Fact]
    public void An_empty_path_cannot_be_published()
    {
        var path = NewPath();

        var result = path.Publish(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("business_rule_violation", result.Error.Code);
    }

    [Fact]
    public void A_path_publishes_without_requiring_its_courses_to_be_published()
    {
        // Same reasoning as a course and its lessons: a track can go live and fill out over time.
        var path = NewPath();
        path.AddCourse(Guid.NewGuid());

        Assert.True(path.Publish(Now).IsSuccess);
        Assert.Equal(CourseStatus.Published, path.Status);
        Assert.Equal(Now, path.PublishedAt);
    }
}
