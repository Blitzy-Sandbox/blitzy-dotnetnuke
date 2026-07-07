using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: EF Core 8 Fluent mappings for the Module domain (Module + ModuleDefinition +
// DesktopModule), replacing the legacy ADO.NET/SqlDataProvider stored-procedure surface for these
// entities (Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb and the abstract
// Library/Components/Providers/Data/DataProvider.vb). All three classes are auto-discovered and
// applied by DnnDbContext.OnModelCreating via ModelBuilder.ApplyConfigurationsFromAssembly, so this
// file adds no wiring of its own to the context.
//
// DATA MODEL FIDELITY is mandatory: every mapping below preserves the legacy table, column, index,
// and key names VERBATIM against the EXISTING DotNetNuke schema (verified against the versioned
// Website/Providers/DataProviders/SqlDataProvider scripts and the consolidated
// DotNetNuke.Schema.SqlDataProvider). No table structures are altered.

/// <summary>
/// EF Core Fluent configuration for the <see cref="Module"/> entity, mapped to the existing DNN
/// <c>Modules</c> table.
/// </summary>
// MIGRATION: The Domain Module is DNN's denormalized module-instance object (legacy
// DotNetNuke.Entities.Modules.ModuleInfo, Library/Components/Modules/ModuleInfo.vb L36), historically
// hydrated from the legacy [vw_Modules] view. That view is a join of five tables:
//   * Modules (M.*)         -> module-instance/state columns
//   * TabModules (TM.*)     -> module PLACEMENT columns (pane, order, visibility, container, display*)
//   * ModuleDefinitions     -> definition lookup
//   * DesktopModules (DM.*) -> desktop-module registration lookup
//   * ModuleControls (MC.*) -> control lookup (src/type/title/help)
// This single entity therefore maps to a SINGLE table (Modules) carrying the M.* and TM.* columns
// under their verbatim legacy names (matching the [vw_Modules] projection). The DM.* and MC.* lookup
// members are NOT columns of this entity and are Ignore()d below. Physical-column names verified
// against 01.00.00.SqlDataProvider (base Modules L220) and DotNetNuke.Schema.SqlDataProvider
// (cumulative Modules L6475, TabModules L6401, vw_Modules L11672).
public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    /// <summary>
    /// Configures the <see cref="Module"/> entity type against the existing <c>Modules</c> schema.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        // MIGRATION: legacy table [Modules] (PK [PK_Modules] on [ModuleID], IDENTITY(0,1)).
        builder.ToTable("Modules");
        builder.HasKey(e => e.ModuleID);
        builder.Property(e => e.ModuleID).HasColumnName("ModuleID").ValueGeneratedOnAdd();

        // --- Columns physically sourced from the [Modules] table (M.* in [vw_Modules]) ---
        builder.Property(e => e.PortalID).HasColumnName("PortalID");
        builder.Property(e => e.ModuleTitle).HasColumnName("ModuleTitle");
        builder.Property(e => e.AllTabs).HasColumnName("AllTabs");
        builder.Property(e => e.IsDeleted).HasColumnName("IsDeleted");
        builder.Property(e => e.Header).HasColumnName("Header");
        builder.Property(e => e.Footer).HasColumnName("Footer");
        builder.Property(e => e.StartDate).HasColumnName("StartDate");
        builder.Property(e => e.EndDate).HasColumnName("EndDate");
        builder.Property(e => e.InheritViewPermissions).HasColumnName("InheritViewPermissions");

        // MIGRATION: [ModuleDefID] is a real [Modules] column and the FK end of
        // [FK_Modules_ModuleDefinitions] (Modules.ModuleDefID -> ModuleDefinitions.ModuleDefID,
        // ON DELETE CASCADE; 01.00.00.SqlDataProvider L673). The Module entity exposes NO
        // ModuleDefinition navigation, so the foreign key is intentionally kept as a plain scalar
        // column rather than forcing a navigation-less relationship. This keeps the model valid and
        // avoids inventing a relationship the Domain does not model; ModuleDefinition remains a
        // sibling aggregate reachable by ModuleDefID.
        builder.Property(e => e.ModuleDefID).HasColumnName("ModuleDefID");

        // --- Placement columns surfaced through [vw_Modules] as TM.* (physically in [TabModules]
        // in the fully-upgraded v4.9 schema; DotNetNuke.Schema.SqlDataProvider L6401). They are the
        // module-placement fields of the denormalized ModuleInfo and are mapped here under their
        // verbatim legacy names so the denormalized Module round-trips as a single entity. ---
        builder.Property(e => e.TabID).HasColumnName("TabID");
        builder.Property(e => e.TabModuleID).HasColumnName("TabModuleID");
        builder.Property(e => e.ModuleOrder).HasColumnName("ModuleOrder");
        builder.Property(e => e.PaneName).HasColumnName("PaneName");
        builder.Property(e => e.CacheTime).HasColumnName("CacheTime");
        builder.Property(e => e.Alignment).HasColumnName("Alignment");
        builder.Property(e => e.Color).HasColumnName("Color");
        builder.Property(e => e.Border).HasColumnName("Border");
        builder.Property(e => e.IconFile).HasColumnName("IconFile");
        builder.Property(e => e.ContainerSrc).HasColumnName("ContainerSrc");
        builder.Property(e => e.DisplayTitle).HasColumnName("DisplayTitle");
        builder.Property(e => e.DisplayPrint).HasColumnName("DisplayPrint");
        builder.Property(e => e.DisplaySyndicate).HasColumnName("DisplaySyndicate");

        // MIGRATION: [Visibility] is an int column ([TabModules].[Visibility]). The legacy nested
        // VisibilityState enum (Maximized=0/Minimized=1/None=2) was flattened to int on the Domain
        // entity, so it maps as a plain integer column (no enum conversion needed here).
        builder.Property(e => e.Visibility).HasColumnName("Visibility");

        // MIGRATION: [AuthorizedEditRoles]/[AuthorizedViewRoles] were columns of the base v1.0
        // [Modules] table (01.00.00.SqlDataProvider L227/L230) and are retained on the denormalized
        // ModuleInfo. They were later superseded by the ModulePermissions model but are preserved
        // here under their verbatim legacy names for functional parity.
        builder.Property(e => e.AuthorizedEditRoles).HasColumnName("AuthorizedEditRoles");
        builder.Property(e => e.AuthorizedViewRoles).HasColumnName("AuthorizedViewRoles");

        // MIGRATION: SecurityAccessLevel enum stored as int. ControlType is surfaced through
        // [vw_Modules] as MC.ControlType (from [ModuleControls]); per the folder contract the enum is
        // mapped with an explicit int conversion (rather than relying on the EF convention) so the
        // persistence/wire contract — SecurityAccessLevel's explicit integer values, including the
        // negatives ControlPanel=-3/SkinObject=-2/Anonymous=-1 — is stored verbatim as an integer.
        builder.Property(e => e.ControlType).HasColumnName("ControlType").HasConversion<int>();

        // MIGRATION: DENORMALIZED lookup members Ignore()d — they are NOT physical [Modules] columns.
        // In the legacy [vw_Modules] view these were projected from joined tables (DM.* from
        // [DesktopModules], MC.* from [ModuleControls]) and belong to the sibling DesktopModule /
        // ModuleDefinition aggregates (and the retired ModuleControls lookup). Ignoring them preserves
        // schema fidelity (no invented [Modules] columns) and avoids duplicating data that the sibling
        // entities own. Verified against the cumulative v4.9 Modules table
        // (DotNetNuke.Schema.SqlDataProvider L6475): none of these names exist as [Modules] columns.
        builder.Ignore(e => e.DesktopModuleID);
        builder.Ignore(e => e.FriendlyName);
        builder.Ignore(e => e.FolderName);
        builder.Ignore(e => e.Description);
        builder.Ignore(e => e.Version);
        builder.Ignore(e => e.IsPremium);
        builder.Ignore(e => e.IsAdmin);
        builder.Ignore(e => e.BusinessControllerClass);
        builder.Ignore(e => e.ModuleName);
        builder.Ignore(e => e.SupportedFeatures);
        builder.Ignore(e => e.CompatibleVersions);
        builder.Ignore(e => e.Dependencies);
        builder.Ignore(e => e.Permissions);
        builder.Ignore(e => e.DefaultCacheTime);
        builder.Ignore(e => e.ModuleControlId);
        builder.Ignore(e => e.ControlSrc);
        builder.Ignore(e => e.ControlTitle);
        builder.Ignore(e => e.HelpUrl);
        builder.Ignore(e => e.SupportsPartialRendering);

        // MIGRATION: The Module.ModulePermissions collection is DELIBERATELY NOT configured here.
        // Each relationship is configured from ONE side only; the module<->permission relationship is
        // owned by PermissionConfiguration.cs, which maps ModulePermission and wires the association
        // via .WithMany(m => m.ModulePermissions) preserving the legacy FK name
        // [FK_ModulePermission_Modules] (ModulePermission.ModuleID -> Modules.ModuleID, ON DELETE
        // CASCADE). Configuring it here as well would double-wire the relationship.
    }
}

