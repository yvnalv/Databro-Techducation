using DataBro.Modules.Content.Domain;
using Xunit;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Tests.Domain;

public class SlugTests
{
    [Theory]
    [InlineData("python-virtual-environments")]
    [InlineData("sql-101")]
    [InlineData("a")]
    public void Create_accepts_valid_slugs(string value)
    {
        var slug = Slug.Create(value);
        Assert.Equal(value, slug.Value);
    }

    [Fact]
    public void Create_lowercases_input()
    {
        Assert.Equal("intro-to-ml", Slug.Create("Intro-To-ML").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has spaces")]
    [InlineData("Special!Chars")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    public void Create_rejects_invalid_slugs(string value)
    {
        Assert.Throws<ArgumentException>(() => Slug.Create(value));
    }

    [Fact]
    public void FromText_slugifies_a_title()
    {
        Assert.Equal("getting-started-with-python", Slug.FromText("  Getting Started with Python!  ").Value);
    }

    [Fact]
    public void Equality_is_by_value()
    {
        Assert.Equal(Slug.Create("abc"), Slug.Create("abc"));
        Assert.NotEqual(Slug.Create("abc"), Slug.Create("abd"));
    }
}
