using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

// MIGRATION (Finding CP5 MAJOR — membership-password sourcing): EF Core 8 Fluent API mapping for the physical
// ASP.NET 2.0 Membership [aspnet_Membership] table, replacing the legacy ADO.NET / SqlDataProvider membership
// stored-procedure layer together with the reflection-based CBO hydration. Per ADR-002 the existing
// DotNetNuke 4.9.0.85 schema is mapped UNCHANGED: no migrations, no schema generation, no data migration.
//
// [aspnet_Membership] is the physical home of the credential hash; it is keyed by the membership UserId (a
// uniqueidentifier) shared 1:1 with [aspnet_Users]. UserRepository joins aspnet_Users -> aspnet_Membership on
// UserId to source the password hash for AuthService.LoginAsync's BCrypt verification.
//
// Only the two columns the credential lookup requires (UserId key + Password) are mapped. The table carries
// many additional columns (PasswordFormat, PasswordSalt, IsApproved, IsLockedOut, FailedPasswordAttemptCount,
// ...) that are intentionally not modelled; EF Core maps only declared properties and leaves the unmapped
// physical columns untouched (ADR-002). The Password column physically holds a BCrypt hash under the migrated
// security model (DEV-033 — DES replaced by BCrypt).

/// <summary>
/// Entity Framework Core configuration that maps the <see cref="AspNetMembership"/> POCO entity onto the
/// pre-existing, unchanged ASP.NET Membership <c>dbo.aspnet_Membership</c> table, exposing only the key and
/// the password hash required for credential verification.
/// </summary>
/// <remarks>
/// <para>
/// The class is discovered automatically by <c>DnnDbContext.OnModelCreating</c> through
/// <c>ModelBuilder.ApplyConfigurationsFromAssembly</c>. No relationship navigations are configured: the
/// repository joins <c>aspnet_Users</c> to <c>aspnet_Membership</c> explicitly by <c>UserId</c>.
/// </para>
/// <para>
/// Schema-fidelity rules (ADR-002): the table and column names are preserved verbatim, the
/// <c>uniqueidentifier</c> primary key is declared via <c>HasKey</c>, and no SQL-Server-specific defaults,
/// computed columns, or raw SQL are configured so the model builds cleanly against
/// <c>Microsoft.EntityFrameworkCore.InMemory</c> as well as SQL Server. Within the current scope this entity
/// is queried read-only, so no value-generation configuration is required (seed rows supply explicit keys).
/// </para>
/// </remarks>
public sealed class AspNetMembershipConfiguration : IEntityTypeConfiguration<AspNetMembership>
{
    /// <summary>
    /// Configures the <see cref="AspNetMembership"/> entity against the physical <c>dbo.aspnet_Membership</c>
    /// table.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="AspNetMembership"/> entity type.</param>
    public void Configure(EntityTypeBuilder<AspNetMembership> builder)
    {
        // Physical table: dbo.aspnet_Membership (created by aspnet_regsql.exe; consumed by InstallMembership.sql).
        // Schema-qualified and named verbatim per ADR-002 — the table already exists and must not be regenerated.
        builder.ToTable("aspnet_Membership", "dbo");

        // Primary key: [UserId] uniqueidentifier (PK NONCLUSTERED, FK to aspnet_Users.UserId). Seed rows
        // supply explicit Guid values, so the key is not store-generated within the read-only scope.
        builder.HasKey(m => m.UserId);

        // --- The physical [aspnet_Membership] columns required for credential verification (mapped verbatim) ---
        builder.Property(m => m.UserId).HasColumnName("UserId");        // [UserId]   uniqueidentifier NOT NULL (PK / FK)
        builder.Property(m => m.Password).HasColumnName("Password");    // [Password] nvarchar(128)    NOT NULL (BCrypt hash)
    }
}
