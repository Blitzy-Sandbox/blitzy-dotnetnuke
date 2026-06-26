using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using RoleEntity = DnnMigration.Domain.Entities.Role;
using UserEntity = DnnMigration.Domain.Entities.User;
using UserRoleEntity = DnnMigration.Domain.Entities.UserRole;

namespace DnnMigration.UnitTests.Services;

// MIGRATION: parity tests for RoleService (derived from RoleController.vb). Auto-assign via user side; read-only user-roles.
public sealed class RoleServiceTests
{
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    // MIGRATION: CP1 review (RoleService) - portal repository for the system-role guard (Administrators/Registered).
    private readonly Mock<IPortalRepository> _portalRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly RoleService _sut;

    public RoleServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _sut = new RoleService(_roleRepo.Object, _userRepo.Object, _portalRepo.Object, _uow.Object, _mapper.Object);
    }

    private static RoleEntity NewRole(int id, int portalId = 1, string name = "Role") =>
        new() { RoleId = id, PortalId = portalId, RoleName = $"{name} {id}", BillingFrequency = "N", TrialFrequency = "N" };

    private void MapResponseByIdentity() =>
        _mapper.Setup(m => m.Map<RoleResponse>(It.IsAny<RoleEntity>()))
               .Returns((RoleEntity r) => new RoleResponse { RoleId = r.RoleId, RoleName = r.RoleName });

    // ---------- GetByPortalAsync ----------
    // MIGRATION: CP1 review (IRoleRepository #paging) - paging is performed server-side by GetByPortalPagedAsync;
    // the service surfaces the repository's page as-is. The mock returns the already-paged result.
    [Fact]
    public async Task GetByPortalAsync_ReturnsPagedMappedItems()
    {
        var page = new List<RoleEntity> { NewRole(1), NewRole(2), NewRole(3) };
        _roleRepo.Setup(r => r.GetByPortalPagedAsync(1, 0, 10))
                 .ReturnsAsync(((IEnumerable<RoleEntity>)page, 3));
        MapResponseByIdentity();

        var result = await _sut.GetByPortalAsync(1, 0, 10);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(3);
        result.Value.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByPortalAsync_AppliesZeroBasedPaging()
    {
        var page = new List<RoleEntity> { NewRole(3), NewRole(4) };
        _roleRepo.Setup(r => r.GetByPortalPagedAsync(1, 1, 2))
                 .ReturnsAsync(((IEnumerable<RoleEntity>)page, 5));
        MapResponseByIdentity();

        var result = await _sut.GetByPortalAsync(1, 1, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.RoleId).Should().Equal(3, 4);
    }

    // ---------- GetByIdAsync ----------
    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsMappedResponse()
    {
        var role = NewRole(2);
        var dto = new RoleResponse { RoleId = 2 };
        _roleRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(role);
        _mapper.Setup(m => m.Map<RoleResponse>(role)).Returns(dto);

        var result = await _sut.GetByIdAsync(1, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsFailureWithExactMessage()
    {
        _roleRepo.Setup(r => r.GetByIdAsync(1, 4)).ReturnsAsync((RoleEntity?)null);

        var result = await _sut.GetByIdAsync(1, 4);

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested role was not found.");
    }

    // ---------- CreateAsync ----------
    [Fact]
    public async Task CreateAsync_WhenNameDuplicate_CaseInsensitive_ReturnsExactFailure()
    {
        var request = new CreateRoleRequest { PortalId = 1, RoleName = "administrators" };
        _roleRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<RoleEntity>
        {
            new() { RoleId = 1, PortalId = 1, RoleName = "Administrators" },
        });

        var result = await _sut.CreateAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("A role with the same name already exists. The role was not added.");
        _roleRepo.Verify(r => r.AddAsync(It.IsAny<RoleEntity>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DefaultsBillingAndTrialFrequencyToN_WhenEmpty()
    {
        var request = new CreateRoleRequest { PortalId = 1, RoleName = "New" };
        var entity = new RoleEntity { RoleId = 7, PortalId = 1, RoleName = "New", BillingFrequency = null, TrialFrequency = null, AutoAssignment = false };
        _roleRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<RoleEntity>());
        _mapper.Setup(m => m.Map<RoleEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<RoleResponse>(entity)).Returns(new RoleResponse { RoleId = 7 });

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.BillingFrequency.Should().Be("N");
        entity.TrialFrequency.Should().Be("N");
        _roleRepo.Verify(r => r.AddAsync(entity), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("M", "W")]
    [InlineData("Y", "D")]
    public async Task CreateAsync_KeepsProvidedFrequencies(string billing, string trial)
    {
        var request = new CreateRoleRequest { PortalId = 1, RoleName = "New" };
        // MIGRATION: CP1 review (RoleService billing/trial defaults, faithful to RoleController/RoleInfo) - the trial
        // frequency is only retained when a service fee exists; a paid role (ServiceFee > 0) therefore keeps both the
        // provided billing AND trial frequency. ServiceFee is set non-zero so this test isolates the "keep provided
        // frequencies" rule rather than the zero-fee reset-to-"N" branch (covered by the defaults test above).
        var entity = new RoleEntity { RoleId = 7, PortalId = 1, RoleName = "New", ServiceFee = 10f, BillingFrequency = billing, TrialFrequency = trial, AutoAssignment = false };
        _roleRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<RoleEntity>());
        _mapper.Setup(m => m.Map<RoleEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<RoleResponse>(entity)).Returns(new RoleResponse { RoleId = 7 });

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.BillingFrequency.Should().Be(billing);
        entity.TrialFrequency.Should().Be(trial);
    }

    [Fact]
    public async Task CreateAsync_AutoAssignsUsers_WhenAutoAssignmentEnabled()
    {
        var request = new CreateRoleRequest { PortalId = 1, RoleName = "AllUsers" };
        var entity = new RoleEntity { RoleId = 7, PortalId = 1, RoleName = "AllUsers", BillingFrequency = "N", TrialFrequency = "N", AutoAssignment = true };
        var users = new List<UserEntity>
        {
            new() { UserId = 10, PortalId = 1, Username = "a" },
            new() { UserId = 11, PortalId = 1, Username = "b" },
        };
        _roleRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<RoleEntity>());
        _mapper.Setup(m => m.Map<RoleEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<RoleResponse>(entity)).Returns(new RoleResponse { RoleId = 7 });
        _userRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(users);

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        users[0].UserRoles.Should().ContainSingle(ur => ur.RoleId == 7 && ur.UserId == 10 && ur.EffectiveDate == null && ur.ExpiryDate == null);
        users[1].UserRoles.Should().ContainSingle(ur => ur.RoleId == 7 && ur.UserId == 11);
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<UserEntity>()), Times.Exactly(2));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAsync_DoesNotAutoAssign_WhenAutoAssignmentDisabled()
    {
        var request = new CreateRoleRequest { PortalId = 1, RoleName = "Manual" };
        var entity = new RoleEntity { RoleId = 7, PortalId = 1, RoleName = "Manual", BillingFrequency = "N", TrialFrequency = "N", AutoAssignment = false };
        _roleRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<RoleEntity>());
        _mapper.Setup(m => m.Map<RoleEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<RoleResponse>(entity)).Returns(new RoleResponse { RoleId = 7 });

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        _userRepo.Verify(r => r.GetByPortalIdAsync(It.IsAny<int>()), Times.Never);
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<UserEntity>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- UpdateAsync ----------
    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsFailure_AndDoesNotSave()
    {
        _roleRepo.Setup(r => r.GetByIdAsync(1, 4)).ReturnsAsync((RoleEntity?)null);

        var result = await _sut.UpdateAsync(1, 4, new UpdateRoleRequest());

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested role was not found.");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_RouteIdIsAuthoritative()
    {
        var entity = new RoleEntity { RoleId = 999, PortalId = 1, RoleName = "X", BillingFrequency = "N", TrialFrequency = "N" };
        var request = new UpdateRoleRequest();
        _roleRepo.Setup(r => r.GetByIdAsync(1, 4)).ReturnsAsync(entity);
        _mapper.Setup(m => m.Map(request, entity)).Returns(entity);
        _mapper.Setup(m => m.Map<RoleResponse>(entity)).Returns(new RoleResponse { RoleId = 4 });

        var result = await _sut.UpdateAsync(1, 4, request);

        result.IsSuccess.Should().BeTrue();
        entity.RoleId.Should().Be(4);
        _roleRepo.Verify(r => r.UpdateAsync(entity), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- DeleteAsync ----------
    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsFailure_AndDoesNothing()
    {
        _roleRepo.Setup(r => r.GetByIdAsync(1, 4)).ReturnsAsync((RoleEntity?)null);

        var result = await _sut.DeleteAsync(1, 4);

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested role was not found.");
        _roleRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_DeletesRole()
    {
        _roleRepo.Setup(r => r.GetByIdAsync(1, 4)).ReturnsAsync(NewRole(4));

        var result = await _sut.DeleteAsync(1, 4);

        result.IsSuccess.Should().BeTrue();
        _roleRepo.Verify(r => r.DeleteAsync(1, 4), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- GetUserRolesAsync (read-only) ----------
    [Fact]
    public async Task GetUserRolesAsync_IsReadOnly_AndReturnsMappedDtos()
    {
        var userRoles = new List<UserRoleEntity>
        {
            new() { UserRoleId = 1, UserId = 5, RoleId = 10 },
            new() { UserRoleId = 2, UserId = 5, RoleId = 11 },
        };
        // MIGRATION: CP1 review (IRoleRepository #1) - portal-scoped (portalId, userId).
        _roleRepo.Setup(r => r.GetUserRolesAsync(1, 5)).ReturnsAsync(userRoles);
        _mapper.Setup(m => m.Map<UserRoleDto>(It.IsAny<UserRoleEntity>()))
               .Returns((UserRoleEntity ur) => new UserRoleDto { UserRoleId = ur.UserRoleId, UserId = ur.UserId, RoleId = ur.RoleId });

        var result = await _sut.GetUserRolesAsync(1, 5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        _roleRepo.Verify(r => r.UpdateAsync(It.IsAny<RoleEntity>()), Times.Never);
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<UserEntity>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
