using System.Text.RegularExpressions;
using AutoMapper;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

// MIGRATION: Application-layer service holding the Tab (DNN content page) business rules. Replaces the
// monolithic VB.NET DotNetNuke.Entities.Tabs.TabController (Library/Components/Tabs/TabController.vb, 1302 lines).
// The legacy controller mixed business rules with data access via the reflection-instantiated
// DataProvider.Instance() + SqlHelper + stored-procedure + IDataReader/FillTabInfo pipeline; here the business
// rules live in this service while data access is delegated to the injected ITabRepository and persistence
// boundaries to IUnitOfWork (Clean/Onion, AAP §0.3.3/§0.7.3).
// MIGRATION: This service NEVER returns a raw Domain entity — every public result is a DTO wrapped in
// Result/Result<T> (AAP §0.7.7). All entity<->DTO conversion goes through the injected IMapper (Mapping/TabProfile);
// there is no hand-mapping here. No DbContext/EF Core/System.Data reference exists in this layer (AAP §0.7.3).
// MIGRATION: Multi-tenant isolation (AAP §0.7.1) — every repository query is scoped by PortalId, mirroring the
// legacy TabController, whose tab operations always took/derived a PortalId for cache-scoping and tenant safety.
/// <summary>
/// Implements <see cref="ITabService"/>: tab (page) CRUD plus the preserved DNN hierarchy rules —
/// tab-path generation on create, recursive child-tab-path recompute on rename/reparent, and the
/// delete guard that prevents removing a page that still has child pages. Child filtering and ordering
/// are performed in memory over the portal's full tab set returned by the repository.
/// </summary>
public sealed class TabService : ITabService
{
    private readonly ITabRepository _tabRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="TabService"/> class.
    /// </summary>
    /// <param name="tabRepository">Tab data-access abstraction (replaces the legacy DataProvider tab operations).</param>
    /// <param name="unitOfWork">Transactional persistence boundary (replaces the legacy DataProvider transaction surface).</param>
    /// <param name="mapper">AutoMapper instance configured with the Tab mapping profile.</param>
    // MIGRATION: Constructor injection ONLY (AAP §0.7.3) — replaces the legacy DataProvider.Instance() reflection
    // singleton lookup and the `New TabController`/`New TabPermissionController` direct instantiations.
    public TabService(
        ITabRepository tabRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(tabRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(mapper);

        _tabRepository = tabRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    // MIGRATION: TabController.GetTabs(PortalId) L516 -> GetTabsByPortal L528 returned the portal's tabs (the
    // underlying GetTabs stored procedure ordered rows by TabOrder). The result is UNPAGED: tabs form a
    // hierarchical page tree the SPA renders whole (TabResponse carries ParentId/Level/TabOrder/HasChildren/TabPath).
    // MIGRATION: legacy GetPortalTabs returned tabs ORDER BY TabOrder; recycle-bin (IsDeleted) tabs are excluded
    // from the active tree. Caching (DataCache.TabCacheKey / GetTabsByPortal L528) is omitted — it is an
    // Infrastructure concern, not a business rule.
    public async Task<Result<IEnumerable<TabResponse>>> GetByPortalAsync(
        int portalId,
        CancellationToken cancellationToken = default)
    {
        var tabs = (await _tabRepository.GetByPortalIdAsync(portalId))
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.TabOrder)
            .ToList();

        return Result<IEnumerable<TabResponse>>.Success(
            tabs.Select(t => _mapper.Map<TabResponse>(t)).ToList());
    }

    /// <inheritdoc />
    // MIGRATION: TabController.GetTab(TabId, PortalId, False) L467. The legacy method consulted the tab cache first
    // (GetTabsByPortal) and fell back to DataProvider.GetTab(TabId) + FillTabInfo; caching is an Infrastructure
    // concern and is omitted here. A missing tab becomes an expected business failure (Result.Failure), not an
    // exception — the Api maps it to a 404 ProblemDetails.
    public async Task<Result<TabResponse>> GetByIdAsync(
        int tabId,
        CancellationToken cancellationToken = default)
    {
        var tab = await _tabRepository.GetByIdAsync(tabId);
        if (tab is null)
        {
            return Result<TabResponse>.Failure($"Tab {tabId} was not found.");
        }

        return Result<TabResponse>.Success(_mapper.Map<TabResponse>(tab));
    }

    /// <inheritdoc />
    // MIGRATION: TabController.AddTab(objTab, AddAllTabsModules) L330. The in-scope portion — compute the TabPath
    // (L333) and the hierarchy Level from the parent, then persist the tab row — is transcribed here.
    public async Task<Result<TabResponse>> CreateAsync(
        CreateTabRequest request,
        CancellationToken cancellationToken = default)
    {
        // TabProfile maps the 18 client-supplied creation fields; TabId/Level/TabPath/HasChildren/IsDeleted/
        // AuthorizedRoles/AdministratorRoles and the TabPermissions navigation are Ignored (server/DB-managed).
        var tab = _mapper.Map<Tab>(request);

        // MIGRATION: AddTab L333 set TabPath = Globals.GenerateTabPath(ParentId, TabName) BEFORE insert. The legacy
        // GenerateTabPath walked the parent chain via per-parent TabController.GetTab DB round-trips; here the
        // portal's tabs are loaded once into an in-memory dictionary (multi-tenant: scoped by the new tab's PortalId).
        // (The pseudo-code's `?? 0` coerces the nullable PortalId sentinel to the repository's int parameter.)
        var portalTabs = (await _tabRepository.GetByPortalIdAsync(tab.PortalId ?? request.PortalId ?? 0)).ToList();
        var tabsById = portalTabs.ToDictionary(t => t.TabId);

        tab.TabPath = GenerateTabPath(tab.ParentId, tab.TabName, tabsById);

        // MIGRATION: AddTab derived Level from the parent — root tabs are Level 0; a child is parent.Level + 1.
        // (Legacy AddTab set Level explicitly only on the host-tab branch L356; for portal tabs Level was assigned
        // inside UpdatePortalTabOrder. The parent-derived depth preserves the resulting hierarchy faithfully.)
        tab.Level = tab.ParentId.HasValue && tabsById.TryGetValue(tab.ParentId.Value, out var parentTab)
            ? parentTab.Level + 1
            : 0;

        await _tabRepository.AddAsync(tab);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION (DEFERRED): legacy AddTab also (a) added TabPermissions via TabPermissionController.AddTabPermission
        // for each AllowAccess row (L336-L349), (b) adjusted sibling TabOrder via UpdatePortalTabOrder / the host-tab
        // AddTabToEndOfList branch (L350-L358), and (c) copied "all tabs" modules via ModuleController.CopyModule
        // (L360-L367, governed by AddAllTabsModules). Tab-permission rows, sibling re-ordering, and module copy are
        // NOT represented on the /api/tabs DTO surface and are deferred to a future phase. Recorded in MIGRATION_NOTES.md.
        return Result<TabResponse>.Success(_mapper.Map<TabResponse>(tab));
    }

    /// <inheritdoc />
    // MIGRATION: TabController.UpdateTab(objTab) L780. The legacy method (1) re-read the persisted tab to detect
    // whether TabName or ParentId changed (L782-L785: updateChildren = (TabName changed) OrElse (ParentId changed)),
    // (2) persisted the edited tab (L789), and (3) when updateChildren was True, called UpdateChildTabPath L306 to
    // recompute every descendant's TabPath. That rename/reparent cascade is a CRITICAL preserved rule (AAP §0.7.1).
    public async Task<Result<TabResponse>> UpdateAsync(
        int tabId,
        UpdateTabRequest request,
        CancellationToken cancellationToken = default)
    {
        var tab = await _tabRepository.GetByIdAsync(tabId);
        if (tab is null)
        {
            return Result<TabResponse>.Failure($"Tab {tabId} was not found.");
        }

        // MIGRATION: UpdateTab L782 re-read the existing row (objTmpTab) to compare against the incoming values.
        // Here the tracked entity still holds the OLD values, so capture them BEFORE mapping the request over it.
        var oldTabName = tab.TabName;
        var oldParentId = tab.ParentId;

        // UpdateTabRequest -> Tab applies the editable fields onto the tracked entity; TabId/PortalId and the
        // server-managed members (Level/TabPath/HasChildren/IsDeleted/AuthorizedRoles/AdministratorRoles/
        // TabPermissions) are Ignored by TabProfile, so PortalId (the tenant) is preserved from the existing entity.
        _mapper.Map(request, tab);

        // MIGRATION: route /api/tabs/{id} is the canonical id (UpdateTabRequest carries no TabId), AAP §0.7.1.
        tab.TabId = tabId;

        // MIGRATION: load the portal's tabs once (multi-tenant: scoped by the entity's preserved PortalId — the tenant
        // is immutable on update, and UpdateTabRequest carries no PortalId, so the entity value is authoritative).
        var portalTabs = (await _tabRepository.GetByPortalIdAsync(tab.PortalId ?? 0)).ToList();
        var tabsById = portalTabs.ToDictionary(t => t.TabId);

        // Ensure the just-mapped, tracked entity is the authoritative copy in the path-walk dictionary (the repository
        // may have returned a distinct instance for this id, which would still carry the pre-update values).
        tabsById[tab.TabId] = tab;

        // MIGRATION: UpdateTab L783 — updateChildren = (TabName changed) OrElse (ParentId changed). VB string `<>`
        // uses Option Compare Binary (ordinal) by default, reproduced with StringComparison.Ordinal.
        bool nameChanged = !string.Equals(oldTabName, tab.TabName, StringComparison.Ordinal);
        bool parentChanged = oldParentId != tab.ParentId;

        // MIGRATION: UpdateTab persisted TabPath alongside the row; recompute it from the (possibly new) parent chain.
        tab.TabPath = GenerateTabPath(tab.ParentId, tab.TabName, tabsById);
        await _tabRepository.UpdateAsync(tab);

        // MIGRATION: UpdateTab L809-L811 — when TabName or ParentId changed, updateChildren=True triggers
        // UpdateChildTabPath L306 to recompute every descendant's TabPath. Preserved here (the CRITICAL cascade).
        if (nameChanged || parentChanged)
        {
            await UpdateChildTabPathAsync(tab.TabId, portalTabs, tabsById);
        }

        // MIGRATION: single unit-of-work boundary — the tab edit and any descendant TabPath recomputes are persisted
        // together (the legacy provider issued a separate UpdateTab per row; SaveChangesAsync wraps them as one unit).
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION (DEFERRED): legacy UpdateTab also adjusted sibling order via UpdatePortalTabOrder (L787) and
        // reconciled TabPermissions — DeleteTabPermissionsByTabID + re-AddTabPermission when the collection differed
        // (L791-L808). Sibling re-ordering and tab-permission management are NOT on the /api/tabs DTO surface and are
        // deferred to a future phase. Recorded in MIGRATION_NOTES.md.
        return Result<TabResponse>.Success(_mapper.Map<TabResponse>(tab));
    }

    /// <inheritdoc />
    // MIGRATION: TabController.DeleteTab(TabId, PortalId) L446. The legacy method first loaded the tab's children
    // (GetTabsByParentId) and ONLY deleted when there were none (`If arrTabs.Count = 0 Then`), permanently removing
    // the tab via DataProvider.DeleteTab and then re-sequencing siblings via UpdatePortalTabOrder. The child guard is
    // a CRITICAL preserved rule (AAP §0.7.1).
    public async Task<Result> DeleteAsync(
        int tabId,
        CancellationToken cancellationToken = default)
    {
        var tab = await _tabRepository.GetByIdAsync(tabId);
        if (tab is null)
        {
            return Result.Failure($"Tab {tabId} was not found.");
        }

        // MIGRATION: DeleteTab L447-L450 — "parent tabs can not be deleted": a tab with child tabs cannot be deleted.
        // The legacy code SILENTLY no-opped when children existed (the delete branch was simply skipped); this
        // migration surfaces that condition as an explicit business failure (Result.Failure -> the Api maps it to a
        // ProblemDetails) so the SPA can show the same "page has child pages" message the legacy admin UI enforced.
        // Multi-tenant: children are filtered in memory from the tab's own portal (no GetTabsByParentId repo method).
        var portalTabs = await _tabRepository.GetByPortalIdAsync(tab.PortalId ?? 0);
        if (portalTabs.Any(t => t.ParentId == tabId))
        {
            return Result.Failure("This page cannot be deleted because it has child pages.");
        }

        // MIGRATION: DeleteTab L451 — for a tab instance this is a PERMANENT delete (DataProvider.DeleteTab removed
        // the row). The shared-tab recycle-bin soft-delete path (set Tab.IsDeleted = true instead of a hard delete)
        // is documented as the alternate behavior for shared tabs and is deferred to a future phase. The sibling
        // re-sequencing (UpdatePortalTabOrder L452) is likewise deferred (tab ordering is out of scope here).
        // Recorded in MIGRATION_NOTES.md.
        await _tabRepository.DeleteAsync(tabId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: a successful non-generic Result maps to HTTP 204 (No Content) at the controller (Gate 5 contract).
        return Result.Success();
    }

    // MIGRATION: UpdateChildTabPath L306 — recompute TabPath for every child, then recurse into grandchildren. The
    // legacy version persisted + recursed only when a child's path actually changed (`If oldTabPath <> objtab.TabPath`);
    // that short-circuit is omitted here because the recompute is idempotent and, for a rename/reparent (the only
    // trigger), every descendant's path necessarily changes — so the resulting state is identical. Children are
    // filtered in memory from the portal's tab set (no GetTabsByParentId repo method); the walk-up dictionary supplies
    // the authoritative (just-mapped) ancestor values.
    private async Task UpdateChildTabPathAsync(
        int parentTabId,
        IReadOnlyList<Tab> portalTabs,
        IReadOnlyDictionary<int, Tab> tabsById)
    {
        foreach (var child in portalTabs.Where(t => t.ParentId == parentTabId))
        {
            child.TabPath = GenerateTabPath(child.ParentId, child.TabName, tabsById);
            await _tabRepository.UpdateAsync(child);
            await UpdateChildTabPathAsync(child.TabId, portalTabs, tabsById);
        }
    }

    // MIGRATION: re-implemented from Globals.GenerateTabPath (Library/Components/Shared/Globals.vb L2385). Globals.vb
    // is excluded as a deprecated DNN API (AAP §0.6.2), but this single helper is re-implemented inline because
    // AddTab/UpdateTab/UpdateChildTabPath all depend on it. Walks UP the parent chain prepending
    // "//" + StripNonWord(parent.TabName), then appends "//" + StripNonWord(tabName). The legacy loop terminated when
    // GetTab returned Nothing (root reached: Null.IsNull(ParentId)) — reproduced here when ParentId is null or the
    // parent is not present in the portal dictionary (an orphan parent terminates the walk exactly as the legacy
    // GetTab returning Nothing did). A top-level tab (ParentId is null) yields "//" + StripNonWord(tabName).
    // The "//" separator literal is preserved verbatim (legacy VB `&` string concatenation).
    private static string GenerateTabPath(int? parentId, string? tabName, IReadOnlyDictionary<int, Tab> tabsById)
    {
        var prefix = string.Empty;
        int? current = parentId;
        while (current.HasValue && tabsById.TryGetValue(current.Value, out var parent))
        {
            prefix = "//" + StripNonWord(parent.TabName) + prefix;
            current = parent.ParentId;            // MIGRATION: Null.IsNull(ParentId) -> loop ends (current is null).
        }

        return prefix + "//" + StripNonWord(tabName);
    }

    // MIGRATION: HtmlUtils.StripNonWord(s, False) (Library/Components/Shared/HtmlUtils.vb L327) returned
    // Regex.Replace(HTML, "\W*", "") — i.e. it removed every non-word character. The "[^\w]" class is the exact
    // equivalent of "\W" and yields identical output for a replace-with-empty-string (all non-word characters
    // stripped). A null input maps to an empty string (the legacy method returned the input unchanged when Nothing;
    // here a null TabName cannot produce a meaningful path segment, so it collapses to "").
    private static string StripNonWord(string? value) => Regex.Replace(value ?? string.Empty, "[^\\w]", string.Empty);
}

