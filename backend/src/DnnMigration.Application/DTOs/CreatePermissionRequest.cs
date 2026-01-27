// -----------------------------------------------------------------------------
// DnnMigration - Modern .NET 8 Migration of DotNetNuke
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: This DTO is derived from PermissionController.vb AddPermission method
// which calls DataProvider.Instance().AddPermission(permissionCode, moduleDefId, permissionKey, permissionName).
// Original source: Library/Components/Security/Permissions/PermissionController.vb
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs;

/// <summary>
/// Data Transfer Object for creating a new permission.
/// Contains all required fields for permission creation including validation constraints.
/// Used as the request body for POST /api/permissions endpoint.
/// </summary>
/// <remarks>
/// MIGRATION: Fields derived from PermissionController.vb AddPermission method parameters:
/// - PermissionCode: Category code for the permission (e.g., "SYSTEM_TAB", "CONTENT_MODULE")
/// - ModuleDefId: Foreign key linking to the module definition
/// - PermissionKey: Unique identifier within the permission code category
/// - PermissionName: Human-readable display name for the permission
/// </remarks>
/// <param name="PermissionCode">
/// The permission category code that groups related permissions together.
/// Examples: "SYSTEM_TAB" for tab permissions, "CONTENT_MODULE" for module permissions.
/// Required, maximum 50 characters.
/// </param>
/// <param name="ModuleDefId">
/// The module definition identifier that this permission is associated with.
/// Links the permission to a specific module type. Use -1 for system-level permissions.
/// Required.
/// </param>
/// <param name="PermissionKey">
/// The unique key identifier for this permission within its permission code category.
/// Examples: "VIEW", "EDIT", "DELETE", "MANAGE".
/// Required, maximum 50 characters.
/// </param>
/// <param name="PermissionName">
/// The human-readable display name for this permission shown in administrative interfaces.
/// Examples: "View Tab", "Edit Content", "Delete Module".
/// Required, maximum 50 characters.
/// </param>
public record CreatePermissionRequest(
    [Required(ErrorMessage = "Permission code is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Permission code must be between 1 and 50 characters.")]
    string PermissionCode,

    [Required(ErrorMessage = "Module definition ID is required.")]
    int ModuleDefId,

    [Required(ErrorMessage = "Permission key is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Permission key must be between 1 and 50 characters.")]
    string PermissionKey,

    [Required(ErrorMessage = "Permission name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Permission name must be between 1 and 50 characters.")]
    string PermissionName
);
