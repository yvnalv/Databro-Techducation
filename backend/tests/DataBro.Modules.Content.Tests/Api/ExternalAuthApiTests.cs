using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Identity.Application;
using DataBro.Modules.Identity.Infrastructure.Persistence;
using DataBro.Platform.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>
/// The social-login round-trip end to end (ADR-0019), with only the provider's HTTP boundary faked:
/// challenge → callback → single-use handoff exchange, against a real database. This exercises the
/// signed state, the code store, and the link-or-create rule (ID-3) as one flow — the parts a unit
/// test cannot see cooperating.
/// </summary>
public class ExternalAuthApiTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    /// <summary>A stand-in provider: it embeds the state it is handed and returns a scripted identity.</summary>
    private sealed class FakeProvider(string name, Result<ExternalUserInfo> response) : IExternalIdentityProvider
    {
        public string Name => name;

        public string BuildAuthorizeUrl(string redirectUri, string state) =>
            $"https://fake-provider.test/authorize?state={Uri.EscapeDataString(state)}";

        public Task<Result<ExternalUserInfo>> ExchangeCodeAsync(
            string code, string redirectUri, CancellationToken ct = default) => Task.FromResult(response);
    }

    /// <summary>
    /// A host whose only change is the provider (faked) and an in-memory handoff store — so the test
    /// touches neither the internet nor Redis. Everything else is the real pipeline. Returned as the
    /// factory (not just a client) so a test can inspect the very database the flow wrote to.
    /// </summary>
    private WebApplicationFactory<Program> FactoryFor(string provider, Result<ExternalUserInfo> response) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IExternalIdentityProvider>();
            services.AddScoped<IExternalIdentityProvider>(_ => new FakeProvider(provider, response));
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
        }));

    private static HttpClient NoRedirect(WebApplicationFactory<Program> f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string? QueryValue(Uri uri, string key) => uri.Query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(pair => pair.Split('=', 2))
        .Where(pair => Uri.UnescapeDataString(pair[0]) == key)
        .Select(pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : "")
        .FirstOrDefault();

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Runs challenge → callback and returns the callback's redirect target.</summary>
    private async Task<Uri> ChallengeAndCallbackAsync(HttpClient client, string provider)
    {
        var challenge = await client.GetAsync($"/api/v1/auth/oauth/{provider}");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);

        var state = QueryValue(challenge.Headers.Location!, "state");
        Assert.False(string.IsNullOrEmpty(state));

        var callback = await client.GetAsync(
            $"/api/v1/auth/oauth/{provider}/callback?code=auth-code&state={Uri.EscapeDataString(state!)}");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        return callback.Headers.Location!;
    }

    // A distinct provider key per identity by default: a shared constant would make two tests' logins
    // collide on (provider, key), so the second would resolve to the first's account via FindByLogin
    // instead of creating its own — exactly the sort of cross-test bleed that makes a suite lie.
    private static ExternalUserInfo Verified(string provider, string email, string? key = null) =>
        new(provider, key ?? $"key-{Guid.NewGuid():N}", email, EmailVerified: true, DisplayName: "Social User");

    [Fact]
    public async Task New_user_is_created_and_can_exchange_the_handoff_code()
    {
        var email = $"new-{Guid.NewGuid():N}@social.test";
        var host = FactoryFor("google", Result.Success(Verified("google", email)));
        var client = NoRedirect(host);

        var receiver = await ChallengeAndCallbackAsync(client, "google");
        Assert.StartsWith("http://localhost:3001/auth/callback", receiver.ToString());

        var handoff = QueryValue(receiver, "code");
        Assert.False(string.IsNullOrEmpty(handoff));

        var exchange = await client.PostAsJsonAsync("/api/v1/auth/oauth/exchange", new { code = handoff });
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        var data = (await BodyAsync(exchange)).GetProperty("data");
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("refreshToken").GetString()));

        // The account exists, is confirmed (the provider vouched for the address), and is linked.
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
        Assert.Contains(await users.GetLoginsAsync(user), l => l.LoginProvider == "google");
    }

    [Fact]
    public async Task The_handoff_code_cannot_be_exchanged_twice()
    {
        var email = $"once-{Guid.NewGuid():N}@social.test";
        var client = NoRedirect(FactoryFor("google", Result.Success(Verified("google", email))));

        var receiver = await ChallengeAndCallbackAsync(client, "google");
        var handoff = QueryValue(receiver, "code");

        var first = await client.PostAsJsonAsync("/api/v1/auth/oauth/exchange", new { code = handoff });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/auth/oauth/exchange", new { code = handoff });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task An_existing_account_is_linked_not_duplicated()
    {
        // A password account arrives first.
        var email = $"link-{Guid.NewGuid():N}@social.test";
        var plain = factory.CreateClient();
        (await plain.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "Password123!", displayName = "Original" })).EnsureSuccessStatusCode();

        Guid originalId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            originalId = (await users.FindByEmailAsync(email))!.Id;
        }

        // Now the same person signs in with Google on the same verified address.
        var host = FactoryFor("google", Result.Success(Verified("google", email, key: "google-999")));
        var client = NoRedirect(host);
        var receiver = await ChallengeAndCallbackAsync(client, "google");
        var handoff = QueryValue(receiver, "code");
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/v1/auth/oauth/exchange", new { code = handoff })).StatusCode);

        using var check = host.Services.CreateScope();
        var manager = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.Equal(originalId, user!.Id); // same account, not a second one
        Assert.Contains(await manager.GetLoginsAsync(user), l => l.LoginProvider == "google");
    }

    [Fact]
    public async Task An_unverified_email_is_refused()
    {
        var email = $"unverified-{Guid.NewGuid():N}@social.test";
        var unverified = new ExternalUserInfo("google", "gk", email, EmailVerified: false, DisplayName: "No");
        var host = FactoryFor("google", Result.Success(unverified));
        var client = NoRedirect(host);

        var receiver = await ChallengeAndCallbackAsync(client, "google");

        // The callback sends the person back to sign-in with an error, not on to the app.
        Assert.Contains("error=oauth", receiver.ToString());

        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync(email));
    }

    [Fact]
    public async Task A_tampered_state_is_refused_at_the_callback()
    {
        var client = NoRedirect(FactoryFor("google", Result.Success(Verified("google", "x@social.test"))));

        var challenge = await client.GetAsync("/api/v1/auth/oauth/google");
        var state = QueryValue(challenge.Headers.Location!, "state")!;
        var tampered = state[..^2] + (state[^1] == 'A' ? "BB" : "AA");

        var callback = await client.GetAsync(
            $"/api/v1/auth/oauth/google/callback?code=auth-code&state={Uri.EscapeDataString(tampered)}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Contains("error=oauth", callback.Headers.Location!.ToString());
    }

    [Fact]
    public async Task An_unknown_provider_redirects_to_the_error_page()
    {
        var client = NoRedirect(FactoryFor("google", Result.Success(Verified("google", "x@social.test"))));

        var challenge = await client.GetAsync("/api/v1/auth/oauth/twitter");

        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        Assert.Contains("error=oauth", challenge.Headers.Location!.ToString());
    }
}
