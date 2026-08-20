using DataBro.Modules.Identity.Application;
using DataBro.Modules.Identity.Infrastructure.Auth;
using DataBro.Modules.Identity.Infrastructure.Auth.External;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>
/// The social-login building blocks in isolation (ADR-0019): the signed state, the single-use handoff
/// store, and GitHub's verified-primary email rule. The full round-trip is covered in
/// <see cref="ExternalAuthApiTests"/>; these pin the pieces the flow relies on.
/// </summary>
public class ExternalAuthUnitTests
{
    private static OAuthStateProtector Protector(string key = "unit-test-signing-key-at-least-32-bytes!!") =>
        new(Options.Create(new JwtOptions { Key = key }));

    [Fact]
    public void State_round_trips_intact()
    {
        var protector = Protector();
        var original = new OAuthState("google", "nonce-1", DateTimeOffset.UtcNow, "/dashboard");

        var restored = protector.Unprotect(protector.Protect(original), TimeSpan.FromMinutes(10));

        Assert.NotNull(restored);
        Assert.Equal("google", restored!.Provider);
        Assert.Equal("/dashboard", restored.ReturnTo);
    }

    [Fact]
    public void Tampered_state_is_rejected()
    {
        var protector = Protector();
        var token = protector.Protect(new OAuthState("google", "n", DateTimeOffset.UtcNow, null));

        // Flip a character in the payload; the signature no longer matches.
        var tampered = (token[0] == 'A' ? 'B' : 'A') + token[1..];

        Assert.Null(protector.Unprotect(tampered, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void State_signed_with_a_different_key_is_rejected()
    {
        var token = Protector("first-key-first-key-first-key-first-key!").Protect(
            new OAuthState("google", "n", DateTimeOffset.UtcNow, null));

        Assert.Null(Protector("second-key-second-key-second-key-second!").Unprotect(token, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void Stale_state_is_rejected()
    {
        var protector = Protector();
        var token = protector.Protect(
            new OAuthState("google", "n", DateTimeOffset.UtcNow.AddMinutes(-11), null));

        Assert.Null(protector.Unprotect(token, TimeSpan.FromMinutes(10)));
    }

    private static DistributedCacheAuthCodeStore Store()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new DistributedCacheAuthCodeStore(cache, Options.Create(new ExternalAuthOptions()));
    }

    [Fact]
    public async Task Handoff_code_redeems_once_then_is_gone()
    {
        var store = Store();
        var tokens = new AuthTokens("access", "refresh", 900);

        var code = await store.IssueAsync(tokens);

        var first = await store.RedeemAsync(code);
        Assert.NotNull(first);
        Assert.Equal("access", first!.AccessToken);

        // Single-use: a replayed code finds nothing.
        Assert.Null(await store.RedeemAsync(code));
    }

    [Fact]
    public async Task Unknown_handoff_code_redeems_to_nothing()
    {
        Assert.Null(await Store().RedeemAsync("never-issued"));
    }

    [Fact]
    public void GitHub_picks_the_primary_verified_email()
    {
        var emails = new[]
        {
            new GitHubProvider.EmailBody("secondary@x.test", Primary: false, Verified: true),
            new GitHubProvider.EmailBody("primary@x.test", Primary: true, Verified: true),
        };

        Assert.Equal("primary@x.test", GitHubProvider.SelectPrimaryVerifiedEmail(emails));
    }

    [Fact]
    public void GitHub_refuses_when_no_email_is_both_primary_and_verified()
    {
        var emails = new[]
        {
            // Primary but unverified, and verified but not primary — neither qualifies.
            new GitHubProvider.EmailBody("primary@x.test", Primary: true, Verified: false),
            new GitHubProvider.EmailBody("other@x.test", Primary: false, Verified: true),
        };

        Assert.Null(GitHubProvider.SelectPrimaryVerifiedEmail(emails));
        Assert.Null(GitHubProvider.SelectPrimaryVerifiedEmail(null));
    }
}
