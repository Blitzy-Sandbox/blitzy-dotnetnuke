using AutoMapper;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using UserEntity = DnnMigration.Domain.Entities.User;
using RoleEntity = DnnMigration.Domain.Entities.Role;
using UserRoleEntity = DnnMigration.Domain.Entities.UserRole;

namespace DnnMigration.UnitTests.Services;

// MIGRATION: parity tests for AuthService (derived from UserMembership.vb + PortalSecurity.vb).
// Login is FAIL-CLOSED this phase (no stored hash); token issuance is exercised via RefreshAsync.
// MIGRATION: CP1 review - AuthService now takes ICredentialStore (between IPasswordHasher and
// IJwtService) and is portal-scoped: ValidateRefreshToken returns RefreshTokenInfo(UserId, PortalId)
// and RefreshAsync/GetCurrentUserAsync resolve the user via GetByIdAsync(portalId, userId), while
// GenerateRefreshToken is issued per (userId, portalId). The mock user is UserId=5, PortalId=1, so
// RefreshTokenInfo(5, 1) drives GetByIdAsync(1, 5).
public sealed class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ICredentialStore> _credentialStore = new();
    private readonly Mock<IJwtService> _jwt = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _sut = new AuthService(_userRepo.Object, _uow.Object, _mapper.Object, _passwordHasher.Object, _credentialStore.Object, _jwt.Object);
    }

    private static UserEntity ApprovedUser(int id = 5, string username = "jane", int portalId = 1, bool superUser = false) =>
        new() { UserId = id, Username = username, PortalId = portalId, IsSuperUser = superUser, IsApproved = true, LockedOut = false };

    // ---------- LoginAsync (check order; fail-closed) ----------
    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ReturnsIncorrectCredentials()
    {
        var request = new LoginRequest { PortalId = 1, Username = "ghost", Password = "pw" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "ghost")).ReturnsAsync((UserEntity?)null);

        var result = await _sut.LoginAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Login failed. The username or password is incorrect.");
        _jwt.Verify(j => j.GenerateAccessToken(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenLockedOut_ReturnsLockedOutMessage()
    {
        var user = ApprovedUser();
        user.LockedOut = true;
        var request = new LoginRequest { PortalId = 1, Username = "jane", Password = "pw" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "jane")).ReturnsAsync(user);

        var result = await _sut.LoginAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("This account is locked out. Please contact your administrator.");
        _jwt.Verify(j => j.GenerateAccessToken(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenNotApproved_ReturnsNotApprovedMessage()
    {
        var user = ApprovedUser();
        user.IsApproved = false;
        var request = new LoginRequest { PortalId = 1, Username = "jane", Password = "pw" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "jane")).ReturnsAsync(user);

        var result = await _sut.LoginAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("This account is not approved. Please contact your administrator.");
        _jwt.Verify(j => j.GenerateAccessToken(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoginAsync_IsFailClosed_WhenStoredHashUnavailable(bool verifyResult)
    {
        var user = ApprovedUser();
        var request = new LoginRequest { PortalId = 1, Username = "jane", Password = "correct-horse" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "jane")).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(verifyResult);

        var result = await _sut.LoginAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Login failed. The username or password is incorrect.");
        _jwt.Verify(j => j.GenerateAccessToken(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("admin", false)]
    [InlineData("dnnadmin", false)]
    [InlineData("host", true)]
    [InlineData("dnnhost", true)]
    public async Task LoginAsync_NeverIssuesToken_ForInsecureDefaultPasswords(string password, bool superUser)
    {
        var user = ApprovedUser(superUser: superUser);
        var request = new LoginRequest { PortalId = 1, Username = "jane", Password = password };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "jane")).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var result = await _sut.LoginAsync(request);

        result.IsFailure.Should().BeTrue();
        _jwt.Verify(j => j.GenerateAccessToken(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    // MIGRATION (CP-FINAL review - Critical #2 "auth approval state persist"): when the verification code matches,
    // the unapproved non-superuser is approved and the approval is PERSISTED onto the existing membership row via
    // ICredentialStore.SetApprovedAsync (User.IsApproved is Ignore()d on [Users]; it lives in aspnet_Membership).
    [Fact]
    public async Task LoginAsync_WhenVerificationCodeMatches_PersistsApprovalViaCredentialStore()
    {
        var user = ApprovedUser();
        user.IsApproved = false; // unapproved non-superuser drives the verification-code branch
        var request = new LoginRequest { PortalId = 1, Username = "jane", Password = "pw", VerificationCode = "1-5" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "jane")).ReturnsAsync(user);

        var result = await _sut.LoginAsync(request);

        // Login still fails closed at credential verification (no stored hash set up), but the approval must have been
        // staged onto the membership row and committed before the password step.
        _credentialStore.Verify(c => c.SetApprovedAsync(5, true, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // MIGRATION (CP-FINAL review - Critical #2 "auth last-login state persist"): a fully successful login issues
    // tokens AND records the last-login timestamp onto the existing membership row via ICredentialStore.RecordLoginAsync
    // (User.LastLoginDate is Ignore()d on [Users]; it lives in aspnet_Membership).
    [Fact]
    public async Task LoginAsync_OnSuccess_IssuesTokens_AndRecordsLoginViaCredentialStore()
    {
        var user = ApprovedUser();
        user.UserRoles = new List<UserRoleEntity>();
        var dto = new CurrentUserDto { UserId = 5, Username = "jane" };
        var expiresAt = DateTime.UtcNow.AddMinutes(60);
        var request = new LoginRequest { PortalId = 1, Username = "jane", Password = "correct-horse" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "jane")).ReturnsAsync(user);
        _credentialStore.Setup(c => c.GetPasswordHashAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync("stored-hash");
        _passwordHasher.Setup(h => h.Verify("correct-horse", "stored-hash")).Returns(true);
        _jwt.Setup(j => j.GenerateAccessToken(5, "jane", 1, false, It.IsAny<IEnumerable<string>>()))
            .Returns(("access-tok", expiresAt, 3600));
        _jwt.Setup(j => j.GenerateRefreshToken(5, 1)).Returns("refresh-tok");
        _mapper.Setup(m => m.Map<CurrentUserDto>(user)).Returns(dto);

        var result = await _sut.LoginAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-tok");
        result.Value.RefreshToken.Should().Be("refresh-tok");
        result.Value.User.Should().BeSameAs(dto);
        _credentialStore.Verify(c => c.RecordLoginAsync(5, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- RefreshAsync (rotation; role building; failures) ----------
    [Fact]
    public async Task RefreshAsync_WhenTokenInvalid_ReturnsFailure_AndDoesNotRotate()
    {
        var request = new RefreshRequest { RefreshToken = "bad" };
        _jwt.Setup(j => j.ValidateRefreshToken("bad")).Returns((RefreshTokenInfo?)null);

        var result = await _sut.RefreshAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Invalid or expired refresh token.");
        _jwt.Verify(j => j.GenerateAccessToken(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        _jwt.Verify(j => j.RevokeRefreshToken(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_WhenUserNotFound_ReturnsFailure()
    {
        var request = new RefreshRequest { RefreshToken = "old" };
        _jwt.Setup(j => j.ValidateRefreshToken("old")).Returns(new RefreshTokenInfo(5, 1));
        _userRepo.Setup(r => r.GetByIdAsync(1, 5)).ReturnsAsync((UserEntity?)null);

        var result = await _sut.RefreshAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task RefreshAsync_WhenLockedOut_ReturnsAccountUnusable()
    {
        var user = ApprovedUser();
        user.LockedOut = true;
        var request = new RefreshRequest { RefreshToken = "old" };
        _jwt.Setup(j => j.ValidateRefreshToken("old")).Returns(new RefreshTokenInfo(5, 1));
        _userRepo.Setup(r => r.GetByIdAsync(1, 5)).ReturnsAsync(user);

        var result = await _sut.RefreshAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("This account can no longer be used.");
    }

    [Fact]
    public async Task RefreshAsync_WhenNotApproved_ReturnsAccountUnusable()
    {
        var user = ApprovedUser();
        user.IsApproved = false;
        var request = new RefreshRequest { RefreshToken = "old" };
        _jwt.Setup(j => j.ValidateRefreshToken("old")).Returns(new RefreshTokenInfo(5, 1));
        _userRepo.Setup(r => r.GetByIdAsync(1, 5)).ReturnsAsync(user);

        var result = await _sut.RefreshAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("This account can no longer be used.");
    }

    [Fact]
    public async Task RefreshAsync_OnSuccess_RotatesTokens_AndBuildsRolesFromUserRoles()
    {
        var user = ApprovedUser();
        user.UserRoles = new List<UserRoleEntity>
        {
            new() { UserRoleId = 1, UserId = 5, RoleId = 10, Role = new RoleEntity { RoleId = 10, RoleName = "Admin" } },
            new() { UserRoleId = 2, UserId = 5, RoleId = 11, Role = new RoleEntity { RoleId = 11, RoleName = "Editor" } },
            new() { UserRoleId = 3, UserId = 5, RoleId = 12, Role = null },
        };
        var dto = new CurrentUserDto { UserId = 5, Username = "jane" };
        var expiresAt = DateTime.UtcNow.AddMinutes(60);
        var request = new RefreshRequest { RefreshToken = "old" };
        _jwt.Setup(j => j.ValidateRefreshToken("old")).Returns(new RefreshTokenInfo(5, 1));
        _userRepo.Setup(r => r.GetByIdAsync(1, 5)).ReturnsAsync(user);
        _jwt.Setup(j => j.GenerateAccessToken(5, "jane", 1, false, It.IsAny<IEnumerable<string>>()))
            .Returns(("access-tok", expiresAt, 3600));
        _jwt.Setup(j => j.GenerateRefreshToken(5, 1)).Returns("new-refresh");
        _mapper.Setup(m => m.Map<CurrentUserDto>(user)).Returns(dto);

        var result = await _sut.RefreshAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-tok");
        result.Value.RefreshToken.Should().Be("new-refresh");
        result.Value.TokenType.Should().Be("Bearer");
        result.Value.ExpiresIn.Should().Be(3600);
        result.Value.ExpiresAt.Should().Be(expiresAt);
        result.Value.User.Should().BeSameAs(dto);
        _jwt.Verify(j => j.RevokeRefreshToken("old"), Times.Once);
        _jwt.Verify(j => j.GenerateAccessToken(5, "jane", 1, false,
            It.Is<IEnumerable<string>>(roles => roles.SequenceEqual(new[] { "Admin", "Editor" }))), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- LogoutAsync ----------
    [Fact]
    public async Task LogoutAsync_RevokesRefreshToken_AndSucceeds()
    {
        var request = new RefreshRequest { RefreshToken = "tok" };

        var result = await _sut.LogoutAsync(request);

        result.IsSuccess.Should().BeTrue();
        _jwt.Verify(j => j.RevokeRefreshToken("tok"), Times.Once);
    }

    // ---------- GetCurrentUserAsync ----------
    [Fact]
    public async Task GetCurrentUserAsync_WhenNotFound_ReturnsFailure()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 5)).ReturnsAsync((UserEntity?)null);

        var result = await _sut.GetCurrentUserAsync(1, 5);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("The current user could not be found.");
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenFound_ReturnsMappedDto()
    {
        var user = ApprovedUser();
        var dto = new CurrentUserDto { UserId = 5, Username = "jane" };
        _userRepo.Setup(r => r.GetByIdAsync(1, 5)).ReturnsAsync(user);
        _mapper.Setup(m => m.Map<CurrentUserDto>(user)).Returns(dto);

        var result = await _sut.GetCurrentUserAsync(1, 5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    // ---------- ForgotPasswordAsync (SendPassword.ascx.vb parity; anti-enumeration) ----------
    // MIGRATION: the password-reset request is SECURE-BY-DESIGN: it performs a portal-scoped lookup unconditionally
    // and returns an identical generic message whether or not the account exists (anti-enumeration + anti-timing).
    // BCrypt (one-way) makes the legacy "send the actual password" reminder impossible; email dispatch is OUT OF
    // SCOPE per AAP 0.6.2 (Services.Mail). Tokens are never issued.

    private const string GenericResetMessage =
        "If an account matching the supplied details exists, instructions to reset the password have been sent to its registered email address.";

    [Fact]
    public async Task ForgotPasswordAsync_WhenAccountExists_ReturnsGenericMessage()
    {
        var request = new ForgotPasswordRequest { PortalId = 1, UsernameOrEmail = "jane" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "jane")).ReturnsAsync(ApprovedUser());

        var result = await _sut.ForgotPasswordAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Be(GenericResetMessage);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenAccountMissing_ReturnsIdenticalGenericMessage()
    {
        var request = new ForgotPasswordRequest { PortalId = 1, UsernameOrEmail = "ghost" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "ghost")).ReturnsAsync((UserEntity?)null);

        var result = await _sut.ForgotPasswordAsync(request);

        // Anti-enumeration: identical to the account-exists outcome.
        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Be(GenericResetMessage);
    }

    [Fact]
    public async Task ForgotPasswordAsync_PerformsPortalScopedLookupUnconditionally()
    {
        var request = new ForgotPasswordRequest { PortalId = 7, UsernameOrEmail = "user@example.com" };
        _userRepo.Setup(r => r.GetByUsernameAsync(7, "user@example.com")).ReturnsAsync((UserEntity?)null);

        await _sut.ForgotPasswordAsync(request);

        _userRepo.Verify(r => r.GetByUsernameAsync(7, "user@example.com"), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_DoesNotIssueTokens()
    {
        var request = new ForgotPasswordRequest { PortalId = 1, UsernameOrEmail = "jane" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "jane")).ReturnsAsync(ApprovedUser());

        await _sut.ForgotPasswordAsync(request);

        _jwt.Verify(j => j.GenerateAccessToken(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        _jwt.Verify(j => j.GenerateRefreshToken(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenRequestNull_Throws()
    {
        var act = async () => await _sut.ForgotPasswordAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
