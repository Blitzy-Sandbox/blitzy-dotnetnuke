namespace DnnMigration.Domain.Enums;

/// <summary>
/// Identifies the rendering format/category of a vendor banner advertisement.
/// Maps to the integer banner-advertising field on the Portal entity
/// (see <c>DnnMigration.Domain.Entities.Portal.BannerAdvertising</c>).
/// </summary>
// MIGRATION: Converted verbatim from the DotNetNuke VB.NET `Public Enum BannerType As Integer`
// in Library/Components/Vendors/BannerTypeInfo.vb (L27; legacy namespace DotNetNuke.Services.Vendors).
// The Vendors domain is out-of-scope context for this migration: ONLY the BannerType enum is
// extracted here. The legacy BannerTypeInfo class and its vendor controllers/collections are NOT
// ported. All 7 members and their explicit integer values are preserved exactly because these
// values are a persistence/wire contract mapped to an existing integer column by EF Core downstream.
public enum BannerType
{
    Banner = 1,
    MicroButton = 2,
    Button = 3,
    Block = 4,
    Skyscraper = 5,
    Text = 6,
    Script = 7
}
