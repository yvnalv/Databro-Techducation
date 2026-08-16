using System.Net;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>
/// The frontend apps call this API from the browser, on a different origin. Without a CORS policy
/// every client-side call is blocked at the preflight — a failure the public site never surfaced,
/// because its reads happen server-side during SSR, but which stopped the authoring app dead at
/// sign-in.
/// </summary>
public class CorsTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private const string AppOrigin = "http://localhost:3001";

    [Fact]
    public async Task Preflight_from_a_configured_origin_is_allowed()
    {
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", AppOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AppOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task An_actual_request_carries_the_allow_origin_header()
    {
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/articles");
        request.Headers.Add("Origin", AppOrigin);

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task An_unlisted_origin_gets_no_allow_header()
    {
        // The browser then blocks the response. `AllowAnyOrigin` would let any site on the internet
        // call this API with a user's bearer token from a script it controls.
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/articles");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task A_401_still_carries_cors_headers()
    {
        // UseCors sits before authentication for this reason: without the header on the 401, the
        // browser reports an opaque CORS error and the app can never see the real status.
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Add("Origin", AppOrigin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
