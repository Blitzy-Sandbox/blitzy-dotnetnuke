using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core 8 Fluent API mapping for the <see cref="Role"/> Domain entity. It is discovered and applied
/// automatically by <c>DnnDbContext.OnModelCreating</c> through
/// <c>ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly)</c>, so this type is never
/// referenced directly — it only needs to exist in the Infrastructure assembly.
/// </summary>
/// <remarks>
/// DATA MODEL FIDELITY (mandatory): this configuration binds <see cref="Role"/> to the pre-existing
/// legacy DNN <c>Roles</c> table (the system of record) WITHOUT altering table, column, or key names.
/// Every column name is preserved verbatim so the migrated stack reads and writes exactly the same rows
/// the legacy VB.NET / ADO.NET stack did; no production schema change is performed here.
/// </remarks>
// MIGRATION: Replaces the stored-procedure column mapping of DotNetNuke.Security.Roles.RoleInfo
// (Library/Components/Security/Roles/RoleInfo.vb, L42, XmlRoot "role"). The legacy code shuttled these
// columns through the ADO.NET SqlDataProvider stored-procedure surface (AddRole / UpdateRole / GetRoles,
// etc.); that dispatch is gone and the persistence shape is now expressed once, declaratively, as this
// Fluent mapping over the existing table.
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>
    /// Configures the <see cref="Role"/> entity: its table, primary key, and the one-to-one column
    /// mappings against the existing DNN <c>Roles</c> schema.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Role"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // MIGRATION: Role is the DNN DOMAIN role and maps to the DNN `Roles` table, whose primary key
        // `RoleID` is an int IDENTITY(0,1) (Website/Providers/DataProviders/SqlDataProvider/
        // 01.00.00.SqlDataProvider L114). It is deliberately NOT the ASP.NET membership provider table
        // `aspnet_Roles`, whose key `RoleId` is a uniqueidentifier (Guid). This mirrors the User
        // int/Guid duality: the DNN domain table — not the aspnet_* provider table — is this entity's home.
        builder.ToTable("Roles");

        // Primary key: RoleID.
        builder.HasKey(e => e.RoleID);

        // RoleID is database-generated (SQL Server IDENTITY(0,1)); EF must not supply a value on insert.
        builder.Property(e => e.RoleID)
            .HasColumnName("RoleID")
            .ValueGeneratedOnAdd();

        // --- Columns present since the base Roles definition (01.00.00.SqlDataProvider L114-123) ---

        // PortalID: plain scalar column. The legacy schema carries an implicit portal association via
        // PortalID, but there is no named FK modeled here and Portal exposes no Roles collection in the
        // target scope, so this stays a plain int column — NOT an EF relationship (none is invented).
        builder.Property(e => e.PortalID)
            .HasColumnName("PortalID");

        builder.Property(e => e.RoleName)
            .HasColumnName("RoleName");

        builder.Property(e => e.Description)
            .HasColumnName("Description");

        builder.Property(e => e.BillingFrequency)
            .HasColumnName("BillingFrequency");

        // MIGRATION: VB `Single` (RoleInfo.ServiceFee) -> C# `float`. The physical column is SQL Server
        // `money` in v4.9. It began as decimal(5,2) (01.00.00.SqlDataProvider L119) but was rebuilt to
        // money (01.00.04.SqlDataProvider L1327/L1342, migrated via CONVERT(money, ServiceFee)) and
        // finalized by `ALTER TABLE Roles ALTER COLUMN [ServiceFee] [money]` (03.01.01.SqlDataProvider
        // L1173); the canonical cumulative schema (DotNetNuke.Schema.SqlDataProvider L6212) confirms
        // [money]. HasColumnType("money") pins the true v4.9 column type for the SqlServer provider
        // (fidelity over the prompt's stale decimal(5,2) example); the InMemory test provider ignores it.
        builder.Property(e => e.ServiceFee)
            .HasColumnName("ServiceFee")
            .HasColumnType("money");

        builder.Property(e => e.TrialFrequency)
            .HasColumnName("TrialFrequency");

        builder.Property(e => e.TrialPeriod)
            .HasColumnName("TrialPeriod");

        // --- Columns added by later version scripts (all physical Roles columns in v4.9) ---

        // BillingPeriod: added in 01.00.08.SqlDataProvider (ALTER TABLE Roles ADD BillingPeriod int NULL, L6829).
        builder.Property(e => e.BillingPeriod)
            .HasColumnName("BillingPeriod");

        // MIGRATION: VB `Single` (RoleInfo.TrialFee) -> C# `float`. The physical column is `money`, added
        // as `TrialFee money NULL` in 01.00.08.SqlDataProvider (L6830) and confirmed [money] in the
        // canonical schema (DotNetNuke.Schema.SqlDataProvider L6215). HasColumnType("money") pins the true
        // type for the SqlServer provider; ignored by the InMemory test provider.
        builder.Property(e => e.TrialFee)
            .HasColumnName("TrialFee")
            .HasColumnType("money");

        // IsPublic / AutoAssignment: bit columns added in 01.00.08.SqlDataProvider (L6831-6832).
        builder.Property(e => e.IsPublic)
            .HasColumnName("IsPublic");

        builder.Property(e => e.AutoAssignment)
            .HasColumnName("AutoAssignment");

        // RoleGroupID: int column added in 03.02.03.SqlDataProvider (ALTER TABLE Roles ADD RoleGroupID int NULL, L34).
        // MIGRATION: the legacy foreign key `FK_Roles_RoleGroups` (03.02.03.SqlDataProvider L37) references the
        // out-of-scope RoleGroups table, which is NOT part of the 14-entity target model. That FK is intentionally
        // left UNMODELED and RoleGroupID is preserved as a plain mapped int column, so the existing column is kept
        // without introducing a dangling relationship to a non-existent entity.
        builder.Property(e => e.RoleGroupID)
            .HasColumnName("RoleGroupID");

        // RSVPCode / IconFile: nvarchar columns added in 03.02.03.SqlDataProvider
        // (ADD RSVPCode nvarchar(50) NULL, IconFile nvarchar(100) NULL, L45). The exact "RSVPCode" casing is
        // the verbatim legacy column name and is preserved as-is.
        builder.Property(e => e.RSVPCode)
            .HasColumnName("RSVPCode");

        builder.Property(e => e.IconFile)
            .HasColumnName("IconFile");
    }
}
