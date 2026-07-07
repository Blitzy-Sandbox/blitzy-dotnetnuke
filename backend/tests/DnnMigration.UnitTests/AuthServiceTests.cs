// -----------------------------------------------------------------------------
//  AuthServiceTests.cs
//
//  xUnit + Moq unit tests for DnnMigration.Application.Services.AuthService, the
//  BFF authentication orchestrator. These tests lock the three CRITICAL review
//  findings resolved in this boundary so they cannot silently regress:
//
//    F1 (credential verification): LoginAsync verifies the password loaded from
//        user.Membership (now an EF owned type) and rejects unknown / bad-password
//        / unapproved / locked-out accounts.
//    F2 (refresh tokens): RefreshAsync validates the OPAQUE refresh token by LOOKUP
//        in IRefreshTokenStore (never as a JWT), rotates it (revoke-then-reissue),
//        and LogoutAsync revokes ALL of the user's refresh tokens. Every issued
//        token is persisted in the store.
//    F3 (portal scoping): LoginAsync derives the effective portal from the trusted
//        IPortalContextAccessor (a client-supplied portal may only agree with it,
//        never override it), and GetCurrentUserAsync enforces that the token's
//        "portalId" claim matches the resolved user's portal (super users exempt).
//
//  All collaborators are Moq test doubles, so these tests are fast and free of I/O
//  or a database. The owned-type PERSISTENCE mapping itself is proven separately
//  against the EF Core InMemory provider; here the repository is mocked to return a
//  user whose Membership is populated, which is exactly what the owned-type mapping
//  guarantees at runtime.
// -----------------------------------------------------------------------------

