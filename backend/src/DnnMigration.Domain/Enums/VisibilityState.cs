// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Modules.VisibilityState → C# 12 enum
// Source: Library/Components/Modules/ModuleInfo.vb
// Changes:
// - Converted from VB.NET Public Enum to C# 12 enum with file-scoped namespace
// - Added explicit integer values (0, 1, 2) for database compatibility with existing DNN schema
// - Added [Serializable] attribute for API serialization support
// - Added comprehensive XML documentation comments explaining each visibility state's purpose
// - Referenced by Module.Visibility property for display state control
// -----------------------------------------------------------------------------

using System;

namespace DnnMigration.Domain.Enums;

/// <summary>
/// Defines the visibility state of a module instance displayed on a tab/page.
/// Controls how a module is rendered and displayed to users.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: Extracted from VB.NET DotNetNuke.Entities.Modules.VisibilityState enum
/// defined in ModuleInfo.vb. The explicit integer values are preserved for database
/// compatibility with the existing DNN schema where module visibility is stored as an integer.
/// </para>
/// <para>
/// This enum is used by the <c>Module.Visibility</c> property to control the display state
/// of module instances on pages. The visibility state affects how the module container
/// and content are rendered in the user interface.
/// </para>
/// </remarks>
[Serializable]
public enum VisibilityState
{
    /// <summary>
    /// The module is fully visible and expanded, showing all content.
    /// This is the default state where the module container and all its content
    /// are rendered completely on the page.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to original VB.NET VisibilityState.Maximized = 0
    /// </remarks>
    Maximized = 0,

    /// <summary>
    /// The module is collapsed/minimized, typically showing only the title bar.
    /// The module content is hidden but the module container remains visible,
    /// allowing users to expand it back to the maximized state.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to original VB.NET VisibilityState.Minimized = 1
    /// </remarks>
    Minimized = 1,

    /// <summary>
    /// The module is completely hidden from view.
    /// Neither the module container nor its content is rendered on the page.
    /// This state is used to temporarily hide modules without deleting them.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Corresponds to original VB.NET VisibilityState.None = 2
    /// </remarks>
    None = 2
}
