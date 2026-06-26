using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for UserRole (legacy UserRoleInfo, Library/Components/Users/UserRoleInfo.vb,
// namespace DotNetNuke.Entities.Users). Code-First mapped to the EXISTING [UserRoles] table (01.00.00.SqlDataProvider);
// schema not altered. UserRole is the user<->role association carrying the real columns
// EffectiveDate/ExpiryDate/IsTrialUsed (Subscribed is NOT a physical column â€” see the Ignore below).
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

        // MIGRATION (QA-4 #5, CRITICAL): Subscribed is NOT a physical [UserRoles] column. The authoritative legacy
        // [UserRoles] table has 6 columns (UserRoleID, UserID, RoleID, ExpiryDate, IsTrialUsed, EffectiveDate);
        // "Subscribed" exists only as a COMPUTED stored-proc alias ('Subscribed' = case when ...). Mapping it by
        // convention emitted a phantom [Subscribed] column -> "Invalid column name" on real SQL Server for
        // GetUserRolesAsync (the role-assignment / auto-assign flows central to the user->role->permission model).
        // Ignore the EF column mapping; the CLR property is retained so the RoleProfile UserRole -> UserRoleDto
        // AutoMapper projection stays valid (the value is computed in-memory, not read from a column).
        builder.Ignore(ur => ur.Subscribed);

        // NOTE: the User -> UserRole side is owned by UserConfiguration (do NOT reconfigure it here).
        // The real scalar columns (EffectiveDate, ExpiryDate, IsTrialUsed) rely on EF convention.
    }
}
