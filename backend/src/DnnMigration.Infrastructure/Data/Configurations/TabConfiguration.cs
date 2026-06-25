using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for Tab (legacy TabInfo, Library/Components/Tabs/TabInfo.vb,
// namespace DotNetNuke.Entities.Tabs). Code-First mapped to the EXISTING [Tabs] table (01.00.00.SqlDataProvider);
// schema not altered. A Tab is a DNN page scoped to a portal via PortalId (tenant discriminator, AAP 0.7.1).
public sealed class TabConfiguration : IEntityTypeConfiguration<Tab>
{
    public void Configure(EntityTypeBuilder<Tab> builder)
    {
        builder.ToTable("Tabs");

        builder.HasKey(t => t.TabId);
        builder.Property(t => t.TabId).HasColumnName("TabID");

        // MIGRATION: tenant discriminator preserved.
        builder.Property(t => t.PortalId).HasColumnName("PortalID");

        // MIGRATION: Level and HasChildren are computed/runtime in DNN (derived from the tab hierarchy),
        // NOT stored columns of [Tabs]. Ignore so EF does not map non-existent columns.
        builder.Ignore(t => t.Level);
        builder.Ignore(t => t.HasChildren);

        // MIGRATION: Tab 1..N TabPermission (page-level access control). OWNED here (configured once);
        // FK is TabPermission.TabId. WithOne() because TabPermission has no back-navigation to Tab.
        builder.HasMany(t => t.TabPermissions)
            .WithOne()
            .HasForeignKey(tp => tp.TabId)
            .OnDelete(DeleteBehavior.Cascade);

        // NOTE: ParentId is intentionally NOT mapped explicitly — the legacy DB column is the mixed-case
        // "ParentId" (== property name), so EF convention maps it correctly. Other scalar columns
        // (TabName, TabOrder, IsVisible, Title, Url, SkinSrc, ...) likewise rely on convention.
    }
}
