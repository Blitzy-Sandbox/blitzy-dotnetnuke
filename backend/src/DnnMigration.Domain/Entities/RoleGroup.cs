namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a role group — a logical grouping of security roles that belong to a
/// single portal within the DotNetNuke multi-portal model. Role groups allow
/// administrators to organize related roles (for example by department or function)
/// so they can be managed and assigned collectively.
/// </summary>
/// <remarks>
/// MIGRATION: Ported verbatim from the legacy VB.NET <c>RoleGroupInfo</c> class
/// (<c>Library/Components/Security/Roles/RoleGroupInfo.vb</c>, namespace
/// <c>DotNetNuke.Security.Roles</c>) of DotNetNuke 4.9.0.85. This is a plain POCO in
/// the Domain layer carrying ZERO framework dependencies; persistence mapping onto
/// the existing (unchanged) database schema is supplied by an EF Core Fluent
/// <c>IEntityTypeConfiguration&lt;RoleGroup&gt;</c> in the Infrastructure layer rather
/// than by attributes on this type.
///
/// The legacy private-backing-field / <c>Property Get/Set</c> pattern is converted
/// directly to C# auto-properties, and the original member names are preserved
/// verbatim to retain the public contract of the original domain object.
/// </remarks>
public class RoleGroup
{
    /// <summary>
    /// Gets or sets the unique identifier (primary key) of this role group.
    /// Legacy VB equivalent: <c>RoleGroupID As Integer</c>.
    /// </summary>
    public int RoleGroupID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the portal that owns this role group.
    /// Acts as the foreign key to the owning <c>Portal</c> entity.
    /// Legacy VB equivalent: <c>PortalID As Integer</c>.
    /// </summary>
    public int PortalID { get; set; }

    /// <summary>
    /// Gets or sets the display name of the role group.
    /// Legacy VB equivalent: <c>RoleGroupName As String</c>.
    /// </summary>
    public string? RoleGroupName { get; set; }

    /// <summary>
    /// Gets or sets the human-readable description of the role group.
    /// Legacy VB equivalent: <c>Description As String</c>.
    /// </summary>
    public string? Description { get; set; }
}
