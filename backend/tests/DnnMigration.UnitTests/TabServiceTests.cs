using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Unit tests for <see cref="TabService"/>, the application service that owns tab (portal page)
/// management. The tab repository port (<see cref="ITabRepository"/>) is replaced with a Moq test
/// double so the tests stay isolated and fast, while a REAL AutoMapper <see cref="IMapper"/> built
/// from the production <see cref="MappingProfile"/> is used so the entity&#8596;DTO projections are
/// exercised exactly as they run in production.
/// </summary>
/// <remarks>
/// MIGRATION: these tests pin the behavioural parity of the migrated tab operations to the legacy
/// DotNetNuke <c>TabController.vb</c> (<c>Library/Components/Tabs/TabController.vb</c>). The single
/// highest-value parity check is the <b>parent-tab delete guard</b>: the legacy
/// <c>DeleteTab(TabId, PortalId)</c> (lines 446-453) fetched
/// <c>GetTabsByParentId(TabId)</c> and only performed the delete when the child count was zero,
/// because "parent tabs can not be deleted". That rule is preserved verbatim by the Minimal Change
/// Clause and is asserted here by
/// <see cref="DeleteAsync_returns_false_and_does_not_delete_when_the_tab_has_children"/>, which also
/// verifies that <see cref="ITabRepository.DeleteAsync(int, System.Threading.CancellationToken)"/>
/// was never invoked in that branch.
/// </remarks>
public sealed class TabServiceTests
{
    /// <summary>The mocked repository collaborator; the only test double in the suite.</summary>
    private readonly Mock<ITabRepository> _repo = new(MockBehavior.Strict);

    /// <summary>A real AutoMapper instance created from the production <see cref="MappingProfile"/>.</summary>
    private readonly IMapper _mapper;

    /// <summary>The system under test.</summary>
    private readonly TabService _sut;

