// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) DnnMigration. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

namespace DnnMigration.Application.DTOs;

/// <summary>
/// Data Transfer Object for permission update requests.
/// Contains all optional fields for updating an existing permission.
/// All fields are nullable to support partial updates (PATCH-style semantics).
/// </summary>
/// <remarks>
/// MIGRATION: Fields derived from PermissionController.vb UpdatePermission method
/// which calls DataProvider.Instance().UpdatePermission(PermissionID, PermissionCode, ModuleDefID, PermissionKey, PermissionName).
/// The PermissionID is provided via the route parameter, not in this request body.
/// </remarks>
/// <param name="PermissionCode">Optional permission code update (e.g., "SYSTEM_VIEW", "MODULE_EDIT").</param>
/// <param name="ModuleDefId">Optional module definition identifier to associate the permission with a different module definition.</param>
/// <param name="PermissionKey">Optional permission key update (e.g., "VIEW", "EDIT", "DELETE").</param>
/// <param name="PermissionName">Optional permission display name update for UI presentation.</param>
public record UpdatePermissionRequest(
    string? PermissionCode = null,
    int? ModuleDefId = null,
    string? PermissionKey = null,
    string? PermissionName = null
);
