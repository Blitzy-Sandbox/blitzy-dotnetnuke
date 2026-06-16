using AutoMapper;
using FluentValidation;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service for the Tab (Page) aggregate (a DNN "Tab" == a site Page).
/// MIGRATION: ports the business surface of <c>Library/Components/Tabs/TabController.vb</c> (1302 lines)
/// into async, DTO-based operations. Data access is delegated to <see cref="ITabRepository"/> (an EF Core
/// implementation in the Infrastructure layer), replacing the legacy controller's co-mingled ADO.NET /
/// <c>SqlDataProvider</c> calls and the <c>DataCache</c> + <c>FillTabInfo</c> reflection hydration. Entity
/// &lt;-&gt; DTO transformation is delegated to AutoMapper, and create/update payloads are validated with
/// FluentValidation before any persistence work occurs.
/// MIGRATION: Tab uses SOFT-delete via the <c>IsDeleted</c> flag (AAP 0.3.3, Section 6.3 of MIGRATION_NOTES).
/// All list reads return only non-deleted tabs; that <c>IsDeleted == false</c> filtering is performed by the
/// repository (<see cref="ITabRepository.GetByPortalAsync"/> / <see cref="ITabRepository.GetByParentAsync"/>),
/// so this service never re-adds deleted rows. The legacy rule that a parent tab still owning child tabs
/// cannot be deleted is reproduced here at the service level (see <see cref="DeleteAsync"/>).
/// </summary>
public class TabService : ITabService
{
    private readonly ITabRepository _tabRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateTabDto> _createValidator;
    private readonly IValidator<UpdateTabDto> _updateValidator;

