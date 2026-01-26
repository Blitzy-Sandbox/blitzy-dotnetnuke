// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Modules.Definitions.ModuleDefinitionInfo → C# 12 ModuleDefinition entity
// Source: Library/Components/Modules/ModuleDefinitionInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Converted Private fields to C# auto-properties
// - Removed TempModuleID (runtime-only for installation process)
// - Applied nullable reference types (FriendlyName is nullable)
// - Added navigation property to DesktopModule
// - Added collection for Module instances using this definition
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a module definition that describes a specific function within a desktop module.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the ModuleDefinitions table. A single <see cref="DesktopModule"/>
/// can have multiple ModuleDefinitions, each representing a different functionality
/// (e.g., a Blog module might have "Blog" and "Archive" definitions).
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Modules.Definitions.ModuleDefinitionInfo.
/// TempModuleID removed as it was only used during the installation process.
/// </para>
/// </remarks>
public class ModuleDefinition
{
    /// <summary>
    /// Gets or sets the unique identifier for the module definition.
    /// </summary>
    /// <value>The primary key of the module definition record.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _ModuleDefID field.
    /// </remarks>
    public int ModuleDefId { get; set; }

    /// <summary>
    /// Gets or sets the desktop module identifier that this definition belongs to.
    /// </summary>
    /// <value>The foreign key to the DesktopModule entity.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _DesktopModuleID field.
    /// </remarks>
    public int DesktopModuleId { get; set; }

    /// <summary>
    /// Gets or sets the friendly name of the module definition.
    /// </summary>
    /// <value>The display name for the module definition. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _FriendlyName field.
    /// </remarks>
    public string? FriendlyName { get; set; }

    /// <summary>
    /// Gets or sets the default cache time in seconds.
    /// </summary>
    /// <value>The default number of seconds to cache module output.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _DefaultCacheTime field.
    /// A value of 0 means no caching.
    /// </remarks>
    public int DefaultCacheTime { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the desktop module that contains this definition.
    /// </summary>
    /// <value>Navigation property to the <see cref="DesktopModule"/> entity.</value>
    public virtual DesktopModule? DesktopModule { get; set; }

    /// <summary>
    /// Gets or sets the collection of module instances using this definition.
    /// </summary>
    /// <value>A collection of <see cref="Module"/> entities created from this definition.</value>
    public virtual ICollection<Module> Modules { get; set; } = new List<Module>();
}
