using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// Registers and confirms, for the tests whose subject is something other than the gate.
    ///
    /// Needed since confirmation became enforced: a freshly registered account cannot sign in, which
    /// is the point — these two tests were quietly relying on the old behaviour.
    /// </summary>
    private async Task<(HttpClient Client, string Email, string Password)> RegisterConfirmedAsync()
    {
        var (client, email, password) = await RegisterAsync();

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<
                DataBro.Modules.Identity.Infrastructure.Persistence.ApplicationUser>>();

        var user = await users.FindByEmailAsync(email);
        var token = await users.GenerateEmailConfirmationTokenAsync(user!);

        (await client.PostAsJsonAsync("/api/v1/auth/confirm-email",
            new { userId = user!.Id, token })).EnsureSuccessStatusCode();

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
        var (client, email, _) = await RegisterConfirmedAsync();

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
        var (client, email, password) = await RegisterConfirmedAsync();

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

    // ---- Email confirmation, enforced ----

    [Fact]
    public async Task An_unconfirmed_account_cannot_sign_in()
    {
        var (client, email, password) = await RegisterAsync();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("email_not_confirmed", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_wrong_password_on_an_unconfirmed_account_is_still_just_invalid_credentials()
    {
        // The confirmation check runs *after* the password check, and this is what that ordering
        // buys. If it ran first, "confirm your email" would be returned to anyone who guessed an
        // address — an enumeration oracle. After the password, the caller has already proved the
        // account exists, so the specific message costs nothing.
        var (client, email, _) = await RegisterAsync();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "WrongPassword123!" });
        var body = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_unknown_address_and_an_unconfirmed_one_are_indistinguishable_before_the_password()
    {
        // The other half of the same property, from the attacker's side: guessing addresses tells
        // you nothing, because both answers are identical until you also know the password.
        var (client, email, _) = await RegisterAsync();

        var unconfirmed = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Guess123!" });
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = $"nobody-{Guid.NewGuid():N}@databro.test", password = "Guess123!" });

        Assert.Equal(unknown.StatusCode, unconfirmed.StatusCode);
        Assert.Equal(
            await unknown.Content.ReadAsStringAsync(),
            await unconfirmed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Confirming_the_address_lets_the_account_in()
    {
        var (client, email, password) = await RegisterAsync();

        // The token would normally arrive by email; generated directly here because the transport
        // has its own tests and this one is about the gate.
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<
                DataBro.Modules.Identity.Infrastructure.Persistence.ApplicationUser>>();

        var user = await users.FindByEmailAsync(email);
        var token = await users.GenerateEmailConfirmationTokenAsync(user!);

        (await client.PostAsJsonAsync("/api/v1/auth/confirm-email",
            new { userId = user!.Id, token })).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
