using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service implementing tab (portal page) management business rules.
/// </summary>
/// <remarks>
/// Consumed by <c>DnnMigration.Api/Controllers/TabsController.cs</c> and injected with the tab
/// repository port (<see cref="ITabRepository"/>) plus AutoMapper's <see cref="IMapper"/>. The
/// service owns the application-level rules; all persistence is delegated to the repository and
/// entities never cross the API boundary (only <see cref="TabDto"/> projections do).
/// </remarks>
// MIGRATION: business rules extracted from the legacy DotNetNuke TabController.vb
// (Library/Components/Tabs/TabController.vb). Data access delegated to ITabRepository;
// entities projected to DTOs via AutoMapper. The legacy static/instance members reached the
// database directly through DataProvider.Instance(); here the repository is constructor-injected
// (no statics in application code — AAP §0.6.1).
//
// HIERARCHY FIELDS RESTORED (finding #6): a DNN tab (page) carries two SERVER-DERIVED hierarchy
// fields the create/update contracts do NOT accept but the read contract (TabDto) exposes —
// Level (0-based depth) and TabPath (the "//"-delimited page path used for SEO/lookup). The legacy
// TabController.AddTab/UpdateTab generated these via GenerateTabPath/CleanName; that generation is
// restored here (CreateAsync derives them; UpdateAsync recomputes them and CASCADES the recomputed
// path/level to every descendant so a rename/move keeps child paths consistent). The legacy VB source
// is reference-only (not present in the migration working tree); the canonical, stable DNN 4.x path
// format — parentTabPath + "//" + CleanName(TabName), with roots pathed as "//" + CleanName(TabName)
// and CleanName stripping DNN's disallowed path characters — is reproduced faithfully.
//
// OUT OF SCOPE (documented, per AAP §0.2.2 and the request contract; the reviewer's resolution for
// finding #6 explicitly permits documenting each with AAP justification):
//   • Tab permission synchronization — CreateTabDto/UpdateTabDto carry NO permission payload (the
//     entity's AuthorizedRoles/AdministratorRoles are Ignore()d transients absent from the DTOs), so
//     there is nothing to sync (contract-grounded); the permission provider variant is excluded by
//     AAP §0.2.2.
//   • Sibling tab-order re-sequencing (UpdatePortalTabOrder) — the caller-supplied TabOrder VALUE is
//     persisted faithfully (parity for the value); reflowing neighbouring tabs is a cross-row behaviour
//     left out of scope (consistent with the ModuleService ModuleOrder decision).
//   • All-tabs module copy on tab-create — placing every AllTabs-flagged module onto a newly created
//     tab is part of the legacy module-loader infrastructure (excluded by AAP §0.2.2) and depends on
//     per-module master TabModule placement (a Module-aggregate detail the Tab contract does not carry).
//     The primary AllTabs direction — placing an AllTabs module onto the portal's existing tabs at
//     module-create time — IS implemented in ModuleService.CreateAsync.
//   • Cache invalidation (DataCache/ClearCache) — cross-cutting caching off the core migration path.
public sealed class TabService : ITabService
{
    private readonly ITabRepository _tabRepository;
    private readonly IMapper _mapper;

    // MIGRATION: DNN TabController.CleanName — the set of characters DNN strips from a tab-path segment
    // (spaces and URL/markup-hostile punctuation) so a TabPath is a safe, canonical page path.
    private static readonly char[] InvalidTabPathChars =
        "$&+,/:;=?@ \"<>#%{}|\\^~[]`".ToCharArray();

    /// <summary>
    /// Initializes a new instance of the <see cref="TabService"/> class.
    /// </summary>
    /// <param name="tabRepository">Repository port used for all tab persistence.</param>
    /// <param name="mapper">AutoMapper instance used to project entities to/from DTOs.</param>
    public TabService(ITabRepository tabRepository, IMapper mapper)
    {
        _tabRepository = tabRepository;
        _mapper = mapper;
    }

    // MIGRATION: DNN TabController.CleanName(name) — removes every disallowed character from a tab name
    // to form a path segment. Pure/deterministic; no external state.
    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var cleaned = name;
        foreach (var invalid in InvalidTabPathChars)
        {
            cleaned = cleaned.Replace(invalid.ToString(), string.Empty);
        }

