using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: [QA-FINAL Issue #1/#2 — CRITICAL] Fluent API mapping for UserPortal, the WRITE command-model over the
// EXISTING legacy [UserPortals] table (DotNetNuke.Schema.SqlDataProvider). Schema NOT altered. [UserPortals] is the
// user<->portal membership table that the vw_Users read view LEFT-joins to surface PortalId/Authorised. Because the
// physical [Users] table has no PortalId column, a user's tenant membership is persisted here; UserRepository.AddAsync
// stages a UserPortal alongside each new User so the write fans out to [Users] + [UserPortals] in one SaveChanges.
public sealed class UserPortalConfiguration : IEntityTypeConfiguration<UserPortal>
{
    public void Configure(EntityTypeBuilder<UserPortal> builder)
    {
        builder.ToTable("UserPortals");

        // [UserPortals].[UserPortalId] int IDENTITY(1,1) PRIMARY KEY (store-generated).
        builder.HasKey(up => up.UserPortalId);
        builder.Property(up => up.UserPortalId)
            .HasColumnName("UserPortalId")
            .ValueGeneratedOnAdd();

        // Legacy column names preserved exactly (DotNetNuke.Schema.SqlDataProvider casing).
        builder.Property(up => up.UserId).HasColumnName("UserId");
        builder.Property(up => up.PortalId).HasColumnName("PortalId");
        builder.Property(up => up.CreatedDate).HasColumnName("CreatedDate");
        builder.Property(up => up.Authorised).HasColumnName("Authorised");

        // MIGRATION: UserPortal -> User (FK = UserPortal.UserId references [Users].[UserID]). WithMany() because User
        // has no inverse collection (minimal blast radius). Cascade so deleting a user removes its membership rows;
        // this is a SEPARATE cascade path from User -> UserRole (a different child table), so no multiple-cascade-path
        // conflict arises. The User entity is mapped to [Users] (write) + vw_Users (read); the FK binds to the [Users]
        // table key on the relational side.
        builder.HasOne(up => up.User)
            .WithMany()
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
