// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Modules.DesktopModuleInfo → C# 12 DesktopModule entity
// Source: Library/Components/Modules/DesktopModuleInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Converted Private fields to C# auto-properties
// - Applied nullable reference types for optional string properties
// - Removed DesktopModuleSupportedFeature enum (moved to Domain/Enums if needed)
// - Removed helper methods (ClearFeature, GetFeature, SetFeature, UpdateFeature)
// - Added navigation collection for ModuleDefinitions
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a desktop module type/definition that can be installed and used in portals.
/// A desktop module is the installable package that contains one or more module definitions.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the DesktopModules table and represents the installed module packages
/// in the DNN system. Each desktop module can contain multiple <see cref="ModuleDefinition"/>
/// entries which define specific functionality within the module package.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Modules.DesktopModuleInfo.
/// The SupportedFeatures property uses bitwise flags where:
/// - IsPortable = 1 (supports import/export)
/// - IsSearchable = 2 (integrates with search)
/// - IsUpgradeable = 4 (supports upgrade scripts)
/// </para>
/// </remarks>
public class DesktopModule
{
    /// <summary>
    /// Gets or sets the unique identifier for the desktop module.
    /// </summary>
    /// <value>The primary key of the desktop module record.</value>
    public int DesktopModuleId { get; set; }

    /// <summary>
    /// Gets or sets the internal name of the module used for identification.
    /// </summary>
    /// <value>The module name, typically matching the folder name. May be null for legacy modules.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _ModuleName field.
    /// </remarks>
    public string? ModuleName { get; set; }

    /// <summary>
    /// Gets or sets the folder name where the module files are stored.
    /// </summary>
    /// <value>The relative folder path under DesktopModules directory. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _FolderName field.
    /// This is typically the same as ModuleName but can differ for modules with custom folder structures.
    /// </remarks>
    public string? FolderName { get; set; }

    /// <summary>
    /// Gets or sets the user-friendly display name of the module.
    /// </summary>
    /// <value>The friendly name shown to users in the module selection UI. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _FriendlyName field.
    /// </remarks>
    public string? FriendlyName { get; set; }

    /// <summary>
    /// Gets or sets the description of the module's functionality.
    /// </summary>
    /// <value>A description of what the module does. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Description field.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the version string of the module.
    /// </summary>
    /// <value>The version number in format "Major.Minor.Build" (e.g., "01.00.00"). May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Version field.
    /// </remarks>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a premium (paid) module.
    /// </summary>
    /// <value><c>true</c> if this is a premium module requiring purchase; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _IsPremium field.
    /// Premium modules are typically not included in the base DNN installation.
    /// </remarks>
    public bool IsPremium { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is an admin-only module.
    /// </summary>
    /// <value><c>true</c> if this module is only available to administrators; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _IsAdmin field.
    /// Admin modules are typically used for site management and configuration.
    /// </remarks>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Gets or sets the supported features as a bitwise flag integer.
    /// </summary>
    /// <value>
    /// A bitwise combination of feature flags:
    /// <list type="bullet">
    /// <item><description>1 = IsPortable (supports import/export)</description></item>
    /// <item><description>2 = IsSearchable (integrates with search)</description></item>
    /// <item><description>4 = IsUpgradeable (supports upgrade scripts)</description></item>
    /// </list>
    /// </value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _SupportedFeatures field.
    /// The original VB.NET code used the DesktopModuleSupportedFeature enum for these flags.
    /// To check if a feature is supported, use bitwise AND: (SupportedFeatures &amp; 1) == 1 for IsPortable.
    /// </remarks>
    public int SupportedFeatures { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified class name of the business controller.
    /// </summary>
    /// <value>
    /// The full type name of the class implementing IPortable, ISearchable, or IUpgradeable interfaces.
    /// May be null if no business controller is defined.
    /// </value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _BusinessControllerClass field.
    /// The business controller class is responsible for handling module-specific operations
    /// such as import/export, search indexing, and version upgrades.
    /// </remarks>
    public string? BusinessControllerClass { get; set; }

    /// <summary>
    /// Gets or sets the compatible DNN versions for this module.
    /// </summary>
    /// <value>A comma-separated list of compatible DNN versions. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _CompatibleVersions field.
    /// </remarks>
    public string? CompatibleVersions { get; set; }

    /// <summary>
    /// Gets or sets the module dependencies.
    /// </summary>
    /// <value>A string listing required dependencies for this module. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Dependencies field.
    /// Dependencies may include other modules, assemblies, or system requirements.
    /// </remarks>
    public string? Dependencies { get; set; }

    /// <summary>
    /// Gets or sets the default permissions for this module.
    /// </summary>
    /// <value>A string defining default permission settings. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Permissions field.
    /// This typically contains permission codes that control access to module functionality.
    /// </remarks>
    public string? Permissions { get; set; }

    /// <summary>
    /// Gets or sets the collection of module definitions associated with this desktop module.
    /// </summary>
    /// <value>
    /// A collection of <see cref="ModuleDefinition"/> entities that define the specific
    /// functionality provided by this desktop module package.
    /// </value>
    /// <remarks>
    /// A desktop module package can contain multiple module definitions, each representing
    /// a different view or functionality within the module.
    /// </remarks>
    public virtual ICollection<ModuleDefinition> ModuleDefinitions { get; set; } = new List<ModuleDefinition>();

    /// <summary>
    /// Determines whether the specified feature flag is set in <see cref="SupportedFeatures"/>.
    /// </summary>
    /// <param name="feature">The feature flag value to check (1=Portable, 2=Searchable, 4=Upgradeable).</param>
    /// <returns><c>true</c> if the feature is supported; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// MIGRATION: Replacement for VB.NET GetFeature helper method.
    /// This provides a convenient way to check feature flags without direct bitwise operations.
    /// </remarks>
    public bool HasFeature(int feature)
    {
        return SupportedFeatures > 0 && (SupportedFeatures & feature) == feature;
    }

    /// <summary>
    /// Gets a value indicating whether this module supports import/export operations.
    /// </summary>
    /// <value><c>true</c> if the module implements IPortable; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Replacement for VB.NET IsPortable property that used GetFeature helper.
    /// Checks if bit 1 (value 1) is set in SupportedFeatures.
    /// </remarks>
    public bool IsPortable => HasFeature(1);

    /// <summary>
    /// Gets a value indicating whether this module integrates with the search system.
    /// </summary>
    /// <value><c>true</c> if the module implements ISearchable; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Replacement for VB.NET IsSearchable property that used GetFeature helper.
    /// Checks if bit 2 (value 2) is set in SupportedFeatures.
    /// </remarks>
    public bool IsSearchable => HasFeature(2);

    /// <summary>
    /// Gets a value indicating whether this module supports version upgrade scripts.
    /// </summary>
    /// <value><c>true</c> if the module implements IUpgradeable; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Replacement for VB.NET IsUpgradeable property that used GetFeature helper.
    /// Checks if bit 3 (value 4) is set in SupportedFeatures.
    /// </remarks>
    public bool IsUpgradeable => HasFeature(4);
}
