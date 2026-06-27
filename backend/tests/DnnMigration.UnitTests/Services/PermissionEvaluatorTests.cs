using DnnMigration.Application.Security;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests.Services;

// MIGRATION (CP-FINAL review - Authorization Parity): verifies the reusable PermissionEvaluator reproduces the
// legacy PortalSecurity helper semantics (IsInRole / IsInRoles / HasNecessaryPermission, PortalSecurity.vb
// L103-136 / L517-549) and ModulePermissionController.HasModulePermission EXACTLY, including the legacy quirks
// preserved per AAP 0.7.2 (IsInRole ignores IsSuperUser; HasModulePermission ignores AllowAccess).
public sealed class PermissionEvaluatorTests
{
    private readonly PermissionEvaluator _sut = new();

    private static SecurityContext Context(
        bool isSuperUser = false,
        bool isAuthenticated = true,
        int? userId = 5,
        params string[] roles) =>
        new()
        {
            Roles = roles,
            IsSuperUser = isSuperUser,
            IsAuthenticated = isAuthenticated,
            UserId = userId
        };

    // ----- IsInRole (PortalSecurity.vb L103-113) ------------------------------------------------------------

    [Fact]
    public void IsInRole_returns_true_for_a_direct_member()
    {
        _sut.IsInRole(Context(roles: "Administrators"), "Administrators").Should().BeTrue();
    }

    [Fact]
    public void IsInRole_returns_false_for_a_non_member()
    {
        _sut.IsInRole(Context(roles: "Subscribers"), "Administrators").Should().BeFalse();
    }

    [Fact]
    public void IsInRole_returns_true_for_unauthenticated_users_role_when_not_authenticated()
    {
        _sut.IsInRole(Context(isAuthenticated: false, userId: null), "Unauthenticated Users").Should().BeTrue();
    }

