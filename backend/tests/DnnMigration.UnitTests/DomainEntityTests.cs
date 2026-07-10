using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Behavioral / structural contract tests for the migrated Domain POCOs
/// (<c>DnnMigration.Domain.Entities</c> and <c>DnnMigration.Domain.Enums</c>).
/// </summary>
/// <remarks>
/// These tests lock behavioral equivalence with the legacy DotNetNuke 4.x <c>*Info.vb</c>
/// classes per the Minimal Change Clause. The Domain layer is a set of dependency-free POCOs,
/// so the observable contract worth pinning is:
/// <list type="bullet">
///   <item>the computed (get-only) members — <c>User.FullName</c>, <c>UserProfile.FullName</c>,
///     and <c>Portal.HomeDirectoryMapPath</c> — including their exact whitespace semantics;</item>
///   <item>the composition defaults — non-null owned child objects and initialized (empty)
///     collections — that guarantee a freshly-constructed aggregate is safe to traverse;</item>
///   <item>the <c>Permission</c> inheritance hierarchy that the security model relies on;</item>
///   <item>the enum-typed member defaults that map to legacy integer persistence columns.</item>
/// </list>
/// IMPORTANT: every assertion below targets a member that has been verified to exist on the
/// migrated entity/enum. A deliberate distinction is preserved and asserted here — <c>User.FullName</c>
/// TRIMS its result whereas <c>UserProfile.FullName</c> does NOT (the legacy VB profile getter returned
/// <c>FirstName &amp; " " &amp; LastName</c> verbatim with no trimming). Tests assert the ACTUAL behavior of
/// each so behavioral equivalence — not an idealized guess — is what gets locked.
/// </remarks>
public class DomainEntityTests
{
    #region Computed member: User.FullName (trims)

    [Fact]
    public void User_FullName_CombinesTopLevelFirstAndLastName()
    {
        var user = new User { FirstName = "Jane", LastName = "Doe" };

        user.FullName.Should().Be("Jane Doe");
    }

    /// <summary>
    /// <c>User.FullName</c> is <c>$"{FirstName} {LastName}".Trim()</c>, so a blank first or last
    /// name must not leave a leading/trailing space, and two blank parts collapse to <see cref="string.Empty"/>.
    /// </summary>
    [Theory]
    [InlineData("Jane", "Doe", "Jane Doe")]
    [InlineData("", "Doe", "Doe")]        // MIGRATION: leading space trimmed away
    [InlineData("Jane", "", "Jane")]      // MIGRATION: trailing space trimmed away
    [InlineData("", "", "")]              // MIGRATION: both blank -> string.Empty (single space trimmed)
    public void User_FullName_TrimsWhitespace(string firstName, string lastName, string expected)
    {
        var user = new User { FirstName = firstName, LastName = lastName };

        user.FullName.Should().Be(expected);
    }

    [Fact]
    public void User_FullName_WithBothNamesBlank_IsStringEmpty()
    {
        var user = new User { FirstName = string.Empty, LastName = string.Empty };

        user.FullName.Should().Be(string.Empty);
    }

    /// <summary>
    /// KEY CONTRACT: <c>User.FullName</c> derives from the User's OWN top-level
    /// <c>FirstName</c>/<c>LastName</c>, never from the composed <c>Profile</c>. Setting the profile's
    /// name to different values must not change <c>User.FullName</c>. This test guards against a
    /// regression that would wire the computed name to <c>Profile</c>.
    /// </summary>
    [Fact]
    public void User_FullName_IsIndependentOfProfile()
    {
        var user = new User
        {
            FirstName = "Top",
            LastName = "Level",
            Profile = new UserProfile { FirstName = "Prof", LastName = "Ile" }
        };

        user.FullName.Should().Be("Top Level", "User.FullName must use the User's own names, not the Profile's");
        // Sanity-check the contrast: the profile still computes its own (independent) value.
        user.Profile.FullName.Should().Be("Prof Ile");
    }

    #endregion

    #region Computed member: UserProfile.FullName (does NOT trim)

    [Fact]
    public void UserProfile_FullName_CombinesFirstAndLastName()
    {
        var profile = new UserProfile { FirstName = "A", LastName = "B" };

        profile.FullName.Should().Be("A B");
    }

    /// <summary>
    /// MIGRATION: unlike <c>User.FullName</c>, the legacy profile getter returned
    /// <c>FirstName &amp; " " &amp; LastName</c> with NO trimming (UserProfile.vb). The migrated
    /// <c>UserProfile.FullName</c> preserves that exactly, so a blank part leaves its space in place and
    /// two blank parts yield a single space. Asserting this locks the no-trim behavior and prevents a
    /// well-meaning "fix" from silently breaking behavioral equivalence.
    /// </summary>
    [Theory]
    [InlineData("Jane", "Doe", "Jane Doe")]
    [InlineData("", "Doe", " Doe")]       // MIGRATION: leading space PRESERVED (no trim)
    [InlineData("Jane", "", "Jane ")]     // MIGRATION: trailing space PRESERVED (no trim)
    [InlineData("", "", " ")]             // MIGRATION: both blank -> a single space (no trim)
    public void UserProfile_FullName_DoesNotTrimWhitespace(string firstName, string lastName, string expected)
    {
        var profile = new UserProfile { FirstName = firstName, LastName = lastName };

        profile.FullName.Should().Be(expected);
    }

