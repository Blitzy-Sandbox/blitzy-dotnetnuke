using Xunit;
using Moq;
using FluentAssertions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

// MIGRATION: The MOST detailed unit-test class in the suite. It asserts verbatim behavioral
// equivalence between DnnMigration.Application.Services.RoleService and the legacy
// Library/Components/Security/Roles/RoleController.vb (+ RoleComparer.vb / UserRoleInfo.vb), and
// validates Gate 2 (`dotnet test --configuration Release` -> 100% pass).
//
// Collaborators are mocked with Moq (the three repository ports are STRICT so any un-arranged call
// fails the test); IMapper is a REAL AutoMapper instance built from the production RoleProfile,
// UserProfile and UserRoleProfile so entity<->DTO projection is exercised end-to-end rather than
// stubbed. The two FluentValidation validators are loose mocks: RoleService invokes them through the
// ValidateAndThrowAsync extension, which dispatches to IValidator.ValidateAsync(IValidationContext,
// CancellationToken) — the overload the setups target.
//
// MIGRATION (role-assignment contract, MIGRATION_NOTES.md §6.2): the admin assignment path
// RoleService.AddUserRoleAsync(AssignUserRoleDto) ports RoleController.AddUserRole(PortalID, UserId,
// RoleId, EffectiveDate, ExpiryDate) [RoleController.vb:L295-317] — a plain UPSERT of the
// admin-supplied effective/expiry window. It deliberately does NOT reproduce the self-service
// RoleController.UpdateUserRole(Cancel) trial/billing computed-window algorithm [L489-557], which is
// out of scope for the admin API (an earlier revision wrongly ported it; corrected per the CP2
// role-assignment-contract finding). These tests therefore exercise the corrected UPSERT contract:
// create-when-absent, update-existing-in-place, admin window persisted verbatim, FirstOrDefault by
// RoleID — together with every other ported branch (RoleComparer ordering, the auto-assign
// swallow-all loop, HARD-delete, and all three CanRemoveUserFromRole guard cases).
public class RoleServiceTests
{
    private const int UserId = 100;
    private const int RoleId = 200;
    private const int PortalId = 1;

    // Strict so the SUT's exact data-access interactions are asserted (an un-arranged call throws).
    private readonly Mock<IRoleRepository> _roleRepo = new(MockBehavior.Strict);
    private readonly Mock<IUserRepository> _userRepo = new(MockBehavior.Strict);
    private readonly Mock<IPortalRepository> _portalRepo = new(MockBehavior.Strict);

    // Loose: reached only through ValidateAndThrowAsync; explicit setups drive the success/throw paths.
    private readonly Mock<IValidator<CreateRoleDto>> _createValidator = new();
    private readonly Mock<IValidator<UpdateRoleDto>> _updateValidator = new();

    // Real mapper. UserRoleProfile is REQUIRED in addition to RoleProfile/UserProfile because the
    // corrected AddUserRoleAsync/GetUserRoleAssignmentsAsync return a mapped UserRoleAssignmentDto.
    // AutoMapper 15.x requires an ILoggerFactory on the MapperConfiguration ctor; NullLoggerFactory is
    // used (matching the sibling profile tests) since these unit tests assert behavior, not logging.
    private readonly IMapper _mapper = new MapperConfiguration(cfg =>
    {
        cfg.AddProfile<RoleProfile>();
        cfg.AddProfile<UserProfile>();
        cfg.AddProfile<UserRoleProfile>();
    }, NullLoggerFactory.Instance).CreateMapper();

    // Captures the UserRole the service hands to the repository (insert or update path).
    private UserRole? _capturedUserRole;

    private RoleService CreateSut() =>
        new(_roleRepo.Object, _userRepo.Object, _portalRepo.Object, _mapper, _createValidator.Object, _updateValidator.Object);

    private void SetupValidCreate() =>
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

    private void SetupValidUpdate() =>
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

