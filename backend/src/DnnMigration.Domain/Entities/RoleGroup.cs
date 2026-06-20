namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from RoleGroupInfo.vb (DotNetNuke.Security.Roles). Plain POCO. EF mapping via Fluent config in Infrastructure.
public class RoleGroup
{
    public int RoleGroupID { get; set; }
    public int PortalID { get; set; }
    public string? RoleGroupName { get; set; }
    public string? Description { get; set; }
}