    #endregion

    #region Computed member: Portal.HomeDirectoryMapPath (returns HomeDirectory verbatim)

    [Fact]
    public void Portal_HomeDirectoryMapPath_ReturnsHomeDirectoryVerbatim()
    {
        var portal = new Portal { HomeDirectory = "/portals/0" };

        portal.HomeDirectoryMapPath.Should().Be("/portals/0");
    }

    [Fact]
    public void Portal_HomeDirectoryMapPath_TracksHomeDirectoryValue()
    {
        var portal = new Portal();

        // Default (unset) HomeDirectory is string.Empty, and the computed map path mirrors it exactly.
        portal.HomeDirectoryMapPath.Should().Be(string.Empty);

        portal.HomeDirectory = "/portals/7/media";
        portal.HomeDirectoryMapPath.Should().Be("/portals/7/media");
    }

    #endregion

    #region Composition defaults (non-null owned objects & initialized empty collections)

    [Fact]
    public void User_Defaults_ComposeNonNullChildrenAndEmptyRoles()
    {
        var user = new User();

        // Owned child objects are initialized (= new()) so the aggregate is always safe to traverse.
        user.Membership.Should().NotBeNull();
        user.Membership.Should().BeOfType<UserMembership>();
        user.Profile.Should().NotBeNull();
        user.Profile.Should().BeOfType<UserProfile>();

        // Roles is initialized to Array.Empty<string>() — non-null and empty, never null.
        user.Roles.Should().NotBeNull().And.BeEmpty();

        // String members default to string.Empty (not null) under the nullable-enabled contract.
        user.FirstName.Should().Be(string.Empty);
        user.LastName.Should().Be(string.Empty);
        user.DisplayName.Should().Be(string.Empty);
        user.Email.Should().Be(string.Empty);
        user.Username.Should().Be(string.Empty);
    }

    [Fact]
    public void Module_ModulePermissions_DefaultsToNonNullEmptyCollection()
    {
        new Module().ModulePermissions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Tab_TabPermissions_DefaultsToNonNullEmptyCollection()
    {
        new Tab().TabPermissions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void UserProfile_ProfileProperties_DefaultsToNonNullEmptyCollection()
    {
        // ProfileProperties is a read-only (get-only) ProfilePropertyDefinitionCollection initialized
        // to an empty collection; it is non-null and empty on a freshly-constructed profile.
        new UserProfile().ProfileProperties.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void UserMembership_Approved_DefaultsToTrue()
    {
        // MIGRATION: the legacy UserMembership.vb initialized `_Approved As Boolean = True`, so a
        // newly-constructed membership must be Approved by default (an uninitialized C# bool would
        // default to false and silently change the user creation/approval workflow behavior).
        var membership = new UserMembership();

        membership.Approved.Should().BeTrue();
        membership.LockedOut.Should().BeFalse();
        membership.Password.Should().Be(string.Empty);
        membership.Username.Should().Be(string.Empty);
        membership.Email.Should().Be(string.Empty);
    }

    #endregion

    #region Permission inheritance hierarchy

    [Fact]
    public void ModulePermission_IsAssignableToPermission()
    {
        new ModulePermission().Should().BeAssignableTo<Permission>();
    }

    [Fact]
    public void TabPermission_IsAssignableToPermission()
    {
        new TabPermission().Should().BeAssignableTo<Permission>();
    }

    [Fact]
    public void FolderPermission_IsAssignableToPermission()
    {
        new FolderPermission().Should().BeAssignableTo<Permission>();
    }

    /// <summary>
    /// A base <see cref="Permission"/> property (<c>PermissionKey</c>) and a derived-only property
    /// (<c>AllowAccess</c>) must both be settable through a derived instance and read back — and the
    /// base member must remain visible when the instance is viewed through the base type.
    /// </summary>
    [Fact]
    public void ModulePermission_BaseAndDerivedProperties_RoundTrip()
    {
        var modulePermission = new ModulePermission { PermissionKey = "EDIT", AllowAccess = true };

        modulePermission.PermissionKey.Should().Be("EDIT");
        modulePermission.AllowAccess.Should().BeTrue();

        // The inherited property is visible through the base-type reference as well.
        Permission asBase = modulePermission;
        asBase.PermissionKey.Should().Be("EDIT");
    }

    [Fact]
    public void Permission_StringMembers_DefaultToStringEmpty()
    {
        var permission = new Permission();

        permission.PermissionCode.Should().Be(string.Empty);
        permission.PermissionKey.Should().Be(string.Empty);
        permission.PermissionName.Should().Be(string.Empty);
    }

    #endregion

    #region Enum-typed member defaults (map to legacy integer persistence columns)

    [Fact]
    public void Portal_UserRegistration_DefaultsToNoRegistration()
    {
        // default(UserRegistrationType) == 0 == NoRegistration.
        new Portal().UserRegistration.Should().Be(UserRegistrationType.NoRegistration);
    }

    [Fact]
    public void Module_ControlType_DefaultsToView()
    {
        // default(SecurityAccessLevel) == 0 == View.
        new Module().ControlType.Should().Be(SecurityAccessLevel.View);
    }

    #endregion
}
