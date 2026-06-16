using System.Text.RegularExpressions;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core 8 LINQ implementation of <see cref="ITabRepository"/>.
/// A DNN "Tab" is a site Page. Replaces the legacy <c>TabController.vb</c> data methods and
/// <c>SqlDataProvider.vb</c> Tab stored-procedure calls. Reads mirror the legacy <c>vw_Tabs</c> view by
/// filtering out soft-deleted rows.
/// </summary>
public class TabRepository : ITabRepository
{
    private readonly DnnDbContext _context;

    public TabRepository(DnnDbContext context)
    {
        _context = context;
    }

    public async Task<Tab?> GetByIdAsync(int tabId, int portalId, CancellationToken cancellationToken = default)
    {
        // Portal-scoped single-entity lookup; intentionally unfiltered by IsDeleted so a tab can still be
        // resolved by its identifier within the portal.
        return await _context.Tabs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TabID == tabId && t.PortalID == portalId, cancellationToken);
    }

    public async Task<IEnumerable<Tab>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // PERFORMANCE: returns the portal's full (non-deleted) page tree (legacy GetTabs/GetTabsByPortal). A
        // portal's page set is a bounded administrative hierarchy, so this is intentionally not paged, matching
        // the legacy all-rows contract. Ordered by TabOrder for tree rendering. See MIGRATION_NOTES.md §4.6
        // (bounded list reads).
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => t.PortalID == portalId && !t.IsDeleted)
            .OrderBy(t => t.TabOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Tab>> GetByParentAsync(int parentId, int portalId, CancellationToken cancellationToken = default)
    {
        // PERFORMANCE: returns the (non-deleted) direct child pages of one parent tab (legacy
        // GetTabsByParentId/GetTabsByParent). The children of a single page are a bounded set, so this is
        // intentionally not paged, matching the legacy all-rows contract. See MIGRATION_NOTES.md §4.6
        // (bounded list reads).
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => t.ParentId == parentId && t.PortalID == portalId && !t.IsDeleted)
            .OrderBy(t => t.TabOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: reproduces the legacy GetTabCount stored procedure VERBATIM
        // [DotNetNuke.Schema.SqlDataProvider / 04.04.00.SqlDataProvider]:
        //   DECLARE @AdminTabId int = (SELECT AdminTabId FROM Portals WHERE PortalID = @PortalID)
        //   SELECT COUNT(*) - 1 FROM Tabs
        //   WHERE PortalID = @PortalID
        //     AND TabID <> @AdminTabId                          -- exclude the admin tab itself
        //     AND (ParentId <> @AdminTabId OR ParentId IS NULL) -- exclude the admin tab's DIRECT children
        // The "- 1" offset is preserved verbatim for behavioral parity (it is the legacy contract that
        // PortalController/admin screens compare against). TWO behaviours are reconciled and preserved here:
        //   1. NO IsDeleted filter — GetTabCount deliberately counts soft-deleted rows too, UNLIKE the list
        //      reads above. Adding !t.IsDeleted would diverge from the legacy count, so it is intentionally
        //      omitted (the prior straight !IsDeleted count, with no admin-tab exclusion, was the CP3 finding).
        //   2. NULL @AdminTabId — AdminTabId is a nullable Portals column. In T-SQL, `TabID <> @AdminTabId`
        //      is UNKNOWN for every row when @AdminTabId IS NULL, so the proc matches no rows (COUNT = 0) and
        //      returns -1. That branch is reproduced explicitly below rather than relying on C#/EF
        //      null-comparison semantics (under the InMemory provider `TabID != null` would be TRUE for all
        //      rows, which would NOT match the proc). A missing portal likewise yields a null AdminTabId.
        // See MIGRATION_NOTES.md §4.2 (Tabs) / D-033.
        int? adminTabId = await _context.Portals
            .AsNoTracking()
            .Where(p => p.PortalID == portalId)
            .Select(p => p.AdminTabId)
            .FirstOrDefaultAsync(cancellationToken);

        int matching;
        if (adminTabId.HasValue)
        {
            int admin = adminTabId.Value;
            matching = await _context.Tabs
                .AsNoTracking()
                .CountAsync(
                    t => t.PortalID == portalId
                      && t.TabID != admin
                      && (t.ParentId != admin || t.ParentId == null),
                    cancellationToken);
        }
        else
        {
            // @AdminTabId IS NULL (portal missing, or AdminTabId not set): the legacy `TabID <> @AdminTabId`
            // predicate is UNKNOWN for every row, so SQL Server matches no rows (COUNT = 0).
            matching = 0;
        }

        // Legacy COUNT(*) - 1 (preserved verbatim, including the resulting -1 when no rows match).
        return matching - 1;
    }

    public async Task<Tab> AddAsync(Tab tab, CancellationToken cancellationToken = default)
    {
        // MIGRATION: ports the legacy TabController.AddTab hierarchy/order behavior
        // [Library/Components/Tabs/TabController.vb:L330-373]. Legacy AddTab inserted the row, generated the
        // hierarchical TabPath (Globals.GenerateTabPath), set Level, and called UpdatePortalTabOrder/UpdateTabOrder
        // to renumber TabOrder across the portal. Those persistence concerns are reproduced here. (Tab-permission
        // seeding, the "all-tabs" ModuleController.CopyModule copy, and the Cache Provider ClearCache remain OUT OF
        // SCOPE per AAP 0.2.2.) The INSERT must run first so the identity (TabID) exists before the reorder pass;
        // both writes run inside one transaction on relational providers. The EF InMemory provider (Gate 5
        // integration tests) does not support transactions, so the ambient transaction is opened only for a
        // relational provider (mirrors PortalRepository.DeleteAsync).

        // MIGRATION: legacy "If .TabOrder = 0 Then .TabOrder = 999" bump (TabController.AddTab) places a
        // new/unordered tab last among its siblings until the renumber pass assigns its final order.
        if (tab.TabOrder == 0)
        {
            tab.TabOrder = 999;
        }

        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            await _context.Tabs.AddAsync(tab, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Renumber TabOrder and (re)generate TabPath/Level across the whole portal. The freshly inserted tab
            // is already change-tracked, so the recompute query resolves to the SAME instance (EF identity
            // resolution) and its mutations are persisted by the second SaveChanges.
            await RecomputeHierarchyAndOrderAsync(tab.PortalID, attachedTarget: null, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return tab;
    }

    public async Task UpdateAsync(Tab tab, CancellationToken cancellationToken = default)
    {
        // MIGRATION: ports the legacy TabController.UpdateTab hierarchy/order behavior
        // [Library/Components/Tabs/TabController.vb:L780-814]. Legacy UpdateTab persisted the row, and when the
        // TabName or ParentId changed it regenerated the child TabPath values (UpdateChildTabPath) and renumbered
        // the portal tab order (UpdatePortalTabOrder). Both are reproduced here by a single full-portal recompute
        // pass that regenerates EVERY tab's TabPath/Level/TabOrder, which inherently re-paths children after a
        // parent rename/move. (Tab-permission diff/replace and the Cache Provider ClearCache remain OUT OF SCOPE
        // per AAP 0.2.2.) The whole operation runs inside one transaction on relational providers; the EF InMemory
        // provider does not support transactions (mirrors PortalRepository.DeleteAsync).

        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // Attach the caller's already-mapped instance as Modified. It carries the NEW ParentId/TabName.
            _context.Tabs.Update(tab);

            // Recompute across the portal. The target is excluded from the tracking query (it is already tracked
            // here, so re-materializing it would raise an identity-conflict) and merged into the working set, so
            // the recompute observes its new parent/name and re-paths + renumbers the whole portal in one save.
            await RecomputeHierarchyAndOrderAsync(tab.PortalID, attachedTarget: tab, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task DeleteAsync(int tabId, int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Tab delete is a SOFT delete (IsDeleted = true), matching the Tabs table's [IsDeleted]
        // bit column. The parent-with-children delete guard is a service-layer rule and is NOT enforced here.
        var tab = await _context.Tabs
            .FirstOrDefaultAsync(t => t.TabID == tabId && t.PortalID == portalId, cancellationToken);

        if (tab is null)
        {
            return;
        }

        tab.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// MIGRATION: ports the data-loading half of the legacy <c>TabController.UpdatePortalTabOrder</c>
    /// [Library/Components/Tabs/TabController.vb]. Loads the portal's <c>AdminTabId</c>/<c>SuperTabId</c> (which
    /// gate the legacy admin/super ordering branch) and the portal's non-deleted tabs as TRACKED entities, then
    /// renumbers <c>TabOrder</c> and regenerates <c>TabPath</c>/<c>Level</c> in place. The caller persists the
    /// mutations with a single <c>SaveChanges</c>.
    /// </summary>
    /// <param name="portalId">Portal whose tab tree is recomputed.</param>
    /// <param name="attachedTarget">
    /// The create/update target that is already change-tracked by the caller (via <c>AddAsync</c> or
    /// <c>Update</c>). When supplied, it is excluded from the tracking query (to avoid an EF identity-tracking
    /// conflict) and merged into the working set so the recompute observes its new <c>ParentId</c>/<c>TabName</c>.
    /// Pass <c>null</c> when the target is already returned by the query (the freshly inserted row in
    /// <c>AddAsync</c>, resolved via EF identity resolution).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task RecomputeHierarchyAndOrderAsync(int portalId, Tab? attachedTarget, CancellationToken cancellationToken)
    {
        // Load the portal's admin/super tab ids. AdminTabId is a nullable Portals column; when it is null
        // (Null.NullInteger in the legacy schema) the legacy `AdminTabId <> -1` guard is false, so every tab uses
        // the desktop ordering branch. A missing portal likewise yields a null AdminTabId.
        var portal = await _context.Portals
            .AsNoTracking()
            .Where(p => p.PortalID == portalId)
            .Select(p => new { p.AdminTabId, p.SuperTabId })
            .FirstOrDefaultAsync(cancellationToken);

        int? adminTabId = portal?.AdminTabId;
        int superTabId = portal?.SuperTabId ?? 0;

        // Load all non-deleted tabs in the portal as TRACKED entities (NOT AsNoTracking) so the recomputed
        // TabPath/Level/TabOrder values are persisted by the caller's SaveChanges.
        var query = _context.Tabs.Where(t => t.PortalID == portalId && !t.IsDeleted);
        if (attachedTarget is not null)
        {
            int targetId = attachedTarget.TabID;
            query = query.Where(t => t.TabID != targetId);
        }

        var tabs = await query.ToListAsync(cancellationToken);

        // Merge the already-tracked target (UpdateAsync) into the working set so its NEW ParentId/TabName drive
        // the recompute. A soft-deleted target is intentionally not re-added.
        if (attachedTarget is not null && !attachedTarget.IsDeleted)
        {
            tabs.Add(attachedTarget);
        }

        RecomputeHierarchyAndOrder(tabs, adminTabId, superTabId);
    }

    /// <summary>
    /// MIGRATION: ports the in-memory renumber/re-path half of the legacy
    /// <c>TabController.UpdatePortalTabOrder</c>/<c>UpdateTabOrder</c> [Library/Components/Tabs/TabController.vb].
    /// Performs a pre-order depth-first traversal of the portal's tab tree, assigning each tab:
    /// <list type="bullet">
    ///   <item><description><c>Level</c> = depth (root tabs are level 0).</description></item>
    ///   <item><description><c>TabPath</c> = parent path + <c>"//"</c> + <c>StripNonWord(TabName)</c>
    ///   (Globals.GenerateTabPath); regenerating every path inherently re-paths children after a parent
    ///   rename/move (the legacy UpdateChildTabPath).</description></item>
    ///   <item><description><c>TabOrder</c> from two independent legacy counters: the desktop counter seeds at
    ///   <c>-1</c> and increments by 2 (1, 3, 5, …); the admin/super counter seeds at <c>9999</c> and increments
    ///   by 2 (10001, 10003, …) for the admin tab, the super tab, and their DIRECT children — but ONLY when the
    ///   portal has an AdminTabId set (the legacy <c>AdminTabId &lt;&gt; -1</c> guard).</description></item>
    /// </list>
    /// Siblings are visited in legacy order: by effective TabOrder (a 0/unset order sorts last as 999, matching
    /// the AddTab bump) then by TabID as a stable tiebreak.
    /// </summary>
    private static void RecomputeHierarchyAndOrder(List<Tab> tabs, int? adminTabId, int superTabId)
    {
        if (tabs.Count == 0)
        {
            return;
        }

        // Index tabs by id and bucket children under their parent. A tab whose ParentId is null, self-referential,
        // or points at a parent that is absent/soft-deleted is treated as a root (orphan-safe).
        var byId = new Dictionary<int, Tab>(tabs.Count);
        foreach (var t in tabs)
        {
            byId[t.TabID] = t;
        }

        var childrenByParent = new Dictionary<int, List<Tab>>();
        var roots = new List<Tab>();
        foreach (var t in tabs)
        {
            if (t.ParentId.HasValue && t.ParentId.Value != t.TabID && byId.ContainsKey(t.ParentId.Value))
            {
                if (!childrenByParent.TryGetValue(t.ParentId.Value, out var siblings))
                {
                    siblings = new List<Tab>();
                    childrenByParent[t.ParentId.Value] = siblings;
                }

                siblings.Add(t);
            }
            else
            {
                roots.Add(t);
            }
        }

        // Legacy sibling ordering: effective TabOrder (0/unset => 999, last) then TabID for stability.
        static int EffectiveOrder(Tab t) => t.TabOrder == 0 ? 999 : t.TabOrder;
        static int CompareSiblings(Tab a, Tab b)
        {
            int byOrder = EffectiveOrder(a).CompareTo(EffectiveOrder(b));
            return byOrder != 0 ? byOrder : a.TabID.CompareTo(b.TabID);
        }

        roots.Sort(CompareSiblings);
        foreach (var siblings in childrenByParent.Values)
        {
            siblings.Sort(CompareSiblings);
        }

        // MIGRATION: legacy seeds (TabController.UpdatePortalTabOrder): intDesktopTabOrder = -1 (=> 1,3,5,…) and
        // intAdminTabOrder = 9999 (=> 10001,10003,…). Each is incremented by 2 immediately before assignment.
        int desktopOrder = -1;
        int adminOrder = 9999;

        // Defensive cycle guard against malformed legacy parent chains (prevents infinite recursion).
        var visited = new HashSet<int>();

        void Visit(Tab tab, int level, string parentPath)
        {
            if (!visited.Add(tab.TabID))
            {
                return;
            }

            tab.Level = level;
            tab.TabPath = parentPath + "//" + StripNonWord(tab.TabName);

            // MIGRATION: legacy predicate
            //   (TabID = AdminTabId Or ParentId = AdminTabId Or TabID = SuperTabId Or ParentId = SuperTabId)
            //   And AdminTabId <> -1
            // The whole branch is gated on the portal having an AdminTabId (HasValue); when it does not, every tab
            // takes the desktop branch. SuperTabId defaults to 0 when the portal is absent, which matches no real
            // (1-based) TabID/ParentId.
            bool isAdminOrSuper =
                adminTabId.HasValue
                && (tab.TabID == adminTabId.Value
                    || tab.ParentId == adminTabId.Value
                    || tab.TabID == superTabId
                    || tab.ParentId == superTabId);

            if (isAdminOrSuper)
            {
                adminOrder += 2;
                tab.TabOrder = adminOrder;
            }
            else
            {
                desktopOrder += 2;
                tab.TabOrder = desktopOrder;
            }

            if (childrenByParent.TryGetValue(tab.TabID, out var children))
            {
                foreach (var child in children)
                {
                    Visit(child, level + 1, tab.TabPath);
                }
            }
        }

        foreach (var root in roots)
        {
            Visit(root, 0, string.Empty);
        }
    }

    /// <summary>
    /// MIGRATION: ports <c>HtmlUtils.StripNonWord(HTML, RetainSpace:=False)</c>
    /// [Library/Components/Shared/HtmlUtils.vb]: <c>Regex.Replace(value, "\W*", "")</c> strips every non-word
    /// character from the tab name to build a URL-safe TabPath segment. A null/empty name yields an empty segment
    /// (legacy returned Nothing, which VB string concatenation coerced to "").
    /// </summary>
    private static string StripNonWord(string? name)
    {
        return Regex.Replace(name ?? string.Empty, @"\W*", string.Empty);
    }
}
