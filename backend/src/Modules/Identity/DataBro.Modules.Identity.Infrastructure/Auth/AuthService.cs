using DataBro.Modules.Identity.Application;
using DataBro.Modules.Identity.Domain;
using DataBro.Modules.Identity.Infrastructure.Persistence;
using DataBro.Platform.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataBro.Modules.Identity.Infrastructure.Auth;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IdentityModuleDbContext db,
    JwtTokenService tokenService,
    IIdentityEmails emails,
    IOptions<IdentityEmailOptions> emailOptions) : IAuthService
{
    private static readonly Error EmailNotConfirmed =
        new("email_not_confirmed", "Confirm your email address before signing in.");

    private static readonly Error InvalidResetToken =
        new("validation_failed", "That reset link is no longer valid. Request a new one.");

    private static readonly Error InvalidCredentials =
        new("unauthenticated", "Invalid email or password.");

    public async Task<Result<UserProfileDto>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return Result.Failure<UserProfileDto>(
                new Error("validation_failed", string.Join(" ", created.Errors.Select(e => e.Description))));

        await userManager.AddToRoleAsync(user, Roles.Default);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emails.SendEmailConfirmationAsync(user.Email!, user.DisplayName, user.Id, token, ct);

        return Result.Success(await BuildProfileAsync(user));
    }

    public async Task<Result<AuthTokens>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Result.Failure<AuthTokens>(InvalidCredentials);

        // Checked **after** the password, and that ordering is the whole reason this can be a
        // specific message rather than the generic one.
        //
        // Saying "confirm your email" before the password check would be an enumeration oracle:
        // anyone could learn which addresses have accounts. Saying it after costs nothing, because
        // whoever reached this line has already proved they know the password — they know the
        // account exists. So the message can be actionable instead of a dead end.
        if (emailOptions.Value.RequireConfirmedEmail && !user.EmailConfirmed)
            return Result.Failure<AuthTokens>(EmailNotConfirmed);

        return Result.Success(await IssueTokensAsync(user, ct));
    }

    public async Task<Result<AuthTokens>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var hash = JwtTokenService.Hash(request.RefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null || !existing.IsActive)
            return Result.Failure<AuthTokens>(new Error("unauthenticated", "Invalid or expired refresh token."));

        var user = await userManager.FindByIdAsync(existing.UserId.ToString());
        if (user is null)
            return Result.Failure<AuthTokens>(InvalidCredentials);

        // Rotate: revoke the presented token and issue a fresh one.
        var tokens = await IssueTokensAsync(user, ct, replacing: existing);
        return Result.Success(tokens);
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result.Failure(Error.NotFound("User not found."));

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded
            ? Result.Success()
            : Result.Failure(new Error("validation_failed", "Invalid or expired confirmation token."));
    }

    /// <summary>
    /// Starts password recovery.
    ///
    /// <para>
    /// <b>Always returns success</b>, whether or not the address belongs to an account. An endpoint
    /// that answered differently would be a membership oracle: anyone could test an address list
    /// against it and learn who has an account here. The cost is that a typo produces silence, which
    /// is why the UI says "if that address has an account" rather than "sent".
    /// </para>
    /// <para>
    /// A user who has never confirmed their address is deliberately still sent a reset link — the
    /// most common reason to be stuck unconfirmed is having forgotten the password too, and refusing
    /// here would leave them with no route back at all.
    /// </para>
    /// </summary>
    public async Task<Result> ForgotPasswordAsync(
        ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await emails.SendPasswordResetAsync(user.Email!, user.DisplayName, user.Id, token, ct);
        }

        return Result.Success();
    }

    /// <summary>
    /// Completes a reset.
    ///
    /// <para>
    /// Every failure reads the same — expired, already used, wrong user, tampered with. Telling
    /// someone holding a stolen link <i>which</i> kind of stolen link they have helps only them.
    /// </para>
    /// <para>
    /// Succeeding also <b>revokes every refresh token</b> the account holds. Resetting a password is
    /// what someone does when they believe the account is compromised, and leaving an attacker's
    /// session alive through it would make the reset theatre.
    /// </para>
    /// </summary>
    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result.Failure(InvalidResetToken);

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.Password);

        if (!result.Succeeded)
        {
            // Password-policy failures are the one thing worth distinguishing: "too short" is
            // actionable and tells an attacker nothing they could not read in the policy.
            var isPolicy = result.Errors.Any(e => e.Code.StartsWith("Password", StringComparison.Ordinal));

            return Result.Failure(isPolicy
                ? new Error("validation_failed", string.Join(" ", result.Errors.Select(e => e.Description)))
                : InvalidResetToken);
        }

        var now = DateTimeOffset.UtcNow;
        var live = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in live) token.RevokedAt = now;
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>
    /// Re-sends the confirmation email. Non-committal for the same reason as
    /// <see cref="ForgotPasswordAsync"/>, and silently does nothing for an address that is already
    /// confirmed — a second confirmation link is useless and the difference is not worth leaking.
    /// </summary>
    public async Task<Result> ResendConfirmationAsync(
        ResendConfirmationRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is not null && !user.EmailConfirmed)
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await emails.SendEmailConfirmationAsync(user.Email!, user.DisplayName, user.Id, token, ct);
        }

        return Result.Success();
    }

    /// <summary>
    /// Revokes one refresh token.
    ///
    /// <para>
    /// Until now signing out only cleared cookies, which left the refresh token valid for a
    /// fortnight — a token copied off a shared machine outlived the sign-out that was supposed to
    /// end it.
    /// </para>
    /// <para>
    /// Succeeds even when the token is unknown or already revoked. Signing out must never fail: a
    /// client that cannot complete it is a client that leaves someone signed in.
    /// </para>
    /// </summary>
    public async Task<Result> LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        var hash = JwtTokenService.Hash(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is { RevokedAt: null })
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? Result.Failure<UserProfileDto>(Error.NotFound("User not found."))
            : Result.Success(await BuildProfileAsync(user));
    }

    /// <summary>
    /// Signs in through an external provider (ADR-0019, ID-3).
    ///
    /// <para>
    /// The order matters. First we look up by the <b>(provider, key)</b> pair: a returning social user
    /// is already linked and must resolve to their account even if they have since changed the email
    /// on the provider. Only if that misses do we match by <b>verified</b> email — which links a
    /// second provider to one account, or adopts a password account the person is now signing into
    /// socially — and only failing that do we create one.
    /// </para>
    /// <para>
    /// An unverified email never reaches a lookup. The providers already refuse to return one, so this
    /// is defence in depth: matching an unverified address to an existing account is exactly how an
    /// attacker who can receive at an unconfirmed provider address would walk into it.
    /// </para>
    /// <para>
    /// A created account is <b>confirmed at birth</b> — the provider has vouched for the address, so a
    /// second confirmation email would be theatre that blocks a legitimate first sign-in. A password
    /// account whose owner arrives socially is confirmed in passing for the same reason.
    /// </para>
    /// </summary>
    public async Task<Result<AuthTokens>> LinkOrCreateExternalAsync(
        ExternalUserInfo info, CancellationToken ct = default)
    {
        if (!info.EmailVerified || string.IsNullOrWhiteSpace(info.Email))
            return Result.Failure<AuthTokens>(
                new Error("validation_failed", "The provider did not return a verified email address."));

        // Already linked: the provider key is the stable identity, ahead of the email.
        var linked = await userManager.FindByLoginAsync(info.Provider, info.ProviderKey);
        if (linked is not null)
            return Result.Success(await IssueTokensAsync(linked, ct));

        var login = new UserLoginInfo(info.Provider, info.ProviderKey, info.Provider);

        var existing = await userManager.FindByEmailAsync(info.Email);
        if (existing is not null)
        {
            var link = await userManager.AddLoginAsync(existing, login);
            if (!link.Succeeded)
                return Result.Failure<AuthTokens>(
                    new Error("conflict", "That account could not be linked to this provider."));

            if (!existing.EmailConfirmed)
            {
                existing.EmailConfirmed = true;
                await userManager.UpdateAsync(existing);
            }

            return Result.Success(await IssueTokensAsync(existing, ct));
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = info.Email,
            Email = info.Email,
            DisplayName = string.IsNullOrWhiteSpace(info.DisplayName)
                ? info.Email.Split('@')[0]
                : info.DisplayName,
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(user);
        if (!created.Succeeded)
            return Result.Failure<AuthTokens>(
                new Error("validation_failed", string.Join(" ", created.Errors.Select(e => e.Description))));

        await userManager.AddToRoleAsync(user, Roles.Default);
        await userManager.AddLoginAsync(user, login);

        return Result.Success(await IssueTokensAsync(user, ct));
    }

    private async Task<AuthTokens> IssueTokensAsync(ApplicationUser user, CancellationToken ct, RefreshToken? replacing = null)
    {
        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var (accessToken, expiresIn) = tokenService.CreateAccessToken(user, roles);

        var refresh = tokenService.CreateRefreshToken();
        var refreshHash = JwtTokenService.Hash(refresh);

        if (replacing is not null)
        {
            replacing.RevokedAt = DateTimeOffset.UtcNow;
            replacing.ReplacedByTokenHash = refreshHash;
        }

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = tokenService.RefreshTokenExpiry(),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        return new AuthTokens(accessToken, refresh, expiresIn);
    }

    private async Task<UserProfileDto> BuildProfileAsync(ApplicationUser user)
    {
        var roles = (await userManager.GetRolesAsync(user)).ToList();
        return new UserProfileDto(user.Id, user.Email!, user.DisplayName, user.EmailConfirmed, roles);
    }
}
