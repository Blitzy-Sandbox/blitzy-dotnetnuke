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
// Result/Result<T> (AAP §0.7.7). The top-level entity<->DTO projection goes through the injected IMapper
// (Mapping/TabProfile). The inbound TabPermission child collection is hand-built from TabPermissionDto
// (MapTabPermission below) because it is part of the tab-permission lifecycle RULE (the legacy add-with-AllowAccess /
// diff-on-update behavior), not a top-level projection — keeping it in the service keeps the rule readable and
// avoids a cyclic AutoMapper graph (TabResponse intentionally omits permissions). No DbContext/EF Core/System.Data
// reference exists in this layer (AAP §0.7.3).
// MIGRATION: Multi-tenant isolation (AAP §0.7.1) — every repository query is scoped by PortalId, mirroring the
// legacy TabController, whose tab operations always took/derived a PortalId for cache-scoping and tenant safety.
// CP1 review (ITabService #1 / TabService #3-#4) — the id-based read/update/delete contracts now carry portalId so
// a tab from another portal can never be read or mutated through the wrong tenant.
/// <summary>
/// Implements <see cref="ITabService"/>: tab (page) CRUD plus the preserved DNN hierarchy rules —
/// tab-path generation on create, recursive child-tab-path recompute on rename/reparent, the tab-permission
/// reconciliation (AllowAccess-filtered add/diff), the portal-wide sibling tab-order normalization, and the
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
    // MIGRATION: CP1 review (ITabService #1 / ITabRepository #1 / TabService #3) — PORTAL-SCOPED: the lookup is
    // constrained to portalId so a tab from another portal is never returned (multi-tenant isolation, AAP §0.7.1).
    // The not-found message is opaque (no raw id echoed back) per CP1 review (AuthService #7 enumeration guidance).
    public async Task<Result<TabResponse>> GetByIdAsync(
        int portalId,
        int tabId,
        CancellationToken cancellationToken = default)
    {
        var tab = await _tabRepository.GetByIdAsync(portalId, tabId);
        if (tab is null)
        {
            return Result<TabResponse>.Failure("The requested tab was not found.");
        }

        return Result<TabResponse>.Success(_mapper.Map<TabResponse>(tab));
    }

    /// <inheritdoc />
    // MIGRATION: TabController.AddTab(objTab, AddAllTabsModules) L330-372 — ported (CP1 review TabService #1/#2).
    // Legacy sequence: (1) TabPath = GenerateTabPath(ParentId, TabName) BEFORE insert (L333); (2) insert the tab row
    // (L334); (3) for each supplied TabPermission, set its TabID and — only when AllowAccess is true (L345) —
    // AddTabPermission (L336-349); (4) for a portal tab, UpdatePortalTabOrder(...,True) (L351) to normalize sibling
    // TabOrder. The host-tab branch (L352-358, PortalID is null) is out of the portal-scoped /api/tabs surface, and
    // the AddAllTabsModules CopyModule step (L360-367) belongs to the module-copy lifecycle (a Module concern) and is
    // documented as a deferred cross-aggregate behavior in MIGRATION_NOTES.md.
    public async Task<Result<TabResponse>> CreateAsync(
        CreateTabRequest request,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (TabService #1) — fail-fast null guard before mapping/reading request fields; a null
        // body is a controlled failure (Api -> 400), never a NullReferenceException deep in the map.
        ArgumentNullException.ThrowIfNull(request);

        // TabProfile maps the 18 client-supplied creation fields; TabId/Level/TabPath/HasChildren/IsDeleted/
        // AuthorizedRoles/AdministratorRoles and the TabPermissions navigation are Ignored (server/DB-managed).
        var tab = _mapper.Map<Tab>(request);

        // MIGRATION: the new tab's portal is the multi-tenant scope for the path walk, the permission rows, and the
        // sibling re-sequence below. (The `?? 0` coerces the nullable PortalId sentinel to the repository int param.)
        var portalId = tab.PortalId ?? request.PortalId ?? 0;

        // MIGRATION: AddTab L333 set TabPath = Globals.GenerateTabPath(ParentId, TabName) BEFORE insert. The legacy
        // GenerateTabPath walked the parent chain via per-parent TabController.GetTab DB round-trips; here the
        // portal's tabs are loaded once into an in-memory dictionary (multi-tenant: scoped by the new tab's PortalId).
        var portalTabs = (await _tabRepository.GetByPortalIdAsync(portalId)).ToList();
        var tabsById = portalTabs.ToDictionary(t => t.TabId);

        tab.TabPath = GenerateTabPath(tab.ParentId, tab.TabName, tabsById);

        // MIGRATION: AddTab derived Level from the parent — root tabs are Level 0; a child is parent.Level + 1.
        // (Legacy AddTab set Level explicitly only on the host-tab branch L356; for portal tabs Level was assigned
        // inside UpdatePortalTabOrder. The parent-derived depth preserves the resulting hierarchy faithfully.)
        tab.Level = tab.ParentId.HasValue && tabsById.TryGetValue(tab.ParentId.Value, out var parentTab)
            ? parentTab.Level + 1
            : 0;

        // MIGRATION: AddTab L336-349 — populate the tab's permission rows from the supplied collection, persisting
        // ONLY AllowAccess grants (L345 `If objTabPermission.AllowAccess Then AddTabPermission`). The legacy code set
        // each row's TabID after the insert returned the id; here the rows ride on the TabPermissions navigation and
        // EF Core fixes up the TabId foreign key from the generated principal key on insert. (Hand-built rather than
        // via AutoMapper because this is the permission-lifecycle RULE — see the class-header note.)
        tab.TabPermissions = request.Permissions
            .Where(p => p.AllowAccess)
            .Select(p => MapTabPermission(p, tab.TabId))
            .ToList();

        await _tabRepository.AddAsync(tab);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: AddTab L351 -> UpdatePortalTabOrder(...,True) normalized the portal's sibling TabOrder after the
        // insert. Reproduced as the portal-wide sibling re-sequence (1,3,5,...) — see ResequencePortalTabOrderAsync.
        await ResequencePortalTabOrderAsync(portalId, cancellationToken);

        return Result<TabResponse>.Success(_mapper.Map<TabResponse>(tab));
    }

    /// <inheritdoc />
    // MIGRATION: TabController.UpdateTab(objTab) L780-814 — ported (CP1 review TabService #2/#3). Legacy sequence:
    // (1) re-read the persisted tab to detect whether TabName or ParentId changed (L782-785: updateChildren =
    // (TabName changed) OrElse (ParentId changed)); (2) UpdatePortalTabOrder (L787) to normalize sibling order;
    // (3) persist the edited tab row (L789); (4) diff TabPermissions — GetTabPermissionsCollectionByTabID then, when
    // the collections differ, DeleteTabPermissionsByTabID + re-AddTabPermission for each AllowAccess grant (L791-808);
    // (5) when updateChildren, UpdateChildTabPath L306 to recompute every descendant's TabPath. The rename/reparent
    // cascade is a CRITICAL preserved rule (AAP §0.7.1). NOTE: the legacy tab-permission diff has NO
    // InheritViewPermissions / "VIEW" special case — that skip is Module-only (UpdateModule L1106-1112).
    public async Task<Result<TabResponse>> UpdateAsync(
        int portalId,
        int tabId,
        UpdateTabRequest request,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (TabService #1-style request guard) — fail-fast null guard before reading request fields.
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: CP1 review (ITabService #1 / ITabRepository #1 / TabService #3) — PORTAL-SCOPED lookup so a tab
        // from another portal can never be updated through this tenant (multi-tenant isolation, AAP §0.7.1). The
        // repository eager-loads TabPermissions so the diff below operates on the stored set. Opaque not-found (#7).
        var tab = await _tabRepository.GetByIdAsync(portalId, tabId);
        if (tab is null)
        {
            return Result<TabResponse>.Failure("The requested tab was not found.");
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

        // MIGRATION: UpdatePortalTabOrder recomputed a moved tab's Level from its (possibly new) parent
        // (intAddTabLevel = parent.Level + 1, L587/L601). Mirror the Create rule so a reparent updates the depth:
        // root tabs are Level 0; a child is parent.Level + 1. (Descendant Levels are not recomputed here — legacy
        // UpdateChildTabPath cascades only TabPath, not Level, to descendants, so this matches the legacy outcome.)
        tab.Level = tab.ParentId.HasValue && tabsById.TryGetValue(tab.ParentId.Value, out var parentTab)
            ? parentTab.Level + 1
            : 0;

        // MIGRATION: UpdateTab L791-808 — reconcile TabPermissions. Legacy compared the supplied collection against
        // the stored set (CompareTo) and, when they differed, deleted all then re-added each AllowAccess grant.
        // Clearing-and-re-adding always reaches the same persisted end state, so the CompareTo short-circuit (a pure
        // optimization) is collapsed to an unconditional rebuild. Applied onto the eager-loaded TabPermissions
        // navigation BEFORE the row write so it rides the same unit of work. (NO "VIEW"-inherit skip — Module-only.)
        ApplyTabPermissionDiff(tab, request.Permissions);

        await _tabRepository.UpdateAsync(tab);

        // MIGRATION: UpdateTab L809-L811 — when TabName or ParentId changed, updateChildren=True triggers
        // UpdateChildTabPath L306 to recompute every descendant's TabPath. Preserved here (the CRITICAL cascade).
        if (nameChanged || parentChanged)
        {
            await UpdateChildTabPathAsync(tab.TabId, portalTabs, tabsById);
        }

        // MIGRATION: single unit-of-work boundary — the tab edit, its permission rows, and any descendant TabPath
        // recomputes are persisted together (the legacy provider issued a separate write per row; SaveChangesAsync
        // wraps them as one unit).
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: UpdateTab L787 -> UpdatePortalTabOrder normalized the portal's sibling TabOrder. Reproduced as
        // the portal-wide sibling re-sequence (1,3,5,...) after the edit is persisted — see ResequencePortalTabOrderAsync.
        await ResequencePortalTabOrderAsync(portalId, cancellationToken);

        return Result<TabResponse>.Success(_mapper.Map<TabResponse>(tab));
    }

    /// <inheritdoc />
    // MIGRATION: TabController.DeleteTab(TabId, PortalId) L446-457 — ported (CP1 review TabService #2/#4). The legacy
    // method first loaded the tab's children (GetTabsByParentId, L448) and ONLY deleted when there were none
    // (`If arrTabs.Count = 0 Then`, L450), permanently removing the tab via DataProvider.DeleteTab (L451) and then
    // re-sequencing siblings via UpdatePortalTabOrder(...,-2,...) (L452). The child guard is a CRITICAL preserved rule
    // (AAP §0.7.1).
    public async Task<Result> DeleteAsync(
        int portalId,
        int tabId,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (ITabService #1 / TabService #3-#4) — PORTAL-SCOPED authorization/ownership BEFORE the
        // child-page guard and the delete. The lookup is constrained to portalId so a tab from another portal can
        // never be deleted through this tenant (multi-tenant isolation, AAP §0.7.1). Opaque not-found (#7).
        var tab = await _tabRepository.GetByIdAsync(portalId, tabId);
        if (tab is null)
        {
            return Result.Failure("The requested tab was not found.");
        }

        // MIGRATION: DeleteTab L447-L450 — "parent tabs can not be deleted": a tab with child tabs cannot be deleted.
        // The legacy code SILENTLY no-opped when children existed (the delete branch was simply skipped); this
        // migration surfaces that condition as an explicit business failure (Result.Failure -> the Api maps it to a
        // ProblemDetails) so the SPA can show the same "page has child pages" message the legacy admin UI enforced.
        // Multi-tenant: children are filtered in memory from the tab's OWN portal (portalId, GetTabsByParentId had no
        // repo equivalent).
        var portalTabs = await _tabRepository.GetByPortalIdAsync(portalId);
        if (portalTabs.Any(t => t.ParentId == tabId))
        {
            return Result.Failure("This page cannot be deleted because it has child pages.");
        }

        // MIGRATION: DeleteTab L451 — for a tab instance this is a PERMANENT delete (DataProvider.DeleteTab removed the
        // row). The shared-tab recycle-bin soft-delete path (set Tab.IsDeleted = true instead of a hard delete) is
        // documented as the alternate behavior for shared tabs and is deferred to a future phase (MIGRATION_NOTES.md).
        // PORTAL-SCOPED delete (CP1 review ITabRepository #1) constrains the row removal to the owning portal.
        await _tabRepository.DeleteAsync(portalId, tabId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: DeleteTab L452 -> UpdatePortalTabOrder(...,-2,...) re-sequenced the portal's siblings after the
        // permanent delete (the final normalization loop runs even for the deleted/NewParentId=-2 case). Reproduced
        // as the portal-wide sibling re-sequence (1,3,5,...) — see ResequencePortalTabOrderAsync.
        await ResequencePortalTabOrderAsync(portalId, cancellationToken);

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

    // MIGRATION: Builds a Domain TabPermission from the inbound TabPermissionDto. Mirrors the legacy
    // TabPermissionInfo population in TabController.AddTab (L342-346) / UpdateTab (L802-805). The permission is scoped
    // to the owning tab (TabId). Hand-mapped (not via AutoMapper) because the permission collection is a child of the
    // tab-permission lifecycle rule, not a top-level entity<->DTO projection (see the class-header note).
    private static TabPermission MapTabPermission(TabPermissionDto dto, int? tabId) => new()
    {
        TabId = tabId,
        RoleId = dto.RoleId,
        RoleName = dto.RoleName,
        AllowAccess = dto.AllowAccess,
        UserId = dto.UserId,
        Username = dto.Username,
        DisplayName = dto.DisplayName,
        PermissionId = dto.PermissionId,
        PermissionKey = dto.PermissionKey,
    };

    // MIGRATION: TabController.UpdateTab L791-808 — tab-permission diff. Legacy compared the supplied
    // TabPermissionCollection against the stored set (CompareTo) and, when they differed, deleted all
    // (DeleteTabPermissionsByTabID) then re-added each grant whose AllowAccess was true (L804). Clearing-and-re-adding
    // always reaches the same persisted end state, so the CompareTo short-circuit (a pure optimization) is collapsed
    // to an unconditional rebuild. ONE legacy filter is preserved EXACTLY: only AllowAccess grants are persisted
    // (L804). NOTE: unlike the Module permission diff there is NO InheritViewPermissions / PermissionKey = "VIEW"
    // skip — that special case is Module-only (UpdateModule L1106-1112).
    private static void ApplyTabPermissionDiff(Tab tab, List<TabPermissionDto> requested)
    {
        tab.TabPermissions.Clear();

        foreach (var dto in requested)
        {
            // MIGRATION: only AllowAccess grants are persisted (legacy L804 added the permission only inside the
            // `If objTabPermission.AllowAccess` branch).
            if (!dto.AllowAccess)
            {
                continue;
            }

            tab.TabPermissions.Add(MapTabPermission(dto, tab.TabId));
        }
    }

    // MIGRATION: TabController.UpdatePortalTabOrder L550-778 — the NET OBSERVABLE EFFECT of the legacy in-memory
    // ArrayList reorder. The legacy method rebuilt the portal's tab list, applied any reparent/move, and in its final
    // loop (L753-774, which runs even for the delete/NewParentId=-2 case) re-assigned every desktop tab's TabOrder to
    // a normalized 1, 3, 5, ... sequence (intDesktopTabOrder seeded -1, += 2), persisting a row only when its
    // TabOrder/Level/ParentId actually changed (the htabs comparison). That odd-only spacing leaves even-numbered gaps
    // so a later insert can be positioned between two tabs. Two legacy details are adapted in this simplified,
    // portal-scoped model and recorded in MIGRATION_NOTES.md:
    //   (1) the legacy TabOrder = 0 -> 999 "push-to-end" (L577-579) is preserved by ordering order-0 tabs last before
    //       the re-sequence;
    //   (2) the admin/super-tab special-casing (intAdminTabOrder seeded 9999, L755-760) is NOT expressible because the
    //       migration dropped the admin/super-tab flag from the Tab entity (and AdminTabId/SuperTabId are presentation
    //       navigation pointers); portal-scoping already excludes host tabs, so all of a portal's tabs are normalized
    //       uniformly here. The MoveTab reparent reordering is likewise out of scope — reparenting is reflected via the
    //       Level-from-parent rule and the TabPath cascade, while this method delivers the observable sibling-order
    //       normalization.
    private async Task ResequencePortalTabOrderAsync(int portalId, CancellationToken cancellationToken)
    {
        var portalTabs = (await _tabRepository.GetByPortalIdAsync(portalId))
            .Where(t => !t.IsDeleted)
            // MIGRATION: legacy GetTabsByPortal returned rows ORDER BY TabOrder; the TabOrder = 0 -> 999 push-to-end
            // (L577-579) sorts brand-new (order 0) tabs after the existing ones. TabId is a deterministic tiebreak
            // standing in for the legacy ArrayList/cache insertion order when two tabs share a TabOrder.
            .OrderBy(t => t.TabOrder == 0 ? 999 : t.TabOrder)
            .ThenBy(t => t.TabId)
            .ToList();

        var changed = false;
        var counter = 1;
        foreach (var t in portalTabs)
        {
            // MIGRATION: intDesktopTabOrder seeded -1 then += 2 before assignment (L762-763) => 1, 3, 5, ...
            var newOrder = (counter * 2) - 1;
            if (t.TabOrder != newOrder)
            {
                // MIGRATION: legacy updated a row only when its order/level/parent changed (htabs compare, L766-773).
                t.TabOrder = newOrder;
                await _tabRepository.UpdateAsync(t);
                changed = true;
            }

            counter++;
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

