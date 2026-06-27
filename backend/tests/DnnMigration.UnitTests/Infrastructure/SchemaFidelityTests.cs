using System;
using System.Collections.Generic;
using System.Linq;
using DnnMigration.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DnnMigration.UnitTests.Infrastructure;

// MIGRATION (QA-4 schema-fidelity guard): The migration gates (Gate 5) run on EF Core InMemory, which stores by
// CLR property and treats ALL relational mapping (ToTable/HasColumnName/ToView/inheritance strategy) as a no-op.
// Consequently column-name fidelity against the EXISTING DNN SQL Server schema — the AAP's non-negotiable
// constraint (Â§0.1.2 / Â§0.3.5 / Â§0.7.1: "EF Core mapped to the EXISTING schema; table/column names preserved;
// schema unchanged") — is NEVER exercised by InMemory. This suite closes that gap (and the QA report's
// "Areas of Concern" #2/#4: "add a GenerateCreateScript-vs-legacy schema assertion to CI to catch phantom-column
// regressions automatically").
//
// HOW IT WORKS: DbContext.Database.GenerateCreateScript() emits the relational provider's CREATE script ENTIRELY
// OFFLINE from the model (it does NOT open a database connection), so a dummy SqlServer connection string is
// sufficient and no live SQL Server is required. The generated DDL is the ground truth for what columns/tables
// the EF model would demand of a real database. Entities mapped with ToView(...) are EXCLUDED from the script
// (a view is not created), which is exactly how the User read-model fidelity (Issue #3) is asserted below.
//
// AUTHORITATIVE LEGACY SCHEMA used by the assertions below was extracted directly from the in-repo legacy
// scripts Website/Providers/DataProviders/SqlDataProvider/*.SqlDataProvider (the consolidated DotNetNuke.Schema
// CREATE TABLE statements plus every ALTER ... ADD through 04.05.04), which the AAP designates as the binding
// reference for the Fluent API mappings.
public sealed class SchemaFidelityTests
{
    // The generated SqlServer DDL for the entire DnnDbContext model. Generated once (offline) and shared.
    private static readonly string Ddl = GenerateSqlServerDdl();

    private static string GenerateSqlServerDdl()
    {
        // A well-formed but non-connecting connection string. GenerateCreateScript() never opens it.
        var options = new DbContextOptionsBuilder<DnnDbContext>()
            .UseSqlServer("Server=localhost;Database=DnnMigration;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var context = new DnnDbContext(options);
        return context.Database.GenerateCreateScript();
    }

    // ---- DDL parsing helpers -------------------------------------------------------------------------------

    /// <summary>True if the generated DDL contains a physical CREATE TABLE for <paramref name="tableName"/>.</summary>
    private static bool TableExists(string tableName) =>
        Ddl.Contains($"CREATE TABLE [{tableName}] (", StringComparison.Ordinal);

    /// <summary>
    /// Extracts the physical column names declared inside CREATE TABLE [tableName] ( ... ). CONSTRAINT lines
    /// (PK/FK) are skipped because they do not begin with a bracketed column token.
    /// </summary>
    private static IReadOnlyList<string> GetTableColumns(string tableName)
    {
        var startMarker = $"CREATE TABLE [{tableName}] (";
        var idx = Ddl.IndexOf(startMarker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return Array.Empty<string>();
        }

        var bodyStart = idx + startMarker.Length;
        var end = Ddl.IndexOf("\n);", bodyStart, StringComparison.Ordinal);
        if (end < 0)
        {
            end = Ddl.IndexOf(");", bodyStart, StringComparison.Ordinal);
        }

        var body = Ddl.Substring(bodyStart, end - bodyStart);

        var columns = new List<string>();
        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.Trim().TrimStart(',').Trim();
            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                var close = line.IndexOf(']');
                if (close > 1)
                {
                    columns.Add(line.Substring(1, close - 1));
                }
            }
        }

