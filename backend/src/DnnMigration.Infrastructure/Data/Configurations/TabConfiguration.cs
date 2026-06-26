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

        // MIGRATION (QA-4 #6): HasChildren IS genuinely computed at runtime (derived from the tab hierarchy) and is
        // NOT a column of [Tabs], so it remains Ignored. Level, by contrast, IS a real persisted column of the
        // legacy [Tabs] table ([Level] int NOT NULL DEFAULT 0 â€” present in the DotNetNuke.Schema CREATE and never
        // DROPped in any of the 88 scripts), so it is now mapped by EF convention (property name == column name).
        // The previous Ignore(t => t.Level) wrongly treated a real column as computed, so the TabService-computed
        // depth (Level = parent.Level + 1) was never persisted or read back (always 0 on the real DB). Removing the
        // Ignore restores behavioral parity with legacy.
        builder.Ignore(t => t.HasChildren);

        // MIGRATION (CP2 review — TabConfiguration #1): AuthorizedRoles and AdministratorRoles are NOT columns of
        // the final [Tabs] table. They existed in 01.x/02.x but were DROPPED in 03.00.01
        // (03.00.01.SqlDataProvider: "DROP COLUMN AdministratorRoles" / "DROP COLUMN AuthorizedRoles"); DNN sources
        // these role strings from tab permissions, not [Tabs]. Ignore them so EF convention does not emit SQL for
        // columns that do not exist in the existing schema. (Tabs.IsSecure, by contrast, was ADDED in 04.05.04 and
        // IS a real column, so it is intentionally left to convention and NOT ignored.)
        builder.Ignore(t => t.AuthorizedRoles);
        builder.Ignore(t => t.AdministratorRoles);

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
