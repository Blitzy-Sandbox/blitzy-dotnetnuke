using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using UserEntity = DnnMigration.Domain.Entities.User;
using RoleEntity = DnnMigration.Domain.Entities.Role;
using PortalEntity = DnnMigration.Domain.Entities.Portal;
using ProfileDefEntity = DnnMigration.Domain.Entities.ProfilePropertyDefinition;
using ProfileValueEntity = DnnMigration.Domain.Entities.UserProfileValue;

namespace DnnMigration.UnitTests.Services;

// MIGRATION: parity tests for UserService (derived from UserController.vb). Auto-assign roles + admin guard preserved.
public sealed class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<IPortalRepository> _portalRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMapper> _mapper = new();
    // MIGRATION: CP1 review (UserService) - initial-credential creation (BCrypt hash + credential store) and the
    // per-portal Security_DisplayNameFormat rule are now collaborators of the user-creation/update flow.
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ICredentialStore> _credentialStore = new();
    private readonly Mock<IPortalSettingsService> _portalSettings = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _sut = new UserService(
            _userRepo.Object,
            _roleRepo.Object,
            _portalRepo.Object,
            _uow.Object,
            _mapper.Object,
            _passwordHasher.Object,
            _credentialStore.Object,
            _portalSettings.Object);
    }

    private static UserEntity NewUser(int id, int portalId = 1, string username = "user") =>
        new() { UserId = id, PortalId = portalId, Username = username, Email = "user@example.com", IsApproved = true };

    private void MapResponseByIdentity() =>
        _mapper.Setup(m => m.Map<UserResponse>(It.IsAny<UserEntity>()))
               .Returns((UserEntity u) => new UserResponse { UserId = u.UserId, Username = u.Username });

    // ---------- GetByPortalAsync ----------
    // MIGRATION: CP1 review (IUserRepository #paging) - paging is performed server-side by GetByPortalPagedAsync;
    // the service surfaces the repository's page as-is. The mock returns the already-paged result.
    [Fact]
    public async Task GetByPortalAsync_ReturnsPagedMappedItems()
    {
        var page = new List<UserEntity> { NewUser(1), NewUser(2), NewUser(3) };
        _userRepo.Setup(r => r.GetByPortalPagedAsync(1, 0, 10))
                 .ReturnsAsync(((IEnumerable<UserEntity>)page, 3));
        MapResponseByIdentity();

        var result = await _sut.GetByPortalAsync(1, 0, 10);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(3);
        result.Value.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByPortalAsync_AppliesZeroBasedPaging()
    {
        var page = new List<UserEntity> { NewUser(3), NewUser(4) };
        _userRepo.Setup(r => r.GetByPortalPagedAsync(1, 1, 2))
                 .ReturnsAsync(((IEnumerable<UserEntity>)page, 5));
        MapResponseByIdentity();

        var result = await _sut.GetByPortalAsync(1, 1, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.UserId).Should().Equal(3, 4);
    }

    // ---------- GetByIdAsync ----------
    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsMappedResponse()
    {
        var user = NewUser(2);
        var dto = new UserResponse { UserId = 2 };
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(user);
        _mapper.Setup(m => m.Map<UserResponse>(user)).Returns(dto);

        var result = await _sut.GetByIdAsync(1, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsFailureWithExactMessage()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 8)).ReturnsAsync((UserEntity?)null);

        var result = await _sut.GetByIdAsync(1, 8);

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested user was not found.");
    }

    // ---------- CreateAsync ----------
    [Fact]
    public async Task CreateAsync_WhenUsernameDuplicate_ReturnsFailure_AndDoesNotAdd()
    {
        var request = new CreateUserRequest { PortalId = 1, Username = "existing", Email = "new@example.com" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "existing")).ReturnsAsync(NewUser(99, username: "existing"));

        var result = await _sut.CreateAsync(request);

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review (UserService) - verbatim legacy "UserNameExists" resource string (exact parity).
        result.Errors.Should().Contain("A User Already Exists For the Username Specified. Please Register Again Using A Different Username.");
        _userRepo.Verify(r => r.AddAsync(It.IsAny<UserEntity>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DoesNotRejectDuplicateEmail_AtServiceLayer()
    {
        // MIGRATION: CP1 review (UserService #4) - the unconditional duplicate-EMAIL guard was REMOVED. Legacy
        // Website/release.config sets the membership provider's requiresUniqueEmail="false", so DotNetNuke did NOT
        // reject a duplicate email on create. Exact-parity migration (AAP 0.7.2) therefore creates the user
        // successfully at the service layer even when another account shares the email address.
        var request = new CreateUserRequest { PortalId = 1, Username = "newuser", Email = "user@example.com" };
        var entity = new UserEntity { UserId = 50, PortalId = 1, Username = "newuser", IsSuperUser = false };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "newuser")).ReturnsAsync((UserEntity?)null);
        _mapper.Setup(m => m.Map<UserEntity>(request)).Returns(entity);
        _roleRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<RoleEntity>());
        _userRepo.Setup(r => r.GetByIdAsync(1, 50)).ReturnsAsync(entity);
        MapResponseByIdentity();

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        _userRepo.Verify(r => r.AddAsync(entity), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_AutoAssignsAutoAssignmentRoles_WhenNotSuperUser()
    {
        var request = new CreateUserRequest { PortalId = 1, Username = "newuser", Email = "new@example.com" };
        var entity = new UserEntity { UserId = 5, PortalId = 1, Username = "newuser", IsSuperUser = false };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "newuser")).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<UserEntity>());
        _mapper.Setup(m => m.Map<UserEntity>(request)).Returns(entity);
        _userRepo.Setup(r => r.GetByIdAsync(1, 5)).ReturnsAsync(entity);
        _roleRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<RoleEntity>
        {
            new() { RoleId = 1, PortalId = 1, RoleName = "Registered", AutoAssignment = true },
            new() { RoleId = 2, PortalId = 1, RoleName = "Subscribers", AutoAssignment = false },
            new() { RoleId = 3, PortalId = 1, RoleName = "Members", AutoAssignment = true },
        });
        MapResponseByIdentity();

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.UserRoles.Should().HaveCount(2);
        entity.UserRoles.Select(ur => ur.RoleId).Should().BeEquivalentTo(new[] { 1, 3 });
        entity.UserRoles.Should().OnlyContain(ur => ur.EffectiveDate == null && ur.ExpiryDate == null);
        _userRepo.Verify(r => r.AddAsync(entity), Times.Once);
        // MIGRATION: CP1 review (UserService) - creation persists in two unit-of-work boundaries: the user row, then
        // the initial credential (BCrypt hash via ICredentialStore). Both invoke SaveChangesAsync.
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAsync_DoesNotAutoAssignRoles_WhenSuperUser()
    {
        var request = new CreateUserRequest { PortalId = 1, Username = "super", Email = "super@example.com" };
        var entity = new UserEntity { UserId = 6, PortalId = 1, Username = "super", IsSuperUser = true };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "super")).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<UserEntity>());
        _mapper.Setup(m => m.Map<UserEntity>(request)).Returns(entity);
        _userRepo.Setup(r => r.GetByIdAsync(1, 6)).ReturnsAsync(entity);
        MapResponseByIdentity();

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.UserRoles.Should().BeEmpty();
        _roleRepo.Verify(r => r.GetByPortalIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithPasswordFields_PersistsCredential_AndSucceeds()
    {
        // MIGRATION: CP1 review (UserService) - the create flow now hashes the supplied password (BCrypt) and persists
        // it via ICredentialStore. Creation still succeeds and adds exactly one user row.
        var request = new CreateUserRequest { PortalId = 1, Username = "newuser", Email = "new@example.com", Password = "Secret123!", Confirm = "Secret123!" };
        var entity = new UserEntity { UserId = 7, PortalId = 1, Username = "newuser", IsSuperUser = false };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "newuser")).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<UserEntity>());
        _mapper.Setup(m => m.Map<UserEntity>(request)).Returns(entity);
        _userRepo.Setup(r => r.GetByIdAsync(1, 7)).ReturnsAsync(entity);
        _roleRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<RoleEntity>());
        _passwordHasher.Setup(h => h.Hash("Secret123!")).Returns("hashed");
        MapResponseByIdentity();

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        _userRepo.Verify(r => r.AddAsync(entity), Times.Once);
        _credentialStore.Verify(c => c.SetPasswordAsync(7, "hashed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ReloadsUserForResponse()
    {
        var request = new CreateUserRequest { PortalId = 1, Username = "newuser", Email = "new@example.com" };
        var entity = new UserEntity { UserId = 8, PortalId = 1, Username = "newuser", IsSuperUser = false };
        var reloaded = new UserEntity { UserId = 8, PortalId = 1, Username = "newuser-reloaded" };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "newuser")).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<UserEntity>());
        _mapper.Setup(m => m.Map<UserEntity>(request)).Returns(entity);
        _roleRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<RoleEntity>());
        _userRepo.Setup(r => r.GetByIdAsync(1, 8)).ReturnsAsync(reloaded);
        _mapper.Setup(m => m.Map<UserResponse>(reloaded)).Returns(new UserResponse { UserId = 8, Username = "newuser-reloaded" });

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Username.Should().Be("newuser-reloaded");
        _userRepo.Verify(r => r.GetByIdAsync(1, 8), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_FallsBackToInsertedEntity_WhenReloadIsNull()
    {
        var request = new CreateUserRequest { PortalId = 1, Username = "newuser", Email = "new@example.com" };
        var entity = new UserEntity { UserId = 9, PortalId = 1, Username = "newuser", IsSuperUser = false };
        _userRepo.Setup(r => r.GetByUsernameAsync(1, "newuser")).ReturnsAsync((UserEntity?)null);
        _userRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<UserEntity>());
        _mapper.Setup(m => m.Map<UserEntity>(request)).Returns(entity);
        _roleRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<RoleEntity>());
        _userRepo.Setup(r => r.GetByIdAsync(1, 9)).ReturnsAsync((UserEntity?)null);
        _mapper.Setup(m => m.Map<UserResponse>(entity)).Returns(new UserResponse { UserId = 9, Username = "newuser" });

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(9);
    }

    // ---------- UpdateAsync ----------
    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsFailure_AndDoesNotSave()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 8)).ReturnsAsync((UserEntity?)null);

        var result = await _sut.UpdateAsync(1, 8, new UpdateUserRequest());

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested user was not found.");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_MapsAndSavesOnce()
    {
        var entity = NewUser(2);
        var request = new UpdateUserRequest();
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(entity);
        _mapper.Setup(m => m.Map(request, entity)).Returns(entity);
        _mapper.Setup(m => m.Map<UserResponse>(entity)).Returns(new UserResponse { UserId = 2 });

        var result = await _sut.UpdateAsync(1, 2, request);

        result.IsSuccess.Should().BeTrue();
        _userRepo.Verify(r => r.UpdateAsync(entity), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- DeleteAsync (admin guard) ----------
    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsFailure_AndDoesNothing()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 8)).ReturnsAsync((UserEntity?)null);

        var result = await _sut.DeleteAsync(1, 8);

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested user was not found.");
        _userRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_BlocksPortalAdministrator()
    {
        var user = new UserEntity { UserId = 8, PortalId = 1 };
        _userRepo.Setup(r => r.GetByIdAsync(1, 8)).ReturnsAsync(user);
        // MIGRATION: IPortalRepository.GetByIdAsync is portal-keyed (single arg) - the portal IS the tenant.
        _portalRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new PortalEntity { PortalId = 1, AdministratorId = 8 });

        var result = await _sut.DeleteAsync(1, 8);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("The portal administrator cannot be deleted.");
        _userRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_DeletesUser_WhenNotAdministrator()
    {
        var user = new UserEntity { UserId = 8, PortalId = 1 };
        _userRepo.Setup(r => r.GetByIdAsync(1, 8)).ReturnsAsync(user);
        _portalRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new PortalEntity { PortalId = 1, AdministratorId = 99 });

        var result = await _sut.DeleteAsync(1, 8);

        result.IsSuccess.Should().BeTrue();
        _userRepo.Verify(r => r.DeleteAsync(1, 8), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DeletesUser_WhenPortalNull()
    {
        var user = new UserEntity { UserId = 8, PortalId = 1 };
        _userRepo.Setup(r => r.GetByIdAsync(1, 8)).ReturnsAsync(user);
        _portalRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((PortalEntity?)null);

        var result = await _sut.DeleteAsync(1, 8);

        result.IsSuccess.Should().BeTrue();
        _userRepo.Verify(r => r.DeleteAsync(1, 8), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- Profile (GetProfileAsync / UpdateProfileAsync) ----------
    // MIGRATION: parity tests for the profile read/update workflow (UserProfile.vb GetPropertyValue /
    // SetProfileProperty over the EXISTING [ProfilePropertyDefinition] + [UserProfile] EAV, AAP 0.7.1).

    private static ProfileDefEntity Def(int id, string name, int portalId = 1, bool required = false, int length = 0, string? validation = null) =>
        new()
        {
            PropertyDefinitionId = id,
            PortalId = portalId,
            PropertyName = name,
            PropertyCategory = "Name",
            Required = required,
            Length = length,
            ValidationExpression = validation,
            Visible = true,
        };

    private static ProfileValueEntity Val(int defId, string? value, int userId = 2) =>
        new() { ProfileId = defId * 10, UserId = userId, PropertyDefinitionId = defId, PropertyValue = value };

    [Fact]
    public async Task GetProfileAsync_WhenUserNotFound_ReturnsFailure()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 99)).ReturnsAsync((UserEntity?)null);

        var result = await _sut.GetProfileAsync(1, 99);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("User '99' was not found in portal '1'.");
    }

    [Fact]
    public async Task GetProfileAsync_ProjectsDefinedValues_ToFlatDto()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity>
        {
            Def(1, "FirstName"), Def(2, "LastName"), Def(3, "Cell"), Def(4, "City"),
        });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity>
        {
            Val(1, "John"), Val(2, "Doe"), Val(3, "555-1234"), Val(4, "Springfield"),
        });

        var result = await _sut.GetProfileAsync(1, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("John");
        result.Value.LastName.Should().Be("Doe");
        result.Value.Cell.Should().Be("555-1234");
        result.Value.City.Should().Be("Springfield");
    }

    [Fact]
    public async Task GetProfileAsync_ComposesFullName_VerbatimWithSingleSpace()
    {
        // MIGRATION: UserProfile.vb FullName = FirstName & " " & LastName (no trim). Verbatim space join.
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity>
        {
            Def(1, "FirstName"), Def(2, "LastName"),
        });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity>
        {
            Val(1, "John"), Val(2, "Doe"),
        });

        var result = await _sut.GetProfileAsync(1, 2);

        result.Value.FullName.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetProfileAsync_FullName_CoalescesMissingPartsToEmpty()
    {
        // MIGRATION: a null part (Null.NullString) coalesces to empty, so the join keeps the single space.
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity>
        {
            Def(1, "FirstName"),
        });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity>
        {
            Val(1, "John"),
        });

        var result = await _sut.GetProfileAsync(1, 2);

        result.Value.FirstName.Should().Be("John");
        result.Value.LastName.Should().BeNull();
        result.Value.FullName.Should().Be("John ");
    }

    [Fact]
    public async Task GetProfileAsync_TimeZone_ParsesStoredInteger()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity> { Def(7, "TimeZone") });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity> { Val(7, "5") });

        var result = await _sut.GetProfileAsync(1, 2);

        result.Value.TimeZone.Should().Be(5);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData(null)]
    public async Task GetProfileAsync_TimeZone_DefaultsToMinusOne_WhenUnsetOrDirty(string? stored)
    {
        // MIGRATION: TimeZone defaults to Null.NullInteger (-1) when unset; documented divergence uses TryParse so
        // dirty stored data yields -1 instead of throwing (legacy Integer.Parse would have thrown).
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity> { Def(7, "TimeZone") });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity>
        {
            stored is null ? Val(7, null) : Val(7, stored),
        });

        var result = await _sut.GetProfileAsync(1, 2);

        result.Value.TimeZone.Should().Be(-1);
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsNull_ForPropertiesThePortalDoesNotDefine()
    {
        // MIGRATION: GetPropertyValue returns Null.NullString (-> null) when the portal does not define the property.
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity> { Def(1, "FirstName") });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity> { Val(1, "John") });

        var result = await _sut.GetProfileAsync(1, 2);

        result.Value.FirstName.Should().Be("John");
        result.Value.Website.Should().BeNull();
        result.Value.City.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenRequestNull_Throws()
    {
        var act = async () => await _sut.UpdateProfileAsync(1, 2, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenUserNotFound_ReturnsFailure()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 99)).ReturnsAsync((UserEntity?)null);

        var result = await _sut.UpdateProfileAsync(1, 99, new UserProfileDto { FirstName = "X" });

        result.IsFailure.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_RequiredViolation_ReturnsFailure_WithoutWriting()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity>
        {
            Def(1, "FirstName", required: true),
        });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity>());

        var result = await _sut.UpdateProfileAsync(1, 2, new UserProfileDto { FirstName = "" });

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("FirstName is required.");
        _userRepo.Verify(r => r.AddProfileValueAsync(It.IsAny<ProfileValueEntity>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_LengthViolation_ReturnsFailure()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity>
        {
            Def(3, "Cell", length: 5),
        });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity>());

        var result = await _sut.UpdateProfileAsync(1, 2, new UserProfileDto { Cell = "0123456789" });

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Cell exceeds the maximum length of 5 characters.");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_ValidationExpressionViolation_ReturnsFailure()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity>
        {
            Def(15, "Website", validation: @"^https?://.+$"),
        });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity>());

        var result = await _sut.UpdateProfileAsync(1, 2, new UserProfileDto { Website = "not-a-url" });

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Website is not in a valid format.");
    }

    [Fact]
    public async Task UpdateProfileAsync_ValidValues_UpdatesExistingRowInPlace()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity> { Def(1, "FirstName") });
        var existing = Val(1, "OldName");
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity> { existing });

        var result = await _sut.UpdateProfileAsync(1, 2, new UserProfileDto { FirstName = "NewName" });

        result.IsSuccess.Should().BeTrue();
        existing.PropertyValue.Should().Be("NewName");
        existing.LastUpdatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        _userRepo.Verify(r => r.AddProfileValueAsync(It.IsAny<ProfileValueEntity>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_ValidValues_InsertsNewRow_WhenNoExistingValue()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity> { Def(1, "FirstName") });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity>());
        ProfileValueEntity? added = null;
        _userRepo.Setup(r => r.AddProfileValueAsync(It.IsAny<ProfileValueEntity>()))
                 .Callback<ProfileValueEntity>(v => added = v)
                 .Returns(Task.CompletedTask);

        var result = await _sut.UpdateProfileAsync(1, 2, new UserProfileDto { FirstName = "Brandnew" });

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.UserId.Should().Be(2);
        added.PropertyDefinitionId.Should().Be(1);
        added.PropertyValue.Should().Be("Brandnew");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_PropertyPortalDoesNotDefine_IsNoOp()
    {
        // MIGRATION: SetProfileProperty did nothing when the portal did not define the property. Here the portal
        // defines only FirstName; the Website value in the request is silently ignored (no insert, no failure).
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity> { Def(1, "FirstName") });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity>());

        var result = await _sut.UpdateProfileAsync(1, 2, new UserProfileDto { FirstName = "John", Website = "https://ignored.example" });

        result.IsSuccess.Should().BeTrue();
        // Exactly one insert (FirstName); the undefined Website property is not written.
        _userRepo.Verify(r => r.AddProfileValueAsync(It.Is<ProfileValueEntity>(v => v.PropertyDefinitionId == 1)), Times.Once);
        _userRepo.Verify(r => r.AddProfileValueAsync(It.IsAny<ProfileValueEntity>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_ValidationExpressionMatches_Passes()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(NewUser(2));
        _userRepo.Setup(r => r.GetProfileDefinitionsAsync(1)).ReturnsAsync(new List<ProfileDefEntity>
        {
            Def(15, "Website", validation: @"^https?://.+$"),
        });
        _userRepo.Setup(r => r.GetProfileValuesAsync(2)).ReturnsAsync(new List<ProfileValueEntity>());

        var result = await _sut.UpdateProfileAsync(1, 2, new UserProfileDto { Website = "https://valid.example" });

        result.IsSuccess.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
