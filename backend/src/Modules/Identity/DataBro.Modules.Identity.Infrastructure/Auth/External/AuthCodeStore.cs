using System.Security.Cryptography;
using System.Text.Json;
using DataBro.Modules.Identity.Application;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DataBro.Modules.Identity.Infrastructure.Auth.External;

/// <summary>
/// The short-lived, single-use handoff between the OAuth callback and the app (ADR-0019). The callback
/// mints a code, parks the freshly issued tokens against it, and redirects the app with only the code;
/// the app exchanges it once. This keeps tokens out of every URL — the whole reason the flow is not
/// the discarded implicit grant.
/// </summary>
public interface IAuthCodeStore
{
    /// <summary>Mints a code, stores the tokens against it, and returns the code.</summary>
    Task<string> IssueAsync(AuthTokens tokens, CancellationToken ct = default);

    /// <summary>Redeems a code exactly once. Returns <c>null</c> if unknown, already used, or expired.</summary>
    Task<AuthTokens?> RedeemAsync(string code, CancellationToken ct = default);
}

/// <summary>
/// <see cref="IAuthCodeStore"/> over <see cref="IDistributedCache"/> — Redis in dev/prod, in-memory in
/// tests. The store holds a secret for seconds; a swap of backing is one line at the host, which is the
/// point of depending on the abstraction rather than Redis.
/// </summary>
public sealed class DistributedCacheAuthCodeStore(
    IDistributedCache cache, IOptions<ExternalAuthOptions> options) : IAuthCodeStore
{
    private const string KeyPrefix = "oauth:handoff:";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly int _ttlSeconds = options.Value.HandoffCodeTtlSeconds;

    public async Task<string> IssueAsync(AuthTokens tokens, CancellationToken ct = default)
    {
        var code = Base64Url(RandomNumberGenerator.GetBytes(32));
        var payload = JsonSerializer.SerializeToUtf8Bytes(tokens, Json);

        await cache.SetAsync(KeyPrefix + code, payload, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_ttlSeconds),
        }, ct);

        return code;
    }

    public async Task<AuthTokens?> RedeemAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(code)) return null;

        var key = KeyPrefix + code;
        var payload = await cache.GetAsync(key, ct);
        if (payload is null) return null;

        // Single-use: remove before returning, so a replayed code finds nothing even in a race.
        await cache.RemoveAsync(key, ct);

        try
        {
            return JsonSerializer.Deserialize<AuthTokens>(payload, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
