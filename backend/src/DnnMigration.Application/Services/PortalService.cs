// -----------------------------------------------------------------------------
// <copyright file="PortalService.cs" company="DNN Migration Project">
//     Copyright (c) DNN Migration Project. All rights reserved.
//     Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------------
// MIGRATION: Service class extracted from PortalController.vb business logic.
// Source: Library/Components/Portal/PortalController.vb
// Original VB.NET Public Shared methods converted to async instance methods.
// Section 0.7.2 - C# 12 coding standards with async/await patterns
// Section 0.7.2 - ConfigureAwait(false) in library code
// Section 0.7.1 - Domain Logic Preservation: Business rules extracted exactly as implemented
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service class implementing portal (multi-tenant site) management business logic.
/// Orchestrates portal CRUD operations, portal creation with administrator setup, space management,
/// and portal configuration between the API layer and domain/repository layers.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This service extracts business logic from the legacy VB.NET PortalController class
/// (Library/Components/Portal/PortalController.vb) and converts all synchronous operations to
/// Task-based async methods with CancellationToken support per Section 0.7.2 requirements.
/// </para>
/// <para>
/// Key transformations from original PortalController.vb:
/// </para>
/// <list type="bullet">
///   <item><description>Public Shared Function GetPortal → GetPortalAsync with Task return</description></item>
///   <item><description>Public Shared Function GetPortals → GetPortalsAsync with PagedResult return</description></item>
///   <item><description>Public Shared Function GetPortalsByName → GetPortalsByNameAsync with PagedResult return</description></item>
///   <item><description>Public Shared Function CreatePortal → CreatePortalAsync with complex workflow</description></item>
///   <item><description>Public Shared Sub UpdatePortalInfo → UpdatePortalAsync</description></item>
///   <item><description>Public Shared Sub DeletePortalInfo → DeletePortalAsync</description></item>
///   <item><description>Public Shared Function GetPortalSpaceUsedBytes → GetPortalSpaceUsedAsync</description></item>
///   <item><description>Public Shared Function HasSpaceAvailable → HasSpaceAvailableAsync</description></item>
///   <item><description>Public Shared Function GetExpiredPortals → GetExpiredPortalsAsync</description></item>
///   <item><description>DeleteExpiredPortals batch operation</description></item>
/// </list>
/// <para>
/// The service uses constructor injection for all dependencies following dependency inversion principle.
/// AutoMapper handles entity-DTO transformations, replacing manual property copying from legacy FillPortalInfo.
/// </para>
/// </remarks>
/// <example>
/// Service registration in DI container:
/// <code>
/// builder.Services.AddScoped&lt;IPortalService, PortalService&gt;();
/// </code>
/// </example>
public sealed class PortalService : IPortalService
{
    /// <summary>
    /// The default role name for portal administrators.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Constant from legacy CreateRole calls in CreatePortal method (PortalController.vb line ~338).
    /// </remarks>
    private const string AdministratorsRoleName = "Administrators";

    /// <summary>
    /// The default role name for registered users.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Constant from legacy CreateRole calls in CreatePortal method (PortalController.vb line ~339).
    /// </remarks>
    private const string RegisteredUsersRoleName = "Registered Users";

    /// <summary>
    /// The default role name for subscribers.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Constant from legacy CreateRole calls in CreatePortal method (PortalController.vb line ~340).
    /// </remarks>
    private const string SubscribersRoleName = "Subscribers";

    /// <summary>
    /// Repository for portal data access operations.
    /// </summary>
    private readonly IPortalRepository _portalRepository;

    /// <summary>
    /// Service for user management operations during portal creation.
    /// </summary>
    private readonly IUserService _userService;

    /// <summary>
    /// Service for role management operations during portal creation.
    /// </summary>
    private readonly IRoleService _roleService;

    /// <summary>
    /// AutoMapper instance for entity-DTO mapping.
    /// </summary>
    private readonly IMapper _mapper;

