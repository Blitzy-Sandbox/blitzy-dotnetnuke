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
// DEVIATION 1 — denormalized Module entity mapped with join-sourced members carried as scalars.
//   The legacy ModuleInfo is a FAT, denormalized value object that DNN hydrated from a JOIN across
//   five tables: Modules + TabModules + ModuleControls + DesktopModules + ModuleDefinitions. The
//   C# Module entity preserves that public contract verbatim, so only a SUBSET of its properties
//   are physical [Modules] columns (the 11 listed below). The remaining properties originate from
//   the joined tables; they have NO physical column on [Modules]. Per the AAP they are CARRIED as
//   ordinary mapped scalar properties (NOT Ignored, NOT mapped to non-existent columns specially)
//   so that the full denormalized Module object round-trips intact under the
//   Microsoft.EntityFrameworkCore.InMemory provider used by the integration-test suite (Gate 5).
//   This mirrors the established Deviation-4 precedent in PermissionConfiguration, where non-physical
//   junction members (RoleName / Username / DisplayName) are likewise carried as scalars.
//
// DEVIATION 2 — Module.IsDeleted soft-delete flag PRESERVED. [IsDeleted] is a real bit NOT NULL
//   column on [Modules]; it is mapped (NEVER Ignored) because the ModuleService / ModuleRepository
//   list query filters on it rather than physically removing rows, faithful to the legacy model.
//
// DEVIATION 3 — Module.Visibility (VisibilityState enum) is persisted as its underlying int.
//   EF Core maps enum properties to int by convention; an explicit HasConversion<int>() is declared
//   for clarity. The enum values are preserved verbatim from the legacy ModuleInfo.VisibilityState
//   (Maximized=0, Minimized=1, None=2) so persisted/compared integer values remain valid. InMemory-safe.
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
    /// exist as <c>[Modules]</c> columns are "real" columns; the remaining denormalized properties
    /// (sourced in legacy from <c>TabModules</c>, <c>ModuleControls</c>, <c>DesktopModules</c> and
    /// <c>ModuleDefinitions</c>) are carried as ordinary mapped scalars for Phase-1 round-trip
    /// fidelity, and the principal side of the <c>Module</c>→<c>ModulePermission</c> relationship is
    /// declared here. See the file-level <c>// MIGRATION</c> banner for the rationale.
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

        // MIGRATION (Deviation 1) — denormalized members sourced from the legacy [TabModules] table.
        // ModuleInfo is a denormalized merge of Modules+TabModules+ModuleControls+DesktopModules+
        // ModuleDefinitions. These properties have no physical column on the Modules table; they are
        // mapped as scalar properties for Phase-1 round-trip fidelity (InMemory-safe, no schema change
        // per ADR-002). Recorded in MIGRATION_NOTES.md.
        builder.Property(m => m.TabModuleID);              // legacy [TabModules].TabModuleID
        builder.Property(m => m.TabID);                    // legacy [TabModules].TabID
        builder.Property(m => m.PaneName);                 // legacy [TabModules].PaneName
        builder.Property(m => m.ModuleOrder);              // legacy [TabModules].ModuleOrder
        builder.Property(m => m.CacheTime);                // legacy [TabModules].CacheTime
        builder.Property(m => m.Alignment);                // legacy [TabModules].Alignment
        builder.Property(m => m.Color);                    // legacy [TabModules].Color
        builder.Property(m => m.Border);                   // legacy [TabModules].Border
        builder.Property(m => m.IconFile);                 // legacy [TabModules].IconFile

        // MIGRATION (Deviation 3): Visibility is the VisibilityState enum (Maximized=0, Minimized=1,
        // None=2 — values preserved verbatim from legacy ModuleInfo). EF maps enums to their underlying
        // int by convention; HasConversion<int>() is declared explicitly for clarity and is InMemory-safe.
        builder.Property(m => m.Visibility).HasConversion<int>();  // legacy [TabModules].Visibility (int)

        builder.Property(m => m.ContainerSrc);             // legacy [TabModules].ContainerSrc
        builder.Property(m => m.DisplayTitle);             // legacy [TabModules].DisplayTitle
        builder.Property(m => m.DisplayPrint);             // legacy [TabModules].DisplayPrint
        builder.Property(m => m.DisplaySyndicate);         // legacy [TabModules].DisplaySyndicate

        // MIGRATION (Deviation 1) — denormalized members sourced from the legacy [ModuleControls]
        // table. Carried as scalars for Phase-1 round-trip fidelity (InMemory-safe, ADR-002). Note that
        // SupportsPartialRendering is a ModuleControlInfo member with no physical column in the 4.9
        // baseline; it is carried as a scalar like the rest of the group.
        builder.Property(m => m.ModuleControlId);          // legacy [ModuleControls].ModuleControlID
        builder.Property(m => m.ControlSrc);               // legacy [ModuleControls].ControlSrc
        builder.Property(m => m.ControlType);              // legacy [ModuleControls].ControlType (int)
        builder.Property(m => m.ControlTitle);             // legacy [ModuleControls].ControlTitle
        builder.Property(m => m.HelpUrl);                  // legacy [ModuleControls].HelpUrl
        builder.Property(m => m.SupportsPartialRendering); // legacy ModuleControlInfo.SupportsPartialRendering

        // MIGRATION (Deviation 1) — denormalized members sourced from the legacy [DesktopModules] /
        // [ModuleDefinitions] tables. Carried as scalars for Phase-1 round-trip fidelity (InMemory-safe,
        // ADR-002). DesktopModuleID is also the join key to the DesktopModule catalog entity, but is kept
        // a plain scalar here (no navigation exists on the denormalized Module).
        builder.Property(m => m.DesktopModuleID);          // legacy [DesktopModules].DesktopModuleID
        builder.Property(m => m.FriendlyName);             // legacy [DesktopModules].FriendlyName
        builder.Property(m => m.FolderName);               // legacy [DesktopModules].FolderName
        builder.Property(m => m.Description);              // legacy [DesktopModules].Description
        builder.Property(m => m.Version);                  // legacy [DesktopModules].Version
        builder.Property(m => m.IsPremium);                // legacy [DesktopModules].IsPremium
        builder.Property(m => m.IsAdmin);                  // legacy [DesktopModules].IsAdmin
        builder.Property(m => m.BusinessControllerClass);  // legacy [DesktopModules].BusinessControllerClass
        builder.Property(m => m.ModuleName);               // legacy [DesktopModules].ModuleName
        builder.Property(m => m.SupportedFeatures);        // legacy [DesktopModules].SupportedFeatures

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
    /// <c>Permissions</c>) are carried as scalars per the per-group <c>// MIGRATION</c> note.
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

        // MIGRATION (Deviation 1): IsUpgradeable/IsPortable/IsSearchable were DERIVED at runtime from the
        // SupportedFeatures bit-flag (legacy DesktopModuleSupportedFeature: Portable=1, Searchable=2,
        // Upgradeable=4) via GetFeature/UpdateFeature; Dependencies/Permissions are ABSENT from the 4.9
        // DesktopModules baseline schema (they are AddDesktopModule proc parameters with no backing
        // column). All five are carried as ordinary mapped scalars for Phase-1 round-trip fidelity; no
        // schema change (ADR-002, InMemory-safe). Recorded in MIGRATION_NOTES.md.
        builder.Property(dm => dm.IsUpgradeable);            // derived from SupportedFeatures bitmask (no physical column)
        builder.Property(dm => dm.IsPortable);               // derived from SupportedFeatures bitmask (no physical column)
        builder.Property(dm => dm.IsSearchable);             // derived from SupportedFeatures bitmask (no physical column)
        builder.Property(dm => dm.Dependencies);             // absent from 4.9 baseline (carried scalar)
        builder.Property(dm => dm.Permissions);              // absent from 4.9 baseline (carried scalar)
    }

    /// <summary>
    /// Maps the <see cref="ModuleDefinition"/> entity onto the existing DotNetNuke
    /// <c>[dbo].[ModuleDefinitions]</c> table (PK <c>ModuleDefID</c>). The four physical columns are
    /// mapped verbatim; the runtime-only <c>TempModuleID</c> identifier is carried as a scalar.
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

        // MIGRATION: TempModuleID is a runtime-only transient identifier used during install/import flows
        // in the legacy ModuleDefinitionInfo; it is NOT a physical [ModuleDefinitions] column. It is
        // carried as an ordinary mapped scalar (NOT Ignored) for Phase-1 round-trip fidelity (InMemory-safe,
        // no schema change per ADR-002). Recorded in MIGRATION_NOTES.md.
        builder.Property(md => md.TempModuleID);       // runtime-only temp identifier (no physical column)
    }
}
