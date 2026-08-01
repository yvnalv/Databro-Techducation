using DataBro.Modules.Content.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Domain;

public class RedirectTests
{
    [Theory]
    [InlineData("/articles/x", "/articles/x")]
    [InlineData("articles/x", "/articles/x")]      // adds the leading slash
    [InlineData("/Articles/X", "/articles/x")]     // lowercased
    [InlineData("/articles/x/", "/articles/x")]     // trailing slash trimmed
    [InlineData("/articles/x?ref=nl", "/articles/x")] // query dropped
    [InlineData("/articles/x#top", "/articles/x")]   // fragment dropped
    [InlineData("  /articles/x  ", "/articles/x")]   // trimmed
    public void NormalizePath_matches_the_edge_normalization(string input, string expected)
        => Assert.Equal(expected, Redirect.NormalizePath(input));

    [Fact]
    public void Create_normalizes_both_paths()
    {
        var redirect = Redirect.Create(Guid.NewGuid(), "/Articles/Old/", "articles/new", "moved");

        Assert.Equal("/articles/old", redirect.FromPath);
        Assert.Equal("/articles/new", redirect.ToPath);
        Assert.Equal(301, redirect.StatusCode);
        Assert.Equal("moved", redirect.Reason);
    }

    [Fact]
    public void Create_rejects_a_self_redirect()
        => Assert.Throws<ArgumentException>(
            () => Redirect.Create(Guid.NewGuid(), "/articles/x", "/articles/x/"));

    [Fact]
    public void RepointTo_normalizes_the_new_destination()
    {
        var redirect = Redirect.Create(Guid.NewGuid(), "/a", "/b");

        redirect.RepointTo("/C/");

        Assert.Equal("/c", redirect.ToPath);
    }
}
