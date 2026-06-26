using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION (CP2 review — DependencyInjection #1): Fluent API mapping for ModuleSetting, backing the
// IPortalSettingsService adapter. Code-First mapped to the EXISTING [ModuleSettings] table
// (DotNetNuke.Schema.SqlDataProvider); the schema is NOT altered. The authoritative CREATE TABLE has exactly
// three columns — [ModuleID] int, [SettingName] nvarchar(50), [SettingValue] nvarchar(2000) — with the natural
// composite key (ModuleID, SettingName). Only those physical columns are mapped, so EF never emits SQL for a
// non-existent column (the class of defect corrected across the CP2 EF-mapping findings).
public sealed class ModuleSettingConfiguration : IEntityTypeConfiguration<ModuleSetting>
{
    public void Configure(EntityTypeBuilder<ModuleSetting> builder)
    {
        builder.ToTable("ModuleSettings");

        // MIGRATION: composite primary key (ModuleID, SettingName) — the natural key of [ModuleSettings]. Lets the
        // adapter find/update a setting by (moduleId, settingName) for the upsert in SetSettingAsync.
        builder.HasKey(ms => new { ms.ModuleId, ms.SettingName });

        builder.Property(ms => ms.ModuleId)
            .HasColumnName("ModuleID");

        builder.Property(ms => ms.SettingName)
            .HasColumnName("SettingName")
            .HasMaxLength(50);

        builder.Property(ms => ms.SettingValue)
            .HasColumnName("SettingValue")
            .HasMaxLength(2000);
    }
}
