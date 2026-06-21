using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

// MIGRATION (Finding CP5 MAJOR — membership-password sourcing): EF Core 8 Fluent API mapping for the physical
// ASP.NET 2.0 Membership [aspnet_Users] table, replacing the legacy ADO.NET / SqlDataProvider membership
// stored-procedure layer (GetUserByUserName / aspnet_Membership_GetPasswordWithFormat) together with the
// reflection-based CBO hydration. Per ADR-002 the existing DotNetNuke 4.9.0.85 schema is mapped UNCHANGED: no
// migrations, no schema generation, no data migration.
//
// [aspnet_Users] is the bridge from a DNN username to the membership UserId (a uniqueidentifier) under which
// credentials live in [aspnet_Membership]. The legacy membership procedures match on the lowercase
// LoweredUserName column ("LOWER(@UserName) = u.LoweredUserName" — InstallMembership.sql), so the repository
// queries this table by an already-lowered username and then joins to [aspnet_Membership] on UserId.
//
// MIGRATION (casing): the [aspnet_Users] DDL declares its key as [UserId] (lowercase 'd'). The CLR property
// keeps the same name, so HasColumnName is supplied for explicitness/parity with the sibling membership
// configuration and the existing UserPortalConfiguration casing-drift corrections. Only the two columns the
// credential lookup requires (UserId key + LoweredUserName) are mapped; aspnet_Users carries additional
// columns (ApplicationId, UserName, IsAnonymous, LastActivityDate, ...) that are intentionally not modelled.

/// <summary>
/// Entity Framework Core configuration that maps the <see cref="AspNetUser"/> POCO entity onto the
/// pre-existing, unchanged ASP.NET Membership <c>dbo.aspnet_Users</c> table, exposing only the key and the
/// lowered user name required to resolve a credential lookup.
/// </summary>
/// <remarks>
/// <para>
/// The class is discovered automatically by <c>DnnDbContext.OnModelCreating</c> through
/// <c>ModelBuilder.ApplyConfigurationsFromAssembly</c>. No relationship navigations are configured: the
/// repository joins <c>aspnet_Users</c> to <c>aspnet_Membership</c> explicitly by <c>UserId</c>, so no
/// navigation collection is added and no cascade path is introduced.
/// </para>
/// <para>
/// Schema-fidelity rules (ADR-002): the table and column names are preserved verbatim, the
/// <c>uniqueidentifier</c> primary key is declared via <c>HasKey</c>, and no SQL-Server-specific defaults,
/// computed columns, or raw SQL are configured so the model builds cleanly against
/// <c>Microsoft.EntityFrameworkCore.InMemory</c> as well as SQL Server. Within the current scope this entity
/// is queried read-only, so no value-generation configuration is required (seed rows supply explicit keys).
/// </para>
/// </remarks>
public sealed class AspNetUserConfiguration : IEntityTypeConfiguration<AspNetUser>
{
    /// <summary>
    /// Configures the <see cref="AspNetUser"/> entity against the physical <c>dbo.aspnet_Users</c> table.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="AspNetUser"/> entity type.</param>
    public void Configure(EntityTypeBuilder<AspNetUser> builder)
    {
        // Physical table: dbo.aspnet_Users (created by aspnet_regsql.exe; referenced by InstallMembership.sql).
        // Schema-qualified and named verbatim per ADR-002 — the table already exists and must not be regenerated.
        builder.ToTable("aspnet_Users", "dbo");

        // Primary key: [UserId] uniqueidentifier. Seed rows supply explicit Guid values, so the key is not
        // store-generated within the read-only scope.
        builder.HasKey(u => u.UserId);

        // --- The physical [aspnet_Users] columns required for the credential bridge (mapped verbatim) ---
        builder.Property(u => u.UserId).HasColumnName("UserId");                       // [UserId]          uniqueidentifier NOT NULL (PK)
        builder.Property(u => u.LoweredUserName).HasColumnName("LoweredUserName");     // [LoweredUserName] nvarchar(256)    NOT NULL (match key)
    }
}
