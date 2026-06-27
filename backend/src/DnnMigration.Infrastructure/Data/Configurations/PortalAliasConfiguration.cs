using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API binding of the PortalAlias POCO to the EXISTING legacy [PortalAlias] table
// (DotNetNuke.Schema.SqlDataProvider L1288). No schema is created or altered (AAP 0.1.2 / 0.6.2 / 0.7.1) — this
// configuration only maps the entity onto the three columns already present in the DNN database:
//   [PortalAliasID] int NOT NULL IDENTITY(1,1)  (PRIMARY KEY CLUSTERED)
//   [PortalID]      int NOT NULL
//   [HTTPAlias]     nvarchar(200) NULL
// The legacy column names/casing are preserved exactly via HasColumnName/HasColumnType so EF Core reads the
// same physical columns DataProvider.GetPortalByAlias read.
public sealed class PortalAliasConfiguration : IEntityTypeConfiguration<PortalAlias>
{
    public void Configure(EntityTypeBuilder<PortalAlias> builder)
    {
        builder.ToTable("PortalAlias");

        // [PortalAliasID] is the identity primary key; EF generates it on insert (ValueGeneratedOnAdd).
        builder.HasKey(a => a.PortalAliasId);
        builder.Property(a => a.PortalAliasId)
            .HasColumnName("PortalAliasID")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.PortalId)
            .HasColumnName("PortalID")
            .IsRequired();

        // Legacy [HTTPAlias] is a nullable nvarchar(200); preserve exact name, type and nullability.
        builder.Property(a => a.HttpAlias)
            .HasColumnName("HTTPAlias")
            .HasColumnType("nvarchar(200)")
            .IsRequired(false);
    }
}
