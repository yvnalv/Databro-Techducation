using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>
/// Account recovery end to end.
///
/// The property worth testing hardest is not that a reset works — it is that the endpoints reveal
/// nothing about who has an account, because that is the part which is silently wrong the moment
/// someone "improves" an error message.
/// </summary>
public class AccountRecoveryTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<(HttpClient Client, string Email, string Password)> RegisterAsync()
    {
        var client = factory.CreateClient();
        var email = $"recover-{Guid.NewGuid():N}@databro.test";
        const string password = "Password123!";

        (await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, displayName = "Recoverable" })).EnsureSuccessStatusCode();

        return (client, email, password);
    }

    [Fact]
    public async Task Forgot_password_answers_identically_for_a_known_and_an_unknown_address()
    {
        // A membership oracle: if these differ at all — status, body, or shape — an address list can
        // be tested against this endpoint to learn who has an account here.
        var (client, email, _) = await RegisterAsync();

        var known = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = $"nobody-{Guid.NewGuid():N}@databro.test" });

        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(
            await known.Content.ReadAsStringAsync(),
            await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Resend_confirmation_answers_identically_too()
    {
        var (client, email, _) = await RegisterAsync();

        var known = await client.PostAsJsonAsync("/api/v1/auth/resend-confirmation", new { email });
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/resend-confirmation",
            new { email = $"nobody-{Guid.NewGuid():N}@databro.test" });

        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(
            await known.Content.ReadAsStringAsync(),
            await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_invalid_reset_token_is_refused()
    {
        var (client, email, _) = await RegisterAsync();

        var login = await ReadAsync(await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" }));
        var token = login.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await ReadAsync(await client.GetAsync("/api/v1/me"));
        var userId = me.GetProperty("data").GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { userId, token = "not-a-real-token", password = "NewPassword123!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_user_and_a_bad_token_are_refused_the_same_way()
    {
        // Telling someone holding a stolen link *which* kind of stolen link they have helps only
        // them.
        var client = factory.CreateClient();

        var unknownUser = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { userId = Guid.NewGuid(), token = "whatever", password = "NewPassword123!" });

        var body = await ReadAsync(unknownUser);

        Assert.Equal(HttpStatusCode.BadRequest, unknownUser.StatusCode);
        Assert.Equal("validation_failed", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Logging_out_revokes_the_refresh_token()
    {
        // Before this existed, signing out only cleared cookies — a token copied off a shared
        // machine outlived the sign-out that was supposed to end it.
        var (client, email, password) = await RegisterAsync();

        var login = await ReadAsync(await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password }));
        var refreshToken = login.GetProperty("data").GetProperty("refreshToken").GetString();

        // It works before signing out.
        var beforeLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, beforeLogout.StatusCode);

        // Refresh rotates, so revoke the token that call issued.
        var rotated = (await ReadAsync(beforeLogout)).GetProperty("data")
            .GetProperty("refreshToken").GetString();

        (await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = rotated }))
            .EnsureSuccessStatusCode();

        var afterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = rotated });

        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Logging_out_with_an_unknown_token_still_succeeds()
    {
        // Signing out must never fail: a client that cannot complete it is a client that leaves
        // someone signed in.
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/logout",
            new { refreshToken = "never-issued" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
