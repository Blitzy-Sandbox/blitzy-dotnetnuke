using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for Module (legacy ModuleInfo, Library/Components/Modules/ModuleInfo.vb,
// namespace DotNetNuke.Entities.Modules). Code-First mapped to the EXISTING schema; schema not altered.
// MIGRATION (CP2 review — ModuleConfiguration #1 / ModuleRepository #1): the C# Module entity is a FLATTENED read
// model that merges columns from [Modules] + [TabModules] + [ModuleDefinitions] + [DesktopModules] +
// [ModuleControls]. The legacy database already exposes exactly this denormalized shape through the existing read
// VIEW [vw_Modules] (DotNetNuke.Schema.SqlDataProvider) — its SELECT joins those five tables and surfaces
// TabId/TabModuleId/ModuleOrder/PaneName/CacheTime/Alignment/... (from TabModules), FriendlyName/FolderName/
// Description/Version/... (from DesktopModules) and ModuleControlId/ControlSrc/ControlType/... (from
// ModuleControls). Mapping the entity to [Modules] made EF convention emit SQL for columns such as TabId and
// FriendlyName against [Modules], where they do not exist (the reviewed defect). Mapping to the view instead makes
// every flattened read resolve to a real column, so ModuleRepository.GetByTabIdAsync (filters TabId) and
// GetByDefinitionAsync (filters FriendlyName) now generate valid SQL with NO repository change. The view is
// READ-oriented; composite writes back to the five base tables are a documented future concern
// (MIGRATION_NOTES.md §13.2/§14.2). The EF Core InMemory gates ignore view/table mapping, so Gate 5 CRUD is unaffected.
// MIGRATION: [QA-FINAL Issue #3/#4, CRITICAL — read/write split] The earlier phase RETAINED ToView-only and DEFERRED
// the real-DB composite write, arguing ToTable("Modules") would regress the denormalized reads. That trade-off is now
// resolved: the entity is mapped to BOTH the read view vw_Modules AND the physical [Modules] write table. EF Core 8
// QUERIES from the view and WRITES to the table when both are configured, so the denormalized reads (GetByTabIdAsync
// filters TabId, GetByDefinitionAsync filters FriendlyName) STILL resolve against vw_Modules with ZERO regression,
// while create/update/delete now persist to real tables instead of throwing on a relational provider. The composite
// write fans out across the normalized legacy schema the review requires: the 11 base columns are written to [Modules]
// here, and the per-page PLACEMENT columns (TabId/PaneName/ModuleOrder/CacheTime/Alignment/Color/Border/IconFile/
// Visibility/ContainerSrc/DisplayTitle/DisplayPrint/DisplaySyndicate — which physically live in [TabModules], NOT in
// [Modules]) are excluded from this write table below and persisted as a [TabModules] row by ModuleRepository
// (see TabModuleConfiguration). Reference data (DesktopModules/ModuleControls/ModuleDefinitions — the view's INNER
// joins) is read-only and never written by a module-instance create. The QA-1 nullable-key fix (store-generated int
// key) is retained below.
public sealed class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        // MIGRATION (CP2): READ side — vw_Modules (flattened across Modules/TabModules/DesktopModules/ModuleControls/
        // ModuleDefinitions). Queries against DbSet<Module> resolve here, so GetByTabIdAsync/GetByDefinitionAsync work.
        builder.ToView("vw_Modules");

        // MIGRATION (QA-FINAL Issue #3, CRITICAL — read/write split): WRITE side — physical [Modules] table (11 base
        // columns). With BOTH ToView + ToTable, EF Core 8 reads from the view and writes to the table, so module
        // create/update/delete now persist on real SQL Server (previously they threw — a view-mapped entity cannot be
        // persisted by a relational provider — while EF InMemory Gate-5 silently passed). View-only columns are removed
        // from this write table by the exclusion loop at the end of Configure.
        builder.ToTable("Modules");

        // MIGRATION: [QA-1 Issue #2] ModuleInfo._ModuleID is the PK. Module.ModuleId was originally modeled int?
        // (nullable), mirroring the legacy Null.NullInteger sentinel. QA-1 runtime testing DISPROVED the earlier
        // "HasKey works and CRUD succeeds (empirically verified)" claim: with a nullable key the EF change tracker
        // rejected a new (null-key) entity during AddAsync with InvalidOperationException ("primary key property
        // 'ModuleId' is null"), failing EVERY Module create. The failure is provider-agnostic — it reproduces under
        // EF Core InMemory, which is precisely the Gate-5 integration store — so the 313 mock-based unit tests (which
        // mock IModuleRepository.AddAsync) never exercised it. The fix makes Module.ModuleId a non-nullable int and
        // declares the key store-generated (ValueGeneratedOnAdd): a new entity carries the int default (0) sentinel
        // that EF/InMemory replaces with a generated value on insert. Gate-5 (Module POST -> 201) now passes.
        builder.HasKey(m => m.ModuleId);
        builder.Property(m => m.ModuleId)
            .HasColumnName("ModuleID")
            .ValueGeneratedOnAdd();

        // MIGRATION (QA-FINAL Issue #3): preserve the EXACT legacy casing for the two kept [Modules] columns whose
        // property name differs only by case from the physical column ([Modules].[ModuleDefID] and [Modules].[PortalID]).
        // These names match both the physical table and the vw_Modules projection (M.ModuleDefID / M.PortalID), so the
        // single HasColumnName applies correctly to both store objects. The remaining kept columns (ModuleTitle, AllTabs,
        // IsDeleted, InheritViewPermissions, Header, Footer, StartDate, EndDate) already match legacy casing by convention.
        builder.Property(m => m.ModuleDefId).HasColumnName("ModuleDefID");
        builder.Property(m => m.PortalId).HasColumnName("PortalID");

        // MIGRATION (CP2 review — ModuleConfiguration #1): Ignore the Module properties that are NOT columns of
        // [vw_Modules] on the authoritative consolidated schema (DotNetNuke.Schema.SqlDataProvider) so EF never
        // emits SQL for a non-existent view column:
        //   - DefaultCacheTime         -> lives on [ModuleDefinitions]; the view does not project it.
        //   - SupportsPartialRendering -> not projected by the view (not among the selected [ModuleControls] cols).
        //   - Dependencies, Permissions-> [DesktopModules] columns added by 04.05.00; the view's "DM.*" binds at
        //                                  view-creation time to the consolidated [DesktopModules] (11 cols), which
        //                                  predate them, so they are NOT exposed by the view. Ignoring is also safe
        //                                  on an upgraded database (a present column simply stays unread); both are
        //                                  package metadata unused by any in-scope repository query or service.
        //   - AuthorizedEditRoles, AuthorizedViewRoles, AuthorizedRoles -> not projected by the view (module role
        //                                  strings are sourced from permissions, not this read model).
        builder.Ignore(m => m.DefaultCacheTime);
        builder.Ignore(m => m.SupportsPartialRendering);
        builder.Ignore(m => m.Dependencies);
        builder.Ignore(m => m.Permissions);
        builder.Ignore(m => m.AuthorizedEditRoles);
        builder.Ignore(m => m.AuthorizedViewRoles);
        builder.Ignore(m => m.AuthorizedRoles);

        // MIGRATION: Module 1..N ModulePermission (module-level access control). OWNED here (configured once);
        // FK is ModulePermission.ModuleId. WithOne() because ModulePermission has no back-navigation to Module.
        // Module is view-mapped, so this is a tracked-graph relationship (no DB FK is generated for a view); the
        // InMemory gate honors it client-side and the navigation still supports Include/projection.
        builder.HasMany(m => m.ModulePermissions)
            .WithOne()
            .HasForeignKey(mp => mp.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION (QA-FINAL Issue #3, CRITICAL — write-table column filter): the physical [Modules] table has exactly
        // 11 columns. Every OTHER mapped Module property is a flattened vw_Modules column sourced from a DIFFERENT base
        // table (TabModules placement, DesktopModules/ModuleControls reference data) and must NOT be emitted into the
        // [Modules] INSERT/UPDATE. For each scalar property whose [Modules]-table column name is not one of the 11
        // physical columns, drop ONLY its table mapping (SetColumnName(null, <Modules table store object>)); the view
        // mapping is untouched, so reads still resolve every flattened column. Placement columns are then persisted to
        // [TabModules] by ModuleRepository; reference columns are read-only. Doing this with a loop (rather than ~30
        // individual SetColumnName calls) keeps the intent explicit and resilient to property additions.
        var physicalModuleColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ModuleID", "ModuleDefID", "ModuleTitle", "AllTabs", "IsDeleted",
            "InheritViewPermissions", "Header", "Footer", "StartDate", "EndDate", "PortalID",
        };
        var modulesTable = StoreObjectIdentifier.Table("Modules", builder.Metadata.GetSchema());
        foreach (var property in builder.Metadata.GetProperties())
        {
            var columnName = property.GetColumnName(modulesTable);
            if (columnName is not null && !physicalModuleColumns.Contains(columnName))
            {
                property.SetColumnName(null, modulesTable);
            }
        }
    }
}
