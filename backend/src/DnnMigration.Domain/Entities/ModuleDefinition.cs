// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Modules.Definitions.ModuleDefinitionInfo → C# 12 ModuleDefinition entity
// Source: Library/Components/Modules/ModuleDefinitionInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Converted Private fields (_ModuleDefID, _FriendlyName, etc.) to C# auto-properties
// - Applied nullable reference types (FriendlyName is nullable string)
// - Added navigation property to DesktopModule
// - Added collection navigation for Module instances using this definition
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a module definition that describes a specific function within a desktop module.
/// A module definition defines a particular view or functionality that can be instantiated on pages.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the ModuleDefinitions table in the DNN database. A single 
/// <see cref="DesktopModule"/> package can contain multiple ModuleDefinitions, each 
/// representing different functionality. For example, a Blog module package might have 
/// "Blog Entry" and "Blog Archive" definitions.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Modules.Definitions.ModuleDefinitionInfo.
/// The original VB.NET class contained the following private fields:
/// <list type="bullet">
/// <item><description>_ModuleDefID → ModuleDefId</description></item>
/// <item><description>_FriendlyName → FriendlyName</description></item>
/// <item><description>_DesktopModuleID → DesktopModuleId</description></item>
/// <item><description>_TempModuleID → TempModuleId</description></item>
/// <item><description>_DefaultCacheTime → DefaultCacheTime</description></item>
/// </list>
/// </para>
/// </remarks>
public class ModuleDefinition
{
    /// <summary>
    /// Gets or sets the unique identifier for the module definition.
    /// </summary>
    /// <value>The primary key of the module definition record in the ModuleDefinitions table.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET Private _ModuleDefID As Integer field with corresponding Property.
    /// </remarks>
    public int ModuleDefId { get; set; }

    /// <summary>
    /// Gets or sets the user-friendly display name of the module definition.
    /// </summary>
    /// <value>
    /// The friendly name displayed to users when selecting this module definition.
    /// May be null for legacy or improperly configured definitions.
    /// </value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET Private _FriendlyName As String field with corresponding Property.
    /// Nullable reference type applied as the original VB.NET did not enforce non-null values.
    /// </remarks>
    public string? FriendlyName { get; set; }

    /// <summary>
    /// Gets or sets the desktop module identifier that this definition belongs to.
    /// </summary>
    /// <value>
    /// The foreign key referencing the <see cref="Entities.DesktopModule"/> entity.
    /// This links the module definition to its parent desktop module package.
    /// </value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET Private _DesktopModuleID As Integer field with corresponding Property.
    /// </remarks>
    public int DesktopModuleId { get; set; }

    /// <summary>
    /// Gets or sets the temporary module identifier used during module installation.
    /// </summary>
    /// <value>
    /// A temporary identifier used during the module installation and registration process.
    /// This value is typically transient and may not persist after installation completes.
    /// </value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET Private _TempModuleID As Integer field with corresponding Property.
    /// This property is primarily used by the module installation infrastructure to track
    /// temporary mappings before final module definition IDs are assigned.
    /// </remarks>
    public int TempModuleId { get; set; }

    /// <summary>
    /// Gets or sets the default cache time in seconds for module output.
    /// </summary>
    /// <value>
    /// The default number of seconds that module output should be cached.
    /// A value of 0 indicates no caching should be applied by default.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET Private _DefaultCacheTime As Integer field with corresponding Property.
    /// The original VB.NET constructor initialized this value to 0.
    /// </para>
    /// <para>
    /// Individual module instances can override this default cache time with their own settings.
    /// This value serves as the initial cache duration when a new module instance is created.
    /// </para>
    /// </remarks>
    public int DefaultCacheTime { get; set; }

    // -------------------------------------------------------------------------
    // Navigation Properties
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the desktop module that contains this definition.
    /// </summary>
    /// <value>
    /// Navigation property to the parent <see cref="Entities.DesktopModule"/> entity.
    /// May be null if the relationship is not loaded or the desktop module was deleted.
    /// </value>
    /// <remarks>
    /// This navigation property enables traversal from a module definition to its
    /// parent desktop module package. The relationship is configured via
    /// <see cref="DesktopModuleId"/> as the foreign key.
    /// </remarks>
    public virtual DesktopModule? DesktopModule { get; set; }

    /// <summary>
    /// Gets or sets the collection of module instances that use this definition.
    /// </summary>
    /// <value>
    /// A collection of <see cref="Module"/> entities that were created from this definition.
    /// Each module instance on a page references a specific module definition.
    /// </value>
    /// <remarks>
    /// <para>
    /// This inverse navigation property allows traversal from a module definition to all
    /// module instances that use it. This is useful for operations like determining
    /// the usage count of a definition or cascading updates.
    /// </para>
    /// <para>
    /// The collection is initialized to an empty list to prevent null reference exceptions
    /// when accessing the property before the collection is populated by Entity Framework.
    /// </para>
    /// </remarks>
    public virtual ICollection<Module> Modules { get; set; } = new List<Module>();
}
