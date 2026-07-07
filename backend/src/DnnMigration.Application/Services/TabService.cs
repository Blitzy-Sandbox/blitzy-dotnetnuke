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
public sealed class TabService : ITabService
{
    private readonly ITabRepository _tabRepository;
    private readonly IMapper _mapper;

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
        // MIGRATION: TabController.AddTab(...) [L326] also generated the tab path (GenerateTabPath),
        // synced permissions (TabPermissionController), set tab order (UpdatePortalTabOrder), copied the
        // all-tabs modules and cleared cache. Those are DROPPED here (service injected only with
        // ITabRepository + IMapper); only the tab record is persisted.
        var tab = _mapper.Map<Tab>(dto);
        var created = await _tabRepository.AddAsync(tab, cancellationToken);
        return _mapper.Map<TabDto>(created);
    }

    /// <inheritdoc />
    public async Task<TabDto?> UpdateAsync(int id, UpdateTabDto dto, CancellationToken cancellationToken = default)
    {
        // MIGRATION: TabController.UpdateTab(...) [L780] did a field copy plus permission sync, tab-order
        // and child-path updates, and a cache clear. Only the field copy is preserved (in-place map onto
        // the fetched entity); the side-effects are DROPPED. A missing tab returns null (the controller
        // maps this to 404) rather than the legacy silent no-op.
        var tab = await _tabRepository.GetByIdAsync(id, cancellationToken);
        if (tab is null)
        {
            return null;
        }

        _mapper.Map(dto, tab);
        await _tabRepository.UpdateAsync(tab, cancellationToken);
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