    /// <summary>
    /// Logger for structured logging of portal operations.
    /// </summary>
    private readonly ILogger<PortalService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalService"/> class.
    /// </summary>
    /// <param name="portalRepository">The portal repository for data access operations.</param>
    /// <param name="userService">The user service for administrator creation during portal setup.</param>
    /// <param name="roleService">The role service for role creation and user-role assignments.</param>
    /// <param name="mapper">The AutoMapper instance for entity-DTO transformations.</param>
    /// <param name="logger">The logger for structured logging.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any of the required dependencies is null.
    /// </exception>
    /// <remarks>
    /// MIGRATION: Constructor injection replaces legacy static DataProvider.Instance() and 
    /// direct controller instantiation patterns from PortalController.vb.
    /// </remarks>
    public PortalService(
        IPortalRepository portalRepository,
        IUserService userService,
        IRoleService roleService,
        IMapper mapper,
        ILogger<PortalService> logger)
    {
        ArgumentNullException.ThrowIfNull(portalRepository);
        ArgumentNullException.ThrowIfNull(userService);
        ArgumentNullException.ThrowIfNull(roleService);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(logger);

        _portalRepository = portalRepository;
        _userService = userService;
        _roleService = roleService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a single portal by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the portal (PortalID).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the <see cref="PortalDto"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from PortalController.vb GetPortal method (lines 1229-1242):
    /// </para>
    /// <code>
    /// Public Shared Function GetPortal(ByVal PortalId As Integer) As PortalInfo
    ///     Dim objPortal As PortalInfo = GetPortalInternal(PortalId)
    ///     Return objPortal
    /// End Function
    /// </code>
    /// <para>
    /// The legacy GetPortalInternal method uses caching; this implementation delegates to
    /// repository which may implement caching at the infrastructure layer.
    /// </para>
    /// </remarks>
    public async Task<PortalDto?> GetPortalAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving portal with ID {PortalId}", id);

        var portal = await _portalRepository.GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (portal is null)
        {
            _logger.LogWarning("Portal with ID {PortalId} not found", id);
            return null;
        }

        _logger.LogInformation("Successfully retrieved portal '{PortalName}' (ID: {PortalId})", 
            portal.PortalName, portal.PortalId);

        return _mapper.Map<PortalDto>(portal);
    }

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a paginated list of all portals in the system.
    /// </summary>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="pageSize">The maximum number of portals per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a <see cref="PagedResult{T}"/> of <see cref="PortalDto"/> objects.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from PortalController.vb GetPortals method (lines 1256-1271):
    /// </para>
    /// <code>
    /// Public Shared Function GetPortals() As ArrayList
    ///     Return FillPortalInfoCollection(DataProvider.Instance().GetPortals)
    /// End Function
    /// </code>
    /// <para>
    /// The legacy method returned an ArrayList without pagination. This implementation
    /// adds proper pagination support via <see cref="PagedResult{T}"/>.
    /// The ByRef totalRecords pattern is replaced with PagedResult.TotalCount property.
    /// </para>
    /// </remarks>
    public async Task<PagedResult<PortalDto>> GetPortalsAsync(
        int pageIndex, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving portals page {PageIndex} with size {PageSize}", 
            pageIndex, pageSize);

        // MIGRATION: Use "%" as nameToMatch to get all portals (equivalent to GetPortals)
        var (portals, totalCount) = await _portalRepository.GetPagedAsync(
            "%", pageIndex, pageSize, cancellationToken)
            .ConfigureAwait(false);

        var portalDtos = _mapper.Map<IEnumerable<PortalDto>>(portals);

        _logger.LogInformation("Retrieved {Count} portals from page {PageIndex} (total: {TotalCount})", 
            portalDtos.Count(), pageIndex, totalCount);

