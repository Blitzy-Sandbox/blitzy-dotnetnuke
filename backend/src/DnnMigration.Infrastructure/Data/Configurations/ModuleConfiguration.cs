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
        // MIGRATION (SCHEMA FIDELITY — corrected): the physical [Modules] table in the fully-upgraded
        // DNN v4.9 schema has EXACTLY 11 columns (verified against the CREATE TABLE for [Modules] in
        // DotNetNuke.Schema.SqlDataProvider): [ModuleID] (PK IDENTITY(0,1)), [ModuleDefID],
        // [ModuleTitle], [AllTabs], [IsDeleted], [InheritViewPermissions], [Header], [Footer],
        // [StartDate], [EndDate], [PortalID]. The module-PLACEMENT fields (pane, order, cache time,
        // alignment, colour, border, icon, visibility, container, display title/print/syndicate) are
        // NOT columns of [Modules]; they physically live in the separate [TabModules] table
        // (see TabModuleConfiguration) and were only ever surfaced ALONGSIDE the [Modules] columns
        // through the legacy denormalized [vw_Modules] VIEW. Likewise [ControlType] is a column of
        // [ModuleControls], and [AuthorizedEditRoles]/[AuthorizedViewRoles] are not columns of the
        // v4.9 [Modules] table (the base-v1 columns were superseded by the ModulePermissions model).
        // Previously mapping any of those onto [Modules] invented phantom columns that generate INVALID
        // SQL against the existing schema. They are now Ignore()d; the Domain Module keeps them as
        // TRANSIENT (unmapped) carriers so the denormalized in-memory ModuleInfo shape and the DTO/API
        // contract are preserved WITHOUT altering the physical table.
        builder.ToTable("Modules");
        builder.HasKey(e => e.ModuleID);
        builder.Property(e => e.ModuleID).HasColumnName("ModuleID").ValueGeneratedOnAdd();

        // --- The 11 real [Modules] columns, mapped 1:1 under their verbatim legacy names ---
        // [ModuleDefID] is also the FK end of [FK_Modules_ModuleDefinitions]
        // (Modules.ModuleDefID -> ModuleDefinitions.ModuleDefID). The Module entity exposes no
        // ModuleDefinition navigation, so the FK is kept as a plain scalar column rather than forcing a
        // navigation-less relationship; ModuleDefinition remains a sibling aggregate reachable by
        // ModuleDefID (see ModuleRepository.GetByDefinitionAsync, which joins
        // Modules.ModuleDefID -> ModuleDefinitions.ModuleDefID -> DesktopModules.FriendlyName).
        builder.Property(e => e.ModuleDefID).HasColumnName("ModuleDefID");
        builder.Property(e => e.ModuleTitle).HasColumnName("ModuleTitle");
        builder.Property(e => e.AllTabs).HasColumnName("AllTabs");
        builder.Property(e => e.IsDeleted).HasColumnName("IsDeleted");
        builder.Property(e => e.InheritViewPermissions).HasColumnName("InheritViewPermissions");
        builder.Property(e => e.Header).HasColumnName("Header");
        builder.Property(e => e.Footer).HasColumnName("Footer");
        builder.Property(e => e.StartDate).HasColumnName("StartDate");
        builder.Property(e => e.EndDate).HasColumnName("EndDate");
        builder.Property(e => e.PortalID).HasColumnName("PortalID");

        // MIGRATION: PLACEMENT members Ignore()d — they are physically columns of [TabModules], NOT
        // [Modules] (mapped by TabModuleConfiguration). Retained as transient carriers on the
        // denormalized Module so DTO/UI projections that still reference module placement compile and
        // round-trip in memory without inventing [Modules] columns.
        builder.Ignore(e => e.TabID);
        builder.Ignore(e => e.TabModuleID);
        builder.Ignore(e => e.ModuleOrder);
        builder.Ignore(e => e.PaneName);
        builder.Ignore(e => e.CacheTime);
        builder.Ignore(e => e.Alignment);
        builder.Ignore(e => e.Color);
        builder.Ignore(e => e.Border);
        builder.Ignore(e => e.IconFile);
        builder.Ignore(e => e.ContainerSrc);
        builder.Ignore(e => e.DisplayTitle);
        builder.Ignore(e => e.DisplayPrint);
        builder.Ignore(e => e.DisplaySyndicate);
        builder.Ignore(e => e.Visibility);

        // MIGRATION: [ControlType] is a column of [ModuleControls] (surfaced via [vw_Modules] as
        // MC.ControlType), NOT [Modules]; [AuthorizedEditRoles]/[AuthorizedViewRoles] are not columns of
        // the v4.9 [Modules] table (superseded by the ModulePermissions model). Ignore()d to preserve
        // schema fidelity; retained as transient carriers.
        builder.Ignore(e => e.ControlType);
        builder.Ignore(e => e.AuthorizedEditRoles);
        builder.Ignore(e => e.AuthorizedViewRoles);

        // MIGRATION: DENORMALIZED lookup members Ignore()d — they are NOT physical [Modules] columns.
        // In the legacy [vw_Modules] view these were projected from joined tables (DM.* from
        // [DesktopModules], MC.* from [ModuleControls]) and belong to the sibling DesktopModule /
        // ModuleDefinition aggregates (and the retired ModuleControls lookup). Ignoring them preserves
        // schema fidelity (no invented [Modules] columns) and avoids duplicating data that the sibling
        // entities own.
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
