using DataBro.Modules.Identity.Application;
using DataBro.Modules.Identity.Domain;
using DataBro.Modules.Identity.Infrastructure.Persistence;
using DataBro.Platform.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Identity.Infrastructure.Auth;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IdentityModuleDbContext db,
    JwtTokenService tokenService,
    IIdentityEmails emails) : IAuthService
{
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

    public async Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? Result.Failure<UserProfileDto>(Error.NotFound("User not found."))
            : Result.Success(await BuildProfileAsync(user));
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
