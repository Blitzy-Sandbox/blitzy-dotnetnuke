namespace DnnMigration.Domain.Entities;

/// <summary>
/// Portal HTTP alias entity.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Portals.PortalAliasInfo
// (Library/Components/Portal/PortalAliasInfo.vb, L29). No XML attributes on the legacy properties.
public class PortalAlias
{
    public int PortalID { get; set; }
    public int PortalAliasID { get; set; }
    public string HTTPAlias { get; set; } = string.Empty;
}
