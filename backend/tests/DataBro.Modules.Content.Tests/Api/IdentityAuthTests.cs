using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

// Auth integration tests. They reuse the app factory (which boots the whole host including Identity).
public class IdentityAuthTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private HttpClient Client => factory.CreateClient();

    private static (string Email, object Payload) NewRegistration()
    {
        var email = $"user-{Guid.NewGuid():N}@databro.test";
        return (email, new { email, password = "Password123!", displayName = "Test User" });
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Register_then_login_issues_tokens()
    {
        var client = Client;
        var (email, payload) = NewRegistration();

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var data = (await BodyAsync(login)).GetProperty("data");
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var client = Client;
        var (email, payload) = NewRegistration();
        await client.PostAsJsonAsync("/api/v1/auth/register", payload);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassword1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Duplicate_registration_fails_validation()
    {
        var client = Client;
        var (_, payload) = NewRegistration();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/register", payload)).StatusCode);
        var second = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var response = await Client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_invalidates_the_old_token()
    {
        var client = Client;
        var (email, payload) = NewRegistration();
        await client.PostAsJsonAsync("/api/v1/auth/register", payload);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        var firstRefresh = (await BodyAsync(login)).GetProperty("data").GetProperty("refreshToken").GetString();

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = firstRefresh });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var secondRefresh = (await BodyAsync(refreshed)).GetProperty("data").GetProperty("refreshToken").GetString();
        Assert.NotEqual(firstRefresh, secondRefresh);

        // The rotated (old) token must no longer work.
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = firstRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }
}
