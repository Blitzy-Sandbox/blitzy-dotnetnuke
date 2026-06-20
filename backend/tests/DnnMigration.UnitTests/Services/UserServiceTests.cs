using Xunit;
using Moq;
using FluentAssertions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// xUnit unit tests for <see cref="UserService"/>, asserting behavioral equivalence with the legacy
/// <c>Library/Components/Users/UserController.vb</c> (DotNetNuke 4.9.0.85) and the single sanctioned
/// security change (DES -> BCrypt, driven by <c>PortalSecurity.vb</c>). Collaborators are mocked with Moq;
/// <see cref="IMapper"/> is built from a REAL <see cref="MapperConfiguration"/> wired with
/// <see cref="UserProfile"/> so the entity&lt;-&gt;DTO projections (the computed <c>FullName</c> and the
/// password anti-corruption boundary) are exercised for real. Validates Gate 2 (<c>dotnet test</c>).
/// </summary>
/// <remarks>
/// MIGRATION: the production <see cref="UserService"/> constructor takes an <see cref="IRoleRepository"/>
/// as its third dependency, used by <see cref="UserService.CreateAsync"/> to reproduce the legacy
/// <c>CreateUser</c> auto-assignment of a new non-superuser to every portal role flagged
/// <see cref="Role.AutoAssignment"/> (UserController.vb <c>CreateUser</c>). It is mocked here alongside the
/// other collaborators so the seven-argument constructor is satisfied and the auto-assignment branch is
/// covered. (The original prototype assumed a six-argument constructor without the role repository.)
/// </remarks>
public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new(MockBehavior.Strict);
    private readonly Mock<IPortalRepository> _portalRepo = new(MockBehavior.Strict);
    // MIGRATION: IRoleRepository is the third constructor dependency; CreateAsync uses it to auto-assign
    // new non-superusers to AutoAssignment portal roles (legacy UserController.CreateUser).
    private readonly Mock<IRoleRepository> _roleRepo = new(MockBehavior.Strict);
    private readonly Mock<IPasswordHasher> _hasher = new(MockBehavior.Strict);
    private readonly Mock<IValidator<CreateUserDto>> _createValidator = new();
    private readonly Mock<IValidator<UpdateUserDto>> _updateValidator = new();
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<UserProfile>()).CreateMapper();

    private UserService CreateSut() =>
        new(_userRepo.Object, _portalRepo.Object, _roleRepo.Object, _hasher.Object, _mapper, _createValidator.Object, _updateValidator.Object);

    private void SetupValidCreate() =>
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

    private void SetupValidUpdate() =>
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

    // Returns an empty portal-role set so the CreateAsync auto-assignment loop is a harmless no-op for the
    // tests that focus on password hashing. (Strict mock => the GetByPortalAsync call must be configured.)
    private void SetupNoPortalRoles() =>
        _roleRepo.Setup(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Role>());

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
        SetupNoPortalRoles();
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
        SetupNoPortalRoles();
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

    // ---- Additional coverage: CreateUser portal-role auto-assignment (legacy UserController.CreateUser) ----

    [Fact]
    public async Task CreateAsync_autoassigns_non_superuser_to_autoassignment_roles_only()
    {
        // MIGRATION: legacy CreateUser enrolled a NEW non-superuser into every portal role flagged
        // AutoAssignment=True via AddUserRole(..., Null.NullDate, Null.NullDate) [UserController.vb CreateUser].
        // The ported CreateAsync calls IRoleRepository.GetByPortalAsync then AddUserRoleAsync per AutoAssignment role.
        SetupValidCreate();
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("BCRYPT$HASH");
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((User u, CancellationToken _) => { u.UserID = 61; u.PortalID = 7; u.IsSuperUser = false; return u; });
        _roleRepo.Setup(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Role>
                 {
                     new() { RoleID = 100, PortalID = 7, AutoAssignment = true },
                     new() { RoleID = 200, PortalID = 7, AutoAssignment = false }
                 });
        _roleRepo.Setup(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((UserRole ur, CancellationToken _) => ur);

        await CreateSut().CreateAsync(new CreateUserDto { Username = "member", Password = "pw" });

        // Only the AutoAssignment role is joined, carrying the created user's id and a null (Null.NullDate) window.
        _roleRepo.Verify(r => r.AddUserRoleAsync(
            It.Is<UserRole>(ur => ur.RoleID == 100 && ur.UserID == 61 && ur.EffectiveDate == null && ur.ExpiryDate == null),
            It.IsAny<CancellationToken>()), Times.Once);
        // The non-AutoAssignment role is never joined.
        _roleRepo.Verify(r => r.AddUserRoleAsync(
            It.Is<UserRole>(ur => ur.RoleID == 200), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_superuser_skips_autoassignment()
    {
        // MIGRATION: the legacy auto-assignment is guarded by `If Not objUser.IsSuperUser` — a superuser is
        // never enrolled into portal roles, so the role repository must not be touched at all.
        SetupValidCreate();
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("BCRYPT$HASH");
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((User u, CancellationToken _) => { u.UserID = 62; u.IsSuperUser = true; return u; });

        await CreateSut().CreateAsync(new CreateUserDto { Username = "host", Password = "pw", IsSuperUser = true });

        _roleRepo.Verify(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _roleRepo.Verify(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
