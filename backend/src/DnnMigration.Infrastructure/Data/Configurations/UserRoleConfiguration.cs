using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for UserRole (legacy UserRoleInfo, Library/Components/Users/UserRoleInfo.vb,
// namespace DotNetNuke.Entities.Users). Code-First mapped to the EXISTING [UserRoles] table (01.00.00.SqlDataProvider);
// schema not altered. UserRole is the user<->role association carrying EffectiveDate/ExpiryDate/IsTrialUsed/Subscribed.
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        builder.HasKey(ur => ur.UserRoleId);
        builder.Property(ur => ur.UserRoleId).HasColumnName("UserRoleID");

        // FK columns carried by the join row.
        builder.Property(ur => ur.UserId).HasColumnName("UserID");
        builder.Property(ur => ur.RoleId).HasColumnName("RoleID");

        // MIGRATION: UserRole -> Role side (OWNED here). Role has no inverse collection, so WithMany().
        // Restrict (not Cascade) because User -> UserRole is already Cascade; two cascade paths into UserRoles
        // would be rejected by relational providers. Preserves the user->role->permission model (AAP 0.7.1).
        builder.HasOne(ur => ur.Role)
            .WithMany()
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // NOTE: the User -> UserRole side is owned by UserConfiguration (do NOT reconfigure it here).
        // Scalar columns (EffectiveDate, ExpiryDate, IsTrialUsed, Subscribed) rely on EF convention.
    }
}
