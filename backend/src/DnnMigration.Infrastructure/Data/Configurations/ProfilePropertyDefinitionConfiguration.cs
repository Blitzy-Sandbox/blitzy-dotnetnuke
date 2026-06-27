using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION (CP-final review - profile workflow parity): Fluent API mapping for ProfilePropertyDefinition,
// Code-First to the EXISTING [ProfilePropertyDefinition] table (04.00.04.SqlDataProvider L1107); the schema is NOT
// altered. Only the physical columns are mapped, so EF never emits SQL for a non-existent column.
public sealed class ProfilePropertyDefinitionConfiguration : IEntityTypeConfiguration<ProfilePropertyDefinition>
{
    public void Configure(EntityTypeBuilder<ProfilePropertyDefinition> builder)
    {
        builder.ToTable("ProfilePropertyDefinition");

        builder.HasKey(p => p.PropertyDefinitionId);

        builder.Property(p => p.PropertyDefinitionId)
            .HasColumnName("PropertyDefinitionID");

        builder.Property(p => p.PortalId)
            .HasColumnName("PortalID");

        builder.Property(p => p.ModuleDefId)
            .HasColumnName("ModuleDefID");

        builder.Property(p => p.Deleted)
            .HasColumnName("Deleted");

        builder.Property(p => p.DataType)
            .HasColumnName("DataType");

        builder.Property(p => p.DefaultValue)
            .HasColumnName("DefaultValue")
            .HasMaxLength(50);

        builder.Property(p => p.PropertyCategory)
            .HasColumnName("PropertyCategory")
            .HasMaxLength(50);

        builder.Property(p => p.PropertyName)
            .HasColumnName("PropertyName")
            .HasMaxLength(50);

        builder.Property(p => p.Length)
            .HasColumnName("Length");

        builder.Property(p => p.Required)
            .HasColumnName("Required");

        builder.Property(p => p.ValidationExpression)
            .HasColumnName("ValidationExpression")
            .HasMaxLength(100);

        builder.Property(p => p.ViewOrder)
            .HasColumnName("ViewOrder");

        builder.Property(p => p.Visible)
            .HasColumnName("Visible");
    }
}
