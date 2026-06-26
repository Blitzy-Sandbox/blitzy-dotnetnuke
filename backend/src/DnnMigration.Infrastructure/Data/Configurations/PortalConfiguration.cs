using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for Portal (legacy PortalInfo, Library/Components/Portal/PortalInfo.vb,
// namespace DotNetNuke.Entities.Portals). Code-First mapped to the EXISTING [Portals] table (created in
// 01.00.00.SqlDataProvider); schema is not altered. Portal is the multi-tenant root: PortalId is the tenant
// discriminator that scopes every other entity/query (AAP 0.7.1).
public sealed class PortalConfiguration : IEntityTypeConfiguration<Portal>
{
    public void Configure(EntityTypeBuilder<Portal> builder)
    {
        builder.ToTable("Portals");

        builder.HasKey(p => p.PortalId);
        builder.Property(p => p.PortalId).HasColumnName("PortalID");

        // MIGRATION: PortalInfo._Users and ._Pages are computed counts (initialized to Null.NullInteger,
        // PortalInfo.vb L61-L62) populated at runtime by DNN, NOT columns of the [Portals] table.
        // Ignore them so EF does not attempt to map non-existent columns.
        builder.Ignore(p => p.Users);
        builder.Ignore(p => p.Pages);

        // MIGRATION (QA-4 #1 â€” schema fidelity, CRITICAL): the following five properties are NOT physical columns
        // of the legacy [Portals] table (authoritative final-state = 31 columns from the consolidated
        // DotNetNuke.Schema CREATE plus every ALTER ... ADD through 04.04.00). In legacy DNN they are computed
        // stored-proc/view aliases or live on other tables:
        //   - AdministratorRoleName / RegisteredRoleName / SuperTabId : surfaced ONLY as "... AS <alias>" /
        //     "'SuperTabId' = (select TabID from Tabs ...)" projections in stored procs/views, never stored on [Portals].
        //   - Email / Version : not columns of [Portals] at all.
        // Mapping them by EF convention emitted phantom columns, so against the real SQL Server schema every
        // Portal SELECT/INSERT/UPDATE raised "Invalid column name" -> HTTP 500. Ignoring them drops the EF column
        // mapping while KEEPING the CLR properties (so DTOs/AutoMapper/services are unaffected) â€” the exact pattern
        // already used for Users/Pages above. They are populated in-memory by the Application layer when needed.
        builder.Ignore(p => p.AdministratorRoleName);
        builder.Ignore(p => p.RegisteredRoleName);
        builder.Ignore(p => p.SuperTabId);
        builder.Ignore(p => p.Email);
        builder.Ignore(p => p.Version);

        // MIGRATION (QA-4 #2 â€” schema fidelity, MINOR): preserve the EXACT legacy column names for the two
        // properties whose PascalCase identifier diverges from the physical column. These resolve under SQL
        // Server's default case-INSENSITIVE collation but break under a case-sensitive collation and violate the
        // AAP's exact-legacy-name preservation. Legacy columns are [GUID] (uniqueidentifier) and [TimezoneOffset] (int).
        builder.Property(p => p.Guid).HasColumnName("GUID");
        builder.Property(p => p.TimeZoneOffset).HasColumnName("TimezoneOffset");

        // NOTE: all remaining scalar columns (PortalName, LogoFile, AdministratorId, etc.) rely on EF convention
        // (property name == legacy column name). Only the key, the two case-divergent columns above, and the
        // Ignored non-columns require explicit configuration.
    }
}
