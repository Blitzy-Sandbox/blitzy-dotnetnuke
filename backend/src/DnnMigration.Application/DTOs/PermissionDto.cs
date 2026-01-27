// MIGRATION: Converted from DotNetNuke.Security.Permissions.PermissionInfo (Permission.vb)
// Original namespace: DotNetNuke.Security.Permissions
// Target: C# 12 record type for API responses with immutability and value semantics

namespace DnnMigration.Application.DTOs;

/// <summary>
/// Permission response DTO representing a permission definition entity in API responses.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This record type is derived from the legacy VB.NET PermissionInfo class.
/// XmlElement attributes have been removed in favor of System.Text.Json serialization.
/// Implements immutability through C# 12 record type for thread-safety and value semantics.
/// </para>
/// <para>
/// Property mappings from PermissionInfo.vb:
/// <list type="bullet">
/// <item><description>PermissionId: Mapped from _permissionID (Integer → int)</description></item>
/// <item><description>PermissionCode: Mapped from _permissionCode (String → string?)</description></item>
/// <item><description>ModuleDefId: Mapped from _moduleDefID (Integer → int)</description></item>
/// <item><description>PermissionKey: Mapped from _permissionKey (String → string?)</description></item>
/// <item><description>PermissionName: Mapped from _PermissionName (String → string?)</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="PermissionId">
/// The unique identifier for the permission.
/// MIGRATION: Mapped from VB.NET _permissionID field.
/// Example: 1
/// </param>
/// <param name="PermissionCode">
/// The permission code string used for programmatic identification.
/// MIGRATION: Mapped from VB.NET _permissionCode field. Nullable to match original VB.NET String behavior.
/// Example: SYSTEM_MODULE
/// </param>
/// <param name="ModuleDefId">
/// The module definition identifier this permission is associated with.
/// MIGRATION: Mapped from VB.NET _moduleDefID field.
/// Example: 0
/// </param>
/// <param name="PermissionKey">
/// The permission key used for localization and lookup.
/// MIGRATION: Mapped from VB.NET _permissionKey field. Nullable to match original VB.NET String behavior.
/// Example: VIEW
/// </param>
/// <param name="PermissionName">
/// The human-readable name of the permission.
/// MIGRATION: Mapped from VB.NET _PermissionName field. Nullable to match original VB.NET String behavior.
/// Example: View
/// </param>
public record PermissionDto(
    int PermissionId,
    string? PermissionCode,
    int ModuleDefId,
    string? PermissionKey,
    string? PermissionName
);
