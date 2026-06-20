using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

// =============================================================================================
// MIGRATION: ModuleConfiguration — EF Core 8 Fluent API mapping for the DotNetNuke 4.9.0.85
// module aggregate. This single class is the SINGLE HOME for mapping the three module POCO
// entities — Module, DesktopModule and ModuleDefinition — onto the pre-existing, UNCHANGED DNN
// tables dbo.Modules, dbo.DesktopModules and dbo.ModuleDefinitions. It is auto-discovered by
// DnnDbContext.OnModelCreating via modelBuilder.ApplyConfigurationsFromAssembly(...). It replaces
// the legacy ADO.NET / SqlDataProvider stored-procedure layer (AddModule / UpdateModule / GetModule,
// AddTabModule, AddDesktopModule, AddModuleDefinition and their Get*/Update* counterparts in
// Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb) together with the
// reflection-based CBO.FillObject hydration of the legacy ModuleInfo / DesktopModuleInfo /
// ModuleDefinitionInfo value objects.
//
// ---------------------------------------------------------------------------------------------
// DEVIATION 1 (DEV-031 — CP2 schema-fidelity correction) — denormalized Module members IGNORED.
//   The legacy ModuleInfo is a FAT, denormalized value object that DNN hydrated from a JOIN across
//   five tables: Modules + TabModules + ModuleControls + DesktopModules + ModuleDefinitions. The
//   C# Module entity preserves that public contract verbatim, so only a SUBSET of its properties
//   are physical [Modules] columns (the 11 listed below). The remaining properties originate from
//   the joined tables and have NO physical column on [Modules]. Per ADR-002 they are Ignore()d —
//   NOT mapped (nor carried) as scalar columns — so EF never references nonexistent [Modules]
//   columns (the prior scalar mapping violated ADR-002 and would fail against SQL Server). The CLR
//   properties remain on the Module entity for DTO/AutoMapper projection; their values are populated
//   from the TabModules / ModuleControls / DesktopModules / ModuleDefinitions source tables by the
//   repository/service layer (joins/projections or a keyless query type) in a later checkpoint.
//
// DEVIATION 2 — Module.IsDeleted soft-delete flag PRESERVED. [IsDeleted] is a real bit NOT NULL
//   column on [Modules]; it is mapped (NEVER Ignored) because the ModuleService / ModuleRepository
//   list query filters on it rather than physically removing rows, faithful to the legacy model.
//
// DEVIATION 3 (DEV-031 — CP2 schema-fidelity correction) — Module.Visibility is a denormalized
//   [TabModules] field, NOT a physical [Modules] column, so per ADR-002 it is Ignore()d along with
//   the rest of the TabModules group (the prior explicit HasConversion<int>() scalar mapping is
//   removed). The VisibilityState enum values remain preserved verbatim in the Domain enum
//   (Maximized=0, Minimized=1, None=2); when module placement is later projected from the
//   [TabModules] source table, the enum<->int conversion is applied at that projection site.
//
// DEVIATION 4 — Module (principal) -> ModulePermissions (dependent) relationship is declared HERE,
//   on the principal side. ModulePermission is configured as an independent root entity (its own
//   table + identity PK) in PermissionConfiguration (via HasBaseType((Type?)null)); EF merges the two
//   configurations. WithOne() declares no inverse navigation (ModulePermission exposes none); ModuleID
//   is the FK on ModulePermission. OnDelete(Cascade) mirrors the legacy ModuleController transactional
//   hard-delete, which removes a module's permission rows when the module itself is deleted.
//
// SCHEMA FIDELITY / InMemory SAFETY (ADR-002): the existing schema is mapped UNCHANGED — no EF
// migrations, no schema generation, no EnsureCreated, no data migration. NO relational-only
// constructs are configured (no HasDefaultValueSql, no HasComputedColumnSql, no HasColumnType, no
// raw SQL), and integer identity primary keys are left at the EF ValueGeneratedOnAdd convention
// (ValueGeneratedNever() is intentionally NOT called) so the InMemory provider can synthesize keys
// on insert and the Module CRUD POST->201 path round-trips. These deviations are also recorded in
// the root MIGRATION_NOTES.md; the // MIGRATION comments in this file are their authoritative source.
// =============================================================================================

