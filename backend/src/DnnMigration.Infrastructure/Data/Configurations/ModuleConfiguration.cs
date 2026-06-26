using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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
// MIGRATION: [QA-1 Issue #2] QA-1 suggested switching this mapping to ToTable("Modules") to make Module writable.
// DEVIATION (retain ToView): ToTable("Modules") would REGRESS the CP2 denormalized reads above — GetByTabIdAsync
// (TabId) and GetByDefinitionAsync (FriendlyName) project columns that live on [TabModules]/[DesktopModules], NOT on
// base [Modules] — while NOT actually enabling a complete real-DB write (those denormalized columns are absent from
// [Modules], so an insert there could not persist a full Module). Retaining ToView strictly dominates: real-DB reads
// stay valid, and Gate-5 InMemory CRUD is identical either way (InMemory ignores the store object, generating the
// key client-side). The real-DB composite write (insert fan-out across the five base tables, or a writable
// read/write split) is deferred and documented (MIGRATION_NOTES.md §14.2). The actual QA-1 create defect was the
// nullable key — fixed below.
public sealed class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.ToView("vw_Modules");

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
    }
}
