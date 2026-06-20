using Xunit;
using Moq;
using FluentAssertions;
using AutoMapper;
using FluentValidation;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="TabService"/>.<c>GetCountAsync</c>, which reproduces the legacy DNN 4.9
/// <c>GetTabCount</c> stored procedure (Website/Providers/DataProviders/SqlDataProvider/04.04.00.SqlDataProvider).
/// The procedure body was:
/// <code>
///   DECLARE @AdminTabId int
///   SET @AdminTabId = (SELECT AdminTabId FROM {oq}Portals WHERE PortalID = @PortalID)
///   SELECT COUNT(*) - 1 FROM {oq}Tabs
///   WHERE (PortalID = @PortalID) AND (TabID &lt;&gt; @AdminTabId)
///     AND (ParentId &lt;&gt; @AdminTabId OR ParentId IS NULL)
/// </code>
/// These tests pin the three behaviours flagged by the CP4 review (MAJOR — Tab count parity, MIGRATION_NOTES.md
/// DEV-054): (1) the portal admin tab and its DIRECT children are excluded; (2) the procedure has NO IsDeleted
/// predicate so soft-deleted (recycle-bin) tabs ARE counted; (3) the trailing <c>COUNT(*) - 1</c> off-by-one is
/// preserved, including the <c>NULL @AdminTabId =&gt; -1</c> three-valued-logic case. The mapper/validators are
/// unused by the count path; the two repositories are <see cref="MockBehavior.Strict"/> so any unexpected data
/// access (e.g. an early-return path that still queries tabs) fails the test.
/// </summary>
public class TabServiceTests
{
    private readonly Mock<ITabRepository> _tabRepo = new(MockBehavior.Strict);
    private readonly Mock<IPortalRepository> _portalRepo = new(MockBehavior.Strict);
    private readonly Mock<IValidator<CreateTabDto>> _createValidator = new();
    private readonly Mock<IValidator<UpdateTabDto>> _updateValidator = new();
    private readonly IMapper _mapper = new Mock<IMapper>().Object;

    private TabService CreateSut() =>
        new(_tabRepo.Object, _portalRepo.Object, _mapper, _createValidator.Object, _updateValidator.Object);

    private void SetupPortal(int portalId, int? adminTabId) =>
        _portalRepo.Setup(r => r.GetByIdAsync(portalId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Portal { PortalID = portalId, AdminTabId = adminTabId });

    private void SetupTabs(int portalId, params Tab[] tabs) =>
        _tabRepo.Setup(r => r.GetByPortalIncludingDeletedAsync(portalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tabs.ToList());

    [Fact]
    public async Task GetCountAsync_excludes_admin_tab_and_direct_admin_children_then_subtracts_one()
    {
        // Admin tab = 10. Regular pages 1, 2, 3 should be counted; the admin tab (10) and its direct child (11)
        // are excluded. matching = {1,2,3} = 3 => COUNT(*) - 1 = 2.
        SetupPortal(1, adminTabId: 10);
        SetupTabs(1,
            new Tab { TabID = 10, PortalID = 1, ParentId = null },  // admin tab        -> excluded (TabID == AdminTabId)
            new Tab { TabID = 11, PortalID = 1, ParentId = 10 },    // admin direct child -> excluded (ParentId == AdminTabId)
            new Tab { TabID = 1, PortalID = 1, ParentId = null },   // top-level page   -> counted
            new Tab { TabID = 2, PortalID = 1, ParentId = 1 },      // child page       -> counted
            new Tab { TabID = 3, PortalID = 1, ParentId = 2 });     // grandchild page  -> counted

        var count = await CreateSut().GetCountAsync(1);

        count.Should().Be(2);
        _portalRepo.Verify(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _tabRepo.Verify(r => r.GetByPortalIncludingDeletedAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCountAsync_counts_soft_deleted_tabs_reproducing_legacy_no_isdeleted_filter()
    {
        // BEHAVIORAL-EQUIVALENCE QUIRK (DEV-054): the legacy SP scanned the raw [Tabs] table with NO IsDeleted
        // predicate, so soft-deleted (recycle-bin) tabs are counted. Here one regular page is soft-deleted.
        // matching = {1 (active), 2 (soft-deleted)} = 2 => COUNT(*) - 1 = 1.
        // (If soft-delete filtering were wrongly applied, matching would be {1} = 1 => 0, so asserting 1 — not 0 —
        // proves the recycle-bin tab is included exactly as DNN 4.9 counted it.)
        SetupPortal(1, adminTabId: 10);
        SetupTabs(1,
            new Tab { TabID = 10, PortalID = 1, ParentId = null, IsDeleted = false }, // admin -> excluded
            new Tab { TabID = 1, PortalID = 1, ParentId = null, IsDeleted = false },  // active page    -> counted
            new Tab { TabID = 2, PortalID = 1, ParentId = null, IsDeleted = true });  // recycle-bin page -> counted

        var count = await CreateSut().GetCountAsync(1);

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetCountAsync_excludes_only_direct_admin_children_not_grandchildren_and_retains_null_parent()
    {
        // Admin tab = 10. A grandchild of the admin tab (ParentId = 11, not 10) is NOT a direct child, so the SP's
        // "ParentId <> @AdminTabId" keeps it. A null-parent top-level page is retained via "ParentId IS NULL".
        // matching = {12 (admin grandchild), 1 (top-level)} = 2 => COUNT(*) - 1 = 1.
        SetupPortal(1, adminTabId: 10);
        SetupTabs(1,
            new Tab { TabID = 10, PortalID = 1, ParentId = null }, // admin tab          -> excluded
            new Tab { TabID = 11, PortalID = 1, ParentId = 10 },   // admin direct child  -> excluded
            new Tab { TabID = 12, PortalID = 1, ParentId = 11 },   // admin grandchild    -> counted
            new Tab { TabID = 1, PortalID = 1, ParentId = null });  // top-level page      -> counted

        var count = await CreateSut().GetCountAsync(1);

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetCountAsync_returns_minus_one_for_empty_portal_preserving_count_minus_one_quirk()
    {
        // No non-admin tabs at all: matching = 0 => COUNT(*) - 1 = -1 (the legacy off-by-one on an empty portal).
        SetupPortal(1, adminTabId: 10);
        SetupTabs(1,
            new Tab { TabID = 10, PortalID = 1, ParentId = null }); // only the admin tab exists -> excluded

        var count = await CreateSut().GetCountAsync(1);

        count.Should().Be(-1);
    }

    [Fact]
    public async Task GetCountAsync_returns_minus_one_when_portal_not_found_without_querying_tabs()
    {
        // SQL three-valued logic: a missing portal => @AdminTabId is NULL => "TabID <> NULL" is UNKNOWN for every
        // row => COUNT(*) = 0 => 0 - 1 = -1. The tab read is short-circuited (Strict _tabRepo would throw if hit).
        _portalRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Portal?)null);

        var count = await CreateSut().GetCountAsync(99);

        count.Should().Be(-1);
        _tabRepo.Verify(r => r.GetByPortalIncludingDeletedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCountAsync_returns_minus_one_when_admin_tab_id_column_is_null_without_querying_tabs()
    {
        // The portal exists but [Portals].AdminTabId is NULL => identical SQL three-valued-logic outcome (-1),
        // again without issuing the tab read.
        SetupPortal(1, adminTabId: null);

        var count = await CreateSut().GetCountAsync(1);

        count.Should().Be(-1);
        _tabRepo.Verify(r => r.GetByPortalIncludingDeletedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
