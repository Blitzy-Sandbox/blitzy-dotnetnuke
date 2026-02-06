// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Create EF Core IEntityTypeConfiguration classes for Permission system
// replacing SqlDataProvider/SqlHelper patterns.
//
// Source References:
// - Library/Components/Security/Permissions/Permission.vb (Permission entity properties)
// - Library/Components/Security/Permissions/ModulePermission.vb (ModulePermission entity)
// - Library/Components/Security/Permissions/TabPermission.vb (TabPermission entity)
// - Library/Components/Security/Permissions/FolderPermission.vb (FolderPermission entity)
// - Library/Components/Security/Permissions/PermissionController.vb (data access patterns)
//
// Key transformations:
// 1) PermissionConfiguration maps Permission entity to 'Permission' table with:
//    - Primary key: PermissionID
//    - Identity: PermissionCode, PermissionKey, PermissionName (nvarchar 50)
//    - Foreign key: ModuleDefID to ModuleDefinitions
// 2) ModulePermissionConfiguration maps ModulePermission to 'ModulePermission' table with:
//    - Primary key: ModulePermissionID
//    - Foreign keys: ModuleID, PermissionID (required), RoleID/UserID (optional)
//    - AllowAccess boolean flag
// 3) TabPermissionConfiguration maps TabPermission to 'TabPermission' table with:
//    - Primary key: TabPermissionID
//    - Foreign keys: TabID, PermissionID (required), RoleID/UserID (optional)
//    - AllowAccess boolean flag
// 4) FolderPermissionConfiguration maps FolderPermission to 'FolderPermission' table with:
//    - Primary key: FolderPermissionID
//    - FolderID integer, PermissionID (required), RoleID/UserID (optional)
//    - AllowAccess boolean flag
// 5) Configures navigation properties for relationships
// 6) Creates indexes for query optimization
// 7) Uses file-scoped namespace and C# 12 features
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Permission"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the Permission entity to the existing 'Permission' table in the DNN database.
/// Permissions define the available permission types (e.g., VIEW, EDIT, DELETE) that can be assigned
/// to modules, tabs, or folders through the corresponding permission entities.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider pattern where permission data was accessed
/// via stored procedures (dbo.GetPermission, dbo.AddPermission, dbo.UpdatePermission, dbo.DeletePermission,
/// dbo.GetPermissions, dbo.GetPermissionByCodeAndKey) through SqlHelper.ExecuteReader/ExecuteNonQuery calls
/// in PermissionController.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema column sizes and types:
/// - String lengths match existing nvarchar(50) column definitions
/// - ModuleDefID is a foreign key to ModuleDefinitions (-1 for system permissions)
/// </para>
/// </remarks>
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="Permission"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for Permission.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'Permission' with PermissionID primary key</item>
    /// <item>Property configurations for PermissionCode, PermissionKey, PermissionName (nvarchar 50)</item>
    /// <item>Foreign key reference to ModuleDefinition via ModuleDefID</item>
    /// <item>Composite unique index on (ModuleDefID, PermissionCode, PermissionKey) for permission lookup</item>
    /// <item>Index on PermissionCode for filtering by permission category</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        // MIGRATION: Map to existing 'Permission' table in DNN database
        builder.ToTable("Permission");

        // MIGRATION: Primary key - maps to PermissionID column (identity column in SQL Server)
        builder.HasKey(p => p.PermissionId);
        builder.Property(p => p.PermissionId)
            .HasColumnName("PermissionID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Identity Properties
        // =====================================================================

        // MIGRATION: PermissionCode - nvarchar(50), nullable
        // Maps from VB.NET _permissionCode field with <XmlElement("permissioncode")> attribute
        // Used to categorize permissions (e.g., "SYSTEM_MODULE_DEFINITION", "CONTENT_FOLDER")
        builder.Property(p => p.PermissionCode)
            .HasColumnName("PermissionCode")
            .HasMaxLength(50);

        // MIGRATION: PermissionKey - nvarchar(50), nullable
        // Maps from VB.NET _permissionKey field with <XmlElement("permissionkey")> attribute
        // Identifies specific permission type (e.g., "VIEW", "EDIT", "DELETE")
        builder.Property(p => p.PermissionKey)
            .HasColumnName("PermissionKey")
            .HasMaxLength(50);

        // MIGRATION: PermissionName - nvarchar(50), nullable
        // Maps from VB.NET _PermissionName field (XmlIgnore in original)
        // Human-readable display name for the permission
        builder.Property(p => p.PermissionName)
            .HasColumnName("PermissionName")
            .HasMaxLength(50);

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: ModuleDefID - int, foreign key reference to ModuleDefinitions table
        // Maps from VB.NET _moduleDefID field (XmlIgnore in original)
        // A value of -1 indicates this is a system-level permission not tied to a specific module
        builder.Property(p => p.ModuleDefId)
            .HasColumnName("ModuleDefID");

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Composite unique index on (ModuleDefID, PermissionCode, PermissionKey)
        // Ensures permission definitions are unique within a module definition context
        builder.HasIndex(p => new { p.ModuleDefId, p.PermissionCode, p.PermissionKey })
            .IsUnique()
            .HasDatabaseName("IX_Permission_ModuleDefID_Code_Key");

        // MIGRATION: Index on PermissionCode for filtering by permission category
        builder.HasIndex(p => p.PermissionCode)
            .HasDatabaseName("IX_Permission_PermissionCode");
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="ModulePermission"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the ModulePermission entity to the existing 'ModulePermission' table
/// in the DNN database. Module permissions enable fine-grained access control for individual
/// module instances, assigning permissions to either roles or individual users.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider pattern where module permission data
/// was accessed via stored procedures (dbo.GetModulePermission, dbo.AddModulePermission,
/// dbo.UpdateModulePermission, dbo.DeleteModulePermission, dbo.GetModulePermissionsByModuleID)
/// through SqlHelper calls in ModulePermissionController.
/// </para>
/// <para>
/// Business Rule: Either RoleId or UserId should be set, but not both simultaneously.
/// This constraint is enforced at the application layer, not in the database schema.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema:
/// - Nullable RoleID/UserID correspond to optional role-based or user-specific permissions
/// - AllowAccess boolean determines whether permission is granted or denied
/// </para>
/// </remarks>
public class ModulePermissionConfiguration : IEntityTypeConfiguration<ModulePermission>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="ModulePermission"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for ModulePermission.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'ModulePermission' with ModulePermissionID primary key</item>
    /// <item>Foreign key to Module via ModuleID (required)</item>
    /// <item>Foreign key to Permission via PermissionID (required)</item>
    /// <item>Optional foreign key to Role via RoleID (nullable)</item>
    /// <item>Optional foreign key to User via UserID (nullable)</item>
    /// <item>AllowAccess boolean flag for grant/deny</item>
    /// <item>Navigation properties for Module, Permission, Role, User</item>
    /// <item>Composite index on (ModuleID, PermissionID) for permission lookup</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<ModulePermission> builder)
    {
        // MIGRATION: Map to existing 'ModulePermission' table in DNN database
        builder.ToTable("ModulePermission");

        // MIGRATION: Primary key - maps to ModulePermissionID column (identity column in SQL Server)
        builder.HasKey(mp => mp.ModulePermissionId);
        builder.Property(mp => mp.ModulePermissionId)
            .HasColumnName("ModulePermissionID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: ModuleID - int, required foreign key to Modules table
        // Maps from VB.NET _moduleID field with <XmlElement("moduleid")> attribute
        builder.Property(mp => mp.ModuleId)
            .HasColumnName("ModuleID")
            .IsRequired();

        // MIGRATION: PermissionID - int, required foreign key to Permission table
        // Maps from VB.NET inherited PermissionID property
        builder.Property(mp => mp.PermissionId)
            .HasColumnName("PermissionID")
            .IsRequired();

        // MIGRATION: RoleID - int, optional foreign key to Roles table
        // Maps from VB.NET _roleID field with <XmlElement("roleid")> attribute
        // Legacy DNN used special values like glbRoleNothing (-4) and Null.NullInteger (-1)
        // Converted to nullable int for cleaner semantics
        builder.Property(mp => mp.RoleId)
            .HasColumnName("RoleID");

        // MIGRATION: UserID - int, optional foreign key to Users table
        // Maps from VB.NET _userID field with <XmlElement("userid")> attribute
        // Legacy DNN used Null.NullInteger (-1) for no user assignment
        // Converted to nullable int for cleaner semantics
        builder.Property(mp => mp.UserId)
            .HasColumnName("UserID");

        // =====================================================================
        // Permission Properties
        // =====================================================================

        // MIGRATION: AllowAccess - bit, determines whether permission is granted or denied
        // Maps from VB.NET _AllowAccess field with <XmlElement("allowaccess")> attribute
        // When false, this represents an explicit deny which typically takes precedence
        builder.Property(mp => mp.AllowAccess)
            .HasColumnName("AllowAccess")
            .IsRequired();

        // =====================================================================
        // Navigation Properties - Relationships
        // =====================================================================

        // MIGRATION: ModulePermission -> Module relationship (many permissions belong to one module)
        // Configures the foreign key relationship to Module entity
        builder.HasOne(mp => mp.Module)
            .WithMany(m => m.ModulePermissions)
            .HasForeignKey(mp => mp.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: ModulePermission -> Permission relationship (many permissions reference one permission definition)
        // Configures the foreign key relationship to Permission entity
        builder.HasOne(mp => mp.Permission)
            .WithMany()
            .HasForeignKey(mp => mp.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // MIGRATION: ModulePermission -> Role relationship (optional, many-to-one)
        // Role-based permissions are the primary mechanism for access control
        builder.HasOne(mp => mp.Role)
            .WithMany()
            .HasForeignKey(mp => mp.RoleId)
            .OnDelete(DeleteBehavior.SetNull);

        // MIGRATION: ModulePermission -> User relationship (optional, many-to-one)
        // User-specific permissions complement role-based permissions for fine-grained control
        builder.HasOne(mp => mp.User)
            .WithMany()
            .HasForeignKey(mp => mp.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Composite index on (ModuleID, PermissionID) for permission lookup
        builder.HasIndex(mp => new { mp.ModuleId, mp.PermissionId })
            .HasDatabaseName("IX_ModulePermission_ModuleID_PermissionID");

        // MIGRATION: Index on RoleID for role-based permission queries
        builder.HasIndex(mp => mp.RoleId)
            .HasDatabaseName("IX_ModulePermission_RoleID");

        // MIGRATION: Index on UserID for user-specific permission queries
        builder.HasIndex(mp => mp.UserId)
            .HasDatabaseName("IX_ModulePermission_UserID");
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="TabPermission"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the TabPermission entity to the existing 'TabPermission' table
/// in the DNN database. Tab permissions enable fine-grained access control for individual
/// pages in the navigation hierarchy, assigning permissions to either roles or individual users.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider pattern where tab permission data
/// was accessed via stored procedures (dbo.GetTabPermission, dbo.AddTabPermission,
/// dbo.UpdateTabPermission, dbo.DeleteTabPermission, dbo.GetTabPermissionsByTabID)
/// through SqlHelper calls in TabPermissionController.
/// </para>
/// <para>
/// Business Rule: Either RoleId or UserId should be set, but not both simultaneously.
/// This constraint is enforced at the application layer, not in the database schema.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema:
/// - Nullable RoleID/UserID correspond to optional role-based or user-specific permissions
/// - AllowAccess boolean determines whether permission is granted or denied
/// </para>
/// </remarks>
public class TabPermissionConfiguration : IEntityTypeConfiguration<TabPermission>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="TabPermission"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for TabPermission.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'TabPermission' with TabPermissionID primary key</item>
    /// <item>Foreign key to Tab via TabID (required)</item>
    /// <item>Foreign key to Permission via PermissionID (required)</item>
    /// <item>Optional foreign key to Role via RoleID (nullable)</item>
    /// <item>Optional foreign key to User via UserID (nullable)</item>
    /// <item>AllowAccess boolean flag for grant/deny</item>
    /// <item>Navigation properties for Tab, Permission, Role, User</item>
    /// <item>Composite index on (TabID, PermissionID) for permission lookup</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<TabPermission> builder)
    {
        // MIGRATION: Map to existing 'TabPermission' table in DNN database
        builder.ToTable("TabPermission");

        // MIGRATION: Primary key - maps to TabPermissionID column (identity column in SQL Server)
        builder.HasKey(tp => tp.TabPermissionId);
        builder.Property(tp => tp.TabPermissionId)
            .HasColumnName("TabPermissionID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: TabID - int, required foreign key to Tabs table
        // Maps from VB.NET _TabID field with <XmlElement("tabid")> attribute
        builder.Property(tp => tp.TabId)
            .HasColumnName("TabID")
            .IsRequired();

        // MIGRATION: PermissionID - int, required foreign key to Permission table
        // Maps from VB.NET inherited PermissionID property
        builder.Property(tp => tp.PermissionId)
            .HasColumnName("PermissionID")
            .IsRequired();

        // MIGRATION: RoleID - int, optional foreign key to Roles table
        // Maps from VB.NET _roleID field with <XmlElement("roleid")> attribute
        // Legacy DNN used special values like glbRoleNothing (-4) and Null.NullInteger (-1)
        // Converted to nullable int for cleaner semantics
        builder.Property(tp => tp.RoleId)
            .HasColumnName("RoleID");

        // MIGRATION: UserID - int, optional foreign key to Users table
        // Maps from VB.NET _userID field with <XmlElement("userid")> attribute
        // Legacy DNN used Null.NullInteger (-1) for no user assignment
        // Converted to nullable int for cleaner semantics
        builder.Property(tp => tp.UserId)
            .HasColumnName("UserID");

        // =====================================================================
        // Permission Properties
        // =====================================================================

        // MIGRATION: AllowAccess - bit, determines whether permission is granted or denied
        // Maps from VB.NET _AllowAccess field with <XmlElement("allowaccess")> attribute
        // When false, this represents an explicit deny which typically takes precedence
        builder.Property(tp => tp.AllowAccess)
            .HasColumnName("AllowAccess")
            .IsRequired();

        // =====================================================================
        // Navigation Properties - Relationships
        // =====================================================================

        // MIGRATION: TabPermission -> Tab relationship (many permissions belong to one tab)
        // Configures the foreign key relationship to Tab entity
        builder.HasOne(tp => tp.Tab)
            .WithMany(t => t.TabPermissions)
            .HasForeignKey(tp => tp.TabId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: TabPermission -> Permission relationship (many permissions reference one permission definition)
        // Configures the foreign key relationship to Permission entity
        builder.HasOne(tp => tp.Permission)
            .WithMany()
            .HasForeignKey(tp => tp.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // MIGRATION: TabPermission -> Role relationship (optional, many-to-one)
        // Role-based permissions are the primary mechanism for access control
        builder.HasOne(tp => tp.Role)
            .WithMany()
            .HasForeignKey(tp => tp.RoleId)
            .OnDelete(DeleteBehavior.SetNull);

        // MIGRATION: TabPermission -> User relationship (optional, many-to-one)
        // User-specific permissions complement role-based permissions for fine-grained control
        builder.HasOne(tp => tp.User)
            .WithMany()
            .HasForeignKey(tp => tp.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Composite index on (TabID, PermissionID) for permission lookup
        builder.HasIndex(tp => new { tp.TabId, tp.PermissionId })
            .HasDatabaseName("IX_TabPermission_TabID_PermissionID");

        // MIGRATION: Index on RoleID for role-based permission queries
        builder.HasIndex(tp => tp.RoleId)
            .HasDatabaseName("IX_TabPermission_RoleID");

        // MIGRATION: Index on UserID for user-specific permission queries
        builder.HasIndex(tp => tp.UserId)
            .HasDatabaseName("IX_TabPermission_UserID");
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="FolderPermission"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the FolderPermission entity to the existing 'FolderPermission' table
/// in the DNN database. Folder permissions enable fine-grained access control for file system
/// folders, assigning permissions to either roles or individual users.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider pattern where folder permission data
/// was accessed via stored procedures (dbo.GetFolderPermission, dbo.AddFolderPermission,
/// dbo.UpdateFolderPermission, dbo.DeleteFolderPermission, dbo.GetFolderPermissionsByFolderID)
/// through SqlHelper calls in FolderPermissionController.
/// </para>
/// <para>
/// Business Rule: Either RoleId or UserId should be set, but not both simultaneously.
/// This constraint is enforced at the application layer, not in the database schema.
/// </para>
/// <para>
/// Note: FolderId references the folder management system. There is no explicit Folder entity
/// navigation property in this configuration as folders are handled separately by the file
/// management subsystem.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema:
/// - Nullable RoleID/UserID correspond to optional role-based or user-specific permissions
/// - AllowAccess boolean determines whether permission is granted or denied
/// </para>
/// </remarks>
public class FolderPermissionConfiguration : IEntityTypeConfiguration<FolderPermission>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="FolderPermission"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for FolderPermission.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'FolderPermission' with FolderPermissionID primary key</item>
    /// <item>FolderID integer (folder path identifier)</item>
    /// <item>Foreign key to Permission via PermissionID (required)</item>
    /// <item>Optional foreign key to Role via RoleID (nullable)</item>
    /// <item>Optional foreign key to User via UserID (nullable)</item>
    /// <item>AllowAccess boolean flag for grant/deny</item>
    /// <item>Navigation properties for Permission, Role, User</item>
    /// <item>Composite index on (FolderID, PermissionID) for permission lookup</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<FolderPermission> builder)
    {
        // MIGRATION: Map to existing 'FolderPermission' table in DNN database
        builder.ToTable("FolderPermission");

        // MIGRATION: Primary key - maps to FolderPermissionID column (identity column in SQL Server)
        builder.HasKey(fp => fp.FolderPermissionId);
        builder.Property(fp => fp.FolderPermissionId)
            .HasColumnName("FolderPermissionID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: FolderID - int, folder identifier
        // Maps from VB.NET _folderID field (XmlIgnore in original)
        // References the folder management system's folder identifier
        // Note: No navigation property as folders are handled by a separate file management subsystem
        builder.Property(fp => fp.FolderId)
            .HasColumnName("FolderID")
            .IsRequired();

        // MIGRATION: PermissionID - int, required foreign key to Permission table
        // Maps from VB.NET inherited PermissionID property
        builder.Property(fp => fp.PermissionId)
            .HasColumnName("PermissionID")
            .IsRequired();

        // MIGRATION: RoleID - int, optional foreign key to Roles table
        // Maps from VB.NET _roleID field (XmlIgnore in original)
        // Legacy DNN used special values like glbRoleNothing for no role assignment
        // Converted to nullable int for cleaner semantics
        builder.Property(fp => fp.RoleId)
            .HasColumnName("RoleID");

        // MIGRATION: UserID - int, optional foreign key to Users table
        // Maps from VB.NET _userID field with <XmlElement("userid")> attribute
        // Legacy DNN used Null.NullInteger (-1) for no user assignment
        // Converted to nullable int for cleaner semantics
        builder.Property(fp => fp.UserId)
            .HasColumnName("UserID");

        // =====================================================================
        // Permission Properties
        // =====================================================================

        // MIGRATION: AllowAccess - bit, determines whether permission is granted or denied
        // Maps from VB.NET _AllowAccess field with <XmlElement("allowaccess")> attribute
        // When false, this represents an explicit deny which typically takes precedence
        builder.Property(fp => fp.AllowAccess)
            .HasColumnName("AllowAccess")
            .IsRequired();

        // =====================================================================
        // Navigation Properties - Relationships
        // =====================================================================

        // MIGRATION: FolderPermission -> Permission relationship (many permissions reference one permission definition)
        // Configures the foreign key relationship to Permission entity
        builder.HasOne(fp => fp.Permission)
            .WithMany()
            .HasForeignKey(fp => fp.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // MIGRATION: FolderPermission -> Role relationship (optional, many-to-one)
        // Role-based permissions are the primary mechanism for access control
        builder.HasOne(fp => fp.Role)
            .WithMany()
            .HasForeignKey(fp => fp.RoleId)
            .OnDelete(DeleteBehavior.SetNull);

        // MIGRATION: FolderPermission -> User relationship (optional, many-to-one)
        // User-specific permissions complement role-based permissions for fine-grained control
        builder.HasOne(fp => fp.User)
            .WithMany()
            .HasForeignKey(fp => fp.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Composite index on (FolderID, PermissionID) for permission lookup
        builder.HasIndex(fp => new { fp.FolderId, fp.PermissionId })
            .HasDatabaseName("IX_FolderPermission_FolderID_PermissionID");

        // MIGRATION: Index on RoleID for role-based permission queries
        builder.HasIndex(fp => fp.RoleId)
            .HasDatabaseName("IX_FolderPermission_RoleID");

        // MIGRATION: Index on UserID for user-specific permission queries
        builder.HasIndex(fp => fp.UserId)
            .HasDatabaseName("IX_FolderPermission_UserID");
    }
}