    /// <summary>
    /// Initializes a new <see cref="TabService"/> with its injected collaborators.
    /// </summary>
    /// <param name="tabRepository">Repository providing data access for the Tab aggregate.</param>
    /// <param name="mapper">AutoMapper instance used for entity &lt;-&gt; DTO projection.</param>
    /// <param name="createValidator">FluentValidation validator for <see cref="CreateTabDto"/>.</param>
    /// <param name="updateValidator">FluentValidation validator for <see cref="UpdateTabDto"/>.</param>
    public TabService(
        ITabRepository tabRepository,
        IMapper mapper,
        IValidator<CreateTabDto> createValidator,
        IValidator<UpdateTabDto> updateValidator)
    {
        _tabRepository = tabRepository;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <inheritdoc />
    public async Task<TabDto?> GetByIdAsync(int tabId, int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy GetTab [TabController.vb:L467-502] first consulted the DataCache (the GetTabsByPortal
        // dictionary, resolving the PortalId from the Portals dictionary when unknown) and otherwise hydrated the
        // row with FillTabInfo reflection over an IDataReader. Both are replaced here by EF Core entity
        // materialization behind ITabRepository (Tab identity is portal-scoped: tabId + portalId).
        var tab = await _tabRepository.GetByIdAsync(tabId, portalId, cancellationToken);
        return tab is null ? null : _mapper.Map<TabDto>(tab);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TabDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy GetTabs(PortalId) [TabController.vb:L516-522] enumerated the cached GetTabsByPortal
        // dictionary into an ArrayList. The repository excludes soft-deleted tabs (IsDeleted == false) - in
        // legacy DNN this filtering happened at the stored-procedure level - so this service must not re-add
        // deleted rows.
        var tabs = await _tabRepository.GetByPortalAsync(portalId, cancellationToken);
        return _mapper.Map<IEnumerable<TabDto>>(tabs);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TabDto>> GetByParentAsync(int parentId, int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy GetTabsByParentId(ParentId, PortalId) [TabController.vb:L524-526] delegated to
        // GetTabsByParent. The repository excludes soft-deleted tabs (IsDeleted == false), preserving the legacy
        // result set for the in-scope read.
        var tabs = await _tabRepository.GetByParentAsync(parentId, portalId, cancellationToken);
        return _mapper.Map<IEnumerable<TabDto>>(tabs);
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy GetTabCount(portalId) [TabController.vb:L512-514] returned
        // DataProvider.GetTabCount(portalId) directly; here the count is delegated to ITabRepository, which
        // reports the EF Core aggregate over the existing (unchanged) schema.
        return await _tabRepository.GetCountAsync(portalId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TabDto> CreateAsync(CreateTabDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // The CreateTabDto -> Tab map ignores TabID (server-generated identity) and the server-managed /
        // hierarchy-derived members IsDeleted, Level, HasChildren, TabPath, DisableLink, PageHeadText,
        // AuthorizedRoles, AdministratorRoles, and the TabPermissions navigation (see TabProfile).
        var tab = _mapper.Map<Tab>(request);

        var created = await _tabRepository.AddAsync(tab, cancellationToken);

        // MIGRATION: legacy AddTab [TabController.vb:L330-373] also generated the hierarchical TabPath
        // (GenerateTabPath), seeded TabPermissions rows from objTab.TabPermissions (TabPermissionController),
        // updated the portal tab order (UpdatePortalTabOrder / UpdateTabOrder), copied every "all-tabs" module
        // onto the new page (ModuleController.CopyModule), and cleared the cache (ClearCache /
        // DataCache.RemoveCache). All of those are OMITTED here - TabPath/ordering are repository/persistence
        // concerns, and tab-permission seeding, the all-tabs module copy, and the Cache Provider are OUT OF
        // SCOPE (AAP 0.2.2).
        return _mapper.Map<TabDto>(created);
    }

    /// <inheritdoc />
    public async Task<TabDto> UpdateAsync(UpdateTabDto request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        // MIGRATION: legacy UpdateTab [TabController.vb:L780-814] loaded the current row via GetTab(objTab.TabID,
        // objTab.PortalID, False) only to decide whether children needed re-pathing. The EF Core repository
        // pattern requires a tracked entity, so the existing tab is loaded first (portal-scoped: TabID + PortalID,
        // both carried by UpdateTabDto) and the request is then mapped onto it.
        var existing = await _tabRepository.GetByIdAsync(request.TabID, request.PortalID, cancellationToken);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Tab {request.TabID} not found.");
        }

        // The UpdateTabDto -> Tab map ignores the server-managed / hierarchy-derived members (Level, HasChildren,
        // TabPath, DisableLink, IsDeleted, PageHeadText, AuthorizedRoles, AdministratorRoles) and the
        // TabPermissions navigation (see TabProfile), so only the editable page settings are applied.
        _mapper.Map(request, existing);

        await _tabRepository.UpdateAsync(existing, cancellationToken);

        // MIGRATION: legacy UpdateTab also regenerated the child TabPath when the name/parent changed
        // (UpdateChildTabPath), performed a TabPermission diff/replace (GetTabPermissionsCollectionByTabID +
        // CompareTo + DeleteTabPermissionsByTabID + AddTabPermission), updated the portal tab order
        // (UpdatePortalTabOrder), and cleared the cache (ClearCache). All of those are OMITTED here - TabPath
        // regeneration and tab ordering are repository/persistence concerns, and tab-permission management plus
        // the Cache Provider are OUT OF SCOPE (AAP 0.2.2).
        return _mapper.Map<TabDto>(existing);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int tabId, int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy instance DeleteTab [TabController.vb:L446-457] fetched children via
        // GetTabsByParentId and only deleted when there were none (arrTabs.Count = 0). This parent-with-children
        // guard is a service-level business rule, reproduced here.
        var children = await _tabRepository.GetByParentAsync(tabId, portalId, cancellationToken);
        if (children.Any())
        {
            // MIGRATION: legacy silently skipped deletion when child tabs existed (no else branch); we surface an
            // explicit error (rendered as RFC 7807 Problem Details by the API ExceptionHandlingMiddleware),
            // consistent with the Portal last-portal and User-delete admin guards.
            throw new InvalidOperationException("Cannot delete a tab that has child tabs.");
        }

        // MIGRATION: ITabRepository.DeleteAsync is the UNCONDITIONAL soft-delete (sets IsDeleted = true); the
        // legacy UpdatePortalTabOrder reorder and the cache removal (ClearCache / DataCache.RemoveCache) are
        // OMITTED (OUT OF SCOPE per AAP 0.2.2). The recursive recycle-bin cascade (the shared
        // DeleteTab/DeleteChildTabs path with special-tab checks and EventLog writes) is also OUT OF SCOPE.
        await _tabRepository.DeleteAsync(tabId, portalId, cancellationToken);
    }
}
