using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent configuration for the <see cref="UserPortal"/> junction entity, mapped to the
/// existing DNN <c>UserPortals</c> table (composite key <c>UserId</c> + <c>PortalId</c>).
/// </summary>
// MIGRATION (SCHEMA FIDELITY — finding #1): restores the real portal-association shape. The legacy DNN
// schema has NO [Users].[PortalID] column; a user's membership of a portal is the [UserPortals]
// junction, keyed by the COMPOSITE primary key ([UserId], [PortalId]) with foreign keys to
// [Users].[UserID] and [Portals].[PortalID]. This configuration maps that table under verbatim legacy
// names with its real composite key. The two FK columns are mapped, but the FK RELATIONSHIPS are not
// declared as EF navigations (see the detailed rationale on Configure below - identifying-relationship
// key propagation would reject the legitimate portal id 0 and break the by-value junction write).
// Column set / nullability / key verified against the [UserPortals] CREATE TABLE and its constraints in
// DotNetNuke.Schema.SqlDataProvider. Auto-discovered and applied by DnnDbContext.OnModelCreating.
//
// The EF Core InMemory provider used by the integration tests ignores ToTable/HasColumnName, but
// honours the composite key; the relational (SQL Server) provider used by the API host honours all
// column/table mappings (the physical FK constraints already exist on the authoritative database).
public class UserPortalConfiguration : IEntityTypeConfiguration<UserPortal>
{
    /// <summary>Applies the <see cref="UserPortal"/> mapping to the model.</summary>
    /// <param name="builder">The entity type builder for <see cref="UserPortal"/>.</param>
    public void Configure(EntityTypeBuilder<UserPortal> builder)
    {
        // MIGRATION: legacy table [UserPortals] (PK [PK_{objectQualifier}UserPortals] CLUSTERED on the
        // composite ([UserId], [PortalId])).
        builder.ToTable("UserPortals");
        builder.HasKey(e => new { e.UserId, e.PortalId });

        builder.Property(e => e.UserId).HasColumnName("UserId");
        builder.Property(e => e.PortalId).HasColumnName("PortalId");

        // MIGRATION: [UserPortalId] is a surrogate IDENTITY(1,1) column but NOT the primary key; mapped
        // as store-generated so it is not required on insert.
        builder.Property(e => e.UserPortalId).HasColumnName("UserPortalId").ValueGeneratedOnAdd();
        builder.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
        builder.Property(e => e.Authorised).HasColumnName("Authorised");

        // MIGRATION (SCHEMA FIDELITY - finding #1): the two foreign keys
        //   [FK_{objectQualifier}UserPortals_{objectQualifier}Users]   (UserId  -> Users.UserID,   CASCADE)
        //   [FK_{objectQualifier}UserPortals_{objectQualifier}Portals] (PortalId -> Portals.PortalID, CASCADE)
        // are deliberately NOT modelled as EF Core relationships (no HasOne/WithMany). Because BOTH FK
        // columns are also part of the composite primary key, declaring them as relationships makes them
        // EF "identifying relationships": on insert EF then requires the principal [Users]/[Portals]
        // entity to be TRACKED so it can propagate the principal key into the dependent key, and it
        // treats a key value equal to the CLR default (PortalId = 0 - a LEGITIMATE DNN portal id) as an
        // unknown value awaiting generation, throwing "The value of 'UserPortal.PortalId' is unknown ...
        // the principal entity in the relationship is not known". That failure originates in the
        // provider-agnostic change tracker, so it would break the SQL Server API host, not merely the
        // InMemory test store. The legacy data layer wrote this junction purely BY KEY VALUE (the
        // AddUserPortal stored procedure inserted [UserId]/[PortalId] directly, never materialising the
        // parent rows), and UserRepository preserves exactly that by-value write. Mapping the columns +
        // real composite key WITHOUT EF relationships is therefore both the faithful mapping and the
        // correct runtime shape: the real FK CONSTRAINTS already exist in the physical database (the
        // authoritative schema, never regenerated from this model), and every user/portal query uses an
        // explicit LINQ join rather than a navigation, so no relationship declaration is needed.
    }
}