    [Fact]
    public void IsInRole_unauthenticated_users_role_falls_through_to_membership_when_authenticated()
    {
        // Authenticated principal: the special-role short-circuit does NOT apply, so it depends on membership.
        _sut.IsInRole(Context(isAuthenticated: true, roles: "Registered Users"), "Unauthenticated Users")
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsInRole_returns_false_for_null_or_empty_role(string? role)
    {
        _sut.IsInRole(Context(roles: "Administrators"), role).Should().BeFalse();
    }

    [Fact]
    public void IsInRole_ignores_IsSuperUser_preserving_legacy_behavior()
    {
        // MIGRATION fidelity: legacy IsInRole has NO SuperUser short-circuit (unlike IsInRoles). A SuperUser who is
        // not a direct member of the specific role returns false.
        _sut.IsInRole(Context(isSuperUser: true, roles: "Subscribers"), "Administrators").Should().BeFalse();
    }

    // ----- IsInRoles (PortalSecurity.vb L115-136) -----------------------------------------------------------

    [Fact]
    public void IsInRoles_returns_true_for_superuser_even_with_empty_string()
    {
        _sut.IsInRoles(Context(isSuperUser: true), string.Empty).Should().BeTrue();
    }

    [Fact]
    public void IsInRoles_returns_false_for_superuser_when_roles_is_null()
    {
        // Verbatim legacy: null roles skips the loop entirely and returns false, even for a SuperUser.
        _sut.IsInRoles(Context(isSuperUser: true), null).Should().BeFalse();
    }

    [Fact]
    public void IsInRoles_returns_true_for_all_users_role_regardless_of_membership()
    {
        _sut.IsInRoles(Context(roles: "Subscribers"), "All Users").Should().BeTrue();
    }

    [Fact]
    public void IsInRoles_returns_true_for_unauthenticated_users_role_when_not_authenticated()
    {
        _sut.IsInRoles(Context(isAuthenticated: false, userId: null), "Unauthenticated Users").Should().BeTrue();
    }

    [Fact]
    public void IsInRoles_returns_true_when_member_of_one_role_in_a_semicolon_list()
    {
        _sut.IsInRoles(Context(roles: "Editors"), "Administrators;Editors;Moderators").Should().BeTrue();
    }

    [Fact]
    public void IsInRoles_returns_false_when_member_of_none_in_the_list()
    {
        _sut.IsInRoles(Context(roles: "Subscribers"), "Administrators;Editors").Should().BeFalse();
    }

    [Fact]
    public void IsInRoles_returns_false_for_null_roles()
    {
        _sut.IsInRoles(Context(roles: "Administrators"), null).Should().BeFalse();
    }

    // ----- HasModulePermission (ModulePermissionController.vb) ----------------------------------------------

    [Fact]
    public void HasModulePermission_grants_by_role_when_user_id_is_null()
    {
        var permissions = new[]
        {
            new ModulePermission { PermissionKey = "EDIT", UserId = null, RoleName = "Editors" }
        };
        _sut.HasModulePermission(Context(roles: "Editors"), permissions, "EDIT").Should().BeTrue();
    }

    [Fact]
    public void HasModulePermission_grants_by_direct_user_when_user_id_matches()
    {
        var permissions = new[]
        {
            new ModulePermission { PermissionKey = "EDIT", UserId = 5, RoleName = null }
        };
        _sut.HasModulePermission(Context(userId: 5), permissions, "EDIT").Should().BeTrue();
    }

    [Fact]
    public void HasModulePermission_denies_when_direct_user_does_not_match()
    {
        var permissions = new[]
        {
            new ModulePermission { PermissionKey = "EDIT", UserId = 99, RoleName = null }
        };
        _sut.HasModulePermission(Context(userId: 5), permissions, "EDIT").Should().BeFalse();
    }

    [Fact]
    public void HasModulePermission_ignores_non_matching_permission_keys()
    {
        var permissions = new[]
        {
            new ModulePermission { PermissionKey = "VIEW", UserId = null, RoleName = "Editors" }
        };
        _sut.HasModulePermission(Context(roles: "Editors"), permissions, "EDIT").Should().BeFalse();
    }

    [Fact]
    public void HasModulePermission_returns_false_for_null_collection()
    {
        _sut.HasModulePermission(Context(roles: "Editors"), null, "EDIT").Should().BeFalse();
    }

    [Fact]
    public void HasModulePermission_ignores_AllowAccess_preserving_legacy_behavior()
    {
        // MIGRATION fidelity: the legacy overload does not consult AllowAccess, so a matching key + role membership
        // grants access even though this row is a deny (AllowAccess = false).
        var permissions = new[]
        {
            new ModulePermission { PermissionKey = "EDIT", UserId = null, RoleName = "Editors", AllowAccess = false }
        };
        _sut.HasModulePermission(Context(roles: "Editors"), permissions, "EDIT").Should().BeTrue();
    }

    // ----- HasTabPermission ---------------------------------------------------------------------------------

    [Fact]
    public void HasTabPermission_grants_by_role_and_by_direct_user()
    {
        var byRole = new[] { new TabPermission { PermissionKey = "EDIT", UserId = null, RoleName = "Editors" } };
        _sut.HasTabPermission(Context(roles: "Editors"), byRole, "EDIT").Should().BeTrue();

        var byUser = new[] { new TabPermission { PermissionKey = "EDIT", UserId = 5, RoleName = null } };
        _sut.HasTabPermission(Context(userId: 5), byUser, "EDIT").Should().BeTrue();

        _sut.HasTabPermission(Context(roles: "Subscribers"), byRole, "EDIT").Should().BeFalse();
    }

    // ----- HasNecessaryPermission (PortalSecurity.vb L517-549) ----------------------------------------------

    private static PermissionContext Permission(
        string? adminRole = "Administrators",
        string? pageAdminRoles = null,
        string? viewRoles = null,
        ModulePermission[]? modulePermissions = null) =>
        new()
        {
            AdministratorRoleName = adminRole,
            PageAdministratorRoles = pageAdminRoles,
            AuthorizedViewRoles = viewRoles,
            ModulePermissions = modulePermissions ?? Array.Empty<ModulePermission>()
        };

    [Fact]
    public void HasNecessaryPermission_anonymous_is_always_authorized()
    {
        // No roles, not a super user: Anonymous access is granted unconditionally.
        _sut.HasNecessaryPermission(Context(isAuthenticated: false, userId: null), SecurityAccessLevel.Anonymous, Permission())
            .Should().BeTrue();
    }

    [Fact]
    public void HasNecessaryPermission_superuser_is_authorized_for_every_level()
    {
        var ctx = Context(isSuperUser: true, roles: "Subscribers");
        _sut.HasNecessaryPermission(ctx, SecurityAccessLevel.View, Permission()).Should().BeTrue();
        _sut.HasNecessaryPermission(ctx, SecurityAccessLevel.Edit, Permission()).Should().BeTrue();
        _sut.HasNecessaryPermission(ctx, SecurityAccessLevel.Admin, Permission()).Should().BeTrue();
        _sut.HasNecessaryPermission(ctx, SecurityAccessLevel.Host, Permission()).Should().BeTrue();
    }

    [Fact]
    public void HasNecessaryPermission_view_granted_to_portal_administrator()
    {
        _sut.HasNecessaryPermission(Context(roles: "Administrators"), SecurityAccessLevel.View, Permission())
            .Should().BeTrue();
    }

    [Fact]
    public void HasNecessaryPermission_view_granted_to_authorized_viewer()
    {
        _sut.HasNecessaryPermission(
                Context(roles: "Members"),
                SecurityAccessLevel.View,
                Permission(viewRoles: "Members;Subscribers"))
            .Should().BeTrue();
    }

    [Fact]
    public void HasNecessaryPermission_view_denied_to_unprivileged_user()
    {
        _sut.HasNecessaryPermission(Context(roles: "Subscribers"), SecurityAccessLevel.View, Permission(viewRoles: "Members"))
            .Should().BeFalse();
    }

    [Fact]
    public void HasNecessaryPermission_edit_requires_view_and_edit_for_non_admins()
    {
        // Can view the module AND has EDIT module permission -> authorized for Edit.
        var editPerms = new[] { new ModulePermission { PermissionKey = "EDIT", UserId = null, RoleName = "Editors" } };
        _sut.HasNecessaryPermission(
                Context(roles: "Editors"),
                SecurityAccessLevel.Edit,
                Permission(viewRoles: "Editors", modulePermissions: editPerms))
            .Should().BeTrue();
    }

    [Fact]
    public void HasNecessaryPermission_edit_denied_when_can_view_but_cannot_edit()
    {
        // Can view but has no EDIT module permission, and is not an admin/page editor -> denied.
        _sut.HasNecessaryPermission(
                Context(roles: "Members"),
                SecurityAccessLevel.Edit,
                Permission(viewRoles: "Members"))
            .Should().BeFalse();
    }

    [Fact]
    public void HasNecessaryPermission_edit_granted_to_admin_without_module_edit_permission()
    {
        _sut.HasNecessaryPermission(Context(roles: "Administrators"), SecurityAccessLevel.Edit, Permission())
            .Should().BeTrue();
    }

    [Fact]
    public void HasNecessaryPermission_admin_requires_admin_or_page_editor()
    {
        _sut.HasNecessaryPermission(Context(roles: "Administrators"), SecurityAccessLevel.Admin, Permission())
            .Should().BeTrue();

        _sut.HasNecessaryPermission(
                Context(roles: "Editors"),
                SecurityAccessLevel.Admin,
                Permission(pageAdminRoles: "Editors"))
            .Should().BeTrue();

        _sut.HasNecessaryPermission(Context(roles: "Members"), SecurityAccessLevel.Admin, Permission())
            .Should().BeFalse();
    }

    [Fact]
    public void HasNecessaryPermission_host_is_superuser_only()
    {
        // A portal administrator (not a super user) is NOT authorized for Host level.
        _sut.HasNecessaryPermission(Context(roles: "Administrators"), SecurityAccessLevel.Host, Permission())
            .Should().BeFalse();
    }
}