    // Arrange the admin UPSERT INSERT path: no matching existing assignment; capture the new UserRole.
    private void ArrangeInsertUserRole(IEnumerable<UserRole>? existing = null)
    {
        _roleRepo.Setup(r => r.GetUserRolesAsync(UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing ?? Enumerable.Empty<UserRole>());
        _roleRepo.Setup(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((UserRole ur, CancellationToken _) => { _capturedUserRole = ur; return ur; });
    }

    // Arrange the admin UPSERT UPDATE path: a matching existing assignment is updated in place.
    private void ArrangeUpdateUserRole(IEnumerable<UserRole> existing)
    {
        _roleRepo.Setup(r => r.GetUserRolesAsync(UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);
        _roleRepo.Setup(r => r.UpdateUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((UserRole ur, CancellationToken _) => { _capturedUserRole = ur; return ur; });
    }

    // ---------- Read paths ----------

    [Fact]
    public async Task GetByIdAsync_maps_when_found()
    {
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new Role { RoleID = RoleId, RoleName = "Admins" });

        (await CreateSut().GetByIdAsync(RoleId))!.RoleName.Should().Be("Admins");
    }

    [Fact]
    public async Task GetByIdAsync_null_when_missing()
    {
        // MIGRATION: legacy GetRole returned Nothing for an unknown id; null projects through to the caller.
        _roleRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);

        (await CreateSut().GetByIdAsync(5)).Should().BeNull();
    }

    [Fact]
    public async Task GetByPortalAsync_orders_case_insensitively_by_name()
    {
        // MIGRATION: RoleComparer [RoleComparer.vb:L55-57] ordered by RoleName via CaseInsensitiveComparer
        // (CurrentCulture); preserved as OrderBy(RoleName ?? "", StringComparer.CurrentCultureIgnoreCase).
        _roleRepo.Setup(r => r.GetByPortalAsync(PortalId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Role>
                 {
                     new() { RoleID = 1, RoleName = "Zebra" },
                     new() { RoleID = 2, RoleName = "alpha" },
                     new() { RoleID = 3, RoleName = "Mango" }
                 });

        var result = (await CreateSut().GetByPortalAsync(PortalId)).ToList();

        result.Select(r => r.RoleName).Should().ContainInOrder("alpha", "Mango", "Zebra");
    }

    [Fact]
    public async Task GetByPortalAsync_handles_null_role_name_without_throwing()
    {
        // MIGRATION: the ordering key coalesces a null RoleName to "" (RoleName ?? string.Empty) so a role
        // with no name sorts first instead of throwing inside the comparer.
        _roleRepo.Setup(r => r.GetByPortalAsync(PortalId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Role>
                 {
                     new() { RoleID = 1, RoleName = "beta" },
                     new() { RoleID = 2, RoleName = null },
                     new() { RoleID = 3, RoleName = "Alpha" }
                 });

        var result = (await CreateSut().GetByPortalAsync(PortalId)).ToList();

        result.Should().HaveCount(3);
        result[0].RoleName.Should().BeNull();
        result.Select(r => r.RoleName).Should().ContainInOrder(null, "Alpha", "beta");
    }

    // ---------- Create / Update / Delete ----------

    [Fact]
    public async Task CreateAsync_persists_and_skips_autoassign_when_disabled()
    {
        SetupValidCreate();
        _roleRepo.Setup(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Role r, CancellationToken _) => { r.RoleID = RoleId; return r; });

        var dto = await CreateSut().CreateAsync(new CreateRoleDto { RoleName = "Editors", AutoAssignment = false });

        dto.RoleID.Should().Be(RoleId);
        // MIGRATION: AddRole only invoked AutoAssignUsers when AutoAssignment was set; here it must NOT query portal users.
        _userRepo.Verify(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_triggers_autoassign_when_enabled()
    {
        // MIGRATION: AddRole called AutoAssignUsers [RoleController.vb:L106] when AutoAssignment is enabled,
        // assigning every portal user to the new role.
        SetupValidCreate();
        _roleRepo.Setup(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Role r, CancellationToken _) => { r.RoleID = RoleId; return r; });
        _userRepo.Setup(r => r.GetByPortalAsync(PortalId, 0, int.MaxValue, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((new List<User> { new() { UserID = 1 }, new() { UserID = 2 } }, 2));
        _roleRepo.Setup(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new UserRole());

        await CreateSut().CreateAsync(new CreateRoleDto { RoleName = "Members", PortalID = PortalId, AutoAssignment = true });

        _roleRepo.Verify(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAsync_autoassign_swallows_exceptions_and_continues_loop()
    {
        // MIGRATION: legacy AutoAssignUsers [RoleController.vb:L68-77] wrapped each AddUserRole in
        // Try/Catch ex As Exception and swallowed the failure (e.g. "user already belongs to role") so the
        // loop continued. Preserved verbatim: the first user throws, the loop still processes the second.
        SetupValidCreate();
        _roleRepo.Setup(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Role r, CancellationToken _) => { r.RoleID = RoleId; return r; });
        _userRepo.Setup(r => r.GetByPortalAsync(PortalId, 0, int.MaxValue, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((new List<User> { new() { UserID = 1 }, new() { UserID = 2 } }, 2));
        _roleRepo.SetupSequence(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new Exception("boom"))
                 .ReturnsAsync(new UserRole());

        Func<Task> act = () => CreateSut().CreateAsync(new CreateRoleDto { PortalID = PortalId, AutoAssignment = true });

        await act.Should().NotThrowAsync();
        _roleRepo.Verify(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAsync_invalid_throws_and_skips_persist()
    {
        // MIGRATION: FluentValidation replaces the legacy admin-screen field validation; ValidateAndThrowAsync
        // surfaces a ValidationException before any persistence work occurs.
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new ValidationException("bad"));

        Func<Task> act = () => CreateSut().CreateAsync(new CreateRoleDto());

        await act.Should().ThrowAsync<ValidationException>();
        _roleRepo.Verify(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_throws_KeyNotFound_when_missing()
    {
        // MIGRATION: updating a non-existent role surfaces KeyNotFoundException (the API maps it to 404);
        // the repository update is never attempted.
        SetupValidUpdate();
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);

        Func<Task> act = () => CreateSut().UpdateAsync(new UpdateRoleDto { RoleID = RoleId });

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _roleRepo.Verify(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_updates_and_returns_mapped_dto_without_autoassign()
    {
        // MIGRATION: UpdateRole loaded the role, applied the edited fields, and persisted; AutoAssignUsers
        // runs only when AutoAssignment is set, so a disabled update must NOT query portal users.
        SetupValidUpdate();
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new Role { RoleID = RoleId, PortalID = PortalId, RoleName = "Old" });
        _roleRepo.Setup(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var dto = await CreateSut().UpdateAsync(new UpdateRoleDto { RoleID = RoleId, PortalID = PortalId, RoleName = "Renamed", AutoAssignment = false });

        dto.RoleName.Should().Be("Renamed");
        _roleRepo.Verify(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Once);
        _userRepo.Verify(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_triggers_autoassign_when_enabled()
    {
        // MIGRATION: UpdateRole called AutoAssignUsers [RoleController.vb:L256] when AutoAssignment is enabled.
        SetupValidUpdate();
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new Role { RoleID = RoleId, PortalID = PortalId });
        _roleRepo.Setup(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.GetByPortalAsync(PortalId, 0, int.MaxValue, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((new List<User> { new() { UserID = 1 } }, 1));
        _roleRepo.Setup(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(new UserRole());

        await CreateSut().UpdateAsync(new UpdateRoleDto { RoleID = RoleId, PortalID = PortalId, AutoAssignment = true });

        _roleRepo.Verify(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_hard_deletes_existing_role()
    {
        // MIGRATION: roles are HARD-deleted with a transactional user-role cascade (AAP §0.3.3); unlike
        // Module/User/Tab there is no IsDeleted soft-delete flag for a role.
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role { RoleID = RoleId });
        _roleRepo.Setup(r => r.DeleteAsync(RoleId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().DeleteAsync(RoleId);

        _roleRepo.Verify(r => r.DeleteAsync(RoleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_missing_role_is_no_op()
    {
        // MIGRATION: legacy DeleteRole no-ops when the role is not found [RoleController.vb:L125-133].
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);

        await CreateSut().DeleteAsync(RoleId);

        _roleRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Admin AddUserRole UPSERT (RoleController.AddUserRole EffectiveDate/ExpiryDate, L295-317) ----------

    [Fact]
    public async Task AddUserRole_inserts_new_assignment_when_none_exists()
    {
        // MIGRATION: legacy "If objUserRole Is Nothing" branch [RoleController.vb:L300-307] inserted a new
        // assignment (MembershipProvider.AddUserToRole). With no existing user-role, the service INSERTS via
        // AddUserRoleAsync and never calls the update path.
        ArrangeInsertUserRole();

        var dto = await CreateSut().AddUserRoleAsync(new AssignUserRoleDto { UserID = UserId, RoleID = RoleId });

        var captured = _capturedUserRole!;
        captured.UserID.Should().Be(UserId);
        captured.RoleID.Should().Be(RoleId);
        dto.UserID.Should().Be(UserId);
        dto.RoleID.Should().Be(RoleId);
        _roleRepo.Verify(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Once);
        _roleRepo.Verify(r => r.UpdateUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddUserRole_persists_admin_supplied_window_verbatim()
    {
        // MIGRATION: the admin path persists the SUPPLIED effective/expiry window unchanged — it does NOT
        // compute a trial/billing window (that is the out-of-scope self-service UpdateUserRole(Cancel) path,
        // L489-557; corrected per MIGRATION_NOTES.md §6.2). The dates flow straight through to persistence
        // and onto the returned DTO.
        var effective = new DateTime(2030, 1, 1);
        var expiry = new DateTime(2031, 6, 15);
        ArrangeInsertUserRole();

        var dto = await CreateSut().AddUserRoleAsync(new AssignUserRoleDto
        {
            UserID = UserId,
            RoleID = RoleId,
            EffectiveDate = effective,
            ExpiryDate = expiry
        });

        var captured = _capturedUserRole!;
        captured.EffectiveDate.Should().Be(effective);
        captured.ExpiryDate.Should().Be(expiry);
        dto.EffectiveDate.Should().Be(effective);
        dto.ExpiryDate.Should().Be(expiry);
    }

    [Fact]
    public async Task AddUserRole_persists_null_dates_as_null()
    {
        // MIGRATION: a blank admin date textbox became Null.NullDate in the legacy SecurityRoles.ascx.vb
        // "Add Role To User" screen; modeled here as null DateTime? that is persisted as null (unbounded window).
        ArrangeInsertUserRole();

        await CreateSut().AddUserRoleAsync(new AssignUserRoleDto { UserID = UserId, RoleID = RoleId, EffectiveDate = null, ExpiryDate = null });

        var captured = _capturedUserRole!;
        captured.EffectiveDate.Should().BeNull();
        captured.ExpiryDate.Should().BeNull();
    }

    [Fact]
    public async Task AddUserRole_updates_existing_assignment_in_place_when_present()
    {
        // MIGRATION: legacy "Else" branch [RoleController.vb:L308-313] updated the existing assignment's
        // window (MembershipProvider.UpdateUserRole). The service reuses the SAME existing instance (its
        // server-assigned UserRoleID is preserved), rewrites the effective/expiry dates, and routes to
        // UpdateUserRoleAsync — never the insert path.
        var existing = new UserRole { UserRoleID = 777, UserID = UserId, RoleID = RoleId, EffectiveDate = null, ExpiryDate = null };
        ArrangeUpdateUserRole(new[] { existing });
        var effective = new DateTime(2030, 1, 1);
        var expiry = new DateTime(2031, 1, 1);

        var dto = await CreateSut().AddUserRoleAsync(new AssignUserRoleDto
        {
            UserID = UserId,
            RoleID = RoleId,
            EffectiveDate = effective,
            ExpiryDate = expiry
        });

        var captured = _capturedUserRole!;
        captured.Should().BeSameAs(existing);   // updated in place, not re-created
        captured.UserRoleID.Should().Be(777);
        captured.EffectiveDate.Should().Be(effective);
        captured.ExpiryDate.Should().Be(expiry);
        dto.UserRoleID.Should().Be(777);
        _roleRepo.Verify(r => r.UpdateUserRoleAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _roleRepo.Verify(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddUserRole_ignores_assignment_for_a_different_role_and_inserts()
    {
        // MIGRATION: the existing-assignment lookup is FirstOrDefault(ur => ur.RoleID == roleId)
        // [RoleController.vb:L299 GetUserRole]; an assignment for a DIFFERENT role must be ignored, so the
        // target role takes the INSERT path with a brand-new (identity-unassigned) row.
        var otherRole = new UserRole { UserRoleID = 1, UserID = UserId, RoleID = RoleId + 1 };
        ArrangeInsertUserRole(new[] { otherRole });

        await CreateSut().AddUserRoleAsync(new AssignUserRoleDto { UserID = UserId, RoleID = RoleId });

        var captured = _capturedUserRole!;
        captured.RoleID.Should().Be(RoleId);
        captured.UserRoleID.Should().Be(0); // new row, identity not yet assigned
        _roleRepo.Verify(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Once);
        _roleRepo.Verify(r => r.UpdateUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- User-role queries ----------

    [Fact]
    public async Task GetUserRolesAsync_maps_non_null_role_navs()
    {
        // MIGRATION: GetUserRoles projects each join row's Role definition; a join row whose Role navigation
        // is null (orphaned/unresolved) is filtered out rather than producing a null RoleDto.
        _roleRepo.Setup(r => r.GetUserRolesAsync(UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<UserRole>
                 {
                     new() { Role = new Role { RoleID = 1, RoleName = "A" } },
                     new() { Role = null }
                 });

        var result = (await CreateSut().GetUserRolesAsync(UserId)).ToList();

        result.Should().ContainSingle();
        result[0].RoleName.Should().Be("A");
    }

    [Fact]
    public async Task GetUserRoleAssignmentsAsync_maps_assignment_metadata()
    {
        // MIGRATION: read side of the round-trip — the per-assignment membership metadata
        // (UserRoleID/effective/expiry/IsTrialUsed/Subscribed) the legacy admin screen exposed, projected
        // via UserRoleProfile onto UserRoleAssignmentDto.
        _roleRepo.Setup(r => r.GetUserRolesAsync(UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<UserRole>
                 {
                     new()
                     {
                         UserRoleID = 1, UserID = UserId, RoleID = RoleId,
                         IsTrialUsed = true, Subscribed = true,
                         EffectiveDate = new DateTime(2030, 1, 1), ExpiryDate = new DateTime(2031, 1, 1)
                     },
                     new() { UserRoleID = 2, UserID = UserId, RoleID = RoleId + 1 }
                 });

        var result = (await CreateSut().GetUserRoleAssignmentsAsync(UserId)).ToList();

        result.Should().HaveCount(2);
        result[0].UserRoleID.Should().Be(1);
        result[0].IsTrialUsed.Should().BeTrue();
        result[0].Subscribed.Should().BeTrue();
        result[0].EffectiveDate.Should().Be(new DateTime(2030, 1, 1));
        result[0].ExpiryDate.Should().Be(new DateTime(2031, 1, 1));
    }

    [Fact]
    public async Task GetUsersInRoleAsync_maps_users()
    {
        // MIGRATION: GetUsersInRole projects the role's membership (legacy GetUserRolesByRoleName) to UserDto.
        _roleRepo.Setup(r => r.GetUsersInRoleAsync(RoleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<User> { new() { UserID = 1 }, new() { UserID = 2 } });

        (await CreateSut().GetUsersInRoleAsync(RoleId)).Should().HaveCount(2);
    }

    // ---------- RemoveUserRole guard (CanRemoveUserFromRole, RoleController.vb:L764-768) ----------

    [Fact]
    public async Task RemoveUserRole_succeeds_for_regular_role()
    {
        // MIGRATION: a role that is neither the administrator role (for the administrator) nor the
        // registered-users role may be removed (CanRemoveUserFromRole returns True).
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role { RoleID = RoleId, PortalID = PortalId });
        _portalRepo.Setup(r => r.GetByIdAsync(PortalId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Portal { PortalID = PortalId, AdministratorId = 9, AdministratorRoleId = 8, RegisteredRoleId = 7 });
        _roleRepo.Setup(r => r.RemoveUserRoleAsync(UserId, RoleId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().RemoveUserRoleAsync(UserId, RoleId);

        _roleRepo.Verify(r => r.RemoveUserRoleAsync(UserId, RoleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveUserRole_throws_when_removing_administrator_from_administrator_role()
    {
        // MIGRATION: CanRemoveUserFromRole [RoleController.vb:L768] = Not ((AdministratorId = UserId And
        // AdministratorRoleId = RoleId) Or ...). The administrator cannot be removed from the administrator
        // role; the legacy False return is modernized to a thrown InvalidOperationException.
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role { RoleID = RoleId, PortalID = PortalId });
        _portalRepo.Setup(r => r.GetByIdAsync(PortalId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Portal { PortalID = PortalId, AdministratorId = UserId, AdministratorRoleId = RoleId, RegisteredRoleId = 7 });

        Func<Task> act = () => CreateSut().RemoveUserRoleAsync(UserId, RoleId);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _roleRepo.Verify(r => r.RemoveUserRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveUserRole_throws_for_registered_users_role()
    {
        // MIGRATION: CanRemoveUserFromRole [RoleController.vb:L768] = Not (... Or RegisteredRoleId = RoleId).
        // No user may be removed from the Registered Users role.
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role { RoleID = RoleId, PortalID = PortalId });
        _portalRepo.Setup(r => r.GetByIdAsync(PortalId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Portal { PortalID = PortalId, AdministratorId = 9, AdministratorRoleId = 8, RegisteredRoleId = RoleId });

        Func<Task> act = () => CreateSut().RemoveUserRoleAsync(UserId, RoleId);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _roleRepo.Verify(r => r.RemoveUserRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveUserRole_missing_role_is_no_op()
    {
        // MIGRATION: nothing to remove if the role does not exist (idempotent); the portal guard is not even consulted.
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);

        await CreateSut().RemoveUserRoleAsync(UserId, RoleId);

        _roleRepo.Verify(r => r.RemoveUserRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
