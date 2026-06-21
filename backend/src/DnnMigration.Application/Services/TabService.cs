using AutoMapper;
using FluentValidation;
using DnnMigration.Application.Common;
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
/// rows into navigational reads. The sole intentional exception is <see cref="GetCountAsync"/>, which
/// reproduces the legacy <c>GetTabCount</c> procedure and therefore counts soft-deleted (recycle-bin) tabs
/// (that procedure had no <c>IsDeleted</c> predicate; see MIGRATION_NOTES.md DEV-054).
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
    private readonly IPortalRepository _portalRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateTabDto> _createValidator;
    private readonly IValidator<UpdateTabDto> _updateValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TabService"/> class.
    /// </summary>
    /// <param name="tabRepository">Repository providing data access for the Tab aggregate.</param>
    /// <param name="portalRepository">
    /// Repository for the Portal aggregate, used to resolve the portal's <c>AdminTabId</c> for the
    /// <see cref="GetCountAsync"/> parity computation (the legacy <c>GetTabCount</c> stored procedure read
    /// <c>AdminTabId</c> from <c>[Portals]</c> internally). MIGRATION: this is a cross-aggregate dependency on
    /// the Domain <see cref="IPortalRepository"/> interface (not <c>IPortalService</c>), preserving
    /// Clean-Architecture dependency direction and DI acyclicity (mirrors the UserService→IRoleRepository
    /// pattern in DEV-033).
    /// </param>
    /// <param name="mapper">AutoMapper instance used for entity&#8596;DTO projection.</param>
    /// <param name="createValidator">FluentValidation validator for <see cref="CreateTabDto"/>.</param>
    /// <param name="updateValidator">FluentValidation validator for <see cref="UpdateTabDto"/>.</param>
    public TabService(
        ITabRepository tabRepository,
        IPortalRepository portalRepository,
        IMapper mapper,
        IValidator<CreateTabDto> createValidator,
        IValidator<UpdateTabDto> updateValidator)
    {
        _tabRepository = tabRepository;
        _portalRepository = portalRepository;
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
        // MIGRATION: faithfully reproduces the legacy GetTabCount stored procedure
        // (Website/Providers/DataProviders/SqlDataProvider/04.04.00.SqlDataProvider), reached via
        // TabController.GetTabCount(portalId) -> DataProvider.GetTabCount [TabController.vb:L512-514]. The
        // procedure body was:
        //     DECLARE @AdminTabId int
        //     SET @AdminTabId = (SELECT AdminTabId FROM {oq}Portals WHERE PortalID = @PortalID)
        //     SELECT COUNT(*) - 1 FROM {oq}Tabs
        //     WHERE (PortalID = @PortalID) AND (TabID <> @AdminTabId)
        //       AND (ParentId <> @AdminTabId OR ParentId IS NULL)
        // Three legacy behaviours are reproduced exactly (see MIGRATION_NOTES.md DEV-054):
        //   1. The portal's admin tab (TabID = AdminTabId) AND its direct children (ParentId = AdminTabId)
        //      are EXCLUDED. The prior implementation omitted this entirely and therefore over-counted
        //      (this is the CP4 review MAJOR finding being fixed).
        //   2. The procedure has NO IsDeleted predicate, so soft-deleted (recycle-bin) tabs ARE counted —
        //      the count therefore reads GetByPortalIncludingDeletedAsync (raw [Tabs] scan), NOT
        //      GetByPortalAsync (which filters !IsDeleted). This deliberately diverges from the review's
        //      "apply soft-delete filtering" suggestion in favour of the AAP §0.7.1 behavioral-equivalence
        //      mandate / Minimal Change Clause: the SP at the DNN 4.9.0.85 schema version (04.04.00, with no
        //      later override) has no such filter, so adding one would change observable output whenever a
        //      portal has recycle-bin tabs.
        //   3. The trailing COUNT(*) - 1 off-by-one quirk is preserved verbatim.
        var portal = await _portalRepository.GetByIdAsync(portalId, cancellationToken);
        var adminTabId = portal?.AdminTabId;

        // SQL three-valued logic: when @AdminTabId is NULL (the portal does not exist, or its [Portals].AdminTabId
        // column is NULL), the predicate "TabID <> @AdminTabId" evaluates to UNKNOWN for every row, so the
        // procedure's COUNT(*) is 0 and it returns 0 - 1 = -1. Reproduced here without issuing the tab read.
        if (adminTabId is null)
        {
            return -1;
        }

        var adminId = adminTabId.Value;
        var tabs = await _tabRepository.GetByPortalIncludingDeletedAsync(portalId, cancellationToken);

        // In-memory predicate equivalent to the SP WHERE clause for a concrete @AdminTabId: exclude the admin
        // tab and its direct children; a NULL ParentId is retained (matches "ParentId IS NULL"). This count runs
        // LINQ-to-Objects over the materialized set, so C# null semantics apply exactly as the SP's three-valued
        // logic does for these rows.
        var matching = tabs.Count(t =>
            t.TabID != adminId
            && (t.ParentId != adminId || t.ParentId is null));

        return matching - 1;
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
            // MIGRATION (QA Finding F1-1): BusinessConflictException (not InvalidOperationException) so the middleware
            // maps ONLY genuine business conflicts to 409 and never an EF Core infrastructure failure.
            throw new BusinessConflictException("Cannot delete a tab that has child tabs.");
        }

        // MIGRATION: ITabRepository.DeleteAsync is the unconditional SOFT-delete (IsDeleted = true) — the legacy
        // DataProvider.DeleteTab plus its UpdatePortalTabOrder(PortalId, TabId, -2, ...) reorder and the
        // ClearCache(PortalId) / DataCache.RemoveCache invalidation are OMITTED (tab-ordering and the Cache
        // Provider are OUT OF SCOPE per AAP §0.2.2). The recursive recycle-bin cascade (the shared
        // DeleteTab/DeleteChildTabs path with special-tab checks and EventLog writes) is likewise OUT OF SCOPE.
        await _tabRepository.DeleteAsync(tabId, portalId, cancellationToken);
    }
}
