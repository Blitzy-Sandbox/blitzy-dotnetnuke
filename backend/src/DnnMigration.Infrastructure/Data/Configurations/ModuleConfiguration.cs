using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for Module (legacy ModuleInfo, Library/Components/Modules/ModuleInfo.vb,
// namespace DotNetNuke.Entities.Modules). Code-First mapped to the EXISTING [Modules] table (01.00.00.SqlDataProvider);
// schema not altered.
// FLATTENING NOTE: the C# Module entity merges Modules + TabModules + ModuleDefinitions + DesktopModules +
// ModuleControls. Per-page placement fields (PortalId, TabId, TabModuleId, PaneName, ModuleOrder, CacheTime,
// Alignment, Color, Border, IconFile, Visibility, ContainerSrc) physically live on [TabModules]; definition/
// desktop/control fields live on their respective tables. Those properties are NOT columns of [Modules] and are
// left to EF convention (a no-op for the EF Core InMemory gates). PortalId/TabId are intentionally NOT mapped
// here because they are not [Modules] columns. See MIGRATION_NOTES.md.
public sealed class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.ToTable("Modules");

        // MIGRATION: ModuleInfo._ModuleID is the PK but the C# property Module.ModuleId is int? (nullable),
        // mirroring the legacy Null.NullInteger sentinel default. EF Core 8 accepts a nullable CLR property as a
        // key (treated as required); HasKey works and CRUD succeeds (empirically verified).
        builder.HasKey(m => m.ModuleId);
        builder.Property(m => m.ModuleId).HasColumnName("ModuleID");

        // MIGRATION: Module 1..N ModulePermission (module-level access control). OWNED here (configured once);
        // FK is ModulePermission.ModuleId. WithOne() because ModulePermission has no back-navigation to Module.
        builder.HasMany(m => m.ModulePermissions)
            .WithOne()
            .HasForeignKey(mp => mp.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // NOTE: PortalId/TabId are NOT mapped (they live on [TabModules], not [Modules]). Other scalar columns
        // (ModuleTitle, ModuleDefId, IsDeleted, ...) rely on EF convention (case-insensitive SQL Server collation).
    }
}
