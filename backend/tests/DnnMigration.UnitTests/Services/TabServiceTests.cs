using Xunit;
using Moq;
using FluentAssertions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="TabService"/>, asserting behavioral equivalence with the legacy
/// <c>Library/Components/Tabs/TabController.vb</c> (DotNetNuke 4.9.0.85) per AAP §0.7.1. These tests pin the
/// migrated Tab (Page) aggregate semantics:
/// <list type="bullet">
///   <item>the portal-scoped <c>GetByIdAsync(tabId, portalId)</c> / <c>DeleteAsync(tabId, portalId)</c> signatures;</item>
///   <item>the <b>SOFT-delete</b> with the <b>parent-with-children guard</b> ("parent tabs can not be deleted")
///         ported from the instance <c>DeleteTab(TabId, PortalId)</c> [TabController.vb:L446-457] — the headline
///         behavior of this aggregate;</item>
///   <item>the validate→map→persist Create path and the validate→load→overlay→persist Update path
///         (missing tab ⇒ <see cref="KeyNotFoundException"/>), both routed through FluentValidation
///         <c>ValidateAndThrowAsync</c>; and</item>
///   <item>the <c>GetCountAsync</c> reproduction of the legacy <c>GetTabCount</c> stored procedure
///         (<c>COUNT(*) - 1</c>, excluding the portal admin tab and its direct children, counting recycle-bin
///         rows) — the CP4 MAJOR Tab-count-parity finding (MIGRATION_NOTES.md DEV-054).</item>
/// </list>
/// Collaborators are mocked with Moq (<see cref="MockBehavior.Strict"/> on both repositories, so any unexpected
/// data access fails the test); <see cref="IMapper"/> is a <b>real</b> mapper built from the production
/// <see cref="TabProfile"/> so the entity↔DTO projection under test is the real one. Tests follow Arrange-Act-Assert.
/// </summary>
public class TabServiceTests
{
    private const int TabId = 50;
    private const int PortalId = 1;

    // Strict so any data access the service performs that a test did not explicitly arrange fails loudly.
    private readonly Mock<ITabRepository> _tabRepo = new(MockBehavior.Strict);
    // Strict; only the GetCountAsync parity path consults the portal repository (for the portal's AdminTabId).
    private readonly Mock<IPortalRepository> _portalRepo = new(MockBehavior.Strict);
    private readonly Mock<IValidator<CreateTabDto>> _createValidator = new();
    private readonly Mock<IValidator<UpdateTabDto>> _updateValidator = new();