/// <summary>
/// Entity Framework Core 8 Fluent API configuration that maps the DotNetNuke module aggregate —
/// the <see cref="Module"/> aggregate root together with the <see cref="DesktopModule"/> and
/// <see cref="ModuleDefinition"/> catalog entities — onto the pre-existing, UNCHANGED DotNetNuke
/// <c>4.9.0.85</c> <c>[dbo].[Modules]</c>, <c>[dbo].[DesktopModules]</c> and
/// <c>[dbo].[ModuleDefinitions]</c> tables.
/// </summary>
/// <remarks>
/// <para>
/// A single class intentionally implements three <see cref="IEntityTypeConfiguration{TEntity}"/>
/// contracts; the three <c>Configure</c> methods are distinct overloads (different builder types)
/// and the assembly scan performed by <c>DnnDbContext.OnModelCreating</c> applies all three. The
/// compiler-generated public parameterless constructor lets EF instantiate the class.
/// </para>
/// <para>
/// ADR-002 — the schema is mapped UNCHANGED: no EF migrations, no schema generation, no
/// <c>EnsureCreated</c>, and no data migration. To keep the model fully compatible with the
/// <c>Microsoft.EntityFrameworkCore.InMemory</c> provider used by the integration-test suite
/// (Gate 5), NO relational-only constructs are configured (no <c>HasDefaultValueSql</c>, no
/// <c>HasComputedColumnSql</c>, no <c>HasColumnType</c>, no raw SQL). Integer identity primary keys
/// are left at the EF convention default (<c>ValueGeneratedOnAdd</c>) so the provider can synthesize
/// keys on insert. See the file-level <c>// MIGRATION</c> banner for the four documented deviations.
/// </para>
/// </remarks>
public sealed class ModuleConfiguration :
    IEntityTypeConfiguration<Module>,
    IEntityTypeConfiguration<DesktopModule>,
    IEntityTypeConfiguration<ModuleDefinition>
{
    /// <summary>
    /// Maps the <see cref="Module"/> aggregate root onto the existing DotNetNuke
    /// <c>[dbo].[Modules]</c> table (PK <c>ModuleID</c>). Only the 11 properties that physically
    /// exist as <c>[Modules]</c> columns are mapped; the remaining denormalized properties (sourced
    /// in legacy from <c>TabModules</c>, <c>ModuleControls</c>, <c>DesktopModules</c> and
    /// <c>ModuleDefinitions</c>) are <see cref="EntityTypeBuilder{TEntity}.Ignore"/>d per the DEV-031
    /// CP2 schema-fidelity correction (ADR-002) so EF never references nonexistent columns, and the
    /// principal side of the <c>Module</c>→<c>ModulePermission</c> relationship is declared here.
    /// See the file-level <c>// MIGRATION</c> banner for the rationale.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="Module"/>.</param>
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        // Map onto the pre-existing, unchanged DNN 4.9 table (ADR-002). Default install
        // qualification is {databaseOwner}[{objectQualifier}Modules]; the dbo schema is used here.
        builder.ToTable("Modules", "dbo");

        // PK [ModuleID] (int NOT NULL IDENTITY(0,1)). Key generation is left at the EF convention
        // default (ValueGeneratedOnAdd) — NOT ValueGeneratedNever() — so the InMemory provider can
        // auto-generate keys on insert, enabling the Module POST->201 round-trip (Gate 5).
        builder.HasKey(m => m.ModuleID);

        // ---- Real physical [Modules] columns (the ONLY 11 columns on this table; verbatim DDL
        // names, so no HasColumnName is required). Default EF type mapping is used throughout to keep
        // the model InMemory-safe (no HasColumnType / HasDefaultValueSql / raw SQL). ----
        builder.Property(m => m.ModuleID);                 // [ModuleID]               int            NOT NULL IDENTITY(0,1)
        builder.Property(m => m.ModuleDefID);              // [ModuleDefID]            int            NOT NULL
        builder.Property(m => m.ModuleTitle);              // [ModuleTitle]            nvarchar(256)  NULL

        builder.Property(m => m.AllTabs);                  // [AllTabs]                bit            NOT NULL (DEFAULT 0)

        // MIGRATION (Deviation 2): [IsDeleted] is a real bit NOT NULL soft-delete column — MAPPED
        // (never Ignored). The module list query filters on this flag instead of physically deleting.
        builder.Property(m => m.IsDeleted);                // [IsDeleted]              bit            NOT NULL (DEFAULT 0)

        builder.Property(m => m.InheritViewPermissions);   // [InheritViewPermissions] bit           NULL
        builder.Property(m => m.Header);                   // [Header]                 ntext          NULL
        builder.Property(m => m.Footer);                   // [Footer]                 ntext          NULL
        builder.Property(m => m.StartDate);                // [StartDate]              datetime       NULL  (DateTime?)
        builder.Property(m => m.EndDate);                  // [EndDate]                datetime       NULL  (DateTime?)
        builder.Property(m => m.PortalID);                 // [PortalID]               int            NULL

        // MIGRATION (Deviation 1 — DEV-031 CP2 schema-fidelity correction) — denormalized members
        // sourced from the legacy [TabModules] table. ModuleInfo is a denormalized merge of
        // Modules+TabModules+ModuleControls+DesktopModules+ModuleDefinitions. These properties have NO
        // physical column on the [Modules] table, so per ADR-002 they are Ignore()d — NOT carried as
        // scalar columns — so EF never references nonexistent [Modules] columns. The CLR properties
        // remain for projection and are populated from [TabModules] by the repository/service layer in a
        // later checkpoint. Visibility (Deviation 3) is part of this group: its prior explicit
        // HasConversion<int>() is removed with it; the enum<->int conversion is applied at the future
        // [TabModules] projection site (the VisibilityState enum values remain preserved in the Domain).
        builder.Ignore(m => m.TabModuleID);              // no [Modules] column (source: [TabModules].TabModuleID)
        builder.Ignore(m => m.TabID);                    // no [Modules] column (source: [TabModules].TabID)
        builder.Ignore(m => m.PaneName);                 // no [Modules] column (source: [TabModules].PaneName)
        builder.Ignore(m => m.ModuleOrder);              // no [Modules] column (source: [TabModules].ModuleOrder)
        builder.Ignore(m => m.CacheTime);                // no [Modules] column (source: [TabModules].CacheTime)
        builder.Ignore(m => m.Alignment);                // no [Modules] column (source: [TabModules].Alignment)
        builder.Ignore(m => m.Color);                    // no [Modules] column (source: [TabModules].Color)
        builder.Ignore(m => m.Border);                   // no [Modules] column (source: [TabModules].Border)
        builder.Ignore(m => m.IconFile);                 // no [Modules] column (source: [TabModules].IconFile)
        builder.Ignore(m => m.Visibility);               // no [Modules] column (source: [TabModules].Visibility; enum<->int at projection)
        builder.Ignore(m => m.ContainerSrc);             // no [Modules] column (source: [TabModules].ContainerSrc)
        builder.Ignore(m => m.DisplayTitle);             // no [Modules] column (source: [TabModules].DisplayTitle)
        builder.Ignore(m => m.DisplayPrint);             // no [Modules] column (source: [TabModules].DisplayPrint)
        builder.Ignore(m => m.DisplaySyndicate);         // no [Modules] column (source: [TabModules].DisplaySyndicate)

        // MIGRATION (Deviation 1 — DEV-031 CP2 schema-fidelity correction) — denormalized members
        // sourced from the legacy [ModuleControls] table. Per ADR-002 they are Ignore()d — NOT carried
        // as [Modules] scalar columns — so EF never references nonexistent [Modules] columns. The CLR
        // properties remain for projection and are populated from [ModuleControls] by the
        // repository/service layer in a later checkpoint. SupportsPartialRendering is a ModuleControlInfo
        // member with no physical column in the 4.9 baseline; it is Ignore()d like the rest of the group.
        builder.Ignore(m => m.ModuleControlId);          // no [Modules] column (source: [ModuleControls].ModuleControlID)
        builder.Ignore(m => m.ControlSrc);               // no [Modules] column (source: [ModuleControls].ControlSrc)
        builder.Ignore(m => m.ControlType);              // no [Modules] column (source: [ModuleControls].ControlType)
        builder.Ignore(m => m.ControlTitle);             // no [Modules] column (source: [ModuleControls].ControlTitle)
        builder.Ignore(m => m.HelpUrl);                  // no [Modules] column (source: [ModuleControls].HelpUrl)
        builder.Ignore(m => m.SupportsPartialRendering); // no [Modules] column (ModuleControlInfo member)

        // MIGRATION (Deviation 1 — DEV-031 CP2 schema-fidelity correction) — denormalized members
        // sourced from the legacy [DesktopModules] / [ModuleDefinitions] tables. Per ADR-002 they are
        // Ignore()d — NOT carried as [Modules] scalar columns — so EF never references nonexistent
        // [Modules] columns. The CLR properties remain for projection and are populated from
        // [DesktopModules]/[ModuleDefinitions] by the repository/service layer in a later checkpoint.
        // DesktopModuleID is also the join key to the DesktopModule catalog entity, but no navigation
        // exists on the denormalized Module, so it is Ignore()d here like the rest of the group.
        builder.Ignore(m => m.DesktopModuleID);          // no [Modules] column (source: [DesktopModules].DesktopModuleID)
        builder.Ignore(m => m.FriendlyName);             // no [Modules] column (source: [DesktopModules].FriendlyName)
        builder.Ignore(m => m.FolderName);               // no [Modules] column (source: [DesktopModules].FolderName)
        builder.Ignore(m => m.Description);              // no [Modules] column (source: [DesktopModules].Description)
        builder.Ignore(m => m.Version);                  // no [Modules] column (source: [DesktopModules].Version)
        builder.Ignore(m => m.IsPremium);                // no [Modules] column (source: [DesktopModules].IsPremium)
        builder.Ignore(m => m.IsAdmin);                  // no [Modules] column (source: [DesktopModules].IsAdmin)
        builder.Ignore(m => m.BusinessControllerClass);  // no [Modules] column (source: [DesktopModules].BusinessControllerClass)
        builder.Ignore(m => m.ModuleName);               // no [Modules] column (source: [DesktopModules].ModuleName)
        builder.Ignore(m => m.SupportedFeatures);        // no [Modules] column (source: [DesktopModules].SupportedFeatures)

        // MIGRATION (Deviation 4): Module (principal) -> ModulePermissions (dependent). ModulePermission
        // is configured as an independent root entity in PermissionConfiguration (HasBaseType((Type?)null));
        // EF merges the two configurations. WithOne() declares no inverse navigation (ModulePermission
        // exposes none); [ModuleID] is the FK on ModulePermission. Cascade delete mirrors the legacy
        // ModuleController transactional hard-delete that removes a module's permission rows with it
        // (InMemory ignores delete behavior, so this is safe for the gates).
        builder.HasMany(m => m.ModulePermissions)
               .WithOne()
               .HasForeignKey(mp => mp.ModuleID)
               .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// Maps the <see cref="DesktopModule"/> catalog entity onto the existing DotNetNuke
    /// <c>[dbo].[DesktopModules]</c> table (PK <c>DesktopModuleID</c>). The 11 properties that
    /// physically exist as columns are mapped verbatim; five additional members
    /// (<c>IsUpgradeable</c>, <c>IsPortable</c>, <c>IsSearchable</c>, <c>Dependencies</c> and
    /// <c>Permissions</c>) are <see cref="EntityTypeBuilder{TEntity}.Ignore"/>d per the DEV-031 CP2
    /// schema-fidelity correction because they have no physical <c>[DesktopModules]</c> column
    /// (ADR-002); they are populated from <c>SupportedFeatures</c> / projection by the service layer.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="DesktopModule"/>.</param>
    public void Configure(EntityTypeBuilder<DesktopModule> builder)
    {
        // Map onto the pre-existing, unchanged DNN 4.9 table (ADR-002).
        builder.ToTable("DesktopModules", "dbo");

        // PK [DesktopModuleID] (int NOT NULL IDENTITY(1,1)); ValueGeneratedOnAdd convention retained so
        // the InMemory provider can synthesize keys on insert.
        builder.HasKey(dm => dm.DesktopModuleID);

        // ---- Real physical [DesktopModules] columns (verbatim DDL names; default type mapping) ----
        builder.Property(dm => dm.DesktopModuleID);          // [DesktopModuleID]         int            NOT NULL IDENTITY(1,1)
        builder.Property(dm => dm.FriendlyName);             // [FriendlyName]            nvarchar(128)  NOT NULL
        builder.Property(dm => dm.Description);              // [Description]             nvarchar(2000) NULL
        builder.Property(dm => dm.Version);                  // [Version]                 nvarchar(8)    NULL
        builder.Property(dm => dm.IsPremium);                // [IsPremium]               bit            NOT NULL
        builder.Property(dm => dm.IsAdmin);                  // [IsAdmin]                 bit            NOT NULL
        builder.Property(dm => dm.BusinessControllerClass);  // [BusinessControllerClass] nvarchar(200)  NULL
        builder.Property(dm => dm.FolderName);               // [FolderName]              nvarchar(128)  NOT NULL
        builder.Property(dm => dm.ModuleName);               // [ModuleName]              nvarchar(128)  NOT NULL
        builder.Property(dm => dm.SupportedFeatures);        // [SupportedFeatures]       int            NOT NULL (DEFAULT 0)
        builder.Property(dm => dm.CompatibleVersions);       // [CompatibleVersions]      nvarchar(500)  NULL

        // MIGRATION (Deviation 1 — DEV-031 CP2 schema-fidelity correction): IsUpgradeable/IsPortable/
        // IsSearchable are DERIVED at runtime from the SupportedFeatures bit-flag (legacy
        // DesktopModuleSupportedFeature: Portable=1, Searchable=2, Upgradeable=4) via GetFeature/
        // UpdateFeature; Dependencies/Permissions are ABSENT from the 4.9 DesktopModules baseline schema
        // (they are AddDesktopModule proc parameters with no backing column). Per ADR-002 all five are
        // Ignore()d — NOT mapped as [DesktopModules] columns — so EF never queries/inserts nonexistent
        // columns. The CLR properties remain for projection; IsUpgradeable/IsPortable/IsSearchable are
        // computed from SupportedFeatures and Dependencies/Permissions are populated by the service layer.
        builder.Ignore(dm => dm.IsUpgradeable);            // derived from SupportedFeatures bitmask (no physical column)
        builder.Ignore(dm => dm.IsPortable);               // derived from SupportedFeatures bitmask (no physical column)
        builder.Ignore(dm => dm.IsSearchable);             // derived from SupportedFeatures bitmask (no physical column)
        builder.Ignore(dm => dm.Dependencies);             // absent from 4.9 baseline (no physical column)
        builder.Ignore(dm => dm.Permissions);              // absent from 4.9 baseline (no physical column)
    }

    /// <summary>
    /// Maps the <see cref="ModuleDefinition"/> entity onto the existing DotNetNuke
    /// <c>[dbo].[ModuleDefinitions]</c> table (PK <c>ModuleDefID</c>). The four physical columns are
    /// mapped verbatim; the runtime-only <c>TempModuleID</c> identifier is
    /// <see cref="EntityTypeBuilder{TEntity}.Ignore"/>d per the DEV-031 CP2 schema-fidelity correction
    /// because it has no physical <c>[ModuleDefinitions]</c> column (ADR-002).
    /// <c>DesktopModuleID</c> is a plain scalar foreign key — no navigation property exists between
    /// <see cref="ModuleDefinition"/> and <see cref="DesktopModule"/>, so no relationship is configured.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="ModuleDefinition"/>.</param>
    public void Configure(EntityTypeBuilder<ModuleDefinition> builder)
    {
        // Map onto the pre-existing, unchanged DNN 4.9 table (ADR-002).
        builder.ToTable("ModuleDefinitions", "dbo");

        // PK [ModuleDefID] (int NOT NULL IDENTITY(1,1)); ValueGeneratedOnAdd convention retained.
        builder.HasKey(md => md.ModuleDefID);

        // ---- Real physical [ModuleDefinitions] columns (verbatim DDL names; default type mapping) ----
        builder.Property(md => md.ModuleDefID);        // [ModuleDefID]      int            NOT NULL IDENTITY(1,1)
        builder.Property(md => md.FriendlyName);       // [FriendlyName]     nvarchar(128)  NOT NULL

        // MIGRATION: [DesktopModuleID] is a scalar FK to [DesktopModules]. No navigation property exists
        // between ModuleDefinition and DesktopModule (faithful to the legacy ModuleDefinitionInfo value
        // object), so NO EF relationship is configured — it remains a plain scalar int column.
        builder.Property(md => md.DesktopModuleID);    // [DesktopModuleID]  int            NOT NULL (scalar FK)

        builder.Property(md => md.DefaultCacheTime);   // [DefaultCacheTime] int            NOT NULL (DEFAULT 0)

        // MIGRATION (DEV-031 CP2 schema-fidelity correction): TempModuleID is a runtime-only transient
        // identifier used during install/import flows in the legacy ModuleDefinitionInfo; it is NOT a
        // physical [ModuleDefinitions] column. Per ADR-002 it is Ignore()d — NOT mapped as a column — so
        // EF never references a nonexistent column. The CLR property remains for transient runtime use.
        builder.Ignore(md => md.TempModuleID);         // runtime-only temp identifier (no physical column)
    }
}
