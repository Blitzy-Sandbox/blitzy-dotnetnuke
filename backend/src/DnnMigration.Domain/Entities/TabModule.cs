namespace DnnMigration.Domain.Entities;

/// <summary>
/// TabModule (module-placement) entity: the association of a <see cref="Module"/> instance onto a
/// <see cref="Tab"/> (page), together with its per-placement presentation state.
/// </summary>
// MIGRATION (SCHEMA FIDELITY — finding #2): In the fully-upgraded DotNetNuke v4.9 schema the
// module-PLACEMENT fields do NOT live on the [Modules] table; they are physical columns of the
// separate [TabModules] table, and the legacy denormalized [vw_Modules] VIEW joined [Modules] (M.*)
// to [TabModules] (TM.*) to surface them together. The previous model mapped these placement columns
// onto [Modules], inventing phantom columns that generate invalid SQL against the existing schema.
// This entity restores the real relational shape: [TabModules] is modelled on its own (see
// TabModuleConfiguration), keyed by [TabModuleID] (IDENTITY(1,1)) with foreign keys to [Modules] and
// [Tabs]. Column set and nullability verified verbatim against the [TabModules] CREATE TABLE in
// DotNetNuke.Schema.SqlDataProvider (15 columns). VB `Date` -> `DateTime` conventions do not apply
// here (this table carries no date columns).
public class TabModule
{
    /// <summary>Surrogate key of the placement row — [TabModuleID], IDENTITY(1,1).</summary>
    public int TabModuleID { get; set; }

    /// <summary>FK to [Tabs].[TabID] — the page the module is placed on. NOT NULL.</summary>
    public int TabID { get; set; }

    /// <summary>FK to [Modules].[ModuleID] — the module instance being placed. NOT NULL.</summary>
    public int ModuleID { get; set; }

    /// <summary>The layout pane the module renders in — [PaneName] nvarchar(50) NOT NULL.</summary>
    public string PaneName { get; set; } = string.Empty;

    /// <summary>Ordinal position of the module within its pane — [ModuleOrder] int NOT NULL.</summary>
    public int ModuleOrder { get; set; }

    /// <summary>Output-cache duration (seconds) for the placement — [CacheTime] int NOT NULL.</summary>
    public int CacheTime { get; set; }

    // MIGRATION: [Alignment]/[Color]/[Border]/[IconFile]/[ContainerSrc] are NULLable columns in the
    // existing [TabModules] schema, so they are nullable reference types; the `= string.Empty`
    // initializer only seeds in-memory-constructed defaults (EF derives column nullability from the
    // type annotation).
    /// <summary>Optional pane alignment — [Alignment] nvarchar(10) NULL.</summary>
    public string? Alignment { get; set; } = string.Empty;

    /// <summary>Optional container colour — [Color] nvarchar(20) NULL.</summary>
    public string? Color { get; set; } = string.Empty;

    /// <summary>Optional container border width — [Border] nvarchar(1) NULL.</summary>
    public string? Border { get; set; } = string.Empty;

    /// <summary>Optional title icon reference — [IconFile] nvarchar(100) NULL.</summary>
    public string? IconFile { get; set; } = string.Empty;

    // MIGRATION: legacy nested enum VisibilityState (Maximized=0, Minimized=1, None=2) flattened to
    // int (matching the [Visibility] int column) to avoid introducing an out-of-scope enum type — the
    // identical treatment used on the denormalized Module carrier.
    /// <summary>Module visibility state — [Visibility] int NOT NULL.</summary>
    public int Visibility { get; set; }

    /// <summary>Optional container skin source — [ContainerSrc] nvarchar(200) NULL.</summary>
    public string? ContainerSrc { get; set; } = string.Empty;

    /// <summary>Whether the module title is rendered — [DisplayTitle] bit NOT NULL DEFAULT 1.</summary>
    public bool DisplayTitle { get; set; } = true;

    /// <summary>Whether the print action is shown — [DisplayPrint] bit NOT NULL DEFAULT 1.</summary>
    public bool DisplayPrint { get; set; } = true;

    /// <summary>Whether the syndicate (RSS) action is shown — [DisplaySyndicate] bit NOT NULL DEFAULT 1.</summary>
    public bool DisplaySyndicate { get; set; } = true;
}
