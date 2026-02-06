// -----------------------------------------------------------------------------
// <copyright file="MappingProfile.cs" company="DNN Migration Project">
//     Copyright (c) DNN Migration Project. All rights reserved.
//     Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------------
// MIGRATION: AutoMapper MappingProfile for Entity-DTO conversions.
// Configures mappings between domain entities (from DotNetNuke 4.x VB.NET legacy)
// and Data Transfer Objects for the REST API layer.
// Source: Section 0.4.1 - Mapping/MappingProfile.cs as AutoMapper configuration
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Application.Mapping;

/// <summary>
/// AutoMapper profile containing all entity-to-DTO and DTO-to-entity mapping configurations.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This profile implements the following mapping categories:
/// </para>
/// <list type="bullet">
/// <item><description>Portal mappings: Portal ↔ PortalDto, CreatePortalRequest → Portal, UpdatePortalRequest → Portal</description></item>
/// <item><description>Module mappings: Module ↔ ModuleDto, CreateModuleRequest → Module, UpdateModuleRequest → Module</description></item>
/// <item><description>User mappings: User ↔ UserDto, CreateUserRequest → User, UpdateUserRequest → User</description></item>
/// <item><description>Role mappings: Role ↔ RoleDto, CreateRoleRequest → Role</description></item>
/// </list>
/// <para>
/// Key design decisions:
/// </para>
/// <list type="bullet">
/// <item><description>Uses ForMember() to handle property name differences between legacy VB.NET naming and new C# conventions</description></item>
/// <item><description>Uses Ignore() for navigation properties that shouldn't be auto-mapped</description></item>
/// <item><description>Handles nullable reference types appropriately with null-conditional operators</description></item>
/// <item><description>Flattens nested objects (e.g., UserProfile into UserDto) for API simplicity</description></item>
/// <item><description>Converts collections to computed values (e.g., UserRoles to role names array)</description></item>
/// </list>
/// </remarks>
public class MappingProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MappingProfile"/> class.
    /// Configures all AutoMapper mappings for the application.
    /// </summary>
    public MappingProfile()
    {
        ConfigurePortalMappings();
        ConfigureModuleMappings();
        ConfigureUserMappings();
        ConfigureRoleMappings();
        ConfigureSupportingTypeMappings();
    }

    /// <summary>
    /// Configures mappings for Portal domain entity and its related DTOs.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Portal mappings handle:
    /// - Portal → PortalDto: Maps all properties, computes Users and Pages counts from navigation collections
    /// - CreatePortalRequest → Portal: Maps input fields, ignores navigation properties and computed fields
    /// - UpdatePortalRequest → Portal: Maps update fields, preserves identity fields
    /// </remarks>
    private void ConfigurePortalMappings()
    {
        // Portal → PortalDto mapping
        // MIGRATION: Maps Portal entity to response DTO with computed collection counts
        CreateMap<Portal, PortalDto>()
            .ForMember(dest => dest.PortalId, opt => opt.MapFrom(src => src.PortalId))
            .ForMember(dest => dest.PortalName, opt => opt.MapFrom(src => src.PortalName))
            .ForMember(dest => dest.LogoFile, opt => opt.MapFrom(src => src.LogoFile))
            .ForMember(dest => dest.FooterText, opt => opt.MapFrom(src => src.FooterText))
            .ForMember(dest => dest.ExpiryDate, opt => opt.MapFrom(src => src.ExpiryDate))
            .ForMember(dest => dest.UserRegistration, opt => opt.MapFrom(src => src.UserRegistration))
            .ForMember(dest => dest.BannerAdvertising, opt => opt.MapFrom(src => src.BannerAdvertising))
            .ForMember(dest => dest.AdministratorId, opt => opt.MapFrom(src => src.AdministratorId))
            .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Currency))
            .ForMember(dest => dest.HostFee, opt => opt.MapFrom(src => src.HostFee))
            .ForMember(dest => dest.HostSpace, opt => opt.MapFrom(src => src.HostSpace))
            .ForMember(dest => dest.PageQuota, opt => opt.MapFrom(src => src.PageQuota))
            .ForMember(dest => dest.UserQuota, opt => opt.MapFrom(src => src.UserQuota))
            .ForMember(dest => dest.AdministratorRoleId, opt => opt.MapFrom(src => src.AdministratorRoleId))
            .ForMember(dest => dest.RegisteredRoleId, opt => opt.MapFrom(src => src.RegisteredRoleId))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.KeyWords, opt => opt.MapFrom(src => src.KeyWords))
            .ForMember(dest => dest.BackgroundFile, opt => opt.MapFrom(src => src.BackgroundFile))
            .ForMember(dest => dest.GUID, opt => opt.MapFrom(src => src.GUID))
            .ForMember(dest => dest.DefaultLanguage, opt => opt.MapFrom(src => src.DefaultLanguage))
            .ForMember(dest => dest.TimeZoneOffset, opt => opt.MapFrom(src => src.TimeZoneOffset))
            .ForMember(dest => dest.HomeDirectory, opt => opt.MapFrom(src => src.HomeDirectory))
            // MIGRATION: Computed counts from navigation collections
            // Users count from Portal.Users collection
            .ForMember(dest => dest.Users, opt => opt.MapFrom(src => src.Users != null ? src.Users.Count : 0))
            // Pages count from Portal.Tabs collection (tabs represent pages)
            .ForMember(dest => dest.Pages, opt => opt.MapFrom(src => src.Tabs != null ? src.Tabs.Count : 0));

        // CreatePortalRequest → Portal mapping
        // MIGRATION: Maps portal creation request to entity, ignoring computed and navigation properties
        CreateMap<CreatePortalRequest, Portal>()
            // Map Title to PortalName (legacy field name difference)
            .ForMember(dest => dest.PortalName, opt => opt.MapFrom(src => src.Title))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.KeyWords, opt => opt.MapFrom(src => src.KeyWords))
            .ForMember(dest => dest.HomeDirectory, opt => opt.MapFrom(src => src.HomeDirectory))
            // Ignore identity fields - set by database
            .ForMember(dest => dest.PortalId, opt => opt.Ignore())
            .ForMember(dest => dest.GUID, opt => opt.Ignore())
            // Ignore navigation properties that exist on Portal entity
            .ForMember(dest => dest.Users, opt => opt.Ignore())
            .ForMember(dest => dest.Modules, opt => opt.Ignore())
            .ForMember(dest => dest.Tabs, opt => opt.Ignore())
            .ForMember(dest => dest.Roles, opt => opt.Ignore())
            .ForMember(dest => dest.PortalAliases, opt => opt.Ignore())
            // Set default values for fields not in creation request
            .ForMember(dest => dest.ExpiryDate, opt => opt.Ignore())
            .ForMember(dest => dest.UserRegistration, opt => opt.Ignore())
            .ForMember(dest => dest.BannerAdvertising, opt => opt.Ignore())
            .ForMember(dest => dest.AdministratorId, opt => opt.Ignore())
            .ForMember(dest => dest.Currency, opt => opt.Ignore())
            .ForMember(dest => dest.HostFee, opt => opt.Ignore())
            .ForMember(dest => dest.HostSpace, opt => opt.Ignore())
            .ForMember(dest => dest.PageQuota, opt => opt.Ignore())
            .ForMember(dest => dest.UserQuota, opt => opt.Ignore())
            .ForMember(dest => dest.AdministratorRoleId, opt => opt.Ignore())
            .ForMember(dest => dest.RegisteredRoleId, opt => opt.Ignore())
            .ForMember(dest => dest.BackgroundFile, opt => opt.Ignore())
            .ForMember(dest => dest.DefaultLanguage, opt => opt.Ignore())
            .ForMember(dest => dest.TimeZoneOffset, opt => opt.Ignore())
            .ForMember(dest => dest.LogoFile, opt => opt.Ignore())
            .ForMember(dest => dest.FooterText, opt => opt.Ignore())
            .ForMember(dest => dest.PaymentProcessor, opt => opt.Ignore())
            .ForMember(dest => dest.ProcessorUserId, opt => opt.Ignore())
            .ForMember(dest => dest.ProcessorPassword, opt => opt.Ignore())
            .ForMember(dest => dest.SiteLogHistory, opt => opt.Ignore())
            .ForMember(dest => dest.Email, opt => opt.Ignore())
            .ForMember(dest => dest.AdminTabId, opt => opt.Ignore())
            .ForMember(dest => dest.SuperTabId, opt => opt.Ignore())
            .ForMember(dest => dest.SplashTabId, opt => opt.Ignore())
            .ForMember(dest => dest.HomeTabId, opt => opt.Ignore())
            .ForMember(dest => dest.LoginTabId, opt => opt.Ignore())
            .ForMember(dest => dest.UserTabId, opt => opt.Ignore())
            // Ignore denormalized role name properties that exist on Portal entity
            .ForMember(dest => dest.AdministratorRoleName, opt => opt.Ignore())
            .ForMember(dest => dest.RegisteredRoleName, opt => opt.Ignore())
            .ForMember(dest => dest.Version, opt => opt.Ignore());

        // UpdatePortalRequest → Portal mapping
        // MIGRATION: Maps portal update request to entity with conditional mapping for optional fields
        CreateMap<UpdatePortalRequest, Portal>()
            .ForMember(dest => dest.PortalName, opt => opt.MapFrom(src => src.PortalName))
            .ForMember(dest => dest.LogoFile, opt => opt.MapFrom(src => src.LogoFile))
            .ForMember(dest => dest.FooterText, opt => opt.MapFrom(src => src.FooterText))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.KeyWords, opt => opt.MapFrom(src => src.KeyWords))
            .ForMember(dest => dest.BackgroundFile, opt => opt.MapFrom(src => src.BackgroundFile))
            .ForMember(dest => dest.UserRegistration, opt => opt.MapFrom(src => src.UserRegistration))
            .ForMember(dest => dest.BannerAdvertising, opt => opt.MapFrom(src => src.BannerAdvertising))
            .ForMember(dest => dest.DefaultLanguage, opt => opt.MapFrom(src => src.DefaultLanguage))
            .ForMember(dest => dest.TimeZoneOffset, opt => opt.MapFrom(src => src.TimeZoneOffset))
            .ForMember(dest => dest.HomeDirectory, opt => opt.MapFrom(src => src.HomeDirectory))
            .ForMember(dest => dest.PageQuota, opt => opt.MapFrom(src => src.PageQuota))
            .ForMember(dest => dest.UserQuota, opt => opt.MapFrom(src => src.UserQuota))
            .ForMember(dest => dest.HostSpace, opt => opt.MapFrom(src => src.HostSpace))
            .ForMember(dest => dest.HostFee, opt => opt.MapFrom(src => src.HostFee))
            .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Currency))
            .ForMember(dest => dest.PaymentProcessor, opt => opt.MapFrom(src => src.PaymentProcessor))
            .ForMember(dest => dest.ProcessorUserId, opt => opt.MapFrom(src => src.ProcessorUserId))
            .ForMember(dest => dest.ProcessorPassword, opt => opt.MapFrom(src => src.ProcessorPassword))
            // Ignore identity fields - cannot be changed
            .ForMember(dest => dest.PortalId, opt => opt.Ignore())
            .ForMember(dest => dest.GUID, opt => opt.Ignore())
            // Ignore navigation properties that exist on Portal entity
            .ForMember(dest => dest.Users, opt => opt.Ignore())
            .ForMember(dest => dest.Modules, opt => opt.Ignore())
            .ForMember(dest => dest.Tabs, opt => opt.Ignore())
            .ForMember(dest => dest.Roles, opt => opt.Ignore())
            .ForMember(dest => dest.PortalAliases, opt => opt.Ignore())
            // Ignore other computed/system fields that exist on Portal entity
            .ForMember(dest => dest.AdministratorId, opt => opt.Ignore())
            .ForMember(dest => dest.AdministratorRoleId, opt => opt.Ignore())
            .ForMember(dest => dest.RegisteredRoleId, opt => opt.Ignore())
            .ForMember(dest => dest.ExpiryDate, opt => opt.Ignore())
            .ForMember(dest => dest.SiteLogHistory, opt => opt.Ignore())
            .ForMember(dest => dest.Email, opt => opt.Ignore())
            .ForMember(dest => dest.AdminTabId, opt => opt.Ignore())
            .ForMember(dest => dest.SuperTabId, opt => opt.Ignore())
            .ForMember(dest => dest.SplashTabId, opt => opt.Ignore())
            .ForMember(dest => dest.HomeTabId, opt => opt.Ignore())
            .ForMember(dest => dest.LoginTabId, opt => opt.Ignore())
            .ForMember(dest => dest.UserTabId, opt => opt.Ignore())
            // Ignore denormalized role name properties that exist on Portal entity
            .ForMember(dest => dest.AdministratorRoleName, opt => opt.Ignore())
            .ForMember(dest => dest.RegisteredRoleName, opt => opt.Ignore())
            .ForMember(dest => dest.Version, opt => opt.Ignore());
    }

    /// <summary>
    /// Configures mappings for Module domain entity and its related DTOs.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Module mappings handle:
    /// - Module → ModuleDto: Maps module properties, includes ModuleDefinition metadata (FriendlyName, ModuleName, Version)
    /// - CreateModuleRequest → Module: Maps input fields, sets defaults for computed fields
    /// - UpdateModuleRequest → Module: Maps update fields with null handling for partial updates
    /// </remarks>
    private void ConfigureModuleMappings()
    {
        // Module → ModuleDto mapping
        // MIGRATION: Maps Module entity to response DTO with navigation property projections
        CreateMap<Module, ModuleDto>()
            .ForMember(dest => dest.ModuleId, opt => opt.MapFrom(src => src.ModuleId))
            .ForMember(dest => dest.TabModuleId, opt => opt.MapFrom(src => src.TabModuleId))
            .ForMember(dest => dest.TabId, opt => opt.MapFrom(src => src.TabId))
            .ForMember(dest => dest.PortalId, opt => opt.MapFrom(src => src.PortalId))
            .ForMember(dest => dest.ModuleDefId, opt => opt.MapFrom(src => src.ModuleDefId))
            .ForMember(dest => dest.ModuleOrder, opt => opt.MapFrom(src => src.ModuleOrder))
            .ForMember(dest => dest.PaneName, opt => opt.MapFrom(src => src.PaneName ?? string.Empty))
            .ForMember(dest => dest.ModuleTitle, opt => opt.MapFrom(src => src.ModuleTitle))
            .ForMember(dest => dest.Alignment, opt => opt.MapFrom(src => src.Alignment))
            .ForMember(dest => dest.Color, opt => opt.MapFrom(src => src.Color))
            .ForMember(dest => dest.Border, opt => opt.MapFrom(src => src.Border))
            .ForMember(dest => dest.IconFile, opt => opt.MapFrom(src => src.IconFile))
            .ForMember(dest => dest.CacheTime, opt => opt.MapFrom(src => src.CacheTime))
            .ForMember(dest => dest.Visibility, opt => opt.MapFrom(src => src.Visibility))
            .ForMember(dest => dest.IsDeleted, opt => opt.MapFrom(src => src.IsDeleted))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate))
            .ForMember(dest => dest.ContainerSrc, opt => opt.MapFrom(src => src.ContainerSrc))
            .ForMember(dest => dest.DisplayTitle, opt => opt.MapFrom(src => src.DisplayTitle))
            .ForMember(dest => dest.DisplayPrint, opt => opt.MapFrom(src => src.DisplayPrint))
            .ForMember(dest => dest.DisplaySyndicate, opt => opt.MapFrom(src => src.DisplaySyndicate))
            .ForMember(dest => dest.Header, opt => opt.MapFrom(src => src.Header))
            .ForMember(dest => dest.Footer, opt => opt.MapFrom(src => src.Footer))
            .ForMember(dest => dest.InheritViewPermissions, opt => opt.MapFrom(src => src.InheritViewPermissions))
            .ForMember(dest => dest.AllTabs, opt => opt.MapFrom(src => src.AllTabs))
            // MIGRATION: Map from ModuleDefinition.DesktopModule navigation properties
            .ForMember(dest => dest.DesktopModuleId, opt => opt.MapFrom(src =>
                src.ModuleDefinition != null ? src.ModuleDefinition.DesktopModuleId : 0))
            .ForMember(dest => dest.FriendlyName, opt => opt.MapFrom(src =>
                src.ModuleDefinition != null && src.ModuleDefinition.DesktopModule != null
                    ? src.ModuleDefinition.DesktopModule.FriendlyName
                    : src.ModuleDefinition != null ? src.ModuleDefinition.FriendlyName : null))
            .ForMember(dest => dest.ModuleName, opt => opt.MapFrom(src =>
                src.ModuleDefinition != null && src.ModuleDefinition.DesktopModule != null
                    ? src.ModuleDefinition.DesktopModule.ModuleName
                    : null))
            .ForMember(dest => dest.FolderName, opt => opt.MapFrom(src =>
                src.ModuleDefinition != null && src.ModuleDefinition.DesktopModule != null
                    ? src.ModuleDefinition.DesktopModule.FolderName
                    : null))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src =>
                src.ModuleDefinition != null && src.ModuleDefinition.DesktopModule != null
                    ? src.ModuleDefinition.DesktopModule.Description
                    : null))
            .ForMember(dest => dest.Version, opt => opt.MapFrom(src =>
                src.ModuleDefinition != null && src.ModuleDefinition.DesktopModule != null
                    ? src.ModuleDefinition.DesktopModule.Version
                    : null))
            // MIGRATION: Computed feature properties from DesktopModule.SupportedFeatures
            .ForMember(dest => dest.IsPortable, opt => opt.MapFrom(src =>
                src.ModuleDefinition != null && src.ModuleDefinition.DesktopModule != null
                    && src.ModuleDefinition.DesktopModule.IsPortable))
            .ForMember(dest => dest.IsSearchable, opt => opt.MapFrom(src =>
                src.ModuleDefinition != null && src.ModuleDefinition.DesktopModule != null
                    && src.ModuleDefinition.DesktopModule.IsSearchable))
            .ForMember(dest => dest.IsUpgradeable, opt => opt.MapFrom(src =>
                src.ModuleDefinition != null && src.ModuleDefinition.DesktopModule != null
                    && src.ModuleDefinition.DesktopModule.IsUpgradeable));

        // CreateModuleRequest → Module mapping
        // MIGRATION: Maps module creation request to entity
        CreateMap<CreateModuleRequest, Module>()
            .ForMember(dest => dest.TabId, opt => opt.MapFrom(src => src.TabId))
            .ForMember(dest => dest.ModuleDefId, opt => opt.MapFrom(src => src.ModuleDefId))
            .ForMember(dest => dest.PaneName, opt => opt.MapFrom(src => src.PaneName))
            .ForMember(dest => dest.ModuleOrder, opt => opt.MapFrom(src => src.ModuleOrder ?? 0))
            .ForMember(dest => dest.ModuleTitle, opt => opt.MapFrom(src => src.ModuleTitle))
            .ForMember(dest => dest.ContainerSrc, opt => opt.MapFrom(src => src.ContainerSrc))
            .ForMember(dest => dest.Alignment, opt => opt.MapFrom(src => src.Alignment))
            .ForMember(dest => dest.CacheTime, opt => opt.MapFrom(src => src.CacheTime ?? 0))
            .ForMember(dest => dest.Color, opt => opt.MapFrom(src => src.Color))
            .ForMember(dest => dest.Border, opt => opt.MapFrom(src => src.Border))
            .ForMember(dest => dest.IconFile, opt => opt.MapFrom(src => src.IconFile))
            .ForMember(dest => dest.Header, opt => opt.MapFrom(src => src.Header))
            .ForMember(dest => dest.Footer, opt => opt.MapFrom(src => src.Footer))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate))
            .ForMember(dest => dest.DisplayTitle, opt => opt.MapFrom(src => src.DisplayTitle))
            .ForMember(dest => dest.DisplayPrint, opt => opt.MapFrom(src => src.DisplayPrint))
            .ForMember(dest => dest.DisplaySyndicate, opt => opt.MapFrom(src => src.DisplaySyndicate))
            .ForMember(dest => dest.InheritViewPermissions, opt => opt.MapFrom(src => src.InheritViewPermissions))
            .ForMember(dest => dest.AllTabs, opt => opt.MapFrom(src => src.AllTabs))
            .ForMember(dest => dest.Visibility, opt => opt.MapFrom(src => src.Visibility))
            // Ignore identity fields - set by database
            .ForMember(dest => dest.ModuleId, opt => opt.Ignore())
            .ForMember(dest => dest.TabModuleId, opt => opt.Ignore())
            .ForMember(dest => dest.PortalId, opt => opt.Ignore())
            // Ignore navigation properties that exist on Module entity
            .ForMember(dest => dest.Portal, opt => opt.Ignore())
            .ForMember(dest => dest.Tab, opt => opt.Ignore())
            .ForMember(dest => dest.ModuleDefinition, opt => opt.Ignore())
            .ForMember(dest => dest.ModulePermissions, opt => opt.Ignore())
            // Ignore other computed/system fields that exist on Module entity
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.AuthorizedEditRoles, opt => opt.Ignore())
            .ForMember(dest => dest.AuthorizedViewRoles, opt => opt.Ignore())
            .ForMember(dest => dest.IsDefaultModule, opt => opt.Ignore())
            .ForMember(dest => dest.AllModules, opt => opt.Ignore());

        // UpdateModuleRequest → Module mapping
        // MIGRATION: Maps module update request with nullable properties for partial updates
        CreateMap<UpdateModuleRequest, Module>()
            .ForMember(dest => dest.ModuleTitle, opt => opt.Condition(src => src.ModuleTitle != null))
            .ForMember(dest => dest.ModuleTitle, opt => opt.MapFrom(src => src.ModuleTitle))
            .ForMember(dest => dest.Alignment, opt => opt.Condition(src => src.Alignment != null))
            .ForMember(dest => dest.Alignment, opt => opt.MapFrom(src => src.Alignment))
            .ForMember(dest => dest.Color, opt => opt.Condition(src => src.Color != null))
            .ForMember(dest => dest.Color, opt => opt.MapFrom(src => src.Color))
            .ForMember(dest => dest.Border, opt => opt.Condition(src => src.Border != null))
            .ForMember(dest => dest.Border, opt => opt.MapFrom(src => src.Border))
            .ForMember(dest => dest.IconFile, opt => opt.Condition(src => src.IconFile != null))
            .ForMember(dest => dest.IconFile, opt => opt.MapFrom(src => src.IconFile))
            .ForMember(dest => dest.CacheTime, opt => opt.Condition(src => src.CacheTime.HasValue))
            .ForMember(dest => dest.CacheTime, opt => opt.MapFrom(src => src.CacheTime ?? 0))
            .ForMember(dest => dest.Visibility, opt => opt.Condition(src => src.Visibility.HasValue))
            .ForMember(dest => dest.Visibility, opt => opt.MapFrom(src => src.Visibility ?? 0))
            .ForMember(dest => dest.Header, opt => opt.Condition(src => src.Header != null))
            .ForMember(dest => dest.Header, opt => opt.MapFrom(src => src.Header))
            .ForMember(dest => dest.Footer, opt => opt.Condition(src => src.Footer != null))
            .ForMember(dest => dest.Footer, opt => opt.MapFrom(src => src.Footer))
            .ForMember(dest => dest.StartDate, opt => opt.Condition(src => src.StartDate.HasValue))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.Condition(src => src.EndDate.HasValue))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate))
            .ForMember(dest => dest.ContainerSrc, opt => opt.Condition(src => src.ContainerSrc != null))
            .ForMember(dest => dest.ContainerSrc, opt => opt.MapFrom(src => src.ContainerSrc))
            .ForMember(dest => dest.InheritViewPermissions, opt => opt.Condition(src => src.InheritViewPermissions.HasValue))
            .ForMember(dest => dest.InheritViewPermissions, opt => opt.MapFrom(src => src.InheritViewPermissions ?? false))
            .ForMember(dest => dest.DisplayTitle, opt => opt.Condition(src => src.DisplayTitle.HasValue))
            .ForMember(dest => dest.DisplayTitle, opt => opt.MapFrom(src => src.DisplayTitle ?? true))
            .ForMember(dest => dest.DisplayPrint, opt => opt.Condition(src => src.DisplayPrint.HasValue))
            .ForMember(dest => dest.DisplayPrint, opt => opt.MapFrom(src => src.DisplayPrint ?? true))
            .ForMember(dest => dest.DisplaySyndicate, opt => opt.Condition(src => src.DisplaySyndicate.HasValue))
            .ForMember(dest => dest.DisplaySyndicate, opt => opt.MapFrom(src => src.DisplaySyndicate ?? false))
            .ForMember(dest => dest.AllTabs, opt => opt.Condition(src => src.AllTabs.HasValue))
            .ForMember(dest => dest.AllTabs, opt => opt.MapFrom(src => src.AllTabs ?? false))
            // Ignore identity fields - cannot be changed
            .ForMember(dest => dest.ModuleId, opt => opt.Ignore())
            .ForMember(dest => dest.TabModuleId, opt => opt.Ignore())
            .ForMember(dest => dest.TabId, opt => opt.Ignore())
            .ForMember(dest => dest.PortalId, opt => opt.Ignore())
            .ForMember(dest => dest.ModuleDefId, opt => opt.Ignore())
            .ForMember(dest => dest.ModuleOrder, opt => opt.Ignore())
            .ForMember(dest => dest.PaneName, opt => opt.Ignore())
            // Ignore navigation properties that exist on Module entity
            .ForMember(dest => dest.Portal, opt => opt.Ignore())
            .ForMember(dest => dest.Tab, opt => opt.Ignore())
            .ForMember(dest => dest.ModuleDefinition, opt => opt.Ignore())
            .ForMember(dest => dest.ModulePermissions, opt => opt.Ignore())
            // Ignore other computed/system fields that exist on Module entity
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.AuthorizedEditRoles, opt => opt.Ignore())
            .ForMember(dest => dest.AuthorizedViewRoles, opt => opt.Ignore());
    }

    /// <summary>
    /// Configures mappings for User domain entity and its related DTOs.
    /// </summary>
    /// <remarks>
    /// MIGRATION: User mappings handle:
    /// - User → UserDto: Maps user properties, flattens UserProfile, converts UserRoles to string array
    /// - CreateUserRequest → User: Maps input fields, initializes empty UserProfile
    /// - UpdateUserRequest → User: Maps update fields with null handling for partial updates
    /// </remarks>
    private void ConfigureUserMappings()
    {
        // User → UserDto mapping
        // MIGRATION: Maps User entity to response DTO with flattened profile and role names
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Username))
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.PortalId, opt => opt.MapFrom(src => src.PortalId))
            .ForMember(dest => dest.IsSuperUser, opt => opt.MapFrom(src => src.IsSuperUser))
            .ForMember(dest => dest.AffiliateId, opt => opt.MapFrom(src => src.AffiliateId))
            // MIGRATION: Flatten UserProfile properties into UserDto
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src =>
                src.Profile != null ? src.Profile.FirstName : src.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src =>
                src.Profile != null ? src.Profile.LastName : src.LastName))
            // MIGRATION: Flatten membership properties that exist on User entity
            .ForMember(dest => dest.IsApproved, opt => opt.MapFrom(src => src.IsApproved))
            .ForMember(dest => dest.IsLockedOut, opt => opt.MapFrom(src => src.IsLockedOut))
            // MIGRATION: IsOnline computed based on LastActivityDate (within last 15 minutes)
            .ForMember(dest => dest.IsOnline, opt => opt.MapFrom(src =>
                src.LastActivityDate.HasValue && src.LastActivityDate.Value > DateTime.UtcNow.AddMinutes(-15)))
            // MIGRATION: CreatedDate not directly available on User, set to null
            .ForMember(dest => dest.CreatedDate, opt => opt.Ignore())
            .ForMember(dest => dest.LastLoginDate, opt => opt.MapFrom(src => src.LastLoginDate))
            .ForMember(dest => dest.LastActivityDate, opt => opt.MapFrom(src => src.LastActivityDate))
            // MIGRATION: Convert UserRoles collection to role names string array
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src =>
                src.UserRoles != null
                    ? src.UserRoles
                        .Where(ur => ur.Role != null)
                        .Select(ur => ur.Role!.RoleName)
                        .ToList()
                    : new List<string>()));

        // CreateUserRequest → User mapping
        // MIGRATION: Maps user creation request to entity
        CreateMap<CreateUserRequest, User>()
            .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Username))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src =>
                !string.IsNullOrEmpty(src.DisplayName)
                    ? src.DisplayName
                    : $"{src.FirstName} {src.LastName}"))
            .ForMember(dest => dest.IsSuperUser, opt => opt.MapFrom(src => src.IsSuperUser))
            .ForMember(dest => dest.IsApproved, opt => opt.MapFrom(src => src.IsAuthorized))
            // Ignore identity fields - set by database
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.PortalId, opt => opt.Ignore())
            // Ignore navigation properties that exist on User entity
            .ForMember(dest => dest.Portal, opt => opt.Ignore())
            .ForMember(dest => dest.Profile, opt => opt.Ignore())
            .ForMember(dest => dest.UserRoles, opt => opt.Ignore())
            // Ignore computed/system fields that exist on User entity
            .ForMember(dest => dest.AffiliateId, opt => opt.Ignore())
            .ForMember(dest => dest.FullName, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.IsLockedOut, opt => opt.Ignore())
            .ForMember(dest => dest.LastActivityDate, opt => opt.Ignore())
            .ForMember(dest => dest.LastLoginDate, opt => opt.Ignore())
            .ForMember(dest => dest.LastPasswordChangeDate, opt => opt.Ignore())
            .ForMember(dest => dest.LastLockoutDate, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedDate, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedDate, opt => opt.Ignore())
            // Ignore security-related fields that are set by the service layer
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.ForcePasswordChange, opt => opt.Ignore());

        // UpdateUserRequest → User mapping
        // MIGRATION: Maps user update request with nullable properties for partial updates
        CreateMap<UpdateUserRequest, User>()
            .ForMember(dest => dest.DisplayName, opt => opt.Condition(src => src.DisplayName != null))
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName))
            .ForMember(dest => dest.FirstName, opt => opt.Condition(src => src.FirstName != null))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.Condition(src => src.LastName != null))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
            .ForMember(dest => dest.Email, opt => opt.Condition(src => src.Email != null))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.IsSuperUser, opt => opt.Condition(src => src.IsSuperUser.HasValue))
            .ForMember(dest => dest.IsSuperUser, opt => opt.MapFrom(src => src.IsSuperUser ?? false))
            .ForMember(dest => dest.IsApproved, opt => opt.Condition(src => src.IsApproved.HasValue))
            .ForMember(dest => dest.IsApproved, opt => opt.MapFrom(src => src.IsApproved ?? true))
            .ForMember(dest => dest.IsLockedOut, opt => opt.Condition(src => src.IsLockedOut.HasValue))
            .ForMember(dest => dest.IsLockedOut, opt => opt.MapFrom(src => src.IsLockedOut ?? false))
            .ForMember(dest => dest.AffiliateId, opt => opt.Condition(src => src.AffiliateId.HasValue))
            .ForMember(dest => dest.AffiliateId, opt => opt.MapFrom(src => src.AffiliateId))
            // Ignore identity fields - cannot be changed
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.Username, opt => opt.Ignore())
            .ForMember(dest => dest.PortalId, opt => opt.Ignore())
            // Ignore navigation properties that exist on User entity
            .ForMember(dest => dest.Portal, opt => opt.Ignore())
            .ForMember(dest => dest.Profile, opt => opt.Ignore())
            .ForMember(dest => dest.UserRoles, opt => opt.Ignore())
            // Ignore computed/system fields that exist on User entity
            .ForMember(dest => dest.FullName, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.LastActivityDate, opt => opt.Ignore())
            .ForMember(dest => dest.LastLoginDate, opt => opt.Ignore())
            .ForMember(dest => dest.LastPasswordChangeDate, opt => opt.Ignore())
            .ForMember(dest => dest.LastLockoutDate, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedDate, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedDate, opt => opt.Ignore())
            // Ignore security-related fields that are set by the service layer
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.ForcePasswordChange, opt => opt.Ignore());
    }

    /// <summary>
    /// Configures mappings for Role domain entity and its related DTOs.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Role mappings handle:
    /// - Role → RoleDto: Maps all role properties including billing configuration
    /// - CreateRoleRequest → Role: Maps input fields, sets defaults for billing-related fields
    /// </remarks>
    private void ConfigureRoleMappings()
    {
        // Role → RoleDto mapping
        // MIGRATION: Maps Role entity to response DTO
        CreateMap<Role, RoleDto>()
            .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.RoleId))
            .ForMember(dest => dest.PortalId, opt => opt.MapFrom(src => src.PortalId))
            .ForMember(dest => dest.RoleGroupId, opt => opt.MapFrom(src => src.RoleGroupId))
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.RoleName))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.ServiceFee, opt => opt.MapFrom(src => src.ServiceFee))
            .ForMember(dest => dest.BillingFrequency, opt => opt.MapFrom(src => src.BillingFrequency))
            .ForMember(dest => dest.BillingPeriod, opt => opt.MapFrom(src => src.BillingPeriod))
            .ForMember(dest => dest.TrialFee, opt => opt.MapFrom(src => src.TrialFee))
            .ForMember(dest => dest.TrialFrequency, opt => opt.MapFrom(src => src.TrialFrequency))
            .ForMember(dest => dest.TrialPeriod, opt => opt.MapFrom(src => src.TrialPeriod))
            .ForMember(dest => dest.IsPublic, opt => opt.MapFrom(src => src.IsPublic))
            .ForMember(dest => dest.AutoAssignment, opt => opt.MapFrom(src => src.AutoAssignment))
            .ForMember(dest => dest.RSVPCode, opt => opt.MapFrom(src => src.RSVPCode))
            .ForMember(dest => dest.IconFile, opt => opt.MapFrom(src => src.IconFile));

        // CreateRoleRequest → Role mapping
        // MIGRATION: Maps role creation request to entity
        CreateMap<CreateRoleRequest, Role>()
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.RoleName))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.RoleGroupId, opt => opt.MapFrom(src => src.RoleGroupId))
            .ForMember(dest => dest.IsPublic, opt => opt.MapFrom(src => src.IsPublic))
            .ForMember(dest => dest.AutoAssignment, opt => opt.MapFrom(src => src.AutoAssignment))
            .ForMember(dest => dest.ServiceFee, opt => opt.MapFrom(src => src.ServiceFee ?? 0m))
            .ForMember(dest => dest.BillingPeriod, opt => opt.MapFrom(src => src.BillingPeriod ?? 0))
            .ForMember(dest => dest.BillingFrequency, opt => opt.MapFrom(src => src.BillingFrequency ?? "N"))
            .ForMember(dest => dest.TrialFee, opt => opt.MapFrom(src => src.TrialFee ?? 0m))
            .ForMember(dest => dest.TrialPeriod, opt => opt.MapFrom(src => src.TrialPeriod ?? 0))
            .ForMember(dest => dest.TrialFrequency, opt => opt.MapFrom(src => src.TrialFrequency ?? "N"))
            .ForMember(dest => dest.RSVPCode, opt => opt.MapFrom(src => src.RSVPCode))
            .ForMember(dest => dest.IconFile, opt => opt.MapFrom(src => src.IconFile))
            // Map PortalId from request - required for role creation
            .ForMember(dest => dest.PortalId, opt => opt.MapFrom(src => src.PortalId))
            // Ignore identity fields - set by database
            .ForMember(dest => dest.RoleId, opt => opt.Ignore())
            // Ignore navigation properties that exist on Role entity
            .ForMember(dest => dest.Portal, opt => opt.Ignore())
            .ForMember(dest => dest.RoleGroup, opt => opt.Ignore())
            .ForMember(dest => dest.UserRoles, opt => opt.Ignore());
    }

    /// <summary>
    /// Configures mappings for supporting types used by the main entity mappings.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Supporting type mappings for:
    /// - UserProfile ↔ UserProfile (for profile cloning/updating)
    /// - UserRole ↔ UserRole (for user-role association)
    /// - RoleGroup ↔ RoleGroup (for role categorization)
    /// - DesktopModule ↔ DesktopModule (for module type information)
    /// - ModuleDefinition ↔ ModuleDefinition (for module definition metadata)
    /// - PortalAlias ↔ PortalAlias (for portal domain aliases)
    /// - Permission → Permission (for base permission definitions)
    /// - ModulePermission → ModulePermission (for module-level permissions)
    /// - TabPermission → TabPermission (for tab-level permissions)
    /// - FolderPermission → FolderPermission (for folder-level permissions)
    /// </remarks>
    private void ConfigureSupportingTypeMappings()
    {
        // UserProfile mapping (for internal use)
        // MIGRATION: Maps UserProfile entity for profile-related operations
        CreateMap<UserProfile, UserProfile>()
            .ForMember(dest => dest.ProfileId, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore());

        // UserRole mapping (for internal use)
        // MIGRATION: Maps UserRole junction entity
        CreateMap<UserRole, UserRole>()
            .ForMember(dest => dest.UserRoleId, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore())
            .ForMember(dest => dest.Role, opt => opt.Ignore());

        // RoleGroup mapping (for internal use)
        // MIGRATION: Maps RoleGroup entity for role categorization
        CreateMap<RoleGroup, RoleGroup>()
            .ForMember(dest => dest.RoleGroupId, opt => opt.Ignore())
            .ForMember(dest => dest.Portal, opt => opt.Ignore())
            .ForMember(dest => dest.Roles, opt => opt.Ignore());

        // DesktopModule mapping (for internal use)
        // MIGRATION: Maps DesktopModule entity for module type definitions
        CreateMap<DesktopModule, DesktopModule>()
            .ForMember(dest => dest.DesktopModuleId, opt => opt.Ignore())
            .ForMember(dest => dest.ModuleDefinitions, opt => opt.Ignore());

        // ModuleDefinition mapping (for internal use)
        // MIGRATION: Maps ModuleDefinition entity for module definition metadata
        CreateMap<ModuleDefinition, ModuleDefinition>()
            .ForMember(dest => dest.ModuleDefId, opt => opt.Ignore())
            .ForMember(dest => dest.DesktopModule, opt => opt.Ignore())
            .ForMember(dest => dest.Modules, opt => opt.Ignore());

        // PortalAlias mapping (for internal use)
        // MIGRATION: Maps PortalAlias entity for multi-domain portal configuration
        CreateMap<PortalAlias, PortalAlias>()
            .ForMember(dest => dest.PortalAliasId, opt => opt.Ignore())
            .ForMember(dest => dest.Portal, opt => opt.Ignore());

        // Permission base mapping (for internal use)
        // MIGRATION: Maps Permission base entity for permission definitions
        CreateMap<Permission, Permission>()
            .ForMember(dest => dest.PermissionId, opt => opt.Ignore());

        // ModulePermission mapping (for internal use)
        // MIGRATION: Maps ModulePermission for module-level permission assignments
        CreateMap<ModulePermission, ModulePermission>()
            .ForMember(dest => dest.ModulePermissionId, opt => opt.Ignore())
            .ForMember(dest => dest.Module, opt => opt.Ignore())
            .ForMember(dest => dest.Permission, opt => opt.Ignore())
            .ForMember(dest => dest.Role, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore());

        // TabPermission mapping (for internal use)
        // MIGRATION: Maps TabPermission for tab-level permission assignments
        CreateMap<TabPermission, TabPermission>()
            .ForMember(dest => dest.TabPermissionId, opt => opt.Ignore())
            .ForMember(dest => dest.Tab, opt => opt.Ignore())
            .ForMember(dest => dest.Permission, opt => opt.Ignore())
            .ForMember(dest => dest.Role, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore());

        // FolderPermission mapping (for internal use)
        // MIGRATION: Maps FolderPermission for folder-level permission assignments
        CreateMap<FolderPermission, FolderPermission>()
            .ForMember(dest => dest.FolderPermissionId, opt => opt.Ignore())
            .ForMember(dest => dest.Permission, opt => opt.Ignore())
            .ForMember(dest => dest.Role, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore());

        // Tab mapping (for internal use in portal hierarchies)
        // MIGRATION: Maps Tab entity for navigation hierarchy
        CreateMap<Tab, Tab>()
            .ForMember(dest => dest.TabId, opt => opt.Ignore())
            .ForMember(dest => dest.Portal, opt => opt.Ignore())
            .ForMember(dest => dest.Parent, opt => opt.Ignore())
            .ForMember(dest => dest.Children, opt => opt.Ignore())
            .ForMember(dest => dest.Modules, opt => opt.Ignore())
            .ForMember(dest => dest.TabPermissions, opt => opt.Ignore());
    }
}
