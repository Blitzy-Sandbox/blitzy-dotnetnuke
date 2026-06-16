using DnnMigration.Domain.Enums;

namespace DnnMigration.Domain.Entities;

// MIGRATION: schema-faithful entity for the dbo.TabModules join table — the per-tab PLACEMENT of a
// module on a page. In legacy DotNetNuke these columns lived on the FAT, denormalized ModuleInfo object
// (Library/Components/Modules/ModuleInfo.vb), which DNN hydrated from a JOIN across
// Modules + TabModules + ModuleControls + DesktopModules + ModuleDefinitions. Per ADR-002 schema fidelity,
// the Module entity maps ONLY the 11 physical dbo.Modules columns and Ignore()s the TabModules-sourced
// placement fields (see ModuleConfiguration.cs); this dedicated entity carries those fields against their
// REAL table so the repository can read placement via an explicit TabModules->Modules join and persist
// placement via AddTabModule/UpdateTabModule semantics (legacy SqlDataProvider.vb). Recorded in
// MIGRATION_NOTES.md §4.2 (Modules) and Deviation Index D-030/D-031.
//
// Authoritative column set (verbatim from the DNN 4.9.0.85 install DDL,
// Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider, table TabModules):
//   [TabModuleID] int NOT NULL IDENTITY(1,1)  -- PK
//   [TabID] int NOT NULL, [ModuleID] int NOT NULL, [PaneName] nvarchar(50) NOT NULL,
//   [ModuleOrder] int NOT NULL, [CacheTime] int NOT NULL, [Alignment] nvarchar(10) NULL,
//   [Color] nvarchar(20) NULL, [Border] nvarchar(1) NULL, [IconFile] nvarchar(100) NULL,
//   [Visibility] int NOT NULL, [ContainerSrc] nvarchar(200) NULL,
//   [DisplayTitle] bit NOT NULL DEFAULT(1), [DisplayPrint] bit NOT NULL DEFAULT(1),
//   [DisplaySyndicate] bit NOT NULL DEFAULT(1)
//
// Pure POCO: ZERO framework dependencies (no EF/DataAnnotation attributes); all persistence mapping lives in
// Infrastructure/Persistence/Configurations/TabModuleConfiguration.cs. Mapped as an INDEPENDENT entity with
// scalar TabID/ModuleID foreign-key columns (no navigation to Module/Tab), matching the join-table mapping
// style used elsewhere in the model — the repository performs explicit joins.
public class TabModule
{
    /// <summary>Identity primary key (<c>TabModules.[TabModuleID]</c>, IDENTITY(1,1)).</summary>
    public int TabModuleID { get; set; }

    /// <summary>Owning tab/page (<c>TabModules.[TabID]</c>).</summary>
    public int TabID { get; set; }

    /// <summary>Placed module (<c>TabModules.[ModuleID]</c>).</summary>
    public int ModuleID { get; set; }

    /// <summary>
    /// Pane the module is rendered in (<c>TabModules.[PaneName]</c>, NOT NULL). Defaults to the DNN
    /// content pane (<c>Globals.glbDefaultPane = "ContentPane"</c>) when a placement is created without one.
    /// </summary>
    public string PaneName { get; set; } = string.Empty;

    /// <summary>Render order within the pane (<c>TabModules.[ModuleOrder]</c>).</summary>
    public int ModuleOrder { get; set; }

    /// <summary>Output cache duration in seconds (<c>TabModules.[CacheTime]</c>).</summary>
    public int CacheTime { get; set; }

    /// <summary>Optional alignment hint (<c>TabModules.[Alignment]</c>, NULL).</summary>
    public string? Alignment { get; set; }

    /// <summary>Optional color hint (<c>TabModules.[Color]</c>, NULL).</summary>
    public string? Color { get; set; }

    /// <summary>Optional border hint (<c>TabModules.[Border]</c>, NULL).</summary>
    public string? Border { get; set; }

    /// <summary>Optional icon file (<c>TabModules.[IconFile]</c>, NULL).</summary>
    public string? IconFile { get; set; }

    // MIGRATION: TabModules.[Visibility] int -> VisibilityState enum (Maximized=0/Minimized=1/None=2).
    // EF maps the enum to its underlying int by convention (no HasConversion needed); the integer value is
    // preserved verbatim so persisted/compared values remain valid (AAP §0.7.1 enum preservation).
    /// <summary>Module visibility state (<c>TabModules.[Visibility]</c>).</summary>
    public VisibilityState Visibility { get; set; }

    /// <summary>Optional container skin source (<c>TabModules.[ContainerSrc]</c>, NULL).</summary>
    public string? ContainerSrc { get; set; }

    // MIGRATION: legacy DB DEFAULT(1); the SQL-Server default constraint is NOT declared in the EF mapping
    // (InMemory-safe per ADR-002). Placement values are always set explicitly by the repository on insert.
    /// <summary>Whether the module title is displayed (<c>TabModules.[DisplayTitle]</c>).</summary>
    public bool DisplayTitle { get; set; } = true;

    /// <summary>Whether the print affordance is displayed (<c>TabModules.[DisplayPrint]</c>).</summary>
    public bool DisplayPrint { get; set; } = true;

    /// <summary>Whether the syndicate affordance is displayed (<c>TabModules.[DisplaySyndicate]</c>).</summary>
    public bool DisplaySyndicate { get; set; } = true;
}
