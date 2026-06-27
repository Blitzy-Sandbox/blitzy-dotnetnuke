namespace DnnMigration.Domain.Entities;

// MIGRATION: [QA-FINAL Issue #3/#4 — CRITICAL] Command-model entity for the EXISTING legacy [TabModules] table
// (DotNetNuke.Schema.SqlDataProvider: TabModuleID IDENTITY PK, TabID, ModuleID, PaneName, ModuleOrder, CacheTime,
// Alignment, Color, Border, IconFile, Visibility, ContainerSrc, DisplayTitle, DisplayPrint, DisplaySyndicate).
// Schema NOT altered. [TabModules] is the per-page PLACEMENT row for a module; the read view vw_Modules surfaces these
// columns (TM.*) by LEFT-joining [TabModules] onto [Modules]. The Module entity maps to BOTH [Modules] (write, 11 base
// columns) and vw_Modules (read, flattened across 5 tables); the placement columns physically live HERE, not on
// [Modules]. ModuleRepository.AddAsync stages a TabModule alongside each placed Module so the (real SQL Server) write
// fans out to [Modules] + [TabModules], and the legacy "module rows/placements" persistence the review requires is
// satisfied; reads via vw_Modules reassemble the flattened shape. Reference data (DesktopModules/ModuleControls/
// ModuleDefinitions — the view's INNER joins) is read-only and never written by a module-instance create.
// Persistence-ignorant POCO (Clean/Onion, AAP §0.3.3/§0.7.3).
public sealed class TabModule
{
    // MIGRATION: [TabModules].[TabModuleID] int IDENTITY(1,1) PRIMARY KEY (store-generated on insert).
    public int TabModuleId { get; set; }

    // MIGRATION: [TabModules].[TabID] int NOT NULL — the page (tab) the module is placed on.
    public int TabId { get; set; }

    // MIGRATION: [TabModules].[ModuleID] int NOT NULL — FK to [Modules].[ModuleID]. Set via the Module navigation so
    // EF fixes it up from the store-generated Module.ModuleId within the single SaveChanges commit boundary.
    public int ModuleId { get; set; }

    // MIGRATION: [TabModules].[PaneName] nvarchar(50) NOT NULL — the skin pane the module renders in.
    public string PaneName { get; set; } = "ContentPane";

    // MIGRATION: [TabModules].[ModuleOrder] int NOT NULL — placement order within the pane (re-sequenced by
    // ModuleService.UpdateTabModuleOrder; that ordering MUST persist here, which is why UpdateAsync syncs this row).
    public int ModuleOrder { get; set; }

    // MIGRATION: [TabModules].[CacheTime] int NOT NULL — output-cache duration for this placement.
    public int CacheTime { get; set; }

    // MIGRATION: [TabModules].[Alignment] nvarchar(10) NULL.
    public string? Alignment { get; set; }

    // MIGRATION: [TabModules].[Color] nvarchar(20) NULL.
    public string? Color { get; set; }

    // MIGRATION: [TabModules].[Border] nvarchar(1) NULL.
    public string? Border { get; set; }

    // MIGRATION: [TabModules].[IconFile] nvarchar(100) NULL.
    public string? IconFile { get; set; }

    // MIGRATION: [TabModules].[Visibility] int NOT NULL (VisibilityState; stored as int matching the column).
    public int Visibility { get; set; }

    // MIGRATION: [TabModules].[ContainerSrc] nvarchar(200) NULL.
    public string? ContainerSrc { get; set; }

    // MIGRATION: [TabModules].[DisplayTitle] bit NOT NULL (legacy DEFAULT 1).
    public bool DisplayTitle { get; set; } = true;

    // MIGRATION: [TabModules].[DisplayPrint] bit NOT NULL (legacy DEFAULT 1).
    public bool DisplayPrint { get; set; } = true;

    // MIGRATION: [TabModules].[DisplaySyndicate] bit NOT NULL (legacy DEFAULT 1).
    public bool DisplaySyndicate { get; set; }

    // MIGRATION: Navigation to the owning Module (FK = ModuleId). Optional reference; lets AddAsync rely on EF FK
    // fixup for the store-generated ModuleId. No inverse collection is added to Module (minimal blast radius).
    public Module? Module { get; set; }
}