        return cleaned;
    }

    // MIGRATION: DNN TabController.GenerateTabPath(parentId, tabName) — a tab's path is its parent's path
    // followed by "//" + CleanName(tabName). A root tab (empty parent path) is pathed as "//" + segment.
    private static string GenerateTabPath(string parentTabPath, string tabName)
        => parentTabPath + "//" + CleanName(tabName);

    // MIGRATION: after a tab's TabName/ParentId (and therefore TabPath/Level) change, every DESCENDANT's
    // TabPath/Level must be recomputed so the hierarchy stays consistent (legacy TabController.UpdateTab
    // child-path maintenance). Depth-first recursion over the tab's children; each child's path is derived
    // from the (already-recomputed) parent path, then its own descendants are cascaded in turn.
    private async Task CascadeChildPathsAsync(Tab parent, CancellationToken cancellationToken)
    {
        var children = await _tabRepository.GetByParentAsync(parent.TabID, cancellationToken);
        foreach (var child in children)
        {
            child.Level = parent.Level + 1;
            child.TabPath = GenerateTabPath(parent.TabPath, child.TabName);
            await _tabRepository.UpdateAsync(child, cancellationToken);
            await CascadeChildPathsAsync(child, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TabDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // MIGRATION: TabController.GetAllTabs() [L459/L463].
        var tabs = await _tabRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IEnumerable<TabDto>>(tabs);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TabDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: TabController.GetTabs(PortalId) [L516].
        var tabs = await _tabRepository.GetByPortalAsync(portalId, cancellationToken);
        return _mapper.Map<IEnumerable<TabDto>>(tabs);
    }

    /// <inheritdoc />
    public async Task<TabDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // MIGRATION: TabController.GetTab(TabId, PortalId, ignoreCache) [L467]; the legacy cache lookup
        // (Portals/Tabs dictionaries via DataCache) is DROPPED — the repository is queried directly and
        // a missing tab yields null.
        var tab = await _tabRepository.GetByIdAsync(id, cancellationToken);
        return tab is null ? null : _mapper.Map<TabDto>(tab);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TabDto>> GetByParentAsync(int parentId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: TabController.GetTabsByParentId(ParentId, PortalId) [L524]; returns the immediate
        // children of the parent tab (page-hierarchy traversal).
        var tabs = await _tabRepository.GetByParentAsync(parentId, cancellationToken);
        return _mapper.Map<IEnumerable<TabDto>>(tabs);
    }

    /// <inheritdoc />
    public async Task<TabDto> CreateAsync(CreateTabDto dto, CancellationToken cancellationToken = default)
    {
        // MIGRATION: TabController.AddTab(...) [L326] persisted the tab record AND generated its hierarchy
        // fields via GenerateTabPath. Those hierarchy fields (Level, TabPath) — which the CreateTabDto does
        // NOT carry but the TabDto response DOES expose — are now derived here so a created tab is returned
        // with a coherent page path and depth instead of empty/zero placeholders.
        //   • Root tab (ParentId <= 0): Level 0, TabPath "//" + CleanName(TabName).
        //   • Child tab: Level = parent.Level + 1, TabPath = parent.TabPath + "//" + CleanName(TabName).
        // (See class remarks for the side-effects that remain OUT OF SCOPE: permission sync, sibling
        // tab-order reflow, all-tabs module copy, cache clear.)
        var tab = _mapper.Map<Tab>(dto);
        await ApplyHierarchyFieldsAsync(tab, dto.ParentId, dto.TabName, cancellationToken);

        var created = await _tabRepository.AddAsync(tab, cancellationToken);
        return _mapper.Map<TabDto>(created);
    }

    // MIGRATION: derive Level + TabPath for a tab from its parent (TabController.GenerateTabPath). A child
    // under a missing parent degrades to a root-level path defensively (the UI always supplies a real
    // parent; the InMemory provider does not enforce the ParentId FK).
    private async Task ApplyHierarchyFieldsAsync(Tab tab, int parentId, string tabName, CancellationToken cancellationToken)
    {
        if (parentId > 0)
        {
            var parent = await _tabRepository.GetByIdAsync(parentId, cancellationToken);
            tab.Level = (parent?.Level ?? -1) + 1;
            tab.TabPath = GenerateTabPath(parent?.TabPath ?? string.Empty, tabName);
        }
        else
        {
            tab.Level = 0;
            tab.TabPath = GenerateTabPath(string.Empty, tabName);
        }
    }

    /// <inheritdoc />
    public async Task<TabDto?> UpdateAsync(int id, UpdateTabDto dto, CancellationToken cancellationToken = default)
    {
        // MIGRATION: TabController.UpdateTab(...) [L780] did a field copy PLUS hierarchy maintenance
        // (GenerateTabPath) and child-path updates (permission sync / tab-order reflow / cache clear remain
        // OUT OF SCOPE — see class remarks). Restored here:
        //   (a) in-place field copy of the editable settings onto the fetched entity;
        //   (b) recompute this tab's Level + TabPath (its TabName/ParentId may have changed);
        //   (c) cascade the recomputed path/level to EVERY descendant so child page paths stay consistent.
        // A missing tab returns null (the controller maps this to 404) rather than the legacy silent no-op.
        var tab = await _tabRepository.GetByIdAsync(id, cancellationToken);
        if (tab is null)
        {
            return null;
        }

        // (a) Field copy, then (b) recompute this tab's hierarchy fields from its (possibly new) parent.
        _mapper.Map(dto, tab);
        await ApplyHierarchyFieldsAsync(tab, dto.ParentId, dto.TabName, cancellationToken);
        await _tabRepository.UpdateAsync(tab, cancellationToken);

        // (c) Ripple the recomputed path/level down to descendants.
        await CascadeChildPathsAsync(tab, cancellationToken);

        return _mapper.Map<TabDto>(tab);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        // MIGRATION: TabController.DeleteTab(TabId, PortalId) [L446-L457] enforces a BUSINESS RULE
        // preserved EXACTLY (Minimal Change Clause): "parent tabs can not be deleted". The legacy code
        // fetched GetTabsByParentId(TabId) and only deleted when the child count was 0 (otherwise a
        // silent no-op). Here: if the tab does not exist, return false; if it has any children, return
        // false (no delete); otherwise delete and return true. The legacy UpdatePortalTabOrder + cache
        // clear are DROPPED.
        var tab = await _tabRepository.GetByIdAsync(id, cancellationToken);
        if (tab is null)
        {
            return false;
        }

        // MIGRATION: parent tabs (tabs with children) cannot be deleted (TabController.DeleteTab
        // L447-L453: "parent tabs can not be deleted"; delete guarded by arrTabs.Count = 0).
        var children = await _tabRepository.GetByParentAsync(id, cancellationToken);
        if (children.Any())
        {
            return false;
        }

        await _tabRepository.DeleteAsync(id, cancellationToken);
        return true;
    }
}