    // Real AutoMapper from the production TabProfile (NOT a mock) — exercises the genuine Tab↔DTO maps.
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<TabProfile>()).CreateMapper();

    // SUT constructor order: (ITabRepository, IPortalRepository, IMapper, IValidator<CreateTabDto>, IValidator<UpdateTabDto>).
    private TabService CreateSut() =>
        new(_tabRepo.Object, _portalRepo.Object, _mapper, _createValidator.Object, _updateValidator.Object);

    // FluentValidation collaborators are mocked on the non-generic IValidator.ValidateAsync(IValidationContext, ct)
    // overload that ValidateAndThrowAsync funnels through (matching the established sibling pattern, e.g. PortalServiceTests).
    private void SetupValidCreate() =>
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new ValidationResult());

    private void SetupValidUpdate() =>
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new ValidationResult());

    // Arranges the portal lookup (GetCountAsync reads [Portals].AdminTabId) for the count-parity tests.
    private void SetupPortal(int portalId, int? adminTabId) =>
        _portalRepo.Setup(r => r.GetByIdAsync(portalId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Portal { PortalID = portalId, AdminTabId = adminTabId });

    // Arranges the unfiltered (including soft-deleted) tab read used exclusively by GetCountAsync.
    private void SetupTabsIncludingDeleted(int portalId, params Tab[] tabs) =>
        _tabRepo.Setup(r => r.GetByPortalIncludingDeletedAsync(portalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tabs.ToList());

    // ---------------------------------------------------------------------------------------------------------
    // GetByIdAsync — portal-scoped single read (legacy TabController.GetTab(TabId, PortalId)).
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_is_portal_scoped_and_maps_when_found()
    {
        _tabRepo.Setup(r => r.GetByIdAsync(TabId, PortalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Tab { TabID = TabId, PortalID = PortalId, TabName = "Home" });

        var dto = await CreateSut().GetByIdAsync(TabId, PortalId);

        dto!.TabID.Should().Be(TabId);
        dto.TabName.Should().Be("Home");
        _tabRepo.Verify(r => r.GetByIdAsync(TabId, PortalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_null_when_missing()
    {
        _tabRepo.Setup(r => r.GetByIdAsync(TabId, PortalId, It.IsAny<CancellationToken>())).ReturnsAsync((Tab?)null);

        (await CreateSut().GetByIdAsync(TabId, PortalId)).Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------------------------
    // GetByPortalAsync / GetByParentAsync — portal-scoped list reads that map every element.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task GetByPortalAsync_maps_all()
    {
        _tabRepo.Setup(r => r.GetByPortalAsync(PortalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Tab> { new() { TabID = 1 }, new() { TabID = 2 }, new() { TabID = 3 } });

        (await CreateSut().GetByPortalAsync(PortalId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByParentAsync_is_portal_scoped()
    {
        _tabRepo.Setup(r => r.GetByParentAsync(9, PortalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Tab> { new() { TabID = 10, ParentId = 9 } });

        (await CreateSut().GetByParentAsync(9, PortalId)).Should().ContainSingle();
    }

    // ---------------------------------------------------------------------------------------------------------
    // CreateAsync — validate → map → persist (invalid input short-circuits before any persistence).
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_persists()
    {
        SetupValidCreate();
        _tabRepo.Setup(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Tab t, CancellationToken _) => { t.TabID = 77; return t; });

        var dto = await CreateSut().CreateAsync(new CreateTabDto { TabName = "New", PortalID = PortalId });

        dto.TabID.Should().Be(77);
        _tabRepo.Verify(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_invalid_throws_and_skips_persist()
    {
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new ValidationException("bad"));

        Func<Task> act = () => CreateSut().CreateAsync(new CreateTabDto());

        await act.Should().ThrowAsync<ValidationException>();
        _tabRepo.Verify(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------------------------------------
    // UpdateAsync — validate → load (portal-scoped) → overlay → persist; a missing tab surfaces KeyNotFoundException.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_updates_existing()
    {
        SetupValidUpdate();
        _tabRepo.Setup(r => r.GetByIdAsync(TabId, PortalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Tab { TabID = TabId, PortalID = PortalId, TabName = "Old" });
        _tabRepo.Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var dto = await CreateSut().UpdateAsync(new UpdateTabDto { TabID = TabId, PortalID = PortalId, TabName = "New" });

        dto.TabName.Should().Be("New");
        _tabRepo.Verify(
            r => r.UpdateAsync(It.Is<Tab>(t => t.TabID == TabId && t.TabName == "New"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_throws_KeyNotFound_when_missing()
    {
        SetupValidUpdate();
        _tabRepo.Setup(r => r.GetByIdAsync(TabId, PortalId, It.IsAny<CancellationToken>())).ReturnsAsync((Tab?)null);

        Func<Task> act = () => CreateSut().UpdateAsync(new UpdateTabDto { TabID = TabId, PortalID = PortalId });

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _tabRepo.Verify(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------------------------------------
    // DeleteAsync — the SOFT-delete + parent-with-children guard (the headline migrated behavior).
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_soft_deletes_when_no_children()
    {
        // MIGRATION: soft-delete via repository when tab has no children (TabController.DeleteTab L446-457).
        // The legacy instance DeleteTab fetched children via GetTabsByParentId(TabId, PortalId) and only
        // deleted when none remained; the repository performs the actual soft-delete (IsDeleted = true).
        _tabRepo.Setup(r => r.GetByParentAsync(TabId, PortalId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Tab>());
        _tabRepo.Setup(r => r.DeleteAsync(TabId, PortalId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().DeleteAsync(TabId, PortalId);

        _tabRepo.Verify(r => r.DeleteAsync(TabId, PortalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_throws_when_tab_has_children()
    {
        // MIGRATION: parent-with-children guard - "parent tabs can not be deleted" (TabController.DeleteTab L446-457).
        // Legacy silently skipped the deletion when child tabs existed; the migrated service surfaces an explicit
        // InvalidOperationException (mapped to RFC 7807 by the API middleware) and never reaches the repository delete.
        _tabRepo.Setup(r => r.GetByParentAsync(TabId, PortalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Tab> { new() { TabID = 51, ParentId = TabId } });

        Func<Task> act = () => CreateSut().DeleteAsync(TabId, PortalId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*child*");
        _tabRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------------------------------------
    // GetCountAsync — reproduces the legacy GetTabCount stored procedure
    // (Website/Providers/DataProviders/SqlDataProvider/04.04.00.SqlDataProvider), reached via
    // TabController.GetTabCount(portalId) [TabController.vb:L512-514]. The procedure body was:
    //     DECLARE @AdminTabId int
    //     SET @AdminTabId = (SELECT AdminTabId FROM {oq}Portals WHERE PortalID = @PortalID)
    //     SELECT COUNT(*) - 1 FROM {oq}Tabs
    //     WHERE (PortalID = @PortalID) AND (TabID <> @AdminTabId)
    //       AND (ParentId <> @AdminTabId OR ParentId IS NULL)
    // MIGRATION (DEV-054): (1) the portal admin tab and its DIRECT children are excluded; (2) there is NO
    // IsDeleted predicate so soft-deleted (recycle-bin) tabs ARE counted; (3) the trailing COUNT(*) - 1
    // off-by-one is preserved, including the NULL @AdminTabId ⇒ -1 SQL three-valued-logic case.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task GetCountAsync_excludes_admin_tab_and_direct_admin_children_then_subtracts_one()
    {
        // Admin tab = 10. Regular pages 1, 2, 3 are counted; the admin tab (10) and its direct child (11) are
        // excluded. matching = {1,2,3} = 3 ⇒ COUNT(*) - 1 = 2.
        SetupPortal(PortalId, adminTabId: 10);
        SetupTabsIncludingDeleted(PortalId,
            new Tab { TabID = 10, PortalID = PortalId, ParentId = null },  // admin tab           -> excluded (TabID == AdminTabId)
            new Tab { TabID = 11, PortalID = PortalId, ParentId = 10 },    // admin direct child  -> excluded (ParentId == AdminTabId)
            new Tab { TabID = 1, PortalID = PortalId, ParentId = null },   // top-level page      -> counted
            new Tab { TabID = 2, PortalID = PortalId, ParentId = 1 },      // child page          -> counted
            new Tab { TabID = 3, PortalID = PortalId, ParentId = 2 });     // grandchild page     -> counted

        var count = await CreateSut().GetCountAsync(PortalId);

        count.Should().Be(2);
        _portalRepo.Verify(r => r.GetByIdAsync(PortalId, It.IsAny<CancellationToken>()), Times.Once);
        _tabRepo.Verify(r => r.GetByPortalIncludingDeletedAsync(PortalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCountAsync_counts_soft_deleted_tabs_reproducing_legacy_no_isdeleted_filter()
    {
        // BEHAVIORAL-EQUIVALENCE QUIRK (DEV-054): the legacy SP scanned the raw [Tabs] table with NO IsDeleted
        // predicate, so soft-deleted (recycle-bin) tabs are counted. One regular page is soft-deleted here.
        // matching = {1 (active), 2 (soft-deleted)} = 2 ⇒ COUNT(*) - 1 = 1. (If soft-delete filtering were
        // wrongly applied, matching would be {1} = 1 ⇒ 0, so asserting 1 — not 0 — proves the recycle-bin tab
        // is included exactly as DNN 4.9 counted it.)
        SetupPortal(PortalId, adminTabId: 10);
        SetupTabsIncludingDeleted(PortalId,
            new Tab { TabID = 10, PortalID = PortalId, ParentId = null, IsDeleted = false }, // admin            -> excluded
            new Tab { TabID = 1, PortalID = PortalId, ParentId = null, IsDeleted = false },  // active page      -> counted
            new Tab { TabID = 2, PortalID = PortalId, ParentId = null, IsDeleted = true });  // recycle-bin page -> counted

        var count = await CreateSut().GetCountAsync(PortalId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetCountAsync_excludes_only_direct_admin_children_not_grandchildren_and_retains_null_parent()
    {
        // Admin tab = 10. A grandchild of the admin tab (ParentId = 11, not 10) is NOT a direct child, so the SP's
        // "ParentId <> @AdminTabId" keeps it. A null-parent top-level page is retained via "ParentId IS NULL".
        // matching = {12 (admin grandchild), 1 (top-level)} = 2 ⇒ COUNT(*) - 1 = 1.
        SetupPortal(PortalId, adminTabId: 10);
        SetupTabsIncludingDeleted(PortalId,
            new Tab { TabID = 10, PortalID = PortalId, ParentId = null }, // admin tab           -> excluded
            new Tab { TabID = 11, PortalID = PortalId, ParentId = 10 },   // admin direct child  -> excluded
            new Tab { TabID = 12, PortalID = PortalId, ParentId = 11 },   // admin grandchild    -> counted
            new Tab { TabID = 1, PortalID = PortalId, ParentId = null });  // top-level page      -> counted

        var count = await CreateSut().GetCountAsync(PortalId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetCountAsync_returns_minus_one_for_empty_portal_preserving_count_minus_one_quirk()
    {
        // No non-admin tabs at all: matching = 0 ⇒ COUNT(*) - 1 = -1 (the legacy off-by-one on an empty portal).
        SetupPortal(PortalId, adminTabId: 10);
        SetupTabsIncludingDeleted(PortalId,
            new Tab { TabID = 10, PortalID = PortalId, ParentId = null }); // only the admin tab exists -> excluded

        var count = await CreateSut().GetCountAsync(PortalId);

        count.Should().Be(-1);
    }

    [Fact]
    public async Task GetCountAsync_returns_minus_one_when_portal_not_found_without_querying_tabs()
    {
        // SQL three-valued logic: a missing portal ⇒ @AdminTabId is NULL ⇒ "TabID <> NULL" is UNKNOWN for every
        // row ⇒ COUNT(*) = 0 ⇒ 0 - 1 = -1. The tab read is short-circuited (Strict _tabRepo would throw if hit).
        _portalRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Portal?)null);

        var count = await CreateSut().GetCountAsync(99);

        count.Should().Be(-1);
        _tabRepo.Verify(r => r.GetByPortalIncludingDeletedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCountAsync_returns_minus_one_when_admin_tab_id_column_is_null_without_querying_tabs()
    {
        // The portal exists but [Portals].AdminTabId is NULL ⇒ identical SQL three-valued-logic outcome (-1),
        // again without issuing the tab read.
        SetupPortal(PortalId, adminTabId: null);

        var count = await CreateSut().GetCountAsync(PortalId);

        count.Should().Be(-1);
        _tabRepo.Verify(r => r.GetByPortalIncludingDeletedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
