using Xunit;
using Moq;
using FluentAssertions;
using System.Security.Claims;
using AutoMapper;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="AuthService"/> — the SINGLE SANCTIONED BEHAVIOR CHANGE of the
/// DotNetNuke 4.9.0.85 -> .NET 8 rewrite (AAP §0.6.2/§0.7.1): legacy ASP.NET Forms Authentication +
/// 56-bit DES (Library/Components/Security/PortalSecurity.vb: SignOut L79, DES Encrypt/Decrypt L138-L215)
/// is replaced by stateless JWT Bearer issuance + BCrypt password verification. These tests assert the
/// login credential guards, BCrypt verification + token issuance, refresh-token rotation via claims, the
/// stateless logout decision, and the /api/auth/me projection. Collaborators (<see cref="IUserRepository"/>,
/// <see cref="IPasswordHasher"/>, <see cref="IJwtService"/>) are mocked with Moq (strict); <see cref="IMapper"/>
/// is a REAL AutoMapper instance built from <see cref="UserProfile"/>. No database, HTTP, or DI host is used.
/// MIGRATION (DEV-032): the prototype contract was extended by the committed auth-chain hardening —
/// <see cref="IJwtService.GenerateRefreshToken(User)"/> now takes the user, and <see cref="AuthService.RefreshAsync"/>
/// requires a <c>token_use=refresh</c> claim before resolving the subject (token-type-confusion guard). The
/// refresh tests below supply that claim and a dedicated case exercises its rejection branch.
/// </summary>
public class AuthServiceTests
{
    private const int DefaultPortalId = 0;

    private readonly Mock<IUserRepository> _userRepo = new(MockBehavior.Strict);
    private readonly Mock<IPasswordHasher> _hasher = new(MockBehavior.Strict);
    private readonly Mock<IJwtService> _jwt = new(MockBehavior.Strict);
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<UserProfile>()).CreateMapper();

    private AuthService CreateSut() => new(_userRepo.Object, _hasher.Object, _jwt.Object, _mapper);

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims));

    private void SetupTokenGeneration()
    {
        _jwt.Setup(j => j.GenerateAccessToken(It.IsAny<User>())).Returns("ACCESS");
        // MIGRATION (DEV-032): refresh tokens are now signed JWTs bound to the user, so GenerateRefreshToken
        // takes the User (was parameterless in the prototype). BuildAuthResponse calls it with the same user.
        _jwt.Setup(j => j.GenerateRefreshToken(It.IsAny<User>())).Returns("REFRESH");
        _jwt.SetupGet(j => j.AccessTokenExpirationMinutes).Returns(60);
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
        _jwt.Verify(j => j.GenerateRefreshToken(user), Times.Once);
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
        // MIGRATION (DEV-032): a valid refresh token carries token_use=refresh; AuthService.RefreshAsync rejects
        // anything else before resolving the subject from ClaimTypes.NameIdentifier (with a "sub" fallback).
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(new Claim("token_use", "refresh"), new Claim(ClaimTypes.NameIdentifier, "42")));
        _userRepo.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        SetupTokenGeneration();
        var result = await CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        result.AccessToken.Should().Be("ACCESS");
        result.RefreshToken.Should().Be("REFRESH");
        result.User!.UserID.Should().Be(42);
    }

    [Fact]
    public async Task RefreshAsync_falls_back_to_sub_claim()
    {
        var user = new User { UserID = 7 };
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(new Claim("token_use", "refresh"), new Claim("sub", "7")));
        _userRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        SetupTokenGeneration();
        var result = await CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        result.User!.UserID.Should().Be(7);
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
    public async Task RefreshAsync_non_refresh_token_use_throws_and_skips_lookup()
    {
        // MIGRATION (DEV-032): the /api/auth/refresh endpoint must reject any token that is NOT stamped
        // token_use=refresh (e.g. an access token replayed at the refresh endpoint), BEFORE any user lookup.
        // Guards against JWT token-type confusion; the user repository must never be queried.
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(new Claim("token_use", "access"), new Claim(ClaimTypes.NameIdentifier, "42")));
        Func<Task> act = () => CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _userRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_unparseable_user_id_claim_throws_and_skips_lookup()
    {
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(new Claim("token_use", "refresh"), new Claim(ClaimTypes.NameIdentifier, "not-an-int")));
        Func<Task> act = () => CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _userRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_user_no_longer_exists_throws_unauthorized()
    {
        _jwt.Setup(j => j.ValidateToken("RT")).Returns(PrincipalWith(new Claim("token_use", "refresh"), new Claim(ClaimTypes.NameIdentifier, "99")));
        _userRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        Func<Task> act = () => CreateSut().RefreshAsync(new RefreshTokenRequestDto { RefreshToken = "RT" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ---------- LogoutAsync (stateless) ----------

    [Fact]
    public async Task LogoutAsync_is_stateless_no_op_and_touches_no_collaborators()
    {
        // MIGRATION: stateless JWT - logout discards token client-side; server keeps no session.
        await CreateSut().LogoutAsync(123);
        _userRepo.VerifyNoOtherCalls();
        _jwt.VerifyNoOtherCalls();
        _hasher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LogoutAsync_returns_completed_task()
    {
        var task = CreateSut().LogoutAsync(1);
        task.IsCompletedSuccessfully.Should().BeTrue();
        await task;
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
