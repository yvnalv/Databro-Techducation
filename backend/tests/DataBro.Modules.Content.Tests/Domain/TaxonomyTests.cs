using DataBro.Modules.Content.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Domain;

/// <summary>Business rules TX-3 and CT-11 (docs/BUSINESS_RULES.md).</summary>
public class CategoryTests
{
    private static Category NewCategory(string slug = "machine-learning", Guid? parentId = null) =>
        Category.Create(Guid.NewGuid(), Slug.Create(slug), "Machine Learning", parentId);

    [Fact]
    public void Create_normalizes_name_and_description()
    {
        var category = Category.Create(
            Guid.NewGuid(), Slug.Create("deep-learning"), "  Deep Learning  ", description: "  Nets.  ");

        Assert.Equal("Deep Learning", category.Name);
        Assert.Equal("Nets.", category.Description);
    }

    [Fact]
    public void Blank_description_becomes_null_rather_than_an_empty_string()
    {
        var category = Category.Create(Guid.NewGuid(), Slug.Create("x"), "X", description: "   ");
        Assert.Null(category.Description);
    }

    // TX-3: cycles are disallowed.
    [Fact]
    public void A_category_cannot_be_its_own_parent()
    {
        var category = NewCategory();

        var result = category.MoveTo(category.Id, []);

        Assert.True(result.IsFailure);
        Assert.Equal("business_rule_violation", result.Error.Code);
    }

    [Fact]
    public void A_category_cannot_move_beneath_its_own_descendant()
    {
        var parent = NewCategory("ai");
        var child = NewCategory("nlp", parent.Id);

        // Moving `parent` under `child`, whose ancestry contains `parent`, would close a cycle.
        var result = parent.MoveTo(child.Id, [child.Id, parent.Id]);

        Assert.True(result.IsFailure);
        Assert.Contains("cycle", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_category_can_move_beneath_an_unrelated_category()
    {
        var category = NewCategory("nlp");
        var newParent = NewCategory("ai");

        var result = category.MoveTo(newParent.Id, [newParent.Id]);

        Assert.True(result.IsSuccess);
        Assert.Equal(newParent.Id, category.ParentId);
    }

    [Fact]
    public void A_category_can_be_moved_to_the_root()
    {
        var parent = NewCategory("ai");
        var category = NewCategory("nlp", parent.Id);

        var result = category.MoveTo(null, []);

        Assert.True(result.IsSuccess);
        Assert.Null(category.ParentId);
    }

    [Fact]
    public void Update_does_not_change_the_slug()
    {
        // A category slug is a public URL; changing it needs a 301 record (CT-3), which is why
        // Update deliberately has no slug parameter.
        var category = NewCategory("machine-learning");

        category.Update("Renamed Entirely", null, 5);

        Assert.Equal("machine-learning", category.Slug.Value);
        Assert.Equal("Renamed Entirely", category.Name);
        Assert.Equal(5, category.Order);
    }
}

public class ArticleTaggingTests
{
    private static Article NewArticle() => Article.CreateDraft(
        Guid.NewGuid(),
        Slug.Create("an-article"),
        "An Article",
        "Summary",
        Guid.NewGuid(),
        new ContentDocument { Version = 1, Blocks = [] });

    // CT-11: an article carries any number of tags.
    [Fact]
    public void Tags_can_be_assigned_and_replaced()
    {
        var article = NewArticle();
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Both sides ordered: SetTags makes no promise about ordering, only about set membership.
        article.SetTags([a, b]);
        Assert.Equal(new[] { a, b }.Order(), article.TagIds.Order());

        article.SetTags([b, c]);
        Assert.Equal(new[] { b, c }.Order(), article.TagIds.Order());
    }

    [Fact]
    public void Duplicate_tag_ids_are_collapsed()
    {
        var article = NewArticle();
        var tag = Guid.NewGuid();

        article.SetTags([tag, tag, tag]);

        Assert.Single(article.TagIds);
    }

    [Fact]
    public void Reassigning_the_same_tags_preserves_the_existing_links()
    {
        // Churning links on every save would make EF delete and reinsert rows needlessly, so
        // SetTags is deliberately idempotent.
        var article = NewArticle();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());

        article.SetTags([a, b]);
        var first = article.TagIds.ToList();

        article.SetTags([b, a]); // same set, different order

        Assert.Equal(first.Order(), article.TagIds.Order());
    }

    [Fact]
    public void Tags_can_be_cleared()
    {
        var article = NewArticle();
        article.SetTags([Guid.NewGuid()]);

        article.SetTags([]);

        Assert.Empty(article.TagIds);
    }

    // CT-11: at most one category.
    [Fact]
    public void Category_can_be_set_and_cleared()
    {
        var article = NewArticle();
        var category = Guid.NewGuid();

        article.SetCategory(category);
        Assert.Equal(category, article.CategoryId);

        article.SetCategory(null);
        Assert.Null(article.CategoryId);
    }
}
