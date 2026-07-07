using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core 8 Fluent (<see cref="IEntityTypeConfiguration{TEntity}"/>) mapping for the
/// <see cref="Tab"/> (portal page) Domain entity. Maps <see cref="Tab"/> onto the existing legacy
/// DotNetNuke <c>Tabs</c> table, preserving the physical table name, column names, and identity
/// semantics VERBATIM so the legacy relational database remains the authoritative system of record
/// (no schema changes are performed).
/// </summary>
/// <remarks>
/// <para>
/// This configuration is auto-discovered and applied by
/// <see cref="DnnDbContext"/>.<c>OnModelCreating</c> through
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(...)</c>; it is never referenced directly.
/// </para>
/// <para>
/// MIGRATION: Converted from the VB.NET DotNetNuke data-access mapping that was expressed implicitly
/// by the <c>SqlDataProvider</c> stored procedures (e.g. <c>AddTab</c>/<c>UpdateTab</c>) and the
/// <c>vw_Tabs</c> view, together with the <c>DotNetNuke.Entities.Tabs.TabInfo</c> XML-serialization
/// contract (<c>Library/Components/Tabs/TabInfo.vb</c>). The physical schema is the contract; the
/// stored-procedure logic is not ported.
/// </para>
/// <para>
/// Schema authority: the final DNN 4.9 <c>Tabs</c> shape is the cumulative result of every
/// <c>*.SqlDataProvider</c> version script (00.00.00 → 04.09.00). Notable evolutions that this
/// mapping reflects (rather than the misleading single-file base definition):
/// <list type="bullet">
///   <item><description>
///     <c>Level</c> is a PERSISTED physical column (<c>[Level] int NOT NULL DEFAULT 0</c>, added in
///     01.00.05, re-affirmed by <c>ALTER COLUMN [Level] int NOT NULL</c> in 03.01.01 and selected
///     directly as <c>T.[Level]</c> in the 04.05.04 <c>vw_Tabs</c> view). It is therefore MAPPED.
///   </description></item>
///   <item><description>
///     <c>AuthorizedRoles</c> and <c>AdministratorRoles</c> were DROPPED from the <c>Tabs</c> table
///     in 03.00.01 (tab-level security moved to the <c>TabPermission</c> table). They are no longer
///     physical columns in 4.9 and are therefore IGNORED.
///   </description></item>
///   <item><description>
///     <c>HasChildren</c> is a computed flag surfaced only by the <c>vw_Tabs</c> view
///     (<c>CASE WHEN EXISTS (SELECT 1 FROM Tabs T2 WHERE T2.ParentId = T.TabID) ...</c>); it has no
///     backing column and is therefore IGNORED.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
public class TabConfiguration : IEntityTypeConfiguration<Tab>
{
    /// <summary>
    /// Configures the <see cref="Tab"/> entity type against the existing <c>Tabs</c> table.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Tab"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Tab> builder)
    {
        // Map to the existing legacy DNN table. Name preserved verbatim (data-model fidelity).
        builder.ToTable("Tabs");

        // Primary key: TabID. In the legacy schema this is an IDENTITY(0, 1) column, so the database
        // generates the value on insert.
        builder.HasKey(e => e.TabID);
        builder.Property(e => e.TabID)
            .HasColumnName("TabID")
            .ValueGeneratedOnAdd();

        // --- Physical scalar columns (mapped 1:1; column names preserved verbatim) -------------------
        // Every mapped property name already equals its physical column name; HasColumnName is applied
        // explicitly nonetheless to lock the mapping to the legacy schema and keep it stable regardless
        // of any future EF Core naming-convention/policy changes.

        builder.Property(e => e.TabOrder).HasColumnName("TabOrder");
        builder.Property(e => e.PortalID).HasColumnName("PortalID");

        // MIGRATION: legacy XML serialization element was "name" (TabInfo.vb: <XmlElement("name")>),
        // but the physical column and the C# property are both "TabName". The property-vs-XML-element
        // divergence does not affect persistence: the column name is what EF maps.
        builder.Property(e => e.TabName).HasColumnName("TabName");

        // MIGRATION: legacy XML element was "visible" (<XmlElement("visible")>); physical column and
        // property are "IsVisible".
        builder.Property(e => e.IsVisible).HasColumnName("IsVisible");

        builder.Property(e => e.ParentId).HasColumnName("ParentId");

        // MIGRATION: Level is <XmlIgnore> in TabInfo.vb, which excludes it from XML SERIALIZATION only
        // — it is NOT excluded from PERSISTENCE. Level is a real, persisted column ([Level] int NOT NULL
        // DEFAULT 0, added 01.00.05; ALTER COLUMN in 03.01.01; selected as T.[Level] straight from the
        // Tabs table in the 04.05.04 vw_Tabs view). Mapping it (rather than ignoring it) is what
        // preserves data-model fidelity to the existing schema. EF Core delimits the reserved word
        // ([Level]) automatically for the SQL Server provider.
        builder.Property(e => e.Level).HasColumnName("Level");

        builder.Property(e => e.IconFile).HasColumnName("IconFile");

        // MIGRATION: legacy XML element was "disabled" (<XmlElement("disabled")>); physical column and
        // property are "DisableLink".
        builder.Property(e => e.DisableLink).HasColumnName("DisableLink");

        builder.Property(e => e.Title).HasColumnName("Title");
        builder.Property(e => e.Description).HasColumnName("Description");
        builder.Property(e => e.KeyWords).HasColumnName("KeyWords");
        builder.Property(e => e.IsDeleted).HasColumnName("IsDeleted");
        builder.Property(e => e.Url).HasColumnName("Url");
        builder.Property(e => e.SkinSrc).HasColumnName("SkinSrc");
        builder.Property(e => e.ContainerSrc).HasColumnName("ContainerSrc");
        builder.Property(e => e.TabPath).HasColumnName("TabPath");
        builder.Property(e => e.StartDate).HasColumnName("StartDate");
        builder.Property(e => e.EndDate).HasColumnName("EndDate");
        builder.Property(e => e.RefreshInterval).HasColumnName("RefreshInterval");
        builder.Property(e => e.PageHeadText).HasColumnName("PageHeadText");
        builder.Property(e => e.IsSecure).HasColumnName("IsSecure");

        // --- Non-persisted properties (ignored so EF never maps them to non-existent columns) ---------

        // MIGRATION: HasChildren has no backing column in the Tabs table. It is a derived flag computed
        // by the legacy vw_Tabs view (CASE WHEN EXISTS (SELECT 1 FROM Tabs T2 WHERE T2.ParentId =
        // T.TabID)). Ignored to avoid inventing a column and to keep model fidelity.
        builder.Ignore(e => e.HasChildren);

        // MIGRATION: AuthorizedRoles and AdministratorRoles existed on the Tabs table in early DNN
        // (base column + 01.00.06 respectively) but were both DROPPED in 03.00.01 when tab-level
        // security migrated to the TabPermission table. They are not physical columns in the 4.9
        // schema, so they are ignored (mapping them would target non-existent columns).
        builder.Ignore(e => e.AuthorizedRoles);
        builder.Ignore(e => e.AdministratorRoles);

        // --- Relationships: intentionally NOT configured here -----------------------------------------

        // MIGRATION: The Tab <-> TabPermission relationship (legacy FK_TabPermission_Tabs:
        // TabPermission.TabID -> Tabs.TabID, ON DELETE CASCADE) is owned and configured from the ONE
        // side in PermissionConfiguration (TabPermission side, via .WithMany(t => t.TabPermissions)).
        // It is deliberately left unconfigured here so the same foreign key is not double-configured,
        // honoring the "configure each relationship from one side only" rule. The Tab.TabPermissions
        // navigation is still satisfied by that single configuration.
        //
        // MIGRATION: PortalID and ParentId are retained as plain integer columns. The legacy database
        // has FK_Tabs_Portals (PortalID -> Portals) and the self-referential FK_Tabs_Tabs (ParentId ->
        // Tabs), but the Tab entity intentionally exposes no Portal or parent/child navigation
        // properties, so no navigation relationship is invented for them here.
    }
}
