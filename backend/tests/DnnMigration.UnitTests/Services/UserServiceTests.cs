using Xunit;
using Moq;
using FluentAssertions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
// MIGRATION: AutoMapper was upgraded to 15.1.1 (security remediation of the AAP §0.5.1 12.0.1 pin —
// see DnnMigration.Application.csproj). From AutoMapper 14+ the MapperConfiguration constructor requires
// an ILoggerFactory, so the test builds its real mapper with NullLoggerFactory.Instance.
using Microsoft.Extensions.Logging.Abstractions;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="UserService"/>, asserting behavioral equivalence with the legacy
/// <c>Library/Components/Users/UserController.vb</c> record-management surface and the sanctioned
/// DES → BCrypt password change driven by <c>PortalSecurity.vb</c>. Validates Gate 2.
/// </summary>
/// <remarks>
/// Collaborators are mocked with Moq (strict for the repositories and the hasher so that only the
/// interactions a test sets up are permitted); <see cref="IMapper"/> is built from a REAL
/// <see cref="MapperConfiguration"/> backed by <see cref="UserProfile"/> so the entity↔DTO projection
/// (including the read-only computed <c>FullName</c> and the security-driven Password ignore) is
/// exercised exactly as it runs in production.
///
/// MIGRATION: the verified <see cref="UserService"/> constructor takes an <see cref="IRoleRepository"/>
/// collaborator (3rd parameter) because <c>CreateAsync</c> ports the legacy CreateUser auto-assignment of
/// AutoAssignment portal roles to a new non-superuser [UserController.vb:L166-180]. These tests focus on
/// the User record-management contract, so <see cref="SetupValidCreate"/> neutralizes that cross-aggregate
/// path by returning an empty portal-role set (no <c>AddUserRoleAsync</c> calls occur).
/// </remarks>
public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new(MockBehavior.Strict);
    private readonly Mock<IPortalRepository> _portalRepo = new(MockBehavior.Strict);
    private readonly Mock<IRoleRepository> _roleRepo = new(MockBehavior.Strict);
    private readonly Mock<IPasswordHasher> _hasher = new(MockBehavior.Strict);
    private readonly Mock<IValidator<CreateUserDto>> _createValidator = new();
    private readonly Mock<IValidator<UpdateUserDto>> _updateValidator = new();
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<UserProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private UserService CreateSut() =>
        new(_userRepo.Object, _portalRepo.Object, _roleRepo.Object, _hasher.Object, _mapper, _createValidator.Object, _updateValidator.Object);

    private void SetupValidCreate()
    {
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
        // MIGRATION: CreateAsync auto-assigns every AutoAssignment portal role to a new non-superuser
        // (legacy UserController.CreateUser L166-180). Return an empty portal-role set so that path is a
        // no-op here and these tests isolate the password-hashing contract.
        _roleRepo.Setup(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Role>());
    }

    private void SetupValidUpdate() =>
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task GetByIdAsync_maps_when_found()
    {
        _userRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new User { UserID = 7, FirstName = "Jane", LastName = "Doe" });
        var dto = await CreateSut().GetByIdAsync(7);
        dto!.UserID.Should().Be(7);
        dto.FullName.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task GetByIdAsync_null_when_missing()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        (await CreateSut().GetByIdAsync(1)).Should().BeNull();
    }

    [Fact]
    public async Task GetByUsernameAsync_passes_portal_scope()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync(2, "admin", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new User { UserID = 3, Username = "admin", PortalID = 2 });
        var dto = await CreateSut().GetByUsernameAsync(2, "admin");
        dto!.Username.Should().Be("admin");
        _userRepo.Verify(r => r.GetByUsernameAsync(2, "admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByEmailAsync_passes_portal_scope()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(2, "a@b.com", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new User { UserID = 4, Email = "a@b.com" });
        (await CreateSut().GetByEmailAsync(2, "a@b.com"))!.Email.Should().Be("a@b.com");
    }

    [Fact]
    public async Task GetByPortalAsync_returns_paged_result()
    {
        var items = new List<User> { new() { UserID = 1 }, new() { UserID = 2 } };
        _userRepo.Setup(r => r.GetByPortalAsync(5, 0, 10, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((items, 25));
        var page = await CreateSut().GetByPortalAsync(5, 0, 10);
        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(25);
        page.PageIndex.Should().Be(0);
        page.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task CreateAsync_hashes_password_never_stores_plaintext()
    {
        // MIGRATION: DES -> BCrypt; plaintext password must NEVER be persisted (UserController + PortalSecurity.vb)
        SetupValidCreate();
        User? captured = null;
        _hasher.Setup(h => h.Hash("s3cret!")).Returns("BCRYPT$HASH");
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((User u, CancellationToken _) => { captured = u; u.UserID = 50; return u; });

        await CreateSut().CreateAsync(new CreateUserDto { Username = "new", Password = "s3cret!" });

        captured!.Password.Should().Be("BCRYPT$HASH");
        captured.Password.Should().NotBe("s3cret!");
        _hasher.Verify(h => h.Hash("s3cret!"), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_hashes_empty_string_when_password_null()
    {
        // MIGRATION: pass request.Password ?? string.Empty to avoid CS8604
        SetupValidCreate();
        _hasher.Setup(h => h.Hash(string.Empty)).Returns("EMPTYHASH");
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((User u, CancellationToken _) => { u.UserID = 51; return u; });

        await CreateSut().CreateAsync(new CreateUserDto { Username = "x", Password = null });

        _hasher.Verify(h => h.Hash(string.Empty), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_invalid_throws_and_does_not_hash_or_persist()
    {
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new ValidationException("bad"));
        Func<Task> act = () => CreateSut().CreateAsync(new CreateUserDto());
        await act.Should().ThrowAsync<ValidationException>();
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_updates_existing()
    {
        SetupValidUpdate();
        _userRepo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(new User { UserID = 9, FirstName = "Old" });
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var dto = await CreateSut().UpdateAsync(new UpdateUserDto { UserID = 9, FirstName = "New" });
        dto.FirstName.Should().Be("New");
    }

    [Fact]
    public async Task UpdateAsync_throws_KeyNotFound_when_missing()
    {
        SetupValidUpdate();
        _userRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        Func<Task> act = () => CreateSut().UpdateAsync(new UpdateUserDto { UserID = 99 });
        await act.Should().ThrowAsync<KeyNotFoundException>();
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_regular_user()
    {
        // MIGRATION: soft-delete via repository (UserController.DeleteUser L200-259)
        _userRepo.Setup(r => r.GetByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(new User { UserID = 11, PortalID = 1 });
        _portalRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Portal { PortalID = 1, AdministratorId = 999 });
        _userRepo.Setup(r => r.DeleteAsync(11, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await CreateSut().DeleteAsync(11);
        _userRepo.Verify(r => r.DeleteAsync(11, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_throws_when_target_is_portal_administrator()
    {
        // MIGRATION: admin guard - cannot delete the portal Administrator (UserController L200-259)
        _userRepo.Setup(r => r.GetByIdAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(new User { UserID = 20, PortalID = 1 });
        _portalRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Portal { PortalID = 1, AdministratorId = 20 });
        Func<Task> act = () => CreateSut().DeleteAsync(20);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*administrator*");
        _userRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_missing_user_is_quiet_no_op()
    {
        // MIGRATION: deleting a missing user is a quiet no-op (legacy Catch Exc -> CanDelete=False)
        _userRepo.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        await CreateSut().DeleteAsync(404);
        _userRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _portalRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
