// =============================================================================
// ModuleConfiguration.cs
// -----------------------------------------------------------------------------
// EF Core 8 Fluent API mapping for the Module aggregate trio — the Module
// aggregate root plus its DesktopModule and ModuleDefinition satellites — onto
// the EXISTING (unchanged) DotNetNuke 4.9.0.85 database schema. A single
// configuration class hosts all three Configure overloads because the three
// types form one tightly-coupled persistence concern: a placed module instance
// (Modules) and the desktop-module / module-definition catalog rows it derives
// from (DesktopModules / ModuleDefinitions).
//
// MIGRATION: This configuration replaces the legacy ADO.NET / SqlDataProvider
// stored-procedure data layer (AddModule / UpdateModule / GetModule,
// AddTabModule, AddDesktopModule, AddModuleDefinition in
// Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb) and the
// reflection-based CBO.FillObject hydration used by ModuleController.vb. The
// Module / DesktopModule / ModuleDefinition POCO entities
// (DnnMigration.Domain.Entities) carry ZERO EF attributes; ALL persistence
// mapping lives here and is auto-discovered by DnnDbContext.OnModelCreating via
// modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly).
//
// MIGRATION (denormalized fat object): the legacy ModuleInfo is a FAT,
// DENORMALIZED object that DNN hydrated from a JOIN across
// Modules + TabModules + ModuleControls + DesktopModules + ModuleDefinitions.
// Only 11 of the Module entity's properties are physical columns on the
// dbo.Modules table (enumerated in Configure(Module)). The remaining properties
// (sourced from TabModules / ModuleControls / DesktopModules / ModuleDefinitions)
// are NOT physical Modules columns, so they are Ignored (builder.Ignore) per
// ADR-002 schema fidelity — mapping them would make SQL Server CRUD issue
// SELECT/INSERT/UPDATE against columns that do not exist on dbo.Modules. The
// join-sourced data lives on its own tables (TabModules / ModuleControls /
// DesktopModules / ModuleDefinitions); the repository/DTO projection layer (CP3)
// rehydrates those fields from explicit joins when a placed-module view is
// required. Every deviation is recorded in the root MIGRATION_NOTES.md §4.2.
//
// ADR-002 (schema preservation): the schema is mapped UNCHANGED — no EF
// migrations, no schema generation, no SQL-Server-only defaults, no data
// migration. Table and column names are reproduced verbatim from the baseline
// install DDL (Website/Providers/DataProviders/SqlDataProvider/
// DotNetNuke.Schema.SqlDataProvider) and the SqlDataProvider.vb Add/Update
// stored-procedure call sites. Every mapping primitive used here (ToTable /
// HasKey / Property / relationship metadata) is InMemory-provider safe; no
// HasDefaultValueSql / HasComputedColumnSql / raw SQL is used, and integer
// primary keys are left at the EF convention default (ValueGeneratedOnAdd) which
// maps to the database IDENTITY columns for SQL Server and enables automatic key
// generation under InMemory. (Do NOT call ValueGeneratedNever.)
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core <see cref="IEntityTypeConfiguration{TEntity}"/> that maps the
/// <see cref="Module"/> aggregate root and its <see cref="DesktopModule"/> and
/// <see cref="ModuleDefinition"/> satellites onto their pre-existing DotNetNuke
/// 4.9.0.85 tables (<c>dbo.Modules</c>, <c>dbo.DesktopModules</c> and
/// <c>dbo.ModuleDefinitions</c>). A single configuration class hosts all three
/// <c>Configure</c> overloads because the three types form one tightly-coupled
/// persistence concern.
/// </summary>
/// <remarks>
/// <para>
/// Discovered automatically by <c>DnnDbContext.OnModelCreating</c> through
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly)</c>,
/// which detects every public type implementing
/// <see cref="IEntityTypeConfiguration{TEntity}"/> in the Infrastructure
/// assembly — including a single class that implements the interface for more
/// than one entity, as this one does. It therefore requires no explicit
/// registration.
/// </para>
/// <para>
/// The legacy <c>ModuleInfo</c> is a denormalized merge of
/// <c>Modules + TabModules + ModuleControls + DesktopModules + ModuleDefinitions</c>;
/// only the 11 physical <c>dbo.Modules</c> columns are real columns of the Module
/// table, while the join-sourced properties are <c>Ignore</c>d (ADR-002 schema
/// fidelity) because they have no column on <c>dbo.Modules</c> (see the per-group
/// <c>// MIGRATION:</c> notes in
/// <see cref="Configure(EntityTypeBuilder{Module})"/>). The
/// <see cref="Module.IsDeleted"/> soft-delete flag is mapped as a real column and
/// is the flag the Module service/repository list filter relies on.
/// </para>
/// <para>
/// Only provider-agnostic relational metadata (<c>ToTable</c>, <c>HasKey</c>,
/// per-property <c>Property</c> declarations, and foreign-key relationship
/// metadata) is configured. No SQL-Server-only constructs
/// (<c>HasDefaultValueSql</c>, <c>HasComputedColumnSql</c>, raw SQL, or
/// value-generation overrides) are used, so the same model builds cleanly under
/// both the SQL Server provider (production) and the EF Core InMemory provider
/// (integration-test fixtures, Gate 5). Per ADR-002 the schema is mapped exactly
/// as it exists in the database; the entities deliberately carry no EF
/// attributes, so this file is the single home for the
/// Module/DesktopModule/ModuleDefinition mapping.
/// </para>
/// </remarks>
public sealed class ModuleConfiguration :
    IEntityTypeConfiguration<Module>,
    IEntityTypeConfiguration<DesktopModule>,
    IEntityTypeConfiguration<ModuleDefinition>
{
    /// <summary>
    /// Maps the <see cref="Module"/> entity onto the existing <c>dbo.Modules</c>
    /// table, reproducing the DotNetNuke 4.9.0.85 column set verbatim for the 11
    /// physical columns and <c>Ignore</c>-ing the denormalized (join-sourced)
    /// properties that have no <c>dbo.Modules</c> column (ADR-002 schema fidelity).
    /// Also declares the principal side of the <see cref="Module"/> →
    /// <see cref="ModulePermission"/> relationship.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="Module"/>.</param>
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        // Map onto the existing physical table (default install schema = dbo).
        // ADR-002: the table already exists; this only describes the mapping and
        // never triggers schema generation.
        builder.ToTable("Modules", "dbo");

        // Primary key. ModuleID is an IDENTITY(0,1) column in the DNN 4.9 schema
        // ([ModuleID] int NOT NULL IDENTITY(0,1)). Leaving the int-PK convention
        // intact preserves ValueGeneratedOnAdd, so the InMemory provider can
        // auto-assign keys on insert — required for the Module POST -> 201
        // round-trip in Gate 5. (Do NOT call ValueGeneratedNever.)
        builder.HasKey(m => m.ModuleID);

        // ---------------------------------------------------------------------
        // The 11 PHYSICAL dbo.Modules columns (verbatim DNN 4.9.0.85 schema
        // names). Property names already equal their column names, so
        // HasColumnName is unnecessary; the explicit Property declarations make
        // this configuration the single, unambiguous source of truth for the
        // physical column set and ensure no real column is silently omitted.
        // ---------------------------------------------------------------------
        builder.Property(m => m.ModuleID);                  // [ModuleID] int NOT NULL IDENTITY(0,1)
        builder.Property(m => m.ModuleDefID);               // [ModuleDefID] int NOT NULL
        builder.Property(m => m.ModuleTitle);               // [ModuleTitle] nvarchar(256) NULL

        // MIGRATION: [AllTabs] bit NOT NULL (DB DEFAULT 0). The default is a
        // SQL-Server-only constraint and is intentionally NOT declared
        // (no HasDefaultValueSql) to stay InMemory-provider safe; the C# bool
        // default (false) already matches the schema default.
        builder.Property(m => m.AllTabs);                   // [AllTabs] bit NOT NULL

        // MIGRATION: IsDeleted soft-delete flag preserved verbatim (AAP §0.4.1).
        // [IsDeleted] bit NOT NULL (DB DEFAULT 0). It is MAPPED (NOT Ignored) — it
        // is the soft-delete flag the ModuleService / ModuleRepository list filter
        // relies on. The SQL-Server DEFAULT(0) is omitted (InMemory-safe); the C#
        // bool default already matches.
        builder.Property(m => m.IsDeleted);                 // [IsDeleted] bit NOT NULL

        builder.Property(m => m.InheritViewPermissions);    // [InheritViewPermissions] bit NULL
        builder.Property(m => m.Header);                    // [Header] ntext NULL
        builder.Property(m => m.Footer);                    // [Footer] ntext NULL
        builder.Property(m => m.StartDate);                 // [StartDate] datetime NULL
        builder.Property(m => m.EndDate);                   // [EndDate] datetime NULL
        builder.Property(m => m.PortalID);                  // [PortalID] int NULL

        // ---------------------------------------------------------------------
        // MIGRATION (ADR-002 schema fidelity): the following denormalized
        // properties are sourced from the dbo.TabModules table (the per-tab
        // placement of a module). They have NO physical column on dbo.Modules, so
        // they are IGNORED — mapping them would make SQL Server CRUD issue
        // SELECT/INSERT/UPDATE against non-existent Modules columns and break
        // against the real DNN 4.9.0.85 schema. The TabModules data is a separate
        // table; the repository/DTO projection layer (CP3) rehydrates these fields
        // from a TabModules join when a placed-module view is required. Recorded in
        // MIGRATION_NOTES.md §4.2.
        // ---------------------------------------------------------------------
        builder.Ignore(m => m.TabModuleID);                 // TabModules.[TabModuleID] (not a Modules column)
        builder.Ignore(m => m.TabID);                       // TabModules.[TabID]
        builder.Ignore(m => m.PaneName);                    // TabModules.[PaneName]
        builder.Ignore(m => m.ModuleOrder);                 // TabModules.[ModuleOrder]
        builder.Ignore(m => m.CacheTime);                   // TabModules.[CacheTime]
        builder.Ignore(m => m.Alignment);                   // TabModules.[Alignment]
        builder.Ignore(m => m.Color);                       // TabModules.[Color]
        builder.Ignore(m => m.Border);                      // TabModules.[Border]
        builder.Ignore(m => m.IconFile);                    // TabModules.[IconFile]
        builder.Ignore(m => m.Visibility);                  // TabModules.[Visibility] (VisibilityState enum)
        builder.Ignore(m => m.ContainerSrc);                // TabModules.[ContainerSrc]
        builder.Ignore(m => m.DisplayTitle);                // TabModules.[DisplayTitle]
        builder.Ignore(m => m.DisplayPrint);                // TabModules.[DisplayPrint]
        builder.Ignore(m => m.DisplaySyndicate);            // TabModules.[DisplaySyndicate]

        // ---------------------------------------------------------------------
        // MIGRATION (ADR-002 schema fidelity): the following properties are
        // sourced from the dbo.ModuleControls table (the control that renders the
        // module). They have NO physical column on dbo.Modules, so they are
        // IGNORED rather than mapped onto non-existent Modules columns. The
        // repository/DTO projection layer (CP3) rehydrates them from a
        // ModuleControls join. Recorded in MIGRATION_NOTES.md §4.2.
        // ---------------------------------------------------------------------
        builder.Ignore(m => m.ModuleControlId);             // ModuleControls.[ModuleControlID] (not a Modules column)
        builder.Ignore(m => m.ControlSrc);                  // ModuleControls.[ControlSrc]
        builder.Ignore(m => m.ControlType);                 // ModuleControls.[ControlType]
        builder.Ignore(m => m.ControlTitle);                // ModuleControls.[ControlTitle]
        builder.Ignore(m => m.HelpUrl);                     // ModuleControls.[HelpUrl]
        builder.Ignore(m => m.SupportsPartialRendering);    // ModuleControls (post-4.9 addition)

        // ---------------------------------------------------------------------
        // MIGRATION (ADR-002 schema fidelity): the following properties are
        // sourced from the dbo.DesktopModules / dbo.ModuleDefinitions catalog
        // tables (the module-definition the placed module derives from). They have
        // NO physical column on dbo.Modules, so they are IGNORED rather than mapped
        // onto non-existent Modules columns. They are mapped on their OWN tables in
        // Configure(DesktopModule)/Configure(ModuleDefinition) below; the
        // repository/DTO projection layer (CP3) rehydrates them from the catalog
        // join. Recorded in MIGRATION_NOTES.md §4.2.
        // ---------------------------------------------------------------------
        builder.Ignore(m => m.DesktopModuleID);             // DesktopModules.[DesktopModuleID] (not a Modules column)
        builder.Ignore(m => m.FriendlyName);                // DesktopModules.[FriendlyName]
        builder.Ignore(m => m.FolderName);                  // DesktopModules.[FolderName]
        builder.Ignore(m => m.Description);                 // DesktopModules.[Description]
        builder.Ignore(m => m.Version);                     // DesktopModules.[Version]
        builder.Ignore(m => m.IsPremium);                   // DesktopModules.[IsPremium]
        builder.Ignore(m => m.IsAdmin);                     // DesktopModules.[IsAdmin]
        builder.Ignore(m => m.BusinessControllerClass);     // DesktopModules.[BusinessControllerClass]
        builder.Ignore(m => m.ModuleName);                  // DesktopModules.[ModuleName]
        builder.Ignore(m => m.SupportedFeatures);           // DesktopModules.[SupportedFeatures]

        // ---------------------------------------------------------------------
        // MIGRATION: relationship — Module (principal) -> ModulePermissions
        // (dependent). NOTE the two distinct delete concerns: the Module *record*
        // uses SOFT delete (the IsDeleted flag mapped above; list queries filter it
        // out — see MIGRATION_NOTES.md §6.3), whereas THIS relationship governs the
        // REFERENTIAL cleanup of the dependent permission rows. The legacy
        // permanent delete (ModuleController.DeleteModule) removes a module's
        // ModulePermission rows in the same transaction, so the relationship is
        // configured with DeleteBehavior.Cascade to preserve that referential
        // semantic when a Module is physically removed.
        //
        // ModulePermission is configured as an INDEPENDENT root entity in
        // PermissionConfiguration (via HasBaseType((Type?)null)); declaring this
        // relationship here on the principal side is correct and EF merges the two
        // configurations. WithOne() has no inverse navigation because
        // ModulePermission exposes no Module navigation property. ModuleID is the
        // real scalar FK column on ModulePermission (mapped in
        // PermissionConfiguration). There is only one relationship into
        // ModulePermission, so Cascade raises no multiple-cascade-path concern;
        // and the InMemory provider ignores delete behavior, so this is safe for
        // the Gate 5 round-trip.
        // ---------------------------------------------------------------------
        builder.HasMany(m => m.ModulePermissions)
            .WithOne()
            .HasForeignKey(mp => mp.ModuleID)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// Maps the <see cref="DesktopModule"/> entity onto the existing
    /// <c>dbo.DesktopModules</c> table, reproducing the DotNetNuke 4.9.0.85
    /// column set verbatim for the 11 physical columns and <c>Ignore</c>-ing the
    /// remaining (derived / non-physical) properties (ADR-002 schema fidelity).
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="DesktopModule"/>.</param>
    public void Configure(EntityTypeBuilder<DesktopModule> builder)
    {
        // Map onto the existing physical table; schema "dbo", table name verbatim
        // (ADR-002, no schema generation).
        builder.ToTable("DesktopModules", "dbo");

        // Primary key. DesktopModuleID is an IDENTITY(1,1) column; the int-PK
        // convention (ValueGeneratedOnAdd) is left intact so the InMemory provider
        // can auto-assign keys on insert.
        builder.HasKey(dm => dm.DesktopModuleID);

        // ---------------------------------------------------------------------
        // The 11 PHYSICAL dbo.DesktopModules columns (verbatim DNN 4.9.0.85
        // schema names). Property names already equal their column names, so
        // HasColumnName is unnecessary; the explicit Property declarations
        // document the full physical column set.
        // ---------------------------------------------------------------------
        builder.Property(dm => dm.DesktopModuleID);         // [DesktopModuleID] int NOT NULL IDENTITY(1,1)
        builder.Property(dm => dm.FriendlyName);            // [FriendlyName] nvarchar(128) NOT NULL
        builder.Property(dm => dm.Description);             // [Description] nvarchar(2000) NULL
        builder.Property(dm => dm.Version);                 // [Version] nvarchar(8) NULL
        builder.Property(dm => dm.IsPremium);               // [IsPremium] bit NOT NULL
        builder.Property(dm => dm.IsAdmin);                 // [IsAdmin] bit NOT NULL
        builder.Property(dm => dm.BusinessControllerClass); // [BusinessControllerClass] nvarchar(200) NULL
        builder.Property(dm => dm.FolderName);              // [FolderName] nvarchar(128) NOT NULL
        builder.Property(dm => dm.ModuleName);              // [ModuleName] nvarchar(128) NOT NULL
        builder.Property(dm => dm.SupportedFeatures);       // [SupportedFeatures] int NOT NULL
        builder.Property(dm => dm.CompatibleVersions);      // [CompatibleVersions] nvarchar(500) NULL

        // ---------------------------------------------------------------------
        // MIGRATION (ADR-002 schema fidelity): IsUpgradeable / IsPortable /
        // IsSearchable are DERIVED at runtime from the SupportedFeatures bitmask
        // (the legacy DesktopModuleSupportedFeature flags via GetFeature/
        // UpdateFeature); Dependencies / Permissions are ABSENT from the 4.9
        // DesktopModules baseline table. None of these is a physical
        // dbo.DesktopModules column, so they are IGNORED — the service/DTO layer
        // computes IsUpgradeable/IsPortable/IsSearchable from SupportedFeatures
        // when needed. Recorded in MIGRATION_NOTES.md §4.2.
        // ---------------------------------------------------------------------
        builder.Ignore(dm => dm.IsUpgradeable);             // derived from SupportedFeatures (not a physical column)
        builder.Ignore(dm => dm.IsPortable);                // derived from SupportedFeatures
        builder.Ignore(dm => dm.IsSearchable);              // derived from SupportedFeatures
        builder.Ignore(dm => dm.Dependencies);              // absent from 4.9 baseline
        builder.Ignore(dm => dm.Permissions);               // absent from 4.9 baseline
    }

    /// <summary>
    /// Maps the <see cref="ModuleDefinition"/> entity onto the existing
    /// <c>dbo.ModuleDefinitions</c> table, reproducing the DotNetNuke 4.9.0.85
    /// column set verbatim for the 4 physical columns and <c>Ignore</c>-ing the
    /// runtime-only <see cref="ModuleDefinition.TempModuleID"/> (ADR-002 schema
    /// fidelity).
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="ModuleDefinition"/>.</param>
    public void Configure(EntityTypeBuilder<ModuleDefinition> builder)
    {
        // Map onto the existing physical table; schema "dbo", table name verbatim
        // (ADR-002, no schema generation).
        builder.ToTable("ModuleDefinitions", "dbo");

        // Primary key. ModuleDefID is an IDENTITY(1,1) column; the int-PK
        // convention (ValueGeneratedOnAdd) is left intact so the InMemory provider
        // can auto-assign keys on insert.
        builder.HasKey(md => md.ModuleDefID);

        // ---------------------------------------------------------------------
        // The 4 PHYSICAL dbo.ModuleDefinitions columns (verbatim DNN 4.9.0.85
        // schema names). DesktopModuleID is a scalar FK to DesktopModules; per the
        // legacy ModuleDefinitionInfo there is NO navigation property between
        // ModuleDefinition and DesktopModule, so NO EF relationship is configured —
        // it stays a plain scalar int column.
        // ---------------------------------------------------------------------
        builder.Property(md => md.ModuleDefID);             // [ModuleDefID] int NOT NULL IDENTITY(1,1)
        builder.Property(md => md.FriendlyName);            // [FriendlyName] nvarchar(128) NOT NULL
        builder.Property(md => md.DesktopModuleID);         // [DesktopModuleID] int NOT NULL (scalar FK; no relationship)
        builder.Property(md => md.DefaultCacheTime);        // [DefaultCacheTime] int NOT NULL

        // MIGRATION (ADR-002 schema fidelity): TempModuleID is a runtime-only
        // transient identifier used during import/installation flows — it is NOT a
        // physical dbo.ModuleDefinitions column, so it is IGNORED rather than
        // mapped onto a non-existent column. Recorded in MIGRATION_NOTES.md §4.2.
        builder.Ignore(md => md.TempModuleID);              // runtime-only temp id (not a physical column)
    }
}
