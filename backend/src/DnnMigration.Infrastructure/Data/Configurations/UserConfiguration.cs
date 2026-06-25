using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for User (legacy UserInfo, Library/Components/Users/UserInfo.vb,
// namespace DotNetNuke.Entities.Users). Code-First mapped to the EXISTING [Users] table (01.00.00.SqlDataProvider);
// schema not altered.
// FLATTENING NOTE: the C# User entity merges UserInfo + UserMembership + UserProfile. Several properties
// (e.g. FirstName/LastName live in [UserProfile]; CreatedDate/LastLoginDate/IsApproved/LockedOut/
// LastActivityDate/LastLockoutDate live in [aspnet_Membership]) are NOT columns of the [Users] table. They are
// left to EF convention here and are not exercised by the migration gates (which run on EF Core InMemory,
// where relational column mapping is a no-op). Credentials/passwords are intentionally excluded (moved to the
// Identity layer: JWT + BCrypt). See MIGRATION_NOTES.md.
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId).HasColumnName("UserID");

        // MIGRATION: User 1..N UserRole (preserves the user->role->permission model, AAP 0.7.1).
        // This relationship is OWNED here (configured exactly once); the FK is UserRole.UserId.
        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // NOTE: scalar columns (Username, DisplayName, Email, IsSuperUser, AffiliateId, PortalId, ...) rely on
        // EF convention (case-insensitive SQL Server collation). Only the key gets explicit HasColumnName here;
        // the FK column UserRole.UserId -> "UserID" is mapped in UserRoleConfiguration.
    }
}