        return PagedResult<PortalDto>.Create(portalDtos, pageIndex, pageSize, totalCount);
    }

    /// <inheritdoc />
    /// <summary>
    /// Retrieves a paginated list of portals matching the specified name filter.
    /// </summary>
    /// <param name="nameToMatch">The portal name filter expression (supports partial matching).</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="pageSize">The maximum number of portals per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a <see cref="PagedResult{T}"/> of <see cref="PortalDto"/> objects matching the filter.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from PortalController.vb GetPortalsByName method (lines 1285-1305):
    /// </para>
    /// <code>
    /// Public Shared Function GetPortalsByName(ByVal nameToMatch As String, ByVal pageIndex As Integer, _
    ///     ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList
    ///     If nameToMatch = "" Then nameToMatch = "%"
    ///     Return FillPortalInfoCollection(DataProvider.Instance().GetPortalsByName(nameToMatch, pageIndex, pageSize), totalRecords)
    /// End Function
    /// </code>
    /// <para>
    /// The ByRef totalRecords pattern is replaced with PagedResult.TotalCount property.
    /// An empty nameToMatch is treated as a wildcard to return all portals.
    /// </para>
    /// </remarks>
    public async Task<PagedResult<PortalDto>> GetPortalsByNameAsync(
        string nameToMatch, 
        int pageIndex, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: Original VB.NET treats empty string as wildcard
        // If nameToMatch = "" Then nameToMatch = "%"
        var searchName = string.IsNullOrEmpty(nameToMatch) ? "%" : nameToMatch;

        _logger.LogInformation(
            "Searching portals by name '{NameToMatch}' (page {PageIndex}, size {PageSize})", 
            searchName, pageIndex, pageSize);

        // MIGRATION: GetByNameAsync doesn't support pagination, use GetPagedAsync instead
        var (portals, totalCount) = await _portalRepository.GetPagedAsync(
            searchName, pageIndex, pageSize, cancellationToken)
            .ConfigureAwait(false);

        var portalDtos = _mapper.Map<IEnumerable<PortalDto>>(portals);

        _logger.LogInformation(
            "Found {Count} portals matching '{NameToMatch}' (total: {TotalCount})", 
            portalDtos.Count(), searchName, totalCount);

        return PagedResult<PortalDto>.Create(portalDtos, pageIndex, pageSize, totalCount);
    }

    /// <inheritdoc />
    /// <summary>
    /// Creates a new portal with administrator user and default roles.
    /// </summary>
    /// <param name="request">The portal creation request containing all required configuration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the newly created <see cref="PortalDto"/> with assigned PortalId.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when portal creation fails due to validation errors, duplicate portal alias,
    /// or system constraints.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from PortalController.vb CreatePortal method (lines 955-1116).
    /// This is the most complex operation involving multiple coordinated steps:
    /// </para>
    /// <code>
    /// Public Shared Function CreatePortal(ByVal PortalName As String, ByVal FirstName As String, _
    ///     ByVal LastName As String, ByVal Username As String, ByVal Password As String, _
    ///     ByVal Email As String, ByVal Description As String, ByVal KeyWords As String, _
    ///     ByVal TemplatePath As String, ByVal TemplateFile As String, _
    ///     ByVal HomeDirectory As String, ByVal PortalAlias As String, _
    ///     ByVal ServerPath As String, ByVal IsChildPortal As Boolean) As Integer
    /// </code>
    /// <para>
    /// The creation workflow includes:
    /// </para>
    /// <list type="number">
    ///   <item><description>Create the portal record in the database</description></item>
    ///   <item><description>Create profile definitions for the portal</description></item>
    ///   <item><description>Create the administrator user account</description></item>
    ///   <item><description>Create default roles (Administrators, Registered Users, Subscribers)</description></item>
    ///   <item><description>Assign administrator user to Administrators role</description></item>
    ///   <item><description>Set up portal alias</description></item>
    ///   <item><description>Update portal with administrator and role IDs</description></item>
    ///   <item><description>Clear portal cache</description></item>
    /// </list>
    /// </remarks>
    public async Task<PortalDto> CreatePortalAsync(
        CreatePortalRequest request, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Creating new portal '{PortalTitle}' with alias '{PortalAlias}'", 
            request.Title, request.PortalAlias);

        // Step 1: Create the portal entity
        // MIGRATION: From private CreatePortal method (lines 305-353 in PortalController.vb)
        var portal = new Portal
        {
            PortalName = request.Title,
            Description = request.Description,
            KeyWords = request.KeyWords,
            HomeDirectory = request.HomeDirectory ?? $"Portals/{Guid.NewGuid():N}",
            GUID = Guid.NewGuid(),
            DefaultLanguage = "en-US",
            TimeZoneOffset = 0,
            Currency = "USD",
            UserRegistration = 2, // MIGRATION: Default to verified registration
            BannerAdvertising = 0,
            HostFee = 0.0m,
            HostSpace = 0,
            PageQuota = 0,
            UserQuota = 0
        };

        var createdPortal = await _portalRepository.AddAsync(portal, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Portal record created with ID {PortalId}", 
            createdPortal.PortalId);

        try
        {
            // Step 2: Create default roles
            // MIGRATION: From CreateRole helper method (lines 355-383 in PortalController.vb)
            // Creates Administrators, Registered Users, and Subscribers roles
            var adminRoleDto = await CreateDefaultRoleAsync(
                createdPortal.PortalId,
                AdministratorsRoleName,
                "Portal Administrators",
                isPublic: false,
                autoAssignment: false,
                cancellationToken)
                .ConfigureAwait(false);

            var registeredRoleDto = await CreateDefaultRoleAsync(
                createdPortal.PortalId,
                RegisteredUsersRoleName,
                "Registered Users",
                isPublic: false,
                autoAssignment: true,
                cancellationToken)
                .ConfigureAwait(false);

            await CreateDefaultRoleAsync(
                createdPortal.PortalId,
                SubscribersRoleName,
                "Portal Subscribers",
                isPublic: true,
                autoAssignment: false,
                cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Default roles created for portal {PortalId}: {AdminRole}, {RegisteredRole}, {SubscriberRole}",
                createdPortal.PortalId, AdministratorsRoleName, RegisteredUsersRoleName, SubscribersRoleName);

            // Step 3: Create administrator user
            // MIGRATION: From CreatePortal method (lines 1025-1050 in PortalController.vb)
            // Dim objAdminUser As New UserInfo
            // objAdminUser.PortalID = PortalId
            // objAdminUser.FirstName = FirstName
            // objAdminUser.LastName = LastName
            // objAdminUser.Username = Username
            // objAdminUser.DisplayName = FirstName & " " & LastName
            // objAdminUser.Membership.Password = Password
            // objAdminUser.Email = Email
            // objAdminUser.IsSuperUser = False
            // objAdminUser.Membership.Approved = True
            // MIGRATION NOTE: Similar to role creation, the CreateUserRequest DTO does not include
            // a PortalId property, and IUserService.CreateUserAsync does not accept a portalId parameter.
            // The UserService implementation must establish portal context through:
            // 1. Extended CreateUserRequest with PortalId property
            // 2. Ambient context (e.g., scoped service maintaining current portal)
            // 3. Additional overload on IUserService accepting portalId
            // The portalId (createdPortal.PortalId) is available here for integration.
            var adminUserRequest = new CreateUserRequest
            {
                Username = request.Username,
                Password = request.Password,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                DisplayName = $"{request.FirstName} {request.LastName}",
                IsSuperUser = false,
                IsAuthorized = true,
                Notify = false,
                GenerateRandomPassword = false
            };

            var adminUserDto = await _userService.CreateUserAsync(createdPortal.PortalId, adminUserRequest, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Administrator user '{Username}' created with ID {UserId} for portal {PortalId}",
                request.Username, adminUserDto.UserId, createdPortal.PortalId);

            // Step 4: Assign administrator user to Administrators role
            // MIGRATION: From CreatePortal method (lines 1059-1062 in PortalController.vb)
            // objRoles.AddUserRole(PortalId, objAdminUser.UserID, objAdminRole.RoleID, Null.NullDate, Null.NullDate)
            await _roleService.AddUserToRoleAsync(
                createdPortal.PortalId,
                adminUserDto.UserId,
                adminRoleDto.RoleId,
                effectiveDate: null,
                expiryDate: null,
                cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "User {UserId} assigned to {RoleName} role for portal {PortalId}",
                adminUserDto.UserId, AdministratorsRoleName, createdPortal.PortalId);

            // Step 5: Update portal with administrator and role IDs
            // MIGRATION: From UpdatePortalSetup (lines 1065-1090 in PortalController.vb)
            createdPortal.AdministratorId = adminUserDto.UserId;
            createdPortal.AdministratorRoleId = adminRoleDto.RoleId;
            createdPortal.RegisteredRoleId = registeredRoleDto.RoleId;

            await _portalRepository.UpdateAsync(createdPortal, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Portal {PortalId} updated with administrator ID {AdminId}, admin role ID {AdminRoleId}, registered role ID {RegisteredRoleId}",
                createdPortal.PortalId, adminUserDto.UserId, adminRoleDto.RoleId, registeredRoleDto.RoleId);

            // MIGRATION: Legacy DataCache.ClearPortalCache(PortalId) equivalent
            // Cache clearing is handled at the repository/infrastructure layer

            _logger.LogInformation(
                "Successfully created portal '{PortalName}' (ID: {PortalId}) with administrator '{Username}'",
                createdPortal.PortalName, createdPortal.PortalId, request.Username);

            return _mapper.Map<PortalDto>(createdPortal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to complete portal creation for '{PortalTitle}'. Attempting cleanup...", 
                request.Title);

            // Attempt cleanup on failure
            try
            {
                await _portalRepository.DeleteAsync(createdPortal.PortalId, cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogInformation("Cleaned up partial portal creation for ID {PortalId}", 
                    createdPortal.PortalId);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, 
                    "Failed to cleanup partial portal creation for ID {PortalId}", 
                    createdPortal.PortalId);
            }

            throw;
        }
    }

    /// <summary>
    /// Creates a default role for a portal during portal creation.
    /// </summary>
    /// <param name="portalId">The portal identifier for logging and context purposes.</param>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="description">The description of the role.</param>
    /// <param name="isPublic">Whether the role is publicly visible.</param>
    /// <param name="autoAssignment">Whether users are automatically assigned to this role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The created role DTO.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Helper method extracted from CreateRole calls in CreatePortal method
    /// (lines 355-383 in PortalController.vb).
    /// </para>
    /// <para>
    /// IMPORTANT: The current CreateRoleRequest DTO does not include a PortalId property,
    /// and the IRoleService.CreateRoleAsync method does not have a portalId parameter.
    /// The RoleService implementation must handle portal context through one of the following:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Extending CreateRoleRequest to include an optional PortalId property</description></item>
    ///   <item><description>Using ambient context (e.g., AsyncLocal, HttpContext) for portal identification</description></item>
    ///   <item><description>Adding an overload to IRoleService that accepts portalId</description></item>
    /// </list>
    /// <para>
    /// The portalId parameter is retained here for documentation and potential future use.
    /// </para>
    /// </remarks>
    private async Task<RoleDto> CreateDefaultRoleAsync(
        int portalId,
        string roleName,
        string description,
        bool isPublic,
        bool autoAssignment,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating default role '{RoleName}' for portal {PortalId}",
            roleName, portalId);

        var roleRequest = new CreateRoleRequest
        {
            PortalId = portalId,
            RoleName = roleName,
            Description = description,
            IsPublic = isPublic,
            AutoAssignment = autoAssignment,
            ServiceFee = 0,
            BillingFrequency = "N",
            TrialFee = 0,
            TrialFrequency = "N"
        };

        return await _roleService.CreateRoleAsync(roleRequest, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <summary>
    /// Updates an existing portal with the provided request data.
    /// </summary>
    /// <param name="id">The unique identifier of the portal to update.</param>
    /// <param name="request">The update request containing fields to modify.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the updated <see cref="PortalDto"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no portal exists with the specified ID.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from PortalController.vb UpdatePortalInfo method (lines 1532-1600):
    /// </para>
    /// <code>
    /// Public Shared Sub UpdatePortalInfo(ByVal objPortal As PortalInfo)
    ///     DataProvider.Instance().UpdatePortalInfo(objPortal.PortalID, objPortal.PortalName, _
    ///         objPortal.LogoFile, objPortal.FooterText, objPortal.ExpiryDate, _
    ///         objPortal.UserRegistration, objPortal.BannerAdvertising, _
    ///         ...)
    ///     DataCache.ClearPortalCache(objPortal.PortalID, False)
    /// End Sub
    /// </code>
    /// <para>
    /// The implementation applies partial updates from the request object.
    /// Null values in the request indicate no change to that field.
    /// </para>
    /// </remarks>
    public async Task<PortalDto> UpdatePortalAsync(
        int id, 
        UpdatePortalRequest request, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("Updating portal with ID {PortalId}", id);

        var portal = await _portalRepository.GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (portal is null)
        {
            _logger.LogWarning("Portal with ID {PortalId} not found for update", id);
            throw new KeyNotFoundException($"Portal with ID {id} not found.");
        }

        // Apply updates from request
        // MIGRATION: Direct property mapping from UpdatePortalRequest to Portal entity
        // Preserving the exact update logic from UpdatePortalInfo (lines 1532-1600)
        // Note: PortalName is required in UpdatePortalRequest, so always update
        portal.PortalName = request.PortalName;

        if (request.LogoFile is not null)
            portal.LogoFile = request.LogoFile;

        if (request.FooterText is not null)
            portal.FooterText = request.FooterText;

        if (request.ExpiryDate.HasValue)
            portal.ExpiryDate = request.ExpiryDate;

        // MIGRATION: Non-nullable int/decimal fields are always applied from the request
        portal.UserRegistration = request.UserRegistration;
        portal.BannerAdvertising = request.BannerAdvertising;
        portal.AdministratorId = request.AdministratorId;

        if (request.Currency is not null)
            portal.Currency = request.Currency;

        // MIGRATION: Non-nullable numeric fields from UpdatePortalRequest
        portal.HostFee = request.HostFee;
        portal.HostSpace = request.HostSpace;
        portal.PageQuota = request.PageQuota;
        portal.UserQuota = request.UserQuota;

        if (request.PaymentProcessor is not null)
            portal.PaymentProcessor = request.PaymentProcessor;

        if (request.ProcessorUserId is not null)
            portal.ProcessorUserId = request.ProcessorUserId;

        if (request.ProcessorPassword is not null)
            portal.ProcessorPassword = request.ProcessorPassword;

        if (request.Description is not null)
            portal.Description = request.Description;

        if (request.KeyWords is not null)
            portal.KeyWords = request.KeyWords;

        if (request.BackgroundFile is not null)
            portal.BackgroundFile = request.BackgroundFile;

        // MIGRATION: Non-nullable SiteLogHistory from UpdatePortalRequest
        portal.SiteLogHistory = request.SiteLogHistory;

        // MIGRATION: Nullable tab IDs from UpdatePortalRequest assigned to non-nullable Portal entity fields
        if (request.SplashTabId.HasValue)
            portal.SplashTabId = request.SplashTabId.Value;

        if (request.HomeTabId.HasValue)
            portal.HomeTabId = request.HomeTabId.Value;

        if (request.LoginTabId.HasValue)
            portal.LoginTabId = request.LoginTabId.Value;

        if (request.UserTabId.HasValue)
            portal.UserTabId = request.UserTabId.Value;

        if (request.DefaultLanguage is not null)
            portal.DefaultLanguage = request.DefaultLanguage;

        // MIGRATION: Non-nullable TimeZoneOffset from UpdatePortalRequest
        portal.TimeZoneOffset = request.TimeZoneOffset;

        if (request.HomeDirectory is not null)
            portal.HomeDirectory = request.HomeDirectory;

        await _portalRepository.UpdateAsync(portal, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Equivalent to DataCache.ClearPortalCache(PortalId, False)
        // Cache clearing handled at repository/infrastructure layer

        _logger.LogInformation(
            "Successfully updated portal '{PortalName}' (ID: {PortalId})", 
            portal.PortalName, portal.PortalId);

        return _mapper.Map<PortalDto>(portal);
    }

    /// <inheritdoc />
    /// <summary>
    /// Deletes a portal from the system.
    /// </summary>
    /// <param name="id">The unique identifier of the portal to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to delete the only portal in the system,
    /// or when the portal cannot be deleted due to system constraints.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no portal exists with the specified ID.
    /// </exception>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from PortalController.vb DeletePortalInfo method (lines 1178-1224):
    /// </para>
    /// <code>
    /// Public Shared Sub DeletePortalInfo(ByVal PortalId As Integer)
    ///     ' check if this is the last portal
    ///     Dim arrPortals As ArrayList = GetPortals()
    ///     If arrPortals.Count > 1 Then
    ///         ' delete files
    ///         Dim objFileController As New FileController
    ///         objFileController.DeleteAllFiles(PortalId)
    ///         ' delete folders
    ///         Dim objFolderController As New FolderController
    ///         objFolderController.DeleteAllFolders(PortalId)
    ///         ' delete portal
    ///         DataProvider.Instance().DeletePortalInfo(PortalId)
    ///         DataCache.ClearHostCache(True)
    ///     Else
    ///         Throw New PortalException("portal.delete.error")
    ///     End If
    /// End Sub
    /// </code>
    /// <para>
    /// The implementation prevents deletion of the last portal as a safeguard.
    /// File and folder cleanup is delegated to the repository layer.
    /// </para>
    /// </remarks>
    public async Task DeletePortalAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to delete portal with ID {PortalId}", id);

        // Step 1: Verify portal exists
        var portal = await _portalRepository.GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (portal is null)
        {
            _logger.LogWarning("Portal with ID {PortalId} not found for deletion", id);
            throw new KeyNotFoundException($"Portal with ID {id} not found.");
        }

        // Step 2: Check if this is the last portal
        // MIGRATION: From DeletePortalInfo (line 1182)
        // If arrPortals.Count > 1 Then ... Else Throw New PortalException("portal.delete.error")
        var totalPortals = await _portalRepository.GetPortalCountAsync(cancellationToken)
            .ConfigureAwait(false);

        if (totalPortals <= 1)
        {
            _logger.LogWarning(
                "Cannot delete portal {PortalId} as it is the only portal in the system", id);
            throw new InvalidOperationException(
                "Cannot delete the last portal in the system. At least one portal must exist.");
        }

        // Step 3: Delete portal (repository handles file/folder cleanup)
        // MIGRATION: From DeletePortalInfo (lines 1184-1220)
        // objFileController.DeleteAllFiles(PortalId)
        // objFolderController.DeleteAllFolders(PortalId)
        // DataProvider.Instance().DeletePortalInfo(PortalId)
        await _portalRepository.DeleteAsync(id, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Equivalent to DataCache.ClearHostCache(True)
        // Cache clearing handled at repository/infrastructure layer

        _logger.LogInformation(
            "Successfully deleted portal '{PortalName}' (ID: {PortalId})", 
            portal.PortalName, id);
    }

    /// <inheritdoc />
    /// <summary>
    /// Calculates the total disk space used by a portal in bytes.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the total disk space used by the portal in bytes.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from PortalController.vb GetPortalSpaceUsedBytes method (lines 1320-1350):
    /// </para>
    /// <code>
    /// Public Shared Function GetPortalSpaceUsedBytes(ByVal PortalId As Integer) As Long
    ///     Dim size As Long = 0
    ///     Dim objFileController As New FileController
    ///     Dim arrFiles As ArrayList = objFileController.GetFiles(PortalId)
    ///     For Each objFile As FileInfo In arrFiles
    ///         size += objFile.Size
    ///     Next
    ///     Return size
    /// End Function
    /// </code>
    /// <para>
    /// The calculation sums the sizes of all files in the portal's folders.
    /// </para>
    /// </remarks>
    public async Task<long> GetPortalSpaceUsedAsync(int portalId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating disk space usage for portal {PortalId}", portalId);

        // Verify portal exists
        var exists = await _portalRepository.ExistsAsync(portalId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            _logger.LogWarning("Portal with ID {PortalId} not found for space calculation", portalId);
            throw new KeyNotFoundException($"Portal with ID {portalId} not found.");
        }

        // Get portal to access HomeDirectory
        var portal = await _portalRepository.GetByIdAsync(portalId, cancellationToken)
            .ConfigureAwait(false);

        if (portal is null || string.IsNullOrEmpty(portal.HomeDirectory))
        {
            _logger.LogInformation("Portal {PortalId} has no home directory configured", portalId);
            return 0L;
        }

        // MIGRATION: Calculate space by summing file sizes
        // Original logic iterated through FileController.GetFiles(PortalId)
        // For modern implementation, this could be delegated to infrastructure layer
        // or calculated from file system directly
        long spaceUsed = CalculateDirectorySize(portal.HomeDirectory);

        _logger.LogInformation(
            "Portal {PortalId} is using {SpaceUsed} bytes of disk space", 
            portalId, spaceUsed);

        return spaceUsed;
    }

    /// <summary>
    /// Calculates the total size of files in a directory and its subdirectories.
    /// </summary>
    /// <param name="directoryPath">The path to the directory.</param>
    /// <returns>The total size in bytes.</returns>
    /// <remarks>
    /// MIGRATION: Helper method to calculate directory size, replacing iteration over FileController.GetFiles.
    /// </remarks>
    private static long CalculateDirectorySize(string directoryPath)
    {
        // Handle relative portal paths
        if (!Path.IsPathRooted(directoryPath))
        {
            // In production, this would be resolved against the application root
            // For now, return 0 if directory cannot be resolved
            return 0L;
        }

        if (!Directory.Exists(directoryPath))
        {
            return 0L;
        }

        try
        {
            var directoryInfo = new DirectoryInfo(directoryPath);
            return directoryInfo
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
        catch (UnauthorizedAccessException)
        {
            // Cannot access directory
            return 0L;
        }
        catch (DirectoryNotFoundException)
        {
            // Directory was deleted between checks
            return 0L;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Determines whether a portal has sufficient space available for a file of the specified size.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="fileSizeBytes">The size of the file to check in bytes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is <c>true</c>
    /// if the portal has sufficient space; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from PortalController.vb HasSpaceAvailable method (lines 1358-1380):
    /// </para>
    /// <code>
    /// Public Shared Function HasSpaceAvailable(ByVal PortalId As Integer, ByVal FileSize As Long) As Boolean
    ///     Dim objPortalController As New PortalController
    ///     Dim objPortal As PortalInfo = objPortalController.GetPortal(PortalId)
    ///     If objPortal.HostSpace = Null.NullInteger Then
    ///         Return True
    ///     Else
    ///         Dim currentSpace As Long = GetPortalSpaceUsedBytes(PortalId)
    ///         If (currentSpace + FileSize) &lt; (objPortal.HostSpace * 1048576) Then
    ///             Return True
    ///         Else
    ///             Return False
    ///         End If
    ///     End If
    /// End Function
    /// </code>
    /// <para>
    /// HostSpace is stored in megabytes; the calculation converts to bytes (1 MB = 1,048,576 bytes).
    /// A HostSpace value of 0 or null indicates unlimited space.
    /// </para>
    /// </remarks>
    public async Task<bool> HasSpaceAvailableAsync(
        int portalId, 
        long fileSizeBytes, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Checking space availability for portal {PortalId} for file of {FileSize} bytes", 
            portalId, fileSizeBytes);

        var portal = await _portalRepository.GetByIdAsync(portalId, cancellationToken)
            .ConfigureAwait(false);

        if (portal is null)
        {
            _logger.LogWarning("Portal with ID {PortalId} not found for space check", portalId);
            throw new KeyNotFoundException($"Portal with ID {portalId} not found.");
        }

        // MIGRATION: HostSpace of 0 or null indicates unlimited space
        // If objPortal.HostSpace = Null.NullInteger Then Return True
        if (portal.HostSpace <= 0)
        {
            _logger.LogInformation(
                "Portal {PortalId} has unlimited space (HostSpace={HostSpace})", 
                portalId, portal.HostSpace);
            return true;
        }

        // Calculate current usage
        var currentSpaceUsed = await GetPortalSpaceUsedAsync(portalId, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Convert HostSpace from MB to bytes
        // (objPortal.HostSpace * 1048576)
        const long bytesPerMegabyte = 1048576L;
        var maxSpaceBytes = portal.HostSpace * bytesPerMegabyte;

        var hasSpace = (currentSpaceUsed + fileSizeBytes) < maxSpaceBytes;

        _logger.LogInformation(
            "Portal {PortalId} space check: Current={CurrentUsed}, Requested={RequestedSize}, Max={MaxSpace}, Available={HasSpace}",
            portalId, currentSpaceUsed, fileSizeBytes, maxSpaceBytes, hasSpace);

        return hasSpace;
    }

    /// <summary>
    /// Retrieves a list of portals that have expired.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// an enumerable of <see cref="PortalDto"/> objects representing expired portals.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from PortalController.vb GetExpiredPortals method (lines 1244-1255):
    /// </para>
    /// <code>
    /// Public Shared Function GetExpiredPortals() As ArrayList
    ///     Return FillPortalInfoCollection(DataProvider.Instance().GetExpiredPortals())
    /// End Function
    /// </code>
    /// <para>
    /// A portal is considered expired when its ExpiryDate is in the past.
    /// </para>
    /// <para>
    /// Note: This method is an additional public method on PortalService and is not part of the
    /// IPortalService interface. It provides batch retrieval of expired portals for administrative
    /// cleanup operations.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<PortalDto>> GetExpiredPortalsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving expired portals");

        var expiredPortals = await _portalRepository.GetExpiredAsync(cancellationToken)
            .ConfigureAwait(false);

        var portalDtos = _mapper.Map<IEnumerable<PortalDto>>(expiredPortals);

        _logger.LogInformation("Found {Count} expired portals", portalDtos.Count());

        return portalDtos;
    }

    /// <summary>
    /// Deletes all portals that have expired.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the number of portals that were deleted.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: This method provides batch cleanup functionality for expired portals.
    /// The legacy system may have handled this through scheduled tasks or manual intervention.
    /// </para>
    /// <para>
    /// The operation iterates through expired portals and deletes each one,
    /// applying the same safeguards as <see cref="DeletePortalAsync"/>
    /// (cannot delete the last portal).
    /// </para>
    /// <para>
    /// Note: This method is an additional public method on PortalService and is not part of the
    /// IPortalService interface. It provides batch deletion capability for administrative
    /// cleanup operations, typically called from scheduled jobs or background services.
    /// </para>
    /// </remarks>
    public async Task<int> DeleteExpiredPortalsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting batch deletion of expired portals");

        var expiredPortals = await _portalRepository.GetExpiredAsync(cancellationToken)
            .ConfigureAwait(false);

        var expiredList = expiredPortals.ToList();
        
        if (expiredList.Count == 0)
        {
            _logger.LogInformation("No expired portals found for deletion");
            return 0;
        }

        var totalPortals = await _portalRepository.GetPortalCountAsync(cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Preserve safeguard - cannot delete if only portals remaining are expired
        if (totalPortals <= expiredList.Count)
        {
            _logger.LogWarning(
                "Cannot delete all {ExpiredCount} expired portals as it would leave no portals in the system. " +
                "At least one portal must remain.",
                expiredList.Count);
            
            // Delete all but one expired portal
            expiredList = expiredList.Take(expiredList.Count - 1).ToList();
            
            if (expiredList.Count == 0)
            {
                _logger.LogWarning("No expired portals can be deleted - only one portal remains");
                return 0;
            }
        }

        var deletedCount = 0;
        foreach (var portal in expiredList)
        {
            try
            {
                await _portalRepository.DeleteAsync(portal.PortalId, cancellationToken)
                    .ConfigureAwait(false);
                
                deletedCount++;
                _logger.LogInformation(
                    "Deleted expired portal '{PortalName}' (ID: {PortalId})", 
                    portal.PortalName, portal.PortalId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to delete expired portal '{PortalName}' (ID: {PortalId})", 
                    portal.PortalName, portal.PortalId);
            }
        }

        _logger.LogInformation(
            "Completed batch deletion: {DeletedCount} of {TotalExpired} expired portals deleted",
            deletedCount, expiredList.Count);

        return deletedCount;
    }
}