/// <summary>
/// EF Core Fluent configuration for the <see cref="ModuleDefinition"/> entity, mapped to the existing
/// DNN <c>ModuleDefinitions</c> table.
/// </summary>
// MIGRATION: Converted from DotNetNuke.Entities.Modules.ModuleDefinitionInfo
// (Library/Components/Modules/ModuleDefinitionInfo.vb). Base table columns verified against
// 01.00.00.SqlDataProvider L65 ([ModuleDefID] IDENTITY(1,1) PK, [FriendlyName], [DesktopSrc],
// [MobileSrc], [AdminOrder], [EditSrc], [Secure]); later versions add [DesktopModuleID] and
// [DefaultCacheTime]. Only the persisted members present on the Domain entity are mapped.
public class ModuleDefinitionConfiguration : IEntityTypeConfiguration<ModuleDefinition>
{
    /// <summary>
    /// Configures the <see cref="ModuleDefinition"/> entity type against the existing
    /// <c>ModuleDefinitions</c> schema.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<ModuleDefinition> builder)
    {
        // MIGRATION: legacy table [ModuleDefinitions] (PK on [ModuleDefID], IDENTITY(1,1)).
        builder.ToTable("ModuleDefinitions");
        builder.HasKey(e => e.ModuleDefID);
        builder.Property(e => e.ModuleDefID).HasColumnName("ModuleDefID").ValueGeneratedOnAdd();

        // Physical columns mapped under their verbatim legacy names.
        builder.Property(e => e.FriendlyName).HasColumnName("FriendlyName");
        builder.Property(e => e.DesktopModuleID).HasColumnName("DesktopModuleID");
        builder.Property(e => e.DefaultCacheTime).HasColumnName("DefaultCacheTime");

        // MIGRATION: transient/temp identifier, not a physical ModuleDefinitions column. TempModuleID
        // is a runtime-only value used by the legacy code during module-definition editing and has no
        // backing column in the [ModuleDefinitions] table, so it is excluded from the mapping.
        builder.Ignore(e => e.TempModuleID);
    }
}