        return columns;
    }

    /// <summary>Case-sensitive membership test (legacy casing fidelity matters under case-sensitive collations).</summary>
    private static bool HasColumnExact(string tableName, string columnName) =>
        GetTableColumns(tableName).Any(c => string.Equals(c, columnName, StringComparison.Ordinal));

    /// <summary>
    /// Returns the database column names the EF model would reference for a <c>ToView</c>-mapped entity, read from
    /// the model metadata. Because <c>GenerateCreateScript()</c> emits NOTHING for a view-mapped entity, the DDL
    /// parser above is blind to read-model column fidelity; this metadata inspection is the only way to assert that
    /// a read-model entity references ONLY columns its backing view actually projects. (This guard would have caught
    /// the User membership-field divergence in QA-4 Issue #3, where ToView alone left 7 non-projected fields mapped
    /// by convention.)
    /// </summary>
    private static IReadOnlyList<string> GetViewMappedColumns(Type clrType, string viewName)
    {
        var options = new DbContextOptionsBuilder<DnnDbContext>()
            .UseSqlServer("Server=localhost;Database=DnnMigration;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var context = new DnnDbContext(options);
        var entityType = context.Model.FindEntityType(clrType)
                         ?? throw new InvalidOperationException($"No EF entity type is mapped for {clrType.Name}.");
        var storeObject = StoreObjectIdentifier.View(viewName, entityType.GetViewSchema());

        var columns = new List<string>();
        foreach (var property in entityType.GetProperties())
        {
            var column = property.GetColumnName(storeObject);
            if (column is not null)
            {
                columns.Add(column);
            }
        }

        return columns;
    }

    // ========================================================================================================
    // PORTAL â€” Issue #1 (CRITICAL: 5 phantom columns) + Issue #2 (MINOR: [GUID]/[TimezoneOffset] casing)
    // ========================================================================================================

    [Fact]
    public void Portal_table_has_no_phantom_columns()
    {
        TableExists("Portals").Should().BeTrue("Portal maps to the physical [Portals] table");
        var columns = GetTableColumns("Portals");

        // None of these are physical [Portals] columns (computed proc/view aliases or fields living on other tables).
        var phantom = new[] { "AdministratorRoleName", "RegisteredRoleName", "Email", "SuperTabId", "Version" };
        columns.Should().NotContain(phantom,
            "Issue #1 â€” the legacy [Portals] table (31 physical columns) contains none of these; mapping them yields 'Invalid column name' on real SQL Server");
    }

    [Fact]
    public void Portal_real_columns_are_present()
    {
        var columns = GetTableColumns("Portals");
        var realColumns = new[]
        {
            "PortalID", "PortalName", "LogoFile", "FooterText", "ExpiryDate", "UserRegistration",
            "BannerAdvertising", "AdministratorId", "Currency", "HostFee", "HostSpace",
            "AdministratorRoleId", "RegisteredRoleId", "Description", "KeyWords", "BackgroundFile",
            "PaymentProcessor", "ProcessorUserId", "ProcessorPassword", "SiteLogHistory",
            "HomeTabId", "LoginTabId", "UserTabId", "DefaultLanguage", "AdminTabId",
            "HomeDirectory", "SplashTabId", "PageQuota", "UserQuota",
        };
        columns.Should().Contain(realColumns, "all 29 non-special legacy [Portals] columns must remain mapped");
    }

    [Fact]
    public void Portal_guid_and_timezone_use_legacy_casing()
    {
        // Issue #2: legacy columns are [GUID] and [TimezoneOffset]; the PascalCase variants break under a
        // case-sensitive collation and violate exact-legacy-name preservation.
        HasColumnExact("Portals", "GUID").Should().BeTrue("legacy column is [GUID] (all caps)");
        HasColumnExact("Portals", "TimezoneOffset").Should().BeTrue("legacy column is [TimezoneOffset]");

        HasColumnExact("Portals", "Guid").Should().BeFalse("the PascalCase [Guid] divergence must be removed");
        HasColumnExact("Portals", "TimeZoneOffset").Should().BeFalse("the PascalCase [TimeZoneOffset] divergence must be removed");
    }

    // ========================================================================================================
    // USER â€” Issue #3 (CRITICAL: 8 phantom membership/UserPortals columns). Resolved by ToView("vw_Users"),
    // mirroring the blessed Module->vw_Modules read-model pattern. User now maps to BOTH vw_Users (read) and the physical [Users] write table, so GenerateCreateScript emits [Users].
    // ========================================================================================================

    [Fact]
    public void User_write_table_emits_only_the_real_users_columns()
    {
        // QA-FINAL Issue #1 (CRITICAL, read/write split): User maps to BOTH the vw_Users read view (queries) AND the
        // physical [Users] write table (insert/update/delete). With both ToView + ToTable, EF Core 8 reads from the
        // view and writes to the table, so GenerateCreateScript now EMITS [Users] - and it must contain EXACTLY the
        // eight legacy [Users] columns the entity owns. PortalId is excluded from the write table (it physically lives
        // in [UserPortals]); the seven membership/computed fields (FullName, IsApproved, CreatedDate, LastLoginDate,
        // LastActivityDate, LastLockoutDate, LockedOut) are Ignore()d (vw_Users does not project them either).
        TableExists("Users").Should().BeTrue(
            "User maps to BOTH vw_Users (read) and the physical [Users] table (write); the write table must be emitted");

        GetTableColumns("Users").Should().BeEquivalentTo(
            new[] { "UserID", "Username", "DisplayName", "Email", "FirstName", "LastName", "IsSuperUser", "AffiliateId" },
            "the physical [Users] table has exactly its eight legacy columns; PortalId and the seven membership/computed fields are not [Users] columns");

        var notOnUsersTable = new[]
        {
            "PortalId", "FullName", "IsApproved", "CreatedDate", "LastLoginDate", "LastActivityDate", "LastLockoutDate", "LockedOut",
        };
        GetTableColumns("Users").Should().NotContain(notOnUsersTable,
            "PortalId lives in [UserPortals] and the membership/computed fields live in aspnet_* (or are computed) - none is a physical [Users] column");

        // The user<->portal membership write table [UserPortals] must be emitted with exactly its five legacy columns,
        // so UserRepository.AddAsync can fan a new user out to [Users] + [UserPortals] on a real SQL Server.
        TableExists("UserPortals").Should().BeTrue("the [UserPortals] membership write table must be emitted");
        GetTableColumns("UserPortals").Should().BeEquivalentTo(
            new[] { "UserPortalId", "UserId", "PortalId", "CreatedDate", "Authorised" },
            "[UserPortals] preserves the legacy schema exactly (UserPortalId IDENTITY PK + UserId, PortalId, CreatedDate, Authorised)");
    }

    [Fact]
    public void User_view_mapping_references_only_columns_vw_users_projects()
    {
        // GenerateCreateScript emits nothing for a ToView entity, so the DDL parser cannot see read-model column
        // fidelity. This metadata guard closes that blind spot: it proves EF references ONLY columns the legacy
        // vw_Users view actually projects. The real view (DotNetNuke.Schema.SqlDataProvider) joins ONLY [UserPortals]
        // (NOT aspnet_Membership/aspnet_Users) and projects exactly these 11 columns:
        var projectedByView = new[]
        {
            "UserID", "PortalId", "Username", "FirstName", "LastName", "DisplayName",
            "IsSuperUser", "Email", "AffiliateId", "UpdatePassword", "Authorised",
        };

        var mapped = GetViewMappedColumns(typeof(Domain.Entities.User), "vw_Users");

        // Every EF-mapped column must be one the view projects. (SQL Server resolves names case-insensitively, which
        // is why the key's HasColumnName("UserID") legitimately resolves against the view's "UserId" projection.)
        mapped.Should().OnlyContain(
            c => projectedByView.Any(v => string.Equals(v, c, StringComparison.OrdinalIgnoreCase)),
            "Issue #3 â€” the User read model must reference only columns vw_Users projects; the seven membership/" +
            "computed fields (FullName, IsApproved, CreatedDate, LastLoginDate, LastActivityDate, LastLockoutDate, " +
            "LockedOut) are Ignore()d because the view does not project them");

        // PortalId MUST remain mapped: it is a real vw_Users column (from the [UserPortals] join) and backs the
        // tenant-scoping repository LINQ filters â€” the reason ToView (not Ignore) was required to resolve Issue #3.
        mapped.Should().Contain(c => string.Equals(c, "PortalId", StringComparison.OrdinalIgnoreCase),
            "PortalId is a real vw_Users column and must stay mapped for multi-tenant query scoping");

        // The seven non-projected fields must NOT be EF-mapped (this is the part ToView alone did not achieve).
        var nonProjected = new[]
        {
            "FullName", "IsApproved", "CreatedDate", "LastLoginDate", "LastActivityDate", "LastLockoutDate", "LockedOut",
        };
        mapped.Should().NotContain(
            c => nonProjected.Any(n => string.Equals(n, c, StringComparison.OrdinalIgnoreCase)),
            "the seven membership/computed fields are not projected by vw_Users and must be Ignore()d");
    }

    // ========================================================================================================
    // PERMISSION FAMILY â€” Issue #4 (CRITICAL: TPC inheritance duplicates 4 base columns onto each child table,
    // forces child PK to PermissionID, and introduces a [PermissionSequence] object). Resolved by COMPOSITION.
    // Plus Issue #2: Permission.ModuleDefId must map to legacy [ModuleDefID].
    // ========================================================================================================

    [Fact]
    public void Base_permission_table_has_legacy_columns_with_correct_casing()
    {
        TableExists("Permission").Should().BeTrue();
        var columns = GetTableColumns("Permission");

        columns.Should().Contain(new[] { "PermissionID", "PermissionCode", "PermissionKey", "PermissionName" },
            "the base catalog columns live on [Permission]");
        HasColumnExact("Permission", "ModuleDefID").Should().BeTrue("Issue #2 â€” legacy column is [ModuleDefID]");
        HasColumnExact("Permission", "ModuleDefId").Should().BeFalse("the PascalCase [ModuleDefId] divergence must be removed");
    }

    [Theory]
    [InlineData("ModulePermission", "ModulePermissionID", "ModuleID")]
    [InlineData("TabPermission", "TabPermissionID", "TabID")]
    [InlineData("FolderPermission", "FolderPermissionID", "FolderID")]
    public void Child_permission_tables_do_not_carry_base_permission_columns(string table, string ownPk, string scopeFk)
    {
        TableExists(table).Should().BeTrue();
        var columns = GetTableColumns(table);

        // The 4 base attributes must NOT be duplicated onto the child table (they live solely on [Permission]).
        var baseColumns = new[] { "PermissionCode", "ModuleDefID", "ModuleDefId", "PermissionKey", "PermissionName" };
        columns.Should().NotContain(baseColumns,
            $"Issue #4 â€” [{table}] has only its own 6 physical columns; base attributes resolve via the PermissionID FK");

        // The child must keep its OWN identity PK + scope FK + the PermissionID FK (legacy 6-column shape).
        columns.Should().Contain(ownPk, $"[{table}] PK is [{ownPk}] (NOT PermissionID)");
        columns.Should().Contain(scopeFk);
        columns.Should().Contain("PermissionID");
        columns.Should().Contain("RoleID");
        columns.Should().Contain("AllowAccess");
    }

    [Fact]
    public void No_permission_sequence_object_is_generated()
    {
        // TPC mapping emitted "CREATE SEQUENCE [PermissionSequence]" and a PermissionID default of NEXT VALUE FOR it.
        Ddl.Should().NotContain("PermissionSequence",
            "Issue #4 â€” the composition refactor removes the TPC sequence object entirely");
    }

    // ========================================================================================================
    // USERROLE â€” Issue #5 (CRITICAL: phantom [Subscribed] column).
    // ========================================================================================================

    [Fact]
    public void UserRoles_table_has_no_subscribed_column_and_keeps_legacy_columns()
    {
        TableExists("UserRoles").Should().BeTrue();
        var columns = GetTableColumns("UserRoles");

        HasColumnExact("UserRoles", "Subscribed").Should().BeFalse(
            "Issue #5 â€” [Subscribed] is a computed proc alias, not a physical [UserRoles] column");

        columns.Should().Contain(new[] { "UserRoleID", "UserID", "RoleID", "ExpiryDate", "IsTrialUsed", "EffectiveDate" },
            "the 6 real legacy [UserRoles] columns must remain mapped");
    }

    // ========================================================================================================
    // TAB â€” Issue #6 (MINOR: real [Level] column wrongly Ignore()d).
    // ========================================================================================================

    [Fact]
    public void Tabs_table_maps_the_real_level_column()
    {
        TableExists("Tabs").Should().BeTrue();
        var columns = GetTableColumns("Tabs");

        columns.Should().Contain("Level",
            "Issue #6 â€” [Level] is a real persisted [Tabs] column (int NOT NULL DEFAULT 0); it must be mapped, not Ignored");

        // Genuinely computed / dropped columns must remain absent (these were correctly handled by the team).
        columns.Should().NotContain("HasChildren", "HasChildren is genuinely computed and correctly Ignored");
        columns.Should().NotContain(new[] { "AuthorizedRoles", "AdministratorRoles" },
            "these were DROPped in 03.00.01 and are correctly Ignored");
        columns.Should().Contain("IsSecure", "IsSecure was ADDed in 04.05.04 and is correctly mapped");
    }

    // ========================================================================================================
    // POSITIVE CONTROLS â€” entities the team mapped correctly must stay correct (regression protection).
    // ========================================================================================================

    [Fact]
    public void Roles_table_matches_legacy_schema_exactly()
    {
        TableExists("Roles").Should().BeTrue();
        var columns = GetTableColumns("Roles");
        var legacy = new[]
        {
            "RoleID", "PortalID", "RoleGroupID", "RoleName", "Description", "ServiceFee", "BillingFrequency",
            "TrialPeriod", "TrialFrequency", "BillingPeriod", "TrialFee", "IsPublic", "AutoAssignment",
            "RSVPCode", "IconFile",
        };
        columns.Should().BeEquivalentTo(legacy, "the [Roles] mapping is already correct (15/15) and must stay so");
    }

    [Fact]
    public void Module_write_table_emits_only_the_real_modules_columns()
    {
        // QA-FINAL Issue #3 (CRITICAL, read/write split): Module maps to BOTH the vw_Modules read view (queries) AND
        // the physical [Modules] write table. With both ToView + ToTable, EF Core 8 reads from the flattened view and
        // writes to the base table, so GenerateCreateScript now EMITS [Modules] - and it must contain EXACTLY the 11
        // legacy base columns. Every other flattened vw_Modules column is sourced from a DIFFERENT base table
        // (TabModules placement, DesktopModules/ModuleControls/ModuleDefinitions reference data) and is excluded.
        TableExists("Modules").Should().BeTrue(
            "Module maps to BOTH vw_Modules (read) and the physical [Modules] table (write); the write table must be emitted");

        GetTableColumns("Modules").Should().BeEquivalentTo(
            new[]
            {
                "ModuleID", "ModuleDefID", "ModuleTitle", "AllTabs", "IsDeleted",
                "InheritViewPermissions", "Header", "Footer", "StartDate", "EndDate", "PortalID",
            },
            "the physical [Modules] table has exactly its 11 legacy base columns");

        var notOnModulesTable = new[]
        {
            "TabID", "PaneName", "ModuleOrder", "CacheTime", "Alignment", "Color", "Border", "IconFile",
            "Visibility", "ContainerSrc", "DisplayTitle", "DisplayPrint", "DisplaySyndicate", "FriendlyName",
        };
        GetTableColumns("Modules").Should().NotContain(notOnModulesTable,
            "placement columns live in [TabModules] and reference columns live in DesktopModules/ModuleControls/ModuleDefinitions - none is a physical [Modules] column");

        // The per-page placement write table [TabModules] must be emitted with exactly its 15 legacy columns so
        // ModuleRepository can fan a placed module out to [Modules] + [TabModules] on a real SQL Server.
        TableExists("TabModules").Should().BeTrue("the [TabModules] placement write table must be emitted");
        GetTableColumns("TabModules").Should().BeEquivalentTo(
            new[]
            {
                "TabModuleID", "TabID", "ModuleID", "PaneName", "ModuleOrder", "CacheTime", "Alignment", "Color",
                "Border", "IconFile", "Visibility", "ContainerSrc", "DisplayTitle", "DisplayPrint", "DisplaySyndicate",
            },
            "[TabModules] preserves the legacy schema exactly (TabModuleID IDENTITY PK + the 14 placement columns)");
    }

    // MIGRATION (CP-FINAL review - Critical #4 / Schema Preservation): the migrated BCrypt credential store must map
    // onto the EXISTING legacy ASP.NET membership schema, NOT a new [UserCredentials] table. This test is the
    // offline guard that the relational model (a) emits NO [UserCredentials] table and (b) emits the three existing
    // membership tables with EXACTLY their legacy columns, so authentication works against an unchanged DNN database
    // (AAP 0.7.1 - no schema alteration).
    [Fact]
    public void Credentials_map_to_existing_membership_schema_and_emit_no_new_UserCredentials_table()
    {
        // (a) The schema-violating new table must NOT be generated anywhere in the model.
        TableExists("UserCredentials").Should().BeFalse(
            "credentials map onto the existing aspnet_Membership schema; a new [UserCredentials] table would violate the no-schema-alteration mandate");

        // (b) The existing membership tables are emitted with exactly their legacy columns (InstallCommon.sql /
        // InstallMembership.sql), bound by the Fluent configs so a real DNN database is matched without alteration.
        TableExists("aspnet_Applications").Should().BeTrue("the existing [aspnet_Applications] table is mapped for credential scoping");
        GetTableColumns("aspnet_Applications").Should().BeEquivalentTo(
            new[] { "ApplicationId", "ApplicationName", "LoweredApplicationName", "Description" },
            "[aspnet_Applications] preserves the legacy InstallCommon.sql columns exactly");

        TableExists("aspnet_Users").Should().BeTrue("the existing [aspnet_Users] membership-identity table is mapped");
        GetTableColumns("aspnet_Users").Should().BeEquivalentTo(
            new[] { "ApplicationId", "UserId", "UserName", "LoweredUserName", "MobileAlias", "IsAnonymous", "LastActivityDate" },
            "[aspnet_Users] preserves the legacy InstallCommon.sql columns exactly");

        TableExists("aspnet_Membership").Should().BeTrue("the existing [aspnet_Membership] credential table is mapped (BCrypt hash stored in [Password])");
        GetTableColumns("aspnet_Membership").Should().BeEquivalentTo(
            new[]
            {
                "ApplicationId", "UserId", "Password", "PasswordFormat", "PasswordSalt", "MobilePIN", "Email",
                "LoweredEmail", "PasswordQuestion", "PasswordAnswer", "IsApproved", "IsLockedOut", "CreateDate",
                "LastLoginDate", "LastPasswordChangedDate", "LastLockoutDate", "FailedPasswordAttemptCount",
                "FailedPasswordAttemptWindowStart", "FailedPasswordAnswerAttemptCount",
                "FailedPasswordAnswerAttemptWindowStart", "Comment",
            },
            "[aspnet_Membership] preserves the 21 legacy InstallMembership.sql columns exactly");
    }

    // MIGRATION (CP-FINAL review - PortalRepository.GetByAliasAsync parity): the alias->portal lookup is now backed
    // by the PortalAlias entity, which MUST map onto the EXISTING legacy [PortalAlias] table
    // (DotNetNuke.Schema.SqlDataProvider L1288) with exactly its three physical columns and no schema addition.
    [Fact]
    public void PortalAlias_maps_to_existing_table_with_legacy_columns()
    {
        TableExists("PortalAlias").Should().BeTrue("the alias lookup maps onto the existing [PortalAlias] table");
        GetTableColumns("PortalAlias").Should().BeEquivalentTo(
            new[] { "PortalAliasID", "PortalID", "HTTPAlias" },
            "[PortalAlias] preserves the legacy schema exactly (PortalAliasID IDENTITY PK + PortalID + HTTPAlias)");
    }

    // MIGRATION (CP-final review - profile workflow parity): the profile read/update workflow is backed by the
    // legacy EAV pair [ProfilePropertyDefinition] (per-portal property definitions) + [UserProfile] (per-user
    // values), both of which MUST map onto the EXISTING tables (04.00.04.SqlDataProvider L1107 / L1411) with
    // exactly their physical columns and no schema addition (AAP 0.7.1 - no schema alteration).
    [Fact]
    public void ProfilePropertyDefinition_maps_to_existing_table_with_legacy_columns()
    {
        TableExists("ProfilePropertyDefinition").Should().BeTrue(
            "profile definitions map onto the existing [ProfilePropertyDefinition] table");
        GetTableColumns("ProfilePropertyDefinition").Should().BeEquivalentTo(
            new[]
            {
                "PropertyDefinitionID", "PortalID", "ModuleDefID", "Deleted", "DataType", "DefaultValue",
                "PropertyCategory", "PropertyName", "Length", "Required", "ValidationExpression", "ViewOrder",
                "Visible",
            },
            "[ProfilePropertyDefinition] preserves the 13 legacy 04.00.04.SqlDataProvider columns exactly");
    }

    // MIGRATION (CP-final review - profile workflow parity): the per-user profile values map onto the EXISTING
    // [UserProfile] EAV table with exactly its seven physical columns; a new table or phantom column would violate
    // the no-schema-alteration mandate.
    [Fact]
    public void UserProfile_value_table_maps_to_existing_table_with_legacy_columns()
    {
        TableExists("UserProfile").Should().BeTrue(
            "per-user profile values map onto the existing [UserProfile] EAV table");
        GetTableColumns("UserProfile").Should().BeEquivalentTo(
            new[]
            {
                "ProfileID", "UserID", "PropertyDefinitionID", "PropertyValue", "PropertyText", "Visibility",
                "LastUpdatedDate",
            },
            "[UserProfile] preserves the seven legacy 04.00.04.SqlDataProvider columns exactly (no phantom columns, no new FKs materialized as shadow columns)");
    }
}
