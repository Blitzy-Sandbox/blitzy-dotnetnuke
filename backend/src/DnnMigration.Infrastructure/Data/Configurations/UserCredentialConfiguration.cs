using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION (CP2 review — DependencyInjection #1): Fluent API mapping for UserCredential, the BCrypt credential
// store that REPLACES the legacy aspnet_Membership table (AAP §0.5.2). This is a documented schema-COMPATIBILITY
// addition (MIGRATION_NOTES.md): a dedicated [UserCredentials] table, NOT an alteration of any existing legacy
// table. The project introduces no EF migration / EnsureCreated / schema SQL (Rules item #2 stays satisfied); the
// table is created out-of-band by the operator alongside the existing DNN schema, exactly as the legacy
// aspnet_* membership tables were installed via InstallMembership.sql.
public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("UserCredentials");

        // MIGRATION: one credential row per user; keyed directly on the DNN integer UserId. The key is NOT
        // database-generated (it mirrors the existing [Users].[UserID]); ValueGeneratedNever keeps EF from
        // treating it as an identity column on insert.
        builder.HasKey(uc => uc.UserId);
        builder.Property(uc => uc.UserId)
            .HasColumnName("UserID")
            .ValueGeneratedNever();

        // MIGRATION: the one-way BCrypt hash. BCrypt.Net-Next emits a 60-char hash; 256 gives ample headroom for
        // any future cost/format change without truncation. Required (a credential row always carries a hash).
        builder.Property(uc => uc.PasswordHash)
            .HasColumnName("PasswordHash")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(uc => uc.CreatedDate)
            .HasColumnName("CreatedDate");

        builder.Property(uc => uc.LastModifiedDate)
            .HasColumnName("LastModifiedDate");
    }
}
