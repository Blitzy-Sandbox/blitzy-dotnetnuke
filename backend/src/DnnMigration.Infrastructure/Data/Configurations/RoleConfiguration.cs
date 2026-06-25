using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for Role (legacy RoleInfo, Library/Components/Security/Roles/RoleInfo.vb,
// namespace DotNetNuke.Security.Roles). Code-First mapped to the EXISTING [Roles] table (01.00.00.SqlDataProvider);
// schema not altered. Preserves PortalId tenant scoping and the user->role->permission model (AAP 0.7.1).
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.RoleId);
        builder.Property(r => r.RoleId).HasColumnName("RoleID");

        // MIGRATION: tenant discriminator preserved.
        builder.Property(r => r.PortalId).HasColumnName("PortalID");

        // RoleGroupID was added in DNN (RoleInfo.vb history: [cnurse] 01/03/2006). Nullable on the entity.
        builder.Property(r => r.RoleGroupId).HasColumnName("RoleGroupID");

        // NOTE: scalar columns (RoleName, Description, ServiceFee, BillingFrequency, TrialPeriod, TrialFrequency,
        // BillingPeriod, TrialFee, IsPublic, AutoAssignment, RSVPCode, IconFile) rely on EF convention
        // (case-insensitive SQL Server collation). No navigation collection on Role (the UserRole->Role
        // relationship is owned by UserRoleConfiguration via WithMany()).
    }
}
