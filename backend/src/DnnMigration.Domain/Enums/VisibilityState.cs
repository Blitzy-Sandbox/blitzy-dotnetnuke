// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Modules.VisibilityState → C# 12 enum
// Source: Library/Components/Modules/ModuleInfo.vb
// Changes:
// - Converted from VB.NET Enum to C# enum with file-scoped namespace
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Enums;

/// <summary>
/// Defines the visibility state of a module on a page.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Modules.VisibilityState.
/// </para>
/// </remarks>
public enum VisibilityState
{
    /// <summary>
    /// The module is fully expanded and visible.
    /// </summary>
    Maximized = 0,

    /// <summary>
    /// The module is collapsed, showing only the title bar.
    /// </summary>
    Minimized = 1,

    /// <summary>
    /// The module is completely hidden from view.
    /// </summary>
    None = 2
}
