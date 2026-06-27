using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION (CP-FINAL review - Schema Preservation / Critical #4): Fluent API mapping for the EXISTING legacy
// membership table [aspnet_Membership] (InstallMembership.sql). The migrated BCrypt credential is stored in the
// existing [Password] column of THIS table instead of a new [UserCredentials] table, so the no-schema-alteration
// mandate (AAP 0.7.1) is satisfied and no out-of-band operator DDL is required for auth to work. Every column maps
// to the install-script DDL by exact name/type. NOTE: the physical schema declares FKs to aspnet_Users and
// aspnet_Applications and a CLUSTERED index on (ApplicationId, LoweredEmail); those are enforced by the existing
// database and are intentionally NOT re-declared here (the credential adapter joins by key, not by navigation), so
// the mapping neither creates nor alters any constraint.
public sealed class AspNetMembershipConfiguration : IEntityTypeConfiguration<AspNetMembership>
{
    public void Configure(EntityTypeBuilder<AspNetMembership> builder)
    {
        builder.ToTable("aspnet_Membership");

        // MIGRATION: UserId is the GUID PK (1:1 with aspnet_Users.UserId); supplied by the adapter, never
        // store-generated.
        builder.HasKey(m => m.UserId);
        builder.Property(m => m.UserId)
            .HasColumnName("UserId")
            .ValueGeneratedNever();

        builder.Property(m => m.ApplicationId).HasColumnName("ApplicationId");

        builder.Property(m => m.Password)
            .HasColumnName("Password")
            .HasColumnType("nvarchar(128)")
            .IsRequired();

        builder.Property(m => m.PasswordFormat).HasColumnName("PasswordFormat");

        builder.Property(m => m.PasswordSalt)
            .HasColumnName("PasswordSalt")
            .HasColumnType("nvarchar(128)")
            .IsRequired();

        builder.Property(m => m.MobilePIN)
            .HasColumnName("MobilePIN")
            .HasColumnType("nvarchar(16)");

        builder.Property(m => m.Email)
            .HasColumnName("Email")
            .HasColumnType("nvarchar(256)");

        builder.Property(m => m.LoweredEmail)
            .HasColumnName("LoweredEmail")
            .HasColumnType("nvarchar(256)");

        builder.Property(m => m.PasswordQuestion)
            .HasColumnName("PasswordQuestion")
            .HasColumnType("nvarchar(256)");

        builder.Property(m => m.PasswordAnswer)
            .HasColumnName("PasswordAnswer")
            .HasColumnType("nvarchar(128)");

        builder.Property(m => m.IsApproved).HasColumnName("IsApproved");
        builder.Property(m => m.IsLockedOut).HasColumnName("IsLockedOut");

        builder.Property(m => m.CreateDate).HasColumnName("CreateDate").HasColumnType("datetime");
        builder.Property(m => m.LastLoginDate).HasColumnName("LastLoginDate").HasColumnType("datetime");
        builder.Property(m => m.LastPasswordChangedDate).HasColumnName("LastPasswordChangedDate").HasColumnType("datetime");
        builder.Property(m => m.LastLockoutDate).HasColumnName("LastLockoutDate").HasColumnType("datetime");

        builder.Property(m => m.FailedPasswordAttemptCount).HasColumnName("FailedPasswordAttemptCount");
        builder.Property(m => m.FailedPasswordAttemptWindowStart).HasColumnName("FailedPasswordAttemptWindowStart").HasColumnType("datetime");
        builder.Property(m => m.FailedPasswordAnswerAttemptCount).HasColumnName("FailedPasswordAnswerAttemptCount");
        builder.Property(m => m.FailedPasswordAnswerAttemptWindowStart).HasColumnName("FailedPasswordAnswerAttemptWindowStart").HasColumnType("datetime");

        builder.Property(m => m.Comment)
            .HasColumnName("Comment")
            .HasColumnType("ntext");
    }
}
