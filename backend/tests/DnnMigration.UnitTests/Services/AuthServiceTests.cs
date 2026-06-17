using Xunit;
using Moq;
using FluentAssertions;
using System.Security.Claims;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="AuthService"/> — the single sanctioned behavior change of the DNN 4.x → .NET 8
/// migration: legacy ASP.NET Forms Authentication + 56-bit DES
/// (<c>Library/Components/Security/PortalSecurity.vb</c>) and the cookie-issuing
/// <c>UserController.UserLogin</c> flow are replaced by stateless JWT Bearer tokens + one-way BCrypt password
/// verification. These tests assert the login credential guards, BCrypt verification + JWT token issuance,
/// revoking refresh-token rotation with replay detection via <see cref="IRefreshTokenStore"/>, and logout
/// that revokes the user's refresh-token families (CP-FINAL / Code-Review G2). They validate Gate 2 (<c>dotnet test</c>).
///
/// Collaborators (<see cref="IUserRepository"/>, <see cref="IPasswordHasher"/>, <see cref="IJwtService"/>) are
/// mocked with Moq using <see cref="MockBehavior.Strict"/> (so any unconfigured call fails the test); the
/// <see cref="IMapper"/> is built from a REAL <see cref="MapperConfiguration"/> with <see cref="UserProfile"/>
/// so the entity→DTO projection is exercised end-to-end. No database, HTTP, or DI host is involved (AAA style).
/// </summary>
public class AuthServiceTests
{
    private const int DefaultPortalId = 0;

    private readonly Mock<IUserRepository> _userRepo = new(MockBehavior.Strict);
    private readonly Mock<IPasswordHasher> _hasher = new(MockBehavior.Strict);
    private readonly Mock<IJwtService> _jwt = new(MockBehavior.Strict);

    // MIGRATION (CP-FINAL / Code-Review G2): the server-side refresh-token store backing revoking rotation,
    // replay detection, and logout invalidation. Strict so any unconfigured interaction fails the test.
    private readonly Mock<IRefreshTokenStore> _store = new(MockBehavior.Strict);

