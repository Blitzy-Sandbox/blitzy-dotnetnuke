using Xunit;
using Moq;
using FluentAssertions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

public class RoleServiceTests
{
    private const int UserId = 100;
    private const int RoleId = 200;
    private const int PortalId = 1;

    private readonly Mock<IRoleRepository> _roleRepo = new(MockBehavior.Strict);
    private readonly Mock<IUserRepository> _userRepo = new(MockBehavior.Strict);
    private readonly Mock<IPortalRepository> _portalRepo = new(MockBehavior.Strict);
    private readonly Mock<IValidator<CreateRoleDto>> _createValidator = new();
    private readonly Mock<IValidator<UpdateRoleDto>> _updateValidator = new();
    private readonly IMapper _mapper = new MapperConfiguration(cfg =>
    {
        cfg.AddProfile<RoleProfile>();
        cfg.AddProfile<UserProfile>();
    }).CreateMapper();

    private UserRole? _capturedUserRole;

    private RoleService CreateSut() =>
        new(_roleRepo.Object, _userRepo.Object, _portalRepo.Object, _mapper, _createValidator.Object, _updateValidator.Object);
    private void SetupValidCreate() =>
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
    private void SetupValidUpdate() =>
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

    // Arrange the AddUserRole pipeline: existing user-roles, target role, and capture of the persisted UserRole.
    private void ArrangeAddUserRole(Role role, IEnumerable<UserRole>? existing = null)
    {
        _roleRepo.Setup(r => r.GetUserRolesAsync(UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing ?? Enumerable.Empty<UserRole>());
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        // MIGRATION: a NEW user->role assignment is INSERTED via the insert-only AddUserRoleAsync (legacy
        // RoleController.AddUserRole -> provider.AddUserRoleToPortal). Capture the persisted record.
        _roleRepo.Setup(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((UserRole ur, CancellationToken _) => { _capturedUserRole = ur; return ur; });
        // MIGRATION: an EXISTING assignment is UPDATED in place via UpdateUserRoleAsync, preserving the legacy
        // add-vs-update split in RoleController.UpdateUserRole (UserRoleId <> -1 -> provider.UpdateUserRole, else
        // provider.AddUserRole). The SAME UserRole instance flows through, so capturing it here lets the
        // existing-record tests assert on the reused record and its recomputed ExpiryDate.
        _roleRepo.Setup(r => r.UpdateUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
                 .Callback((UserRole ur, CancellationToken _) => { _capturedUserRole = ur; })
                 .Returns(Task.CompletedTask);
    }

    private static Role BillingRole(string frequency, int period) => new()
    {
        RoleID = RoleId,
        PortalID = PortalId,
        TrialFrequency = "N", // force billing branch
        BillingFrequency = frequency,
        BillingPeriod = period
    };

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
        _roleRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);
        (await CreateSut().GetByIdAsync(5)).Should().BeNull();
    }

    [Fact]
    public async Task GetByPortalAsync_orders_case_insensitively_by_name()
    {
        // MIGRATION: RoleComparer ordering (RoleController/RoleComparer) - case-insensitive by RoleName
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

    // ---------- Create / Update / Delete ----------

    [Fact]
    public async Task CreateAsync_persists_and_skips_autoassign_when_disabled()
    {
        SetupValidCreate();
        _roleRepo.Setup(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Role r, CancellationToken _) => { r.RoleID = RoleId; return r; });
        var dto = await CreateSut().CreateAsync(new CreateRoleDto { RoleName = "Editors", AutoAssignment = false });
        dto.RoleID.Should().Be(RoleId);
        // AutoAssign must NOT have queried portal users
        _userRepo.Verify(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_triggers_autoassign_when_enabled()
    {
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
        // MIGRATION: legacy AutoAssignUsers swallows exceptions per user (catch Exception)
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
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new ValidationException("bad"));
        Func<Task> act = () => CreateSut().CreateAsync(new CreateRoleDto());
        await act.Should().ThrowAsync<ValidationException>();
        _roleRepo.Verify(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_throws_KeyNotFound_when_missing()
    {
        SetupValidUpdate();
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);
        Func<Task> act = () => CreateSut().UpdateAsync(new UpdateRoleDto { RoleID = RoleId });
        await act.Should().ThrowAsync<KeyNotFoundException>();
        _roleRepo.Verify(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_triggers_autoassign_when_enabled()
    {
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
        // MIGRATION: roles are HARD-deleted (RoleController)
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role { RoleID = RoleId });
        _roleRepo.Setup(r => r.DeleteAsync(RoleId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await CreateSut().DeleteAsync(RoleId);
        _roleRepo.Verify(r => r.DeleteAsync(RoleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_missing_role_is_no_op()
    {
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);
        await CreateSut().DeleteAsync(RoleId);
        _roleRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- AddUserRole expiry algorithm (RoleController.UpdateUserRole L489-557) ----------

    [Fact]
    public async Task AddUserRole_period_minus_one_yields_no_expiry()
    {
        // MIGRATION: Period == Null.NullInteger(-1) short-circuits to no-expiry
        ArrangeAddUserRole(BillingRole("M", -1));
        await CreateSut().AddUserRoleAsync(UserId, RoleId);
        _capturedUserRole!.ExpiryDate.Should().BeNull();
        _capturedUserRole.UserID.Should().Be(UserId);
        _capturedUserRole.RoleID.Should().Be(RoleId);
        _capturedUserRole.EffectiveDate.Should().BeNull();
    }

    [Fact]
    public async Task AddUserRole_frequency_N_yields_null_expiry()
    {
        // "N" -> null / NullDate
        ArrangeAddUserRole(BillingRole("N", 5));
        await CreateSut().AddUserRoleAsync(UserId, RoleId);
        _capturedUserRole!.ExpiryDate.Should().BeNull();
    }

    [Fact]
    public async Task AddUserRole_frequency_O_yields_max_date()
    {
        // "O" -> new DateTime(9999,12,31)
        ArrangeAddUserRole(BillingRole("O", 1));
        await CreateSut().AddUserRoleAsync(UserId, RoleId);
        _capturedUserRole!.ExpiryDate.Should().Be(new DateTime(9999, 12, 31));
    }

    [Fact]
    public async Task AddUserRole_frequency_D_adds_days()
    {
        // "D" -> base.AddDays(Period)
        ArrangeAddUserRole(BillingRole("D", 10));
        var before = DateTime.UtcNow;
        await CreateSut().AddUserRoleAsync(UserId, RoleId);
        var after = DateTime.UtcNow;
        _capturedUserRole!.ExpiryDate.Should().NotBeNull();
        _capturedUserRole.ExpiryDate!.Value.Should().BeOnOrAfter(before.AddDays(10)).And.BeOnOrBefore(after.AddDays(10));
    }

    [Fact]
    public async Task AddUserRole_frequency_W_adds_weeks()
    {
        // "W" -> base.AddDays(Period*7)
        ArrangeAddUserRole(BillingRole("W", 2));
        var before = DateTime.UtcNow;
        await CreateSut().AddUserRoleAsync(UserId, RoleId);
        var after = DateTime.UtcNow;
        _capturedUserRole!.ExpiryDate!.Value.Should().BeOnOrAfter(before.AddDays(14)).And.BeOnOrBefore(after.AddDays(14));
    }

    [Fact]
    public async Task AddUserRole_frequency_M_adds_months()
    {
        // "M" -> base.AddMonths(Period)
        ArrangeAddUserRole(BillingRole("M", 3));
        var before = DateTime.UtcNow;
        await CreateSut().AddUserRoleAsync(UserId, RoleId);
        var after = DateTime.UtcNow;
        _capturedUserRole!.ExpiryDate!.Value.Should().BeOnOrAfter(before.AddMonths(3)).And.BeOnOrBefore(after.AddMonths(3));
    }

    [Fact]
    public async Task AddUserRole_frequency_Y_adds_years()
    {
        // "Y" -> base.AddYears(Period)
        ArrangeAddUserRole(BillingRole("Y", 1));
        var before = DateTime.UtcNow;
        await CreateSut().AddUserRoleAsync(UserId, RoleId);
        var after = DateTime.UtcNow;
        _capturedUserRole!.ExpiryDate!.Value.Should().BeOnOrAfter(before.AddYears(1)).And.BeOnOrBefore(after.AddYears(1));
    }

    [Fact]
    public async Task AddUserRole_selects_trial_when_not_used_and_trial_frequency_set()
    {
        // Trial selected: IsTrialUsed==false && TrialFrequency not empty && != "N"
        var role = new Role
        {
            RoleID = RoleId,
            PortalID = PortalId,
            TrialFrequency = "M",
            TrialPeriod = 1,
            BillingFrequency = "Y",
            BillingPeriod = 5
        };
        ArrangeAddUserRole(role); // no existing -> IsTrialUsed=false
        var before = DateTime.UtcNow;
        await CreateSut().AddUserRoleAsync(UserId, RoleId);
        var after = DateTime.UtcNow;
        // Trial "M"/1 chosen, not billing "Y"/5
        _capturedUserRole!.ExpiryDate!.Value.Should().BeOnOrAfter(before.AddMonths(1)).And.BeOnOrBefore(after.AddMonths(1));
    }

    [Fact]
    public async Task AddUserRole_selects_billing_when_trial_already_used_and_reuses_existing_record()
    {
        // IsTrialUsed==true -> billing branch; existing UserRole is updated in place (same instance)
        var existing = new UserRole { UserRoleID = 777, UserID = UserId, RoleID = RoleId, IsTrialUsed = true, ExpiryDate = null, EffectiveDate = null };
        var role = new Role
        {
            RoleID = RoleId,
            PortalID = PortalId,
            TrialFrequency = "M",
            TrialPeriod = 1,
            BillingFrequency = "Y",
            BillingPeriod = 1
        };
        ArrangeAddUserRole(role, new[] { existing });
        var before = DateTime.UtcNow;
        await CreateSut().AddUserRoleAsync(UserId, RoleId);
        var after = DateTime.UtcNow;
        _capturedUserRole!.UserRoleID.Should().Be(777); // reused existing record
        _capturedUserRole.ExpiryDate!.Value.Should().BeOnOrAfter(before.AddYears(1)).And.BeOnOrBefore(after.AddYears(1));
    }

    [Fact]
    public async Task AddUserRole_resets_past_expiry_to_now_before_applying_frequency()
    {
        // Date guard: if ExpiryDate < Now then ExpiryDate = Now, then add Period
        var existing = new UserRole { UserRoleID = 888, UserID = UserId, RoleID = RoleId, IsTrialUsed = true, ExpiryDate = DateTime.UtcNow.AddDays(-100) };
        ArrangeAddUserRole(BillingRole("D", 5), new[] { existing });
        var before = DateTime.UtcNow;
        await CreateSut().AddUserRoleAsync(UserId, RoleId);
        var after = DateTime.UtcNow;
        _capturedUserRole!.UserRoleID.Should().Be(888);
        // past expiry reset to now, then +5 days
        _capturedUserRole.ExpiryDate!.Value.Should().BeOnOrAfter(before.AddDays(5)).And.BeOnOrBefore(after.AddDays(5));
    }

    // ---------- AddUserRole explicit-date (admin) path — DEV-069 / Finding 5 ----------

    [Fact]
    public async Task AddUserRole_explicit_dates_insert_new_uses_dates_directly_and_bypasses_schedule()
    {
        // MIGRATION (DEV-069): supplying EffectiveDate/ExpiryDate marks the legacy SecurityRoles ADMIN
        // workflow (RoleController.AddUserRole with explicit dates); the dates are used VERBATIM and the
        // trial/billing schedule is NOT consulted. BillingRole("Y", 5) would compute now+5y — proving the
        // bypass by asserting the captured ExpiryDate is the operator value, not the computed one.
        ArrangeAddUserRole(BillingRole("Y", 5));
        var effective = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expiry = new DateTime(2031, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        await CreateSut().AddUserRoleAsync(UserId, RoleId, effective, expiry);

        _capturedUserRole!.EffectiveDate.Should().Be(effective);
        _capturedUserRole.ExpiryDate.Should().Be(expiry);
        _capturedUserRole.UserID.Should().Be(UserId);
        _capturedUserRole.RoleID.Should().Be(RoleId);
        // The schedule is never consulted on the admin path: GetByIdAsync must not be invoked.
        _roleRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _roleRepo.Verify(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddUserRole_explicit_expiry_only_inserts_with_that_expiry_and_null_effective()
    {
        // Only ExpiryDate supplied -> still the admin direct path; EffectiveDate stays null ("effective now").
        ArrangeAddUserRole(BillingRole("Y", 5));
        var expiry = new DateTime(2032, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        await CreateSut().AddUserRoleAsync(UserId, RoleId, requestedExpiryDate: expiry);

        _capturedUserRole!.ExpiryDate.Should().Be(expiry);
        _capturedUserRole.EffectiveDate.Should().BeNull();
        _roleRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddUserRole_explicit_dates_update_existing_overwrites_both_dates_in_place()
    {
        // MIGRATION (DEV-069): an EXISTING membership on the admin path is UPDATED in place with the supplied
        // dates (legacy AddUserRole -> provider.UpdateUserRole(UserRoleId, EffectiveDate, ExpiryDate)). The
        // existing record's prior dates are OVERWRITTEN (not merged) and the schedule is not consulted.
        var existing = new UserRole { UserRoleID = 555, UserID = UserId, RoleID = RoleId, IsTrialUsed = true, EffectiveDate = null, ExpiryDate = null };
        ArrangeAddUserRole(BillingRole("Y", 5), new[] { existing });
        var effective = new DateTime(2029, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var expiry = new DateTime(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        await CreateSut().AddUserRoleAsync(UserId, RoleId, effective, expiry);

        _capturedUserRole!.UserRoleID.Should().Be(555); // reused existing record
        _capturedUserRole.EffectiveDate.Should().Be(effective);
        _capturedUserRole.ExpiryDate.Should().Be(expiry);
        _roleRepo.Verify(r => r.UpdateUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Once);
        _roleRepo.Verify(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddUserRole_notify_true_is_accepted_as_noop_and_still_assigns()
    {
        // MIGRATION (DEV-069): notify is a DOCUMENTED NO-OP (no mail subsystem in scope). It is accepted on the
        // contract and has no observable effect — the assignment still occurs with the supplied expiry.
        ArrangeAddUserRole(BillingRole("Y", 5));
        var expiry = new DateTime(2033, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await CreateSut().AddUserRoleAsync(UserId, RoleId, requestedExpiryDate: expiry, notify: true);

        _capturedUserRole!.ExpiryDate.Should().Be(expiry);
        _roleRepo.Verify(r => r.AddUserRoleAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- User-role queries ----------

    [Fact]
    public async Task GetUserRolesAsync_maps_non_null_role_navs()
    {
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
    public async Task GetUsersInRoleAsync_maps_users()
    {
        _roleRepo.Setup(r => r.GetUsersInRoleAsync(RoleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<User> { new() { UserID = 1 }, new() { UserID = 2 } });
        (await CreateSut().GetUsersInRoleAsync(RoleId)).Should().HaveCount(2);
    }

    // ---------- RemoveUserRole guard (CanRemoveUserFromRole L741-768) ----------

    [Fact]
    public async Task RemoveUserRole_succeeds_for_regular_role()
    {
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role { RoleID = RoleId, PortalID = PortalId });
        _portalRepo.Setup(r => r.GetByIdAsync(PortalId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Portal { PortalID = PortalId, AdministratorId = 9, AdministratorRoleId = 8, RegisteredRoleId = 7 });
        _roleRepo.Setup(r => r.RemoveUserRoleAsync(UserId, RoleId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await CreateSut().RemoveUserRoleAsync(UserId, RoleId);
        _roleRepo.Verify(r => r.RemoveUserRoleAsync(UserId, RoleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveUserRole_throws_when_removing_admin_from_administrator_role()
    {
        // MIGRATION: cannot remove the administrator from the administrator role
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role { RoleID = RoleId, PortalID = PortalId });
        _portalRepo.Setup(r => r.GetByIdAsync(PortalId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Portal { PortalID = PortalId, AdministratorId = UserId, AdministratorRoleId = RoleId, RegisteredRoleId = 7 });
        Func<Task> act = () => CreateSut().RemoveUserRoleAsync(UserId, RoleId);
        await act.Should().ThrowAsync<BusinessConflictException>();
        _roleRepo.Verify(r => r.RemoveUserRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveUserRole_throws_for_registered_users_role()
    {
        // MIGRATION: cannot remove a user from the Registered Users role
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(new Role { RoleID = RoleId, PortalID = PortalId });
        _portalRepo.Setup(r => r.GetByIdAsync(PortalId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Portal { PortalID = PortalId, AdministratorId = 9, AdministratorRoleId = 8, RegisteredRoleId = RoleId });
        Func<Task> act = () => CreateSut().RemoveUserRoleAsync(UserId, RoleId);
        await act.Should().ThrowAsync<BusinessConflictException>();
        _roleRepo.Verify(r => r.RemoveUserRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveUserRole_missing_role_is_no_op()
    {
        _roleRepo.Setup(r => r.GetByIdAsync(RoleId, It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);
        await CreateSut().RemoveUserRoleAsync(UserId, RoleId);
        _roleRepo.Verify(r => r.RemoveUserRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