using System.Security.Claims;
using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Unit tests for <see cref="AuthService"/> covering login credential verification (F1), refresh-token
/// validate/rotate/revoke and logout revocation (F2), and portal scoping on login and current-user (F3).
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new(MockBehavior.Strict);
    private readonly Mock<IPasswordHasher> _passwordHasher = new(MockBehavior.Strict);
    private readonly Mock<IJwtTokenService> _jwtTokenService = new(MockBehavior.Strict);
    private readonly Mock<IRefreshTokenStore> _refreshTokenStore = new(MockBehavior.Strict);
    private readonly Mock<IPortalContextAccessor> _portalContextAccessor = new(MockBehavior.Strict);
    private readonly Mock<IMapper> _mapper = new(MockBehavior.Strict);

    private readonly AuthService _sut;

    /// <summary>
    /// Wires the strict mocks with the harmless default behaviours shared by most cases (token issuance
    /// and DTO projection) and constructs the system under test. Case-specific behaviour (repository
    /// results, portal context, refresh-token validity) is configured per test.
    /// </summary>
    public AuthServiceTests()
    {
        // Token issuance is not the focus of most cases; give it deterministic defaults.
        _jwtTokenService
            .Setup(x => x.CreateToken(It.IsAny<UserDto>(), It.IsAny<IEnumerable<string>>()))
            .Returns(("access-token", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        _jwtTokenService
            .Setup(x => x.GenerateRefreshToken())
            .Returns("new-refresh-token");

        // Every issued token is persisted (F2); default the store writes to no-ops.
        _refreshTokenStore
            .Setup(x => x.StoreAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _refreshTokenStore
            .Setup(x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _refreshTokenStore
            .Setup(x => x.RevokeAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Projection defaults: the concrete DTO contents are irrelevant because token creation is mocked.
        _mapper.Setup(x => x.Map<UserDto>(It.IsAny<User>())).Returns(new UserDto());

        // No ambient portal by default (matches the Infrastructure default accessor).
        _portalContextAccessor.Setup(x => x.GetPortalId()).Returns((int?)null);

        _sut = new AuthService(
            _userRepository.Object,
            _passwordHasher.Object,
            _jwtTokenService.Object,
            _refreshTokenStore.Object,
            _portalContextAccessor.Object,
            _mapper.Object);
    }

    // =========================================================================
    //  F1 — LoginAsync credential verification
    // =========================================================================

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenPairAndStoresRefreshToken()
    {
        var user = BuildUser(userId: 42, portalId: 0, password: "stored-hash");
        _userRepository
            .Setup(x => x.GetByUsernameAsync(0, "jdoe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(x => x.Verify("Secret123!", "stored-hash")).Returns(true);

        var result = await _sut.LoginAsync(new LoginRequestDto { Username = "jdoe", Password = "Secret123!" });

        result.Should().NotBeNull("valid credentials must yield a token pair");
        result!.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("new-refresh-token");
        result.TokenType.Should().Be("Bearer");
        // F2: the issued refresh token is persisted server-side under the authenticated user's id.
        _refreshTokenStore.Verify(
            x => x.StoreAsync(42, "new-refresh-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownUser_ReturnsNull()
    {
        _userRepository
            .Setup(x => x.GetByUsernameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.LoginAsync(new LoginRequestDto { Username = "ghost", Password = "x" });

        result.Should().BeNull("an unknown user cannot authenticate");
        _refreshTokenStore.Verify(
            x => x.StoreAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNull()
    {
        var user = BuildUser(password: "stored-hash");
        _userRepository
            .Setup(x => x.GetByUsernameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(x => x.Verify("wrong", "stored-hash")).Returns(false);

        var result = await _sut.LoginAsync(new LoginRequestDto { Username = "jdoe", Password = "wrong" });

        result.Should().BeNull("a bad password must fail authentication");
    }

    [Fact]
    public async Task LoginAsync_WithUnapprovedAccount_ReturnsNull()
    {
        var user = BuildUser(password: "stored-hash", approved: false);
        _userRepository
            .Setup(x => x.GetByUsernameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), "stored-hash")).Returns(true);

        var result = await _sut.LoginAsync(new LoginRequestDto { Username = "jdoe", Password = "Secret123!" });

        result.Should().BeNull("an unapproved account must not receive tokens even with the correct password");
    }

    [Fact]
    public async Task LoginAsync_WithLockedOutAccount_ReturnsNull()
    {
        var user = BuildUser(password: "stored-hash", lockedOut: true);
        _userRepository
            .Setup(x => x.GetByUsernameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), "stored-hash")).Returns(true);

        var result = await _sut.LoginAsync(new LoginRequestDto { Username = "jdoe", Password = "Secret123!" });

        result.Should().BeNull("a locked-out account must not receive tokens even with the correct password");
    }

    // =========================================================================
    //  F3 — LoginAsync portal scoping
    // =========================================================================

    [Fact]
    public async Task LoginAsync_WhenAmbientPortalPresent_UsesItInsteadOfClientPortal()
    {
        _portalContextAccessor.Setup(x => x.GetPortalId()).Returns(7);
        var user = BuildUser(userId: 1, portalId: 7, password: "stored-hash");
        _userRepository
            .Setup(x => x.GetByUsernameAsync(7, "jdoe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), "stored-hash")).Returns(true);

        // Client sends NO portal id; the trusted ambient portal (7) must be used for the lookup.
        var result = await _sut.LoginAsync(new LoginRequestDto { Username = "jdoe", Password = "Secret123!" });

        result.Should().NotBeNull();
        _userRepository.Verify(
            x => x.GetByUsernameAsync(7, "jdoe", It.IsAny<CancellationToken>()), Times.Once,
            "the trusted ambient portal must be used to scope the user lookup");
    }

    [Fact]
    public async Task LoginAsync_WhenClientPortalDisagreesWithAmbient_ReturnsNullWithoutLookup()
    {
        _portalContextAccessor.Setup(x => x.GetPortalId()).Returns(7);

        // Client attempts to authenticate against portal 9 while the request host resolves to portal 7.
        var result = await _sut.LoginAsync(
            new LoginRequestDto { Username = "jdoe", Password = "Secret123!", PortalId = 9 });

        result.Should().BeNull("a client must not override the trusted host-derived portal");
        _userRepository.Verify(
            x => x.GetByUsernameAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "the request must be rejected before any user lookup occurs");
    }

    [Fact]
    public async Task LoginAsync_WhenNoAmbientPortal_FallsBackToClientPortal()
    {
        // Default accessor returns null; the client-supplied portal id (9) is honoured as the fallback.
        var user = BuildUser(userId: 1, portalId: 9, password: "stored-hash");
        _userRepository
            .Setup(x => x.GetByUsernameAsync(9, "jdoe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), "stored-hash")).Returns(true);

        var result = await _sut.LoginAsync(
            new LoginRequestDto { Username = "jdoe", Password = "Secret123!", PortalId = 9 });

        result.Should().NotBeNull();
        _userRepository.Verify(
            x => x.GetByUsernameAsync(9, "jdoe", It.IsAny<CancellationToken>()), Times.Once,
            "with no ambient portal the client-supplied portal is used");
    }

    [Fact]
    public async Task LoginAsync_WhenNoAmbientAndNoClientPortal_DefaultsToPortalZero()
    {
        var user = BuildUser(userId: 1, portalId: 0, password: "stored-hash");
        _userRepository
            .Setup(x => x.GetByUsernameAsync(0, "jdoe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), "stored-hash")).Returns(true);

        var result = await _sut.LoginAsync(new LoginRequestDto { Username = "jdoe", Password = "Secret123!" });

        result.Should().NotBeNull();
        _userRepository.Verify(
            x => x.GetByUsernameAsync(0, "jdoe", It.IsAny<CancellationToken>()), Times.Once,
            "portal 0 is the default when neither an ambient nor a client portal is supplied");
    }

    // =========================================================================
    //  F2 — RefreshAsync validate / rotate
    // =========================================================================

    [Fact]
    public async Task RefreshAsync_WithValidToken_RevokesOldTokenAndIssuesNewPair()
    {
        _refreshTokenStore
            .Setup(x => x.ValidateAsync("old-refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)5);
        _userRepository
            .Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildUser(userId: 5));

        var result = await _sut.RefreshAsync(new RefreshRequestDto { RefreshToken = "old-refresh" });

        result.Should().NotBeNull("a valid refresh token must yield a new token pair");
        result!.RefreshToken.Should().Be("new-refresh-token");
        // Rotation: the presented token is revoked and the freshly issued one is stored.
        _refreshTokenStore.Verify(x => x.RevokeAsync("old-refresh", It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenStore.Verify(x => x.StoreAsync(5, "new-refresh-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_WithInvalidToken_ReturnsNullAndDoesNotTouchRepositoryOrRotate()
    {
        _refreshTokenStore
            .Setup(x => x.ValidateAsync("bogus", It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var result = await _sut.RefreshAsync(new RefreshRequestDto { RefreshToken = "bogus" });

        result.Should().BeNull("an unknown/expired/revoked refresh token must fail validation");
        _userRepository.Verify(
            x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenStore.Verify(
            x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenStore.Verify(
            x => x.StoreAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_WhenSubjectNoLongerExists_ReturnsNullAndRevokesToken()
    {
        _refreshTokenStore
            .Setup(x => x.ValidateAsync("orphan", It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)77);
        _userRepository
            .Setup(x => x.GetByIdAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.RefreshAsync(new RefreshRequestDto { RefreshToken = "orphan" });

        result.Should().BeNull("a refresh token whose user no longer exists must not renew");
        _refreshTokenStore.Verify(x => x.RevokeAsync("orphan", It.IsAny<CancellationToken>()), Times.Once,
            "the orphaned token must be revoked so it cannot be retried");
        _refreshTokenStore.Verify(
            x => x.StoreAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    //  F2 — LogoutAsync revocation
    // =========================================================================

    [Fact]
    public async Task LogoutAsync_WithAuthenticatedUser_RevokesAllTheirRefreshTokens()
    {
        var principal = BuildPrincipal(new Claim(ClaimTypes.NameIdentifier, "5"));

        await _sut.LogoutAsync(principal);

        _refreshTokenStore.Verify(x => x.RevokeAllAsync(5, It.IsAny<CancellationToken>()), Times.Once,
            "logout must revoke every refresh token held for the user");
    }

    [Fact]
    public async Task LogoutAsync_WithUnauthenticatedPrincipal_DoesNothing()
    {
        var principal = BuildPrincipal(); // no NameIdentifier claim

        await _sut.LogoutAsync(principal);

        _refreshTokenStore.Verify(
            x => x.RevokeAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never,
            "with no resolvable user id there is nothing to revoke");
    }

    // =========================================================================
    //  F3 — GetCurrentUserAsync claim/resource scoping
    // =========================================================================

    [Fact]
    public async Task GetCurrentUserAsync_WhenPortalClaimMatches_ReturnsProjectedUser()
    {
        var entity = BuildUser(userId: 5, portalId: 7);
        _userRepository.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var dto = new CurrentUserDto { User = new UserDto { UserID = 5, PortalID = 7 } };
        _mapper.Setup(x => x.Map<CurrentUserDto>(entity)).Returns(dto);

        var principal = BuildPrincipal(
            new Claim(ClaimTypes.NameIdentifier, "5"),
            new Claim("portalId", "7"));

        var result = await _sut.GetCurrentUserAsync(principal);

        result.Should().BeSameAs(dto, "a matching portal claim must resolve the current user");
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenPortalClaimMismatches_ReturnsNull()
    {
        var entity = BuildUser(userId: 5, portalId: 9); // resolved user lives in portal 9
        _userRepository.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var principal = BuildPrincipal(
            new Claim(ClaimTypes.NameIdentifier, "5"),
            new Claim("portalId", "7")); // token was issued for portal 7

        var result = await _sut.GetCurrentUserAsync(principal);

        result.Should().BeNull("a token issued for a different portal must not resolve the user");
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenPortalClaimMissing_ReturnsNullForNonSuperUser()
    {
        var entity = BuildUser(userId: 5, portalId: 7);
        _userRepository.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var principal = BuildPrincipal(new Claim(ClaimTypes.NameIdentifier, "5")); // no portalId claim

        var result = await _sut.GetCurrentUserAsync(principal);

        result.Should().BeNull("a missing portal claim cannot satisfy portal scoping for a non-super user");
    }

    [Fact]
    public async Task GetCurrentUserAsync_ForSuperUser_BypassesPortalScoping()
    {
        var entity = BuildUser(userId: 5, portalId: 9, isSuperUser: true);
        _userRepository.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var dto = new CurrentUserDto { User = new UserDto { UserID = 5, PortalID = 9, IsSuperUser = true } };
        _mapper.Setup(x => x.Map<CurrentUserDto>(entity)).Returns(dto);

        // Super user's token carries a different portal claim; scoping is intentionally bypassed for host users.
        var principal = BuildPrincipal(
            new Claim(ClaimTypes.NameIdentifier, "5"),
            new Claim("portalId", "0"));

        var result = await _sut.GetCurrentUserAsync(principal);

        result.Should().BeSameAs(dto, "super users are host-level and span portals, so scoping is bypassed");
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithUnauthenticatedPrincipal_ReturnsNull()
    {
        var principal = BuildPrincipal(); // no NameIdentifier

        var result = await _sut.GetCurrentUserAsync(principal);

        result.Should().BeNull("an unauthenticated principal has no current user");
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenUserNotFound_ReturnsNull()
    {
        _userRepository.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var principal = BuildPrincipal(
            new Claim(ClaimTypes.NameIdentifier, "5"),
            new Claim("portalId", "7"));

        var result = await _sut.GetCurrentUserAsync(principal);

        result.Should().BeNull("a token whose subject no longer exists must not resolve a user");
    }

    // =========================================================================
    //  Helpers
    // =========================================================================

    private static User BuildUser(
        int userId = 1,
        int portalId = 0,
        string password = "stored-hash",
        bool approved = true,
        bool lockedOut = false,
        bool isSuperUser = false)
        => new()
        {
            UserID = userId,
            PortalID = portalId,
            Username = "jdoe",
            Email = "jdoe@example.com",
            IsSuperUser = isSuperUser,
            Roles = new[] { "Registered Users" },
            Membership = new UserMembership
            {
                Password = password,
                Approved = approved,
                LockedOut = lockedOut
            }
        };

    private static ClaimsPrincipal BuildPrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "TestAuth"));
}
