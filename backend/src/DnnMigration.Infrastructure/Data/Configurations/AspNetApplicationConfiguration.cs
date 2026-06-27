using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION (CP-FINAL review - Schema Preservation): Fluent API mapping for the EXISTING legacy membership table
// [aspnet_Applications] (InstallCommon.sql). This REPLACES the previously-introduced [UserCredentials] schema
// addition: credentials now map onto the membership tables that already ship with the DNN database, so NO new
// table is required (AAP 0.7.1 - no schema alteration; Rules item #2 - no EF migration / EnsureCreated / schema
// SQL). The column names/types below bind exactly to the install-script DDL.
public sealed class AspNetApplicationConfiguration : IEntityTypeConfiguration<AspNetApplication>
{
    public void Configure(EntityTypeBuilder<AspNetApplication> builder)
    {
        builder.ToTable("aspnet_Applications");

        // MIGRATION: ApplicationId is the GUID PK (physical DEFAULT NEWID()). The adapter generates the GUID in
        // application code, so ValueGeneratedNever keeps EF from treating it as store-generated on insert.
        builder.HasKey(a => a.ApplicationId);
        builder.Property(a => a.ApplicationId)
            .HasColumnName("ApplicationId")
            .ValueGeneratedNever();

        builder.Property(a => a.ApplicationName)
            .HasColumnName("ApplicationName")
            .HasColumnType("nvarchar(256)")
            .IsRequired();

        builder.Property(a => a.LoweredApplicationName)
            .HasColumnName("LoweredApplicationName")
            .HasColumnType("nvarchar(256)")
            .IsRequired();

        builder.Property(a => a.Description)
            .HasColumnName("Description")
            .HasColumnType("nvarchar(256)");
    }
}
