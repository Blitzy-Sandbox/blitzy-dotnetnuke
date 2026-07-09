using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent configuration for the <see cref="UserMembership"/> entity, mapped as a STANDALONE
/// aggregate to the existing ASP.NET provider <c>aspnet_Membership</c> table (uniqueidentifier
/// <c>UserId</c> primary key).
/// </summary>
// MIGRATION (SCHEMA FIDELITY — finding #1): the previous model mapped membership as an EF OWNED type of
// the int-keyed User, which derived an INTEGER key column for [aspnet_Membership] and omitted several
// required NOT NULL columns — SQL that cannot run against the real GUID-keyed table. This configuration
// maps membership as a standalone entity keyed by the real uniqueidentifier [UserId], with EVERY
// required NOT NULL column present under its verbatim legacy name. UserRepository correlates a DNN
// integer User to its membership row through a deterministic [UserId] projection (a valid read/write
// projection that invents no column). Column set / nullability / key verified verbatim against the
// [aspnet_Membership] CREATE TABLE in Website/Providers/DataProviders/SqlDataProvider/InstallMembership.sql.
//
// The EF Core InMemory provider used by the integration tests ignores ToTable/HasColumnName and honours
// the key shape; the relational (SQL Server) provider used by the API host honours all mappings.
public class UserMembershipConfiguration : IEntityTypeConfiguration<UserMembership>
{
    /// <summary>Applies the <see cref="UserMembership"/> mapping to the model.</summary>
    /// <param name="builder">The entity type builder for <see cref="UserMembership"/>.</param>
    public void Configure(EntityTypeBuilder<UserMembership> builder)
    {
        // MIGRATION: ASP.NET membership-provider credential table (Guid-keyed), mapped verbatim.
        builder.ToTable("aspnet_Membership");

        // MIGRATION (SCHEMA FIDELITY — finding #1): the real primary key is a uniqueidentifier [UserId]
        // (FK -> aspnet_Users.UserId). The CLR key property MembershipUserId maps to column [UserId] and
        // is supplied by the repository (a deterministic projection of the DNN integer UserID), never
        // store-generated here.
        builder.HasKey(m => m.MembershipUserId);
        builder.Property(m => m.MembershipUserId).HasColumnName("UserId").ValueGeneratedNever();

        // --- Required NOT NULL columns (the shape the previous owned mapping could not satisfy) ---
        builder.Property(m => m.ApplicationId).HasColumnName("ApplicationId");
        builder.Property(m => m.Password).HasColumnName("Password");
        builder.Property(m => m.PasswordFormat).HasColumnName("PasswordFormat");
        builder.Property(m => m.PasswordSalt).HasColumnName("PasswordSalt");
        builder.Property(m => m.Approved).HasColumnName("IsApproved");                         // Approved               -> IsApproved
        builder.Property(m => m.LockedOut).HasColumnName("IsLockedOut");                       // LockedOut              -> IsLockedOut
        builder.Property(m => m.CreatedDate).HasColumnName("CreateDate");                      // CreatedDate            -> CreateDate
        builder.Property(m => m.LastLoginDate).HasColumnName("LastLoginDate");
        builder.Property(m => m.LastPasswordChangeDate).HasColumnName("LastPasswordChangedDate"); // LastPasswordChangeDate -> LastPasswordChangedDate
        builder.Property(m => m.LastLockoutDate).HasColumnName("LastLockoutDate");
        builder.Property(m => m.FailedPasswordAttemptCount).HasColumnName("FailedPasswordAttemptCount");
        builder.Property(m => m.FailedPasswordAttemptWindowStart).HasColumnName("FailedPasswordAttemptWindowStart");
        builder.Property(m => m.FailedPasswordAnswerAttemptCount).HasColumnName("FailedPasswordAnswerAttemptCount");
        builder.Property(m => m.FailedPasswordAnswerAttemptWindowStart).HasColumnName("FailedPasswordAnswerAttemptWindowStart");

        // --- Nullable columns ---
        builder.Property(m => m.Email).HasColumnName("Email");
        builder.Property(m => m.LoweredEmail).HasColumnName("LoweredEmail");
        builder.Property(m => m.PasswordQuestion).HasColumnName("PasswordQuestion");
        builder.Property(m => m.PasswordAnswer).HasColumnName("PasswordAnswer");
        builder.Property(m => m.MobilePIN).HasColumnName("MobilePIN");
        builder.Property(m => m.Comment).HasColumnName("Comment");

        // MIGRATION: these CLR members have NO [aspnet_Membership] column and are ignored:
        //   * IsOnLine / ObjectHydrated / UpdatePassword — legacy transient / progressive-hydration flags.
        //   * LastActivityDate and Username — these physically live on [aspnet_Users] (LastActivityDate /
        //     UserName), not [aspnet_Membership]; Username is retained only as a transient carrier.
        builder.Ignore(m => m.IsOnLine);
        builder.Ignore(m => m.ObjectHydrated);
        builder.Ignore(m => m.UpdatePassword);
        builder.Ignore(m => m.LastActivityDate);
        builder.Ignore(m => m.Username);
    }
}