    /// <summary>
    /// Builds the real mapper from <see cref="MappingProfile"/> and wires the service with the
    /// strict repository mock. A strict mock guarantees the tests assert precisely which repository
    /// members each service method touches (unexpected calls fail the test).
    /// </summary>
    public TabServiceTests()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        _mapper = configuration.CreateMapper();
        _sut = new TabService(_repo.Object, _mapper);
    }

    /// <summary>
    /// Creates a <see cref="Tab"/> populated with the handful of fields the assertions inspect;
    /// the remaining members keep their entity defaults.
    /// </summary>
    private static Tab NewTab(int id, string name = "Tab", int portalId = 1, int parentId = 0) =>
        new()
        {
            TabID = id,
            TabName = name,
            PortalID = portalId,
            ParentId = parentId,
        };

    // ---------------------------------------------------------------------
    // GetAllAsync / GetByPortalAsync
    // MIGRATION: TabController.GetAllTabs() [L459] and TabController.GetTabs(PortalId) [L516].
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_maps_every_tab_returned_by_the_repository()
    {
        var tabs = new List<Tab>
        {
            NewTab(1, "Home", portalId: 1, parentId: 0),
            NewTab(2, "About", portalId: 1, parentId: 1),
        };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tabs);

        var result = (await _sut.GetAllAsync()).ToList();

        result.Should().HaveCount(2);
        result.Should().ContainSingle(t => t.TabID == 1 && t.TabName == "Home");
        result.Should().ContainSingle(t => t.TabID == 2 && t.TabName == "About");
        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByPortalAsync_returns_the_tabs_for_the_requested_portal()
    {
        const int portalId = 1;
        var tabs = new List<Tab>
        {
            NewTab(1, "Home", portalId, parentId: 0),
            NewTab(2, "About", portalId, parentId: 0),
        };
        _repo.Setup(r => r.GetByPortalAsync(portalId, It.IsAny<CancellationToken>())).ReturnsAsync(tabs);

        var result = (await _sut.GetByPortalAsync(portalId)).ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.PortalID == portalId);
        // Verify the portal id argument was forwarded verbatim to the repository.
        _repo.Verify(r => r.GetByPortalAsync(portalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------
    // GetByIdAsync (found + not found)
    // MIGRATION: TabController.GetTab(TabId, ...) [L467]; a missing tab yields null.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_returns_the_mapped_dto_when_the_tab_exists()
    {
        var tab = NewTab(7, "Home", portalId: 1, parentId: 0);
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(tab);

        var result = await _sut.GetByIdAsync(7);

        result.Should().NotBeNull();
        result!.TabID.Should().Be(7);
        result.TabName.Should().Be("Home");
        result.PortalID.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_the_tab_is_not_found()
    {
        // The (Tab?)null cast selects the Task<Tab?> overload of ReturnsAsync so the service's
        // "tab is null -> null" branch is exercised.
        _repo.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Tab?)null);

        var result = await _sut.GetByIdAsync(999);

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // GetByParentAsync (non-empty + empty)
    // MIGRATION: TabController.GetTabsByParentId(ParentId) [L1282]; page-hierarchy traversal.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetByParentAsync_returns_the_mapped_children_of_the_parent_tab()
    {
        const int parentId = 10;
        var children = new List<Tab>
        {
            NewTab(11, "Child A", portalId: 1, parentId: parentId),
            NewTab(12, "Child B", portalId: 1, parentId: parentId),
            NewTab(13, "Child C", portalId: 1, parentId: parentId),
        };
        _repo.Setup(r => r.GetByParentAsync(parentId, It.IsAny<CancellationToken>())).ReturnsAsync(children);

        var result = (await _sut.GetByParentAsync(parentId)).ToList();

        result.Should().HaveCount(3);
        result.Should().OnlyContain(t => t.ParentId == parentId);
        _repo.Verify(r => r.GetByParentAsync(parentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByParentAsync_returns_empty_when_the_parent_has_no_children()
    {
        _repo.Setup(r => r.GetByParentAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Tab>());

        var result = await _sut.GetByParentAsync(20);

        result.Should().BeEmpty();
        _repo.Verify(r => r.GetByParentAsync(20, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------
    // CreateAsync
    // MIGRATION: TabController.AddTab(...) [L326]; only the tab record is persisted here.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_persists_the_tab_and_returns_the_assigned_identifier()
    {
        var dto = new CreateTabDto
        {
            PortalID = 1,
            TabName = "Contact",
            ParentId = 0,
            TabOrder = 5,
            IsVisible = true,
        };

        // Echo the entity back with a database-assigned identifier so the mapped DTO can be asserted.
        _repo.Setup(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab entity, CancellationToken _) =>
            {
                entity.TabID = 99;
                return entity;
            });

        var result = await _sut.CreateAsync(dto);

        result.TabID.Should().Be(99);
        result.TabName.Should().Be("Contact");
        result.PortalID.Should().Be(1);
        result.TabOrder.Should().Be(5);
        result.IsVisible.Should().BeTrue();
        // MIGRATION (finding #6): a root tab receives a generated path + depth (not empty/zero).
        result.TabPath.Should().Be("//Contact", "a root tab is pathed as '//' + CleanName(TabName)");
        result.Level.Should().Be(0, "a root tab is at depth 0");
        _repo.Verify(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // MIGRATION PARITY GUARD (finding #6): a CHILD tab inherits its parent's path + depth. The service must
    // fetch the parent and derive TabPath = parent.TabPath + "//" + CleanName(name), Level = parent.Level+1.
    // Fails if page-path generation is dropped (child would be created path-less at level 0).
    [Fact]
    public async Task CreateAsync_child_tab_inherits_parent_path_and_level()
    {
        // Parent is a first-level child of Home: "//Home//Products", depth 1.
        var parent = new Tab { TabID = 10, TabName = "Products", PortalID = 1, ParentId = 1, Level = 1, TabPath = "//Home//Products" };
        _repo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(parent);

        Tab? persisted = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab t, CancellationToken _) => { t.TabID = 42; persisted = t; return t; });

        var dto = new CreateTabDto { PortalID = 1, TabName = "Widgets", ParentId = 10, IsVisible = true };

        var result = await _sut.CreateAsync(dto);

        result.TabPath.Should().Be("//Home//Products//Widgets", "a child path is parent.TabPath + '//' + CleanName(name)");
        result.Level.Should().Be(2, "a child sits one level below its parent");
        persisted!.TabPath.Should().Be("//Home//Products//Widgets");
        persisted.Level.Should().Be(2);
        _repo.Verify(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    // MIGRATION PARITY GUARD (finding #6): CleanName strips disallowed characters (spaces, punctuation) from
    // the path segment. "My Page!" -> "MyPage!" ('!' is allowed; space removed). Documents the sanitization.
    [Fact]
    public async Task CreateAsync_root_tab_path_strips_disallowed_characters()
    {
        _repo.Setup(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab t, CancellationToken _) => { t.TabID = 7; return t; });

        // Space, '&' and '?' are stripped by CleanName; letters/digits/'!' survive.
        var dto = new CreateTabDto { PortalID = 1, TabName = "News & Events?", ParentId = 0, IsVisible = true };

        var result = await _sut.CreateAsync(dto);

        result.TabPath.Should().Be("//NewsEvents", "spaces, '&' and '?' are stripped from the path segment");
    }

    // MIGRATION PARITY GUARD (finding #6): renaming/moving a tab must recompute its OWN path AND cascade the
    // recomputed path/level to its descendants (child path updates). This test proves a grandparent->child
    // ripple: renaming the tab updates the child's path to track the new parent path. Fails if the child
    // cascade is dropped.
    [Fact]
    public async Task UpdateAsync_recomputes_path_and_cascades_to_children()
    {
        // Existing root tab (id 5) named "Old"; it has one child (id 6, "Sub") whose old path trailed "Old".
        var existing = NewTab(5, "Old", portalId: 1, parentId: 0);
        _repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var child = new Tab { TabID = 6, TabName = "Sub", PortalID = 1, ParentId = 5, Level = 1, TabPath = "//Old//Sub" };
        _repo.Setup(r => r.GetByParentAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Tab> { child });
        _repo.Setup(r => r.GetByParentAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Tab>());

        var dto = new UpdateTabDto { TabName = "Renamed", ParentId = 0, TabOrder = 1, IsVisible = true };

        var result = await _sut.UpdateAsync(5, dto);

        result!.TabPath.Should().Be("//Renamed");
        // The child's path/level must have been recomputed off the renamed parent and persisted.
        child.TabPath.Should().Be("//Renamed//Sub", "the child path must track the renamed parent path");
        child.Level.Should().Be(1);
        _repo.Verify(r => r.UpdateAsync(It.Is<Tab>(t => t.TabID == 6 && t.TabName == "Sub"), It.IsAny<CancellationToken>()), Times.Once);
    }

    // MIGRATION BOUNDARY GUARD (finding #6): the caller-supplied TabOrder VALUE is persisted faithfully
    // (parity for the value), but sibling re-sequencing (reflow) is out of scope — creating a tab performs
    // no read/mutation of sibling tabs. This pins that documented boundary (only AddAsync + the create-time
    // hierarchy derivation touch the repository; no sibling enumeration).
    [Fact]
    public async Task CreateAsync_persists_supplied_TabOrder_without_reflowing_siblings()
    {
        _repo.Setup(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab t, CancellationToken _) => { t.TabID = 1; return t; });

        var dto = new CreateTabDto { PortalID = 1, TabName = "Third", ParentId = 0, TabOrder = 7, IsVisible = true };

        var result = await _sut.CreateAsync(dto);

        result.TabOrder.Should().Be(7, "the supplied order value is persisted verbatim");
        // No sibling reflow: the service never enumerates the portal's tabs on create.
        _repo.Verify(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------
    // UpdateAsync (found + not found)
    // MIGRATION: TabController.UpdateTab(...) [L780]; in-place field copy, side-effects dropped.
    // A missing tab returns null (mapped to 404 by the controller) rather than a silent no-op.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_applies_changes_and_returns_the_updated_dto_when_the_tab_exists()
    {
        // The fetched entity carries the immutable TabID/PortalID; the DTO carries the new name.
        var existing = NewTab(5, "Old", portalId: 1, parentId: 0);
        _repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        // MIGRATION (finding #6): UpdateAsync now cascades recomputed paths to descendants, so the strict
        // mock must expect the child lookup (this root tab has no children -> no child updates).
        _repo.Setup(r => r.GetByParentAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Tab>());

        var dto = new UpdateTabDto
        {
            TabName = "New",
            ParentId = 0,
            TabOrder = 3,
            IsVisible = true,
        };

        var result = await _sut.UpdateAsync(5, dto);

        result.Should().NotBeNull();
        result!.TabID.Should().Be(5);            // identity preserved (not carried on UpdateTabDto)
        result.TabName.Should().Be("New");       // in-place map applied the DTO change
        result.TabPath.Should().Be("//New", "a renamed root tab's path is recomputed from the new name");
        result.Level.Should().Be(0, "a root tab is at depth 0");
        // The mutated entity (same id, new name) is what gets persisted.
        _repo.Verify(
            r => r.UpdateAsync(It.Is<Tab>(t => t.TabID == 5 && t.TabName == "New"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_and_never_persists_when_the_tab_is_not_found()
    {
        _repo.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((Tab?)null);

        var result = await _sut.UpdateAsync(404, new UpdateTabDto { TabName = "Nope" });

        result.Should().BeNull();
        // A missing tab short-circuits before any persistence occurs.
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------
    // DeleteAsync — ALL THREE BRANCHES (CRITICAL parent-tab delete rule)
    // MIGRATION: TabController.DeleteTab(TabId, PortalId) [L446-L453] — "parent tabs can not be
    // deleted"; the legacy delete was guarded by "If arrTabs.Count = 0". Preserved verbatim under
    // the Minimal Change Clause: not-found -> false; has children -> false (NO delete); otherwise
    // delete and return true.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_returns_false_when_the_tab_is_not_found()
    {
        _repo.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((Tab?)null);

        var result = await _sut.DeleteAsync(404);

        result.Should().BeFalse();
        // Not-found short-circuits before the child lookup AND before the delete.
        _repo.Verify(r => r.GetByParentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_returns_false_and_does_not_delete_when_the_tab_has_children()
    {
        // MIGRATION: the single most important assertion in this file — a parent tab (a tab that has
        // one or more children) can NOT be deleted, and DeleteAsync must never be invoked.
        _repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(NewTab(1, "Parent", portalId: 1, parentId: 0));
        _repo.Setup(r => r.GetByParentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab> { NewTab(2, "Child", portalId: 1, parentId: 1) });

        var result = await _sut.DeleteAsync(1);

        result.Should().BeFalse();
        _repo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_returns_true_and_deletes_when_the_tab_has_no_children()
    {
        _repo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(NewTab(3, "Leaf", portalId: 1, parentId: 0));
        _repo.Setup(r => r.GetByParentAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Tab>());
        _repo.Setup(r => r.DeleteAsync(3, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.DeleteAsync(3);

        result.Should().BeTrue();
        _repo.Verify(r => r.DeleteAsync(3, It.IsAny<CancellationToken>()), Times.Once);
    }
}
