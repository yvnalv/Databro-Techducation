using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DataBro.Modules.Identity.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace DataBro.Modules.Identity.Infrastructure.Auth.External;

/// <summary>The payload carried through the OAuth round-trip in a signed <c>state</c> parameter.</summary>
/// <param name="Provider">The provider the challenge was for; a callback for a different one is a replay.</param>
/// <param name="Nonce">Random per-request value, so two challenges never produce the same state.</param>
/// <param name="IssuedAt">When the state was minted, for the freshness check.</param>
/// <param name="ReturnTo">Where to send the person after login; already validated when minted.</param>
public sealed record OAuthState(string Provider, string Nonce, DateTimeOffset IssuedAt, string? ReturnTo);

/// <summary>
/// Signs and verifies the OAuth <c>state</c> parameter (ADR-0019).
///
/// <para>
/// This replaces the correlation cookie a framework OAuth handler would set. A bearer-only host has no
/// cookie to carry across the provider redirect, so state must be self-contained: an HMAC over the
/// payload, keyed by the same secret that signs access tokens. A forged or tampered callback fails the
/// signature; a stale one fails the freshness window. Nothing is stored server-side.
/// </para>
/// </summary>
public sealed class OAuthStateProtector(IOptions<JwtOptions> jwtOptions)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(jwtOptions.Value.Key);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Protect(OAuthState state)
    {
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(state, Json));
        var signature = Base64Url(Sign(payload));
        return $"{payload}.{signature}";
    }

    /// <summary>
    /// Verifies signature and freshness and returns the payload, or <c>null</c> if the token is
    /// malformed, tampered with, or older than <paramref name="maxAge"/>. One <c>null</c> for every
    /// kind of failure: a caller holding a bad state learns only that it is bad.
    /// </summary>
    public OAuthState? Unprotect(string token, TimeSpan maxAge)
    {
        if (string.IsNullOrEmpty(token)) return null;

        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1) return null;

        var payload = token[..dot];
        var presented = token[(dot + 1)..];

        var expected = Base64Url(Sign(payload));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(presented), Encoding.ASCII.GetBytes(expected)))
            return null;

        OAuthState? state;
        try
        {
            state = JsonSerializer.Deserialize<OAuthState>(FromBase64Url(payload), Json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (state is null) return null;
        if (DateTimeOffset.UtcNow - state.IssuedAt > maxAge) return null;

        return state;
    }

    private byte[] Sign(string payload) =>
        HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(payload));

    private static string Base64Url(byte[] bytes) => WebEncoders_Base64UrlEncode(bytes);

    private static byte[] FromBase64Url(string value) => WebEncoders_Base64UrlDecode(value);

    // Small local base64url so the protector carries no extra dependency; the payload is short.
    private static string WebEncoders_Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] WebEncoders_Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };
        return Convert.FromBase64String(padded);
    }
}
