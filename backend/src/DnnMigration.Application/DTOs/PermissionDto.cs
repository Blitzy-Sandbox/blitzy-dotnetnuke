// MIGRATION: Converted from DotNetNuke.Security.Permissions.PermissionInfo (Permission.vb)
// Original namespace: DotNetNuke.Security.Permissions
// Target: C# 12 record type for API responses with immutability and value semantics

namespace DnnMigration.Application.DTOs;

/// <summary>
/// Permission response DTO representing a permission definition entity in API responses.
/// </summary>
/// <remarks>
/// MIGRATION: This record type is derived from the legacy VB.NET PermissionInfo class.
/// XmlElement attributes have been removed in favor of System.Text.Json serialization.
/// Implements immutability through C# 12 record type for thread-safety and value semantics.
/// </remarks>
/// <param name="PermissionId">
/// The unique identifier for the permission.
/// MIGRATION: Mapped from VB.NET _permissionID field.
/// </param>
/// <param name="PermissionCode">
/// The permission code string used for programmatic identification.
/// MIGRATION: Mapped from VB.NET _permissionCode field. Nullable to match original VB.NET String behavior.
/// </param>
/// <param name="ModuleDefId">
/// The module definition identifier this permission is associated with.
/// MIGRATION: Mapped from VB.NET _moduleDefID field.
/// </param>
/// <param name="PermissionKey">
/// The permission key used for localization and lookup.
/// MIGRATION: Mapped from VB.NET _permissionKey field. Nullable to match original VB.NET String behavior.
/// </param>
/// <param name="PermissionName">
/// The human-readable name of the permission.
/// MIGRATION: Mapped from VB.NET _PermissionName field. Nullable to match original VB.NET String behavior.
/// </param>
public record PermissionDto(
    /// <summary>
    /// Gets the unique identifier for the permission.
    /// </summary>
    /// <example>1</example>
    int PermissionId,

    /// <summary>
    /// Gets the permission code string used for programmatic identification.
    /// </summary>
    /// <example>SYSTEM_MODULE</example>
    string? PermissionCode,

    /// <summary>
    /// Gets the module definition identifier this permission is associated with.
    /// </summary>
    /// <example>0</example>
    int ModuleDefId,

    /// <summary>
    /// Gets the permission key used for localization and lookup.
    /// </summary>
    /// <example>VIEW</example>
    string? PermissionKey,

    /// <summary>
    /// Gets the human-readable name of the permission.
    /// </summary>
    /// <example>View</example>
    string? PermissionName
);
