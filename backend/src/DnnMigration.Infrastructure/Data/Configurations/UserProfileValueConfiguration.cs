using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION (CP-final review - profile workflow parity): Fluent API mapping for UserProfileValue, Code-First to the
// EXISTING [UserProfile] EAV table (04.00.04.SqlDataProvider L1411); the schema is NOT altered. Only the physical
// columns are mapped. The legacy FK_UserProfile_Users / FK_UserProfile_ProfilePropertyDefinition (ON DELETE CASCADE)
// constraints exist on the physical table; this phase reads/writes value rows by (UserID, PropertyDefinitionID) and
// does not re-declare those FKs as navigations (the profile is loaded as a flat value set, not an object graph).
public sealed class UserProfileValueConfiguration : IEntityTypeConfiguration<UserProfileValue>
{
    public void Configure(EntityTypeBuilder<UserProfileValue> builder)
    {
        builder.ToTable("UserProfile");

        builder.HasKey(v => v.ProfileId);

        builder.Property(v => v.ProfileId)
            .HasColumnName("ProfileID");

        builder.Property(v => v.UserId)
            .HasColumnName("UserID");

        builder.Property(v => v.PropertyDefinitionId)
            .HasColumnName("PropertyDefinitionID");

        builder.Property(v => v.PropertyValue)
            .HasColumnName("PropertyValue")
            .HasMaxLength(3750);

        builder.Property(v => v.PropertyText)
            .HasColumnName("PropertyText");

        builder.Property(v => v.Visibility)
            .HasColumnName("Visibility");

        builder.Property(v => v.LastUpdatedDate)
            .HasColumnName("LastUpdatedDate");

        // MIGRATION: a user has at most one value row per property definition - the natural (UserID,
        // PropertyDefinitionID) pairing the legacy upsert relied on. A unique index makes that explicit and lets
        // the upsert in UserService.UpdateProfileAsync find an existing row deterministically.
        builder.HasIndex(v => new { v.UserId, v.PropertyDefinitionId }).IsUnique();
    }
}
