using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION (CP-FINAL review - Schema Preservation): Fluent API mapping for the EXISTING legacy membership table
// [aspnet_Users] (InstallCommon.sql). Part of the credential-store remap onto the existing membership schema (see
// AspNetApplicationConfiguration). The DNN integer [Users].Username is bridged to this GUID-keyed identity by
// UserName; the adapter resolves/creates the row by (ApplicationId, LoweredUserName).
public sealed class AspNetUserConfiguration : IEntityTypeConfiguration<AspNetUser>
{
    public void Configure(EntityTypeBuilder<AspNetUser> builder)
    {
        builder.ToTable("aspnet_Users");

        // MIGRATION: UserId is the GUID PK (physical DEFAULT NEWID()); generated in application code, so
        // ValueGeneratedNever keeps EF from treating it as store-generated on insert.
        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId)
            .HasColumnName("UserId")
            .ValueGeneratedNever();

        builder.Property(u => u.ApplicationId)
            .HasColumnName("ApplicationId");

        builder.Property(u => u.UserName)
            .HasColumnName("UserName")
            .HasColumnType("nvarchar(256)")
            .IsRequired();

        builder.Property(u => u.LoweredUserName)
            .HasColumnName("LoweredUserName")
            .HasColumnType("nvarchar(256)")
            .IsRequired();

        builder.Property(u => u.MobileAlias)
            .HasColumnName("MobileAlias")
            .HasColumnType("nvarchar(16)");

        builder.Property(u => u.IsAnonymous)
            .HasColumnName("IsAnonymous");

        builder.Property(u => u.LastActivityDate)
            .HasColumnName("LastActivityDate")
            .HasColumnType("datetime");
    }
}
