using AutoMapper;
using FluentValidation;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service for the Tab (Page) aggregate.
/// MIGRATION: ports the business logic of the legacy <c>Library/Components/Tabs/TabController.vb</c>
/// (DotNetNuke 4.9.0.85, 1302 lines), decoupled from data access. The legacy controller co-mingled
/// ADO.NET / <c>SqlDataProvider</c> calls, <c>CBO</c>/<c>FillTabInfo</c> reflection hydration, the
/// <c>DataCache</c> tab dictionary, tab-permission management, hierarchical <c>TabPath</c> generation,
/// and portal tab-order maintenance directly inside its methods. Here data access is delegated to
/// <see cref="ITabRepository"/> (EF Core in the Infrastructure layer), entity&#8596;DTO translation to
/// AutoMapper, and inbound input validation to FluentValidation.
/// </summary>
/// <remarks>
/// <para>
/// Tab is a <b>soft-delete</b> aggregate (AAP §0.3.3): <see cref="DeleteAsync"/> flips the
/// <c>IsDeleted</c> flag through the repository rather than physically removing the row, and every list
/// read (<see cref="GetByPortalAsync"/>, <see cref="GetByParentAsync"/>) returns only non-deleted tabs.
/// In the legacy stack the soft-delete and the list filtering lived in the stored procedures; here the
/// repository owns the <c>IsDeleted == false</c> predicate, so this service never re-introduces deleted
/// rows.
/// </para>
/// <para>
/// The <b>cannot-delete-a-parent-tab-that-still-has-children</b> business rule is enforced here at the
/// service layer (ported from the legacy instance <c>DeleteTab</c> [TabController.vb:L446-457], which
/// fetched children via <c>GetTabsByParentId</c> and only deleted when none remained). The repository
/// <see cref="ITabRepository.DeleteAsync"/> is intentionally unconditional — the guard does not belong in
/// the data-access contract.
/// </para>
/// <para>
/// The service is async-first and stateless; each deviation from the legacy behavior is annotated with a
/// <c>// MIGRATION:</c> comment and recorded in the root <c>MIGRATION_NOTES.md</c>. Per the Minimal
/// Change Clause the ported domain logic is preserved as-is and is not "improved".
/// </para>
/// </remarks>
public class TabService : ITabService
{
    private readonly ITabRepository _tabRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateTabDto> _createValidator;
    private readonly IValidator<UpdateTabDto> _updateValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TabService"/> class.
    /// </summary>
    /// <param name="tabRepository">Repository providing data access for the Tab aggregate.</param>
    /// <param name="mapper">AutoMapper instance used for entity&#8596;DTO projection.</param>
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
        // MIGRATION: legacy TabController.GetTab(TabId, PortalId, ignoreCache) [TabController.vb:L467-502]
        // first probed the in-memory Tabs dictionary (DataCache / GetTabsByPortal) and, on a miss, hydrated
        // an IDataReader through FillTabInfo reflection. Both the cache lookup and the reflection mapping are
        // replaced by EF Core entity materialization behind the repository; the legacy ignoreCache parameter
        // collapses into a single portal-scoped id lookup.
        var tab = await _tabRepository.GetByIdAsync(tabId, portalId, cancellationToken);
        return tab is null ? null : _mapper.Map<TabDto>(tab);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TabDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy TabController.GetTabs(PortalId) [TabController.vb:L516-522] enumerated the
        // GetTabsByPortal dictionary (hydrated via FillTabInfoCollection reflection, fronted by DataCache)
        // into an ArrayList; replaced by EF Core materialization + AutoMapper projection. The repository
        // excludes soft-deleted tabs (IsDeleted == false) — legacy filtering lived in the data layer, so this
        // service does NOT re-add deleted rows.
        var tabs = await _tabRepository.GetByPortalAsync(portalId, cancellationToken);
        return _mapper.Map<IEnumerable<TabDto>>(tabs);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TabDto>> GetByParentAsync(int parentId, int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy TabController.GetTabsByParentId(ParentId, PortalId) [TabController.vb:L524-526]
        // delegated to GetTabsByParent (FillTabInfoCollection reflection, DataCache-backed) and returned an
        // ArrayList; replaced by EF Core materialization + AutoMapper projection. The repository excludes
        // soft-deleted tabs (IsDeleted == false).
        var tabs = await _tabRepository.GetByParentAsync(parentId, portalId, cancellationToken);
        return _mapper.Map<IEnumerable<TabDto>>(tabs);
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy TabController.GetTabCount(portalId) [TabController.vb:L512-514] returned
        // DataProvider.Instance().GetTabCount(portalId) (a single stored-procedure scalar); replaced by the
        // repository's EF Core count. Behavioral equivalence is preserved (count of the portal's tabs).
        return await _tabRepository.GetCountAsync(portalId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TabDto> CreateAsync(CreateTabDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // The CreateTabDto->Tab map (TabProfile) ignores the database-generated TabID, the service-managed
        // IsDeleted flag, the server-computed hierarchy metadata (Level, HasChildren, TabPath), the
        // non-create-contract fields (DisableLink, PageHeadText, AuthorizedRoles, AdministratorRoles), and the
        // TabPermissions navigation collection.
        var tab = _mapper.Map<Tab>(request);

        // MIGRATION: legacy TabController.AddTab [TabController.vb:L330-373] did far more than the single tab
        // insert: it generated the hierarchical TabPath (GenerateTabPath), looped objTab.TabPermissions to seed
        // TabPermission rows (TabPermissionController.AddTabPermission), positioned the tab within its portal via
        // UpdatePortalTabOrder / UpdateTabOrder, copied every "all tabs" module onto the new page
        // (ModuleController.GetAllTabsModules + CopyModule when AddAllTabsModules was set), and finally called
        // ClearCache(PortalID) + DataCache.RemoveCache. All of those side-effects are OMITTED here: TabPath
        // generation, TabPermission seeding, tab ordering, all-tabs module copy, and the Cache Provider are OUT
        // OF SCOPE (AAP §0.2.2); the bare persistence is delegated to the repository.
        var created = await _tabRepository.AddAsync(tab, cancellationToken);
        return _mapper.Map<TabDto>(created);
    }

    /// <inheritdoc />
    public async Task<TabDto> UpdateAsync(UpdateTabDto request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        // MIGRATION: legacy TabController.UpdateTab [TabController.vb:L780-814] re-read the existing tab via
        // GetTab and passed the supplied values straight to the data provider. The DTO+repository pattern
        // requires loading the tracked entity first; a missing tab is surfaced as KeyNotFoundException (mapped
        // to a 404 RFC 7807 response by the API middleware) instead of throwing the legacy NullReferenceException
        // when GetTab returned Nothing. UpdateTabDto carries both TabID and PortalID for the portal-scoped lookup.
        var existing = await _tabRepository.GetByIdAsync(request.TabID, request.PortalID, cancellationToken);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Tab {request.TabID} not found.");
        }

        // The UpdateTabDto->Tab map (TabProfile) overlays only the mutable tab fields, ignoring the
        // server-computed hierarchy metadata (Level, HasChildren, TabPath), the service-managed IsDeleted flag,
        // and the TabPermissions navigation collection.
        _mapper.Map(request, existing);
        await _tabRepository.UpdateAsync(existing, cancellationToken);

        // MIGRATION: the legacy UpdateTab additionally regenerated child TabPath values when the name/parent
        // changed (UpdateChildTabPath), diffed and replaced the TabPermission collection
        // (TabPermissionController.CompareTo / DeleteTabPermissionsByTabID / AddTabPermission), maintained the
        // portal tab order (UpdatePortalTabOrder), and called ClearCache(PortalID). All of those are OMITTED
        // here: TabPath regeneration, tab-permission management, portal tab-ordering, and the Cache Provider are
        // OUT OF SCOPE (AAP §0.2.2).
        return _mapper.Map<TabDto>(existing);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int tabId, int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy instance DeleteTab [TabController.vb:L446-457] fetched children via
        // GetTabsByParentId(TabId, PortalId) and only deleted when there were none (arrTabs.Count = 0).
        var children = await _tabRepository.GetByParentAsync(tabId, portalId, cancellationToken);
        if (children.Any())
        {
            // MIGRATION: legacy silently skipped the deletion when child tabs existed (the If/Count guard simply
            // fell through with no action); we surface an explicit error (RFC 7807 via middleware) so the caller
            // is not misled into believing the parent tab was removed.
            throw new InvalidOperationException("Cannot delete a tab that has child tabs.");
        }

        // MIGRATION: ITabRepository.DeleteAsync is the unconditional SOFT-delete (IsDeleted = true) — the legacy
        // DataProvider.DeleteTab plus its UpdatePortalTabOrder(PortalId, TabId, -2, ...) reorder and the
        // ClearCache(PortalId) / DataCache.RemoveCache invalidation are OMITTED (tab-ordering and the Cache
        // Provider are OUT OF SCOPE per AAP §0.2.2). The recursive recycle-bin cascade (the shared
        // DeleteTab/DeleteChildTabs path with special-tab checks and EventLog writes) is likewise OUT OF SCOPE.
        await _tabRepository.DeleteAsync(tabId, portalId, cancellationToken);
    }
}