    // MIGRATION: AutoMapper was upgraded to the patched 15.x line (security advisory GHSA-rvv3-g6hj-g44x,
    // MIGRATION_NOTES.md §7.1), a deviation from the AAP §0.5.1 12.0.1 pin. From v13+ the single-argument
    // MapperConfiguration(cfg) constructor is obsolete, so the ILoggerFactory-aware overload is used here
    // (NullLoggerFactory in tests) — identical to the sibling *ProfileTests. A real mapper (not a mock) keeps
    // the User → UserDto projection under test.
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<UserProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private AuthService CreateSut() => new(_userRepo.Object, _hasher.Object, _jwt.Object, _store.Object, _mapper);

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims));

    // MIGRATION (CP2 auth-chain fix): a refresh token is a signed JWT carrying token_type=refresh. The
    // refresh-flow principals below include that marker so they pass AuthService.RefreshAsync's token-type
    // gate and exercise the claim-extraction path under test (rather than tripping the gate).
    private static Claim RefreshTypeClaim() =>
        new(IJwtService.TokenTypeClaim, IJwtService.RefreshTokenType);

    // MIGRATION (CP-FINAL / Code-Review G2): a refresh token also carries its rotation family (token_family)
    // and a per-token id (jti). RefreshAsync requires BOTH to validate the token against the store, so the
    // refresh-flow principals include them. (ValidateToken surfaces jti verbatim as the "jti" claim type.)
    private static Claim FamilyClaim(string family = "fam-A") =>
        new(IJwtService.TokenFamilyClaim, family);

    private static Claim JtiClaim(string jti = "jti-1") =>
        new("jti", jti);

    private void SetupTokenGeneration()
    {
        // MIGRATION (CP-FINAL / Code-Review G2): GenerateRefreshToken now takes (User, tokenFamily, tokenId)
        // and the service registers/rotates the token id against IRefreshTokenStore. Stub the JWT issuance,
        // the lifetime getters, and the store's Register (login) + TryRotate (refresh) so the happy paths
        // complete. Unused stubs are harmless under MockBehavior.Strict (Strict fails only on UNCONFIGURED
        // calls, never on unused setups).
        _jwt.Setup(j => j.GenerateAccessToken(It.IsAny<User>())).Returns("ACCESS");
        _jwt.Setup(j => j.GenerateRefreshToken(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>())).Returns("REFRESH");
        _jwt.SetupGet(j => j.AccessTokenExpirationMinutes).Returns(60);
        _jwt.SetupGet(j => j.RefreshTokenExpirationDays).Returns(7);

        _store.Setup(s => s.Register(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>()));
        _store.Setup(s => s.TryRotate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>())).Returns(true);
    }

    // ---------- LoginAsync ----------

    [Fact]
    public async Task LoginAsync_valid_credentials_returns_tokens_and_invokes_jwt_and_hasher()
    {
        // MIGRATION: Forms Auth + DES -> JWT + BCrypt (the single sanctioned behavior change)
        var user = new User { UserID = 5, Username = "admin", Password = "BCRYPTHASH", FirstName = "Ada", LastName = "Min" };
        _userRepo.Setup(r => r.GetByUsernameAsync(DefaultPortalId, "admin", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("pw", "BCRYPTHASH")).Returns(true);
        SetupTokenGeneration();

        var before = DateTime.UtcNow;
        var result = await CreateSut().LoginAsync(new LoginRequestDto { Username = "admin", Password = "pw" });
        var after = DateTime.UtcNow;

        result.AccessToken.Should().Be("ACCESS");
        result.RefreshToken.Should().Be("REFRESH");
        result.ExpiresIn.Should().Be(3600); // 60 minutes * 60
        result.ExpiresAt.Should().BeOnOrAfter(before.AddMinutes(60)).And.BeOnOrBefore(after.AddMinutes(60));
        result.User!.UserID.Should().Be(5);
        _hasher.Verify(h => h.Verify("pw", "BCRYPTHASH"), Times.Once);
        _jwt.Verify(j => j.GenerateAccessToken(user), Times.Once);
        _jwt.Verify(j => j.GenerateRefreshToken(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        // MIGRATION (CP-FINAL / Code-Review G2): login establishes a NEW refresh-token family registered with the store.
        _store.Verify(s => s.Register(It.IsAny<string>(), It.IsAny<string>(), 5, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_uses_default_portal_id_zero()
    {
        var user = new User { UserID = 1, Username = "u", Password = "H" };
        _userRepo.Setup(r => r.GetByUsernameAsync(DefaultPortalId, "u", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("p", "H")).Returns(true);
        SetupTokenGeneration();
        await CreateSut().LoginAsync(new LoginRequestDto { Username = "u", Password = "p" });
        _userRepo.Verify(r => r.GetByUsernameAsync(0, "u", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null, "pw")]
    [InlineData("", "pw")]
    [InlineData("   ", "pw")]
    [InlineData("admin", null)]
    [InlineData("admin", "")]
    [InlineData("admin", "   ")]
    public async Task LoginAsync_blank_credentials_throws_unauthorized(string? username, string? password)
    {
        Func<Task> act = () => CreateSut().LoginAsync(new LoginRequestDto { Username = username, Password = password });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _userRepo.Verify(r => r.GetByUsernameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_unknown_user_throws_unauthorized()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync(DefaultPortalId, "ghost", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        Func<Task> act = () => CreateSut().LoginAsync(new LoginRequestDto { Username = "ghost", Password = "pw" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginAsync_wrong_password_throws_unauthorized()
    {
        var user = new User { UserID = 5, Username = "admin", Password = "HASH" };
        _userRepo.Setup(r => r.GetByUsernameAsync(DefaultPortalId, "admin", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrong", "HASH")).Returns(false);
        Func<Task> act = () => CreateSut().LoginAsync(new LoginRequestDto { Username = "admin", Password = "wrong" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _jwt.Verify(j => j.GenerateAccessToken(It.IsAny<User>()), Times.Never);
    }

    // ---------- RefreshAsync ----------

    [Fact]
    public async Task RefreshAsync_valid_token_rotates_via_name_identifier_claim()
    {
        var user = new User { UserID = 42, Username = "u" };
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(
            RefreshTypeClaim(), new Claim(ClaimTypes.NameIdentifier, "42"), FamilyClaim(), JtiClaim()));
        _userRepo.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        SetupTokenGeneration();
        var result = await CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        result.AccessToken.Should().Be("ACCESS");
        result.RefreshToken.Should().Be("REFRESH");
        result.User!.UserID.Should().Be(42);
        // MIGRATION (CP-FINAL / Code-Review G2): the presented token id is rotated within its family via the store.
        _store.Verify(s => s.TryRotate("fam-A", "jti-1", It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_falls_back_to_sub_claim()
    {
        var user = new User { UserID = 7 };
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(
            RefreshTypeClaim(), new Claim("sub", "7"), FamilyClaim(), JtiClaim()));
        _userRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        SetupTokenGeneration();
        var result = await CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        result.User!.UserID.Should().Be(7);
    }

    [Fact]
    public async Task RefreshAsync_missing_family_or_jti_throws_and_skips_lookup()
    {
        // MIGRATION (CP-FINAL / Code-Review G2): a refresh token lacking the token_family/jti claims is not a
        // token this server issued under the current contract; reject it BEFORE any user lookup or rotation.
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(
            RefreshTypeClaim(), new Claim(ClaimTypes.NameIdentifier, "42"))); // no family, no jti
        Func<Task> act = () => CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _userRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_when_store_rejects_rotation_throws_unauthorized()
    {
        // MIGRATION (CP-FINAL / Code-Review G2): TryRotate returns false for an unknown/revoked family, an
        // expired entry, or a SUPERSEDED (replayed) token id — all surface as the same generic 401.
        var user = new User { UserID = 42, Username = "u" };
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(
            RefreshTypeClaim(), new Claim(ClaimTypes.NameIdentifier, "42"), FamilyClaim(), JtiClaim("stale")));
        _userRepo.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _jwt.SetupGet(j => j.RefreshTokenExpirationDays).Returns(7);
        _store.Setup(s => s.TryRotate("fam-A", "stale", It.IsAny<string>(), It.IsAny<DateTime>())).Returns(false);

        Func<Task> act = () => CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        // A rejected rotation must NOT issue replacement tokens.
        _jwt.Verify(j => j.GenerateAccessToken(It.IsAny<User>()), Times.Never);
        _jwt.Verify(j => j.GenerateRefreshToken(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefreshAsync_blank_token_throws_unauthorized(string? token)
    {
        Func<Task> act = () => CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = token });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _jwt.Verify(j => j.ValidateToken(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_invalid_token_throws_unauthorized()
    {
        _jwt.Setup(j => j.ValidateToken("bad")).Returns((ClaimsPrincipal?)null);
        Func<Task> act = () => CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "bad" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RefreshAsync_non_refresh_token_type_throws_unauthorized()
    {
        // MIGRATION (CP2 auth-chain fix): an access token replayed at /api/auth/refresh validates against the
        // same signing key but carries token_type=access; it MUST be rejected (token-type confusion) and the
        // user lookup MUST be skipped.
        _jwt.Setup(j => j.ValidateToken("AT")).Returns(PrincipalWith(
            new Claim(IJwtService.TokenTypeClaim, IJwtService.AccessTokenType),
            new Claim(ClaimTypes.NameIdentifier, "42")));
        Func<Task> act = () => CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "AT" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _userRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_unparseable_user_id_claim_throws_and_skips_lookup()
    {
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(RefreshTypeClaim(), new Claim(ClaimTypes.NameIdentifier, "not-an-int")));
        Func<Task> act = () => CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _userRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_user_no_longer_exists_throws_unauthorized()
    {
        // Family + jti are present so the flow reaches (and fails at) the user lookup, not the claim guard.
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(
            RefreshTypeClaim(), new Claim(ClaimTypes.NameIdentifier, "99"), FamilyClaim(), JtiClaim()));
        _userRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        Func<Task> act = () => CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        // The store must not be touched when the subject no longer resolves to a user.
        _store.Verify(s => s.TryRotate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    // ---------- LogoutAsync (revokes refresh-token families) ----------

    [Fact]
    public async Task LogoutAsync_revokes_all_refresh_families_for_the_user()
    {
        // MIGRATION (CP-FINAL / Code-Review G2): logout now revokes EVERY refresh-token family the user owns
        // so their refresh tokens can no longer be rotated; the access token expires on its own. It must not
        // touch the credential/token collaborators.
        _store.Setup(s => s.RevokeAllForUser(123));

        await CreateSut().LogoutAsync(123);

        _store.Verify(s => s.RevokeAllForUser(123), Times.Once);
        _userRepo.VerifyNoOtherCalls();
        _jwt.VerifyNoOtherCalls();
        _hasher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LogoutAsync_returns_completed_task()
    {
        _store.Setup(s => s.RevokeAllForUser(1));
        var task = CreateSut().LogoutAsync(1);
        task.IsCompletedSuccessfully.Should().BeTrue();
        await task;
        _store.Verify(s => s.RevokeAllForUser(1), Times.Once);
    }

    // ---------- GetCurrentUserAsync ----------

    [Fact]
    public async Task GetCurrentUserAsync_maps_when_found()
    {
        _userRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new User { UserID = 3, Username = "me" });
        (await CreateSut().GetCurrentUserAsync(3))!.Username.Should().Be("me");
    }

    [Fact]
    public async Task GetCurrentUserAsync_null_when_missing()
    {
        _userRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        (await CreateSut().GetCurrentUserAsync(3)).Should().BeNull();
    }
}