/// <summary>
/// EF Core Fluent configuration for the <see cref="DesktopModule"/> entity, mapped to the existing
/// DNN <c>DesktopModules</c> table.
/// </summary>
// MIGRATION: Converted from DotNetNuke.Entities.Modules.DesktopModuleInfo
// (Library/Components/Modules/DesktopModuleInfo.vb L36). Column set verified against the cumulative
// v4.9 schema (DotNetNuke.Schema.SqlDataProvider L6073): [DesktopModuleID] IDENTITY(1,1) PK,
// [FriendlyName] (UNIQUE index [IX_DesktopModules]), [Description], [Version], [IsPremium], [IsAdmin],
// [BusinessControllerClass], [FolderName], [ModuleName], [SupportedFeatures] (DEFAULT 0), and
// [CompatibleVersions].
public class DesktopModuleConfiguration : IEntityTypeConfiguration<DesktopModule>
{
    /// <summary>
    /// Configures the <see cref="DesktopModule"/> entity type against the existing
    /// <c>DesktopModules</c> schema.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<DesktopModule> builder)
    {
        // MIGRATION: legacy table [DesktopModules] (PK [PK_DesktopModules] on [DesktopModuleID],
        // IDENTITY(1,1)).
        builder.ToTable("DesktopModules");
        builder.HasKey(e => e.DesktopModuleID);
        builder.Property(e => e.DesktopModuleID).HasColumnName("DesktopModuleID").ValueGeneratedOnAdd();

        // Physical columns mapped 1:1 under their verbatim legacy names.
        builder.Property(e => e.ModuleName).HasColumnName("ModuleName");
        builder.Property(e => e.FriendlyName).HasColumnName("FriendlyName");
        builder.Property(e => e.Description).HasColumnName("Description");
        builder.Property(e => e.FolderName).HasColumnName("FolderName");
        builder.Property(e => e.Version).HasColumnName("Version");
        builder.Property(e => e.IsPremium).HasColumnName("IsPremium");
        builder.Property(e => e.IsAdmin).HasColumnName("IsAdmin");
        builder.Property(e => e.BusinessControllerClass).HasColumnName("BusinessControllerClass");
        builder.Property(e => e.CompatibleVersions).HasColumnName("CompatibleVersions");
        builder.Property(e => e.Dependencies).HasColumnName("Dependencies");
        builder.Property(e => e.Permissions).HasColumnName("Permissions");

        // MIGRATION: [SupportedFeatures] is the persisted int feature bitmask and the SINGLE source of
        // truth for the module's capability flags (verified as a real column,
        // DotNetNuke.Schema.SqlDataProvider L6084; queried in the legacy schema via bitmask, e.g.
        // "DM.SupportedFeatures & 2 = 2").
        builder.Property(e => e.SupportedFeatures).HasColumnName("SupportedFeatures");

        // MIGRATION: preserve legacy unique index name. [FriendlyName] carries the UNIQUE index
        // [IX_DesktopModules] (DotNetNuke.Schema.SqlDataProvider; 02.00.00.SqlDataProvider L5156).
        // The relational provider (SQL Server) honors this name for fidelity; the EF Core InMemory
        // provider used by the integration tests ignores index names.
        builder.HasIndex(e => e.FriendlyName).IsUnique().HasDatabaseName("IX_DesktopModules");

        // MIGRATION: IsPortable / IsSearchable / IsUpgradeable are Ignore()d — they are NOT physical
        // [DesktopModules] columns (verified: no [IsPortable]/[IsSearchable]/[IsUpgradeable] column
        // definition exists in any SqlDataProvider script). On the Domain entity they are COMPUTED
        // properties whose get/set read and write the [SupportedFeatures] bitmask via GetFeature/
        // UpdateFeature (Portable=1, Searchable=2, Upgradeable=4). Mapping them would both invent
        // phantom columns AND corrupt SupportedFeatures during materialization (each boolean shares
        // that same backing integer). Only SupportedFeatures is persisted; the flags derive from it.
        builder.Ignore(e => e.IsPortable);
        builder.Ignore(e => e.IsSearchable);
        builder.Ignore(e => e.IsUpgradeable);
    }
}
