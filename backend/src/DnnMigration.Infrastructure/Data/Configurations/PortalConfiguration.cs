using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for Portal (legacy PortalInfo, Library/Components/Portal/PortalInfo.vb,
// namespace DotNetNuke.Entities.Portals). Code-First mapped to the EXISTING [Portals] table (created in
// 01.00.00.SqlDataProvider); schema is not altered. Portal is the multi-tenant root: PortalId is the tenant
// discriminator that scopes every other entity/query (AAP 0.7.1).
public sealed class PortalConfiguration : IEntityTypeConfiguration<Portal>
{
    public void Configure(EntityTypeBuilder<Portal> builder)
    {
        builder.ToTable("Portals");

        builder.HasKey(p => p.PortalId);
        builder.Property(p => p.PortalId).HasColumnName("PortalID");

        // MIGRATION: PortalInfo._Users and ._Pages are computed counts (initialized to Null.NullInteger,
        // PortalInfo.vb L61-L62) populated at runtime by DNN, NOT columns of the [Portals] table.
        // Ignore them so EF does not attempt to map non-existent columns.
        builder.Ignore(p => p.Users);
        builder.Ignore(p => p.Pages);

        // NOTE: all other scalar columns (PortalName, LogoFile, Guid, AdministratorId, etc.) rely on EF
        // convention (property name == column name; SQL Server's default collation is case-insensitive, so
        // e.g. "AdministratorId" resolves to legacy "AdministratorId"). Only the key gets explicit HasColumnName.
    }
}
