using AutoMapper;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using TabEntity = DnnMigration.Domain.Entities.Tab;

namespace DnnMigration.UnitTests.Services;

// MIGRATION: parity tests for TabService (derived from TabController.vb). TabPath/Level + recurse-on-change + child guard.
public sealed class TabServiceTests
{
    private readonly Mock<ITabRepository> _tabRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly TabService _sut;

    public TabServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        // Two-arg in-place map copies the request fields that drive path recomputation.
        _mapper.Setup(m => m.Map(It.IsAny<UpdateTabRequest>(), It.IsAny<TabEntity>()))
               .Returns((UpdateTabRequest r, TabEntity e) => { e.TabName = r.TabName; e.ParentId = r.ParentId; return e; });
        _sut = new TabService(_tabRepo.Object, _uow.Object, _mapper.Object);
    }

    private void MapTabResponseByIdentity() =>
        _mapper.Setup(m => m.Map<TabResponse>(It.IsAny<TabEntity>()))
               .Returns((TabEntity t) => new TabResponse { TabId = t.TabId, TabOrder = t.TabOrder, TabName = t.TabName, TabPath = t.TabPath, Level = t.Level });

    // ---------- GetByPortalAsync (unpaged, excludes deleted, ordered) ----------
    [Fact]
    public async Task GetByPortalAsync_ExcludesDeleted_OrdersByTabOrder_Unpaged()
    {
        var tabs = new List<TabEntity>
        {
            new() { TabId = 1, PortalId = 1, TabName = "B", TabOrder = 2, IsDeleted = false },
            new() { TabId = 2, PortalId = 1, TabName = "A", TabOrder = 1, IsDeleted = false },
            new() { TabId = 3, PortalId = 1, TabName = "Gone", TabOrder = 0, IsDeleted = true },
        };
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(tabs);
        MapTabResponseByIdentity();

        var result = await _sut.GetByPortalAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(t => t.TabId).Should().Equal(2, 1); // ordered by TabOrder; deleted excluded
    }

    // ---------- GetByIdAsync ----------
    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsMappedResponse()
    {
        var tab = new TabEntity { TabId = 2, PortalId = 1, TabName = "Home" };
        var dto = new TabResponse { TabId = 2 };
        // MIGRATION: CP1 review (ITabRepository #1) - portal-scoped lookup (portalId, tabId).
        _tabRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(tab);
        _mapper.Setup(m => m.Map<TabResponse>(tab)).Returns(dto);

        var result = await _sut.GetByIdAsync(1, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsFailureWithExactMessage()
    {
        _tabRepo.Setup(r => r.GetByIdAsync(1, 6)).ReturnsAsync((TabEntity?)null);

        var result = await _sut.GetByIdAsync(1, 6);

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested tab was not found.");
    }

    // ---------- CreateAsync (TabPath + Level) ----------
    [Fact]
    public async Task CreateAsync_RootTab_GeneratesRootPath_AndLevelZero()
    {
        var entity = new TabEntity { PortalId = 1, TabName = "Home", ParentId = null };
        var request = new CreateTabRequest { PortalId = 1, TabName = "Home" };
        _mapper.Setup(m => m.Map<TabEntity>(request)).Returns(entity);
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<TabEntity>());
        MapTabResponseByIdentity();

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.TabPath.Should().Be("//Home");
        entity.Level.Should().Be(0);
        _tabRepo.Verify(r => r.AddAsync(entity), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_StripsNonWordCharactersFromPath()
    {
        var entity = new TabEntity { PortalId = 1, TabName = "My Page!", ParentId = null };
        var request = new CreateTabRequest { PortalId = 1, TabName = "My Page!" };
        _mapper.Setup(m => m.Map<TabEntity>(request)).Returns(entity);
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<TabEntity>());
        MapTabResponseByIdentity();

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.TabPath.Should().Be("//MyPage");
    }

    [Fact]
    public async Task CreateAsync_ChildTab_GeneratesChildPath_AndLevelOne()
    {
        var entity = new TabEntity { PortalId = 1, TabName = "Child", ParentId = 10 };
        var request = new CreateTabRequest { PortalId = 1, TabName = "Child", ParentId = 10 };
        _mapper.Setup(m => m.Map<TabEntity>(request)).Returns(entity);
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<TabEntity>
        {
            new() { TabId = 10, PortalId = 1, TabName = "Home", ParentId = null, Level = 0 },
        });
        MapTabResponseByIdentity();

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.TabPath.Should().Be("//Home//Child");
        entity.Level.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_GrandchildTab_GeneratesFullPath_AndLevelTwo()
    {
        var entity = new TabEntity { PortalId = 1, TabName = "Leaf", ParentId = 20 };
        var request = new CreateTabRequest { PortalId = 1, TabName = "Leaf", ParentId = 20 };
        _mapper.Setup(m => m.Map<TabEntity>(request)).Returns(entity);
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<TabEntity>
        {
            new() { TabId = 10, PortalId = 1, TabName = "Home", ParentId = null, Level = 0 },
            new() { TabId = 20, PortalId = 1, TabName = "Sub", ParentId = 10, Level = 1 },
        });
        MapTabResponseByIdentity();

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.TabPath.Should().Be("//Home//Sub//Leaf");
        entity.Level.Should().Be(2);
    }

    // ---------- UpdateAsync (recompute path; recurse children only on name/parent change) ----------
    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsFailure_AndDoesNotSave()
    {
        _tabRepo.Setup(r => r.GetByIdAsync(1, 6)).ReturnsAsync((TabEntity?)null);

        var result = await _sut.UpdateAsync(1, 6, new UpdateTabRequest { TabName = "X" });

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested tab was not found.");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_RecomputesPath_AndRecursesChildren_WhenNameChanged()
    {
        // MIGRATION: CP1 review (TabService) - TabOrder pre-normalized (1,3) so the portal-wide sibling
        // re-sequence (ResequencePortalTabOrderAsync) is a no-op and this test isolates the rename/recurse rule.
        var tab = new TabEntity { TabId = 10, PortalId = 1, TabName = "Home", ParentId = null, TabOrder = 1 };
        var child = new TabEntity { TabId = 11, PortalId = 1, TabName = "Child", ParentId = 10, TabOrder = 3 };
        var request = new UpdateTabRequest { TabName = "HomeRenamed", ParentId = null };
        _tabRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(tab);
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<TabEntity> { tab, child });
        MapTabResponseByIdentity();

        var result = await _sut.UpdateAsync(1, 10, request);

        result.IsSuccess.Should().BeTrue();
        tab.TabPath.Should().Be("//HomeRenamed");
        child.TabPath.Should().Be("//HomeRenamed//Child");
        _tabRepo.Verify(r => r.UpdateAsync(It.Is<TabEntity>(t => t.TabId == 10)), Times.Once);
        _tabRepo.Verify(r => r.UpdateAsync(It.Is<TabEntity>(t => t.TabId == 11)), Times.Once);
        _tabRepo.Verify(r => r.UpdateAsync(It.IsAny<TabEntity>()), Times.Exactly(2));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotRecurseChildren_WhenNameAndParentUnchanged()
    {
        // MIGRATION: CP1 review (TabService) - TabOrder pre-normalized (1,3) so the sibling re-sequence is a no-op.
        var tab = new TabEntity { TabId = 10, PortalId = 1, TabName = "Home", ParentId = null, TabOrder = 1 };
        var child = new TabEntity { TabId = 11, PortalId = 1, TabName = "Child", ParentId = 10, TabPath = "//Home//Child", TabOrder = 3 };
        var request = new UpdateTabRequest { TabName = "Home", ParentId = null, Title = "New Title" };
        _tabRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(tab);
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<TabEntity> { tab, child });
        MapTabResponseByIdentity();

        var result = await _sut.UpdateAsync(1, 10, request);

        result.IsSuccess.Should().BeTrue();
        _tabRepo.Verify(r => r.UpdateAsync(It.Is<TabEntity>(t => t.TabId == 10)), Times.Once);
        _tabRepo.Verify(r => r.UpdateAsync(It.Is<TabEntity>(t => t.TabId == 11)), Times.Never);
        child.TabPath.Should().Be("//Home//Child"); // untouched
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_RecomputesPath_WhenParentChanged()
    {
        var tab = new TabEntity { TabId = 10, PortalId = 1, TabName = "Page", ParentId = null };
        var request = new UpdateTabRequest { TabName = "Page", ParentId = 5 };
        _tabRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(tab);
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<TabEntity>
        {
            tab,
            new() { TabId = 5, PortalId = 1, TabName = "NewParent", ParentId = null, Level = 0 },
        });
        MapTabResponseByIdentity();

        var result = await _sut.UpdateAsync(1, 10, request);

        result.IsSuccess.Should().BeTrue();
        tab.TabPath.Should().Be("//NewParent//Page");
        tab.ParentId.Should().Be(5);
    }

    [Fact]
    public async Task UpdateAsync_RouteIdIsAuthoritative()
    {
        var tab = new TabEntity { TabId = 999, PortalId = 1, TabName = "Page", ParentId = null };
        var request = new UpdateTabRequest { TabName = "Page", ParentId = null };
        _tabRepo.Setup(r => r.GetByIdAsync(1, 6)).ReturnsAsync(tab);
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<TabEntity> { tab });
        MapTabResponseByIdentity();

        var result = await _sut.UpdateAsync(1, 6, request);

        result.IsSuccess.Should().BeTrue();
        tab.TabId.Should().Be(6);
    }

    // ---------- DeleteAsync (child guard) ----------
    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsFailure_AndDoesNothing()
    {
        _tabRepo.Setup(r => r.GetByIdAsync(1, 6)).ReturnsAsync((TabEntity?)null);

        var result = await _sut.DeleteAsync(1, 6);

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested tab was not found.");
        _tabRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_BlocksWhenTabHasChildren()
    {
        var tab = new TabEntity { TabId = 10, PortalId = 1, TabName = "Parent" };
        _tabRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(tab);
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<TabEntity>
        {
            tab,
            new() { TabId = 11, PortalId = 1, TabName = "Child", ParentId = 10 },
        });

        var result = await _sut.DeleteAsync(1, 10);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("This page cannot be deleted because it has child pages.");
        _tabRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_DeletesWhenNoChildren()
    {
        // MIGRATION: CP1 review (TabService) - TabOrder pre-normalized (1,3) so the post-delete sibling re-sequence
        // is a no-op and SaveChanges is invoked exactly once (the delete itself).
        var tab = new TabEntity { TabId = 10, PortalId = 1, TabName = "Leaf", TabOrder = 1 };
        _tabRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(tab);
        _tabRepo.Setup(r => r.GetByPortalIdAsync(1)).ReturnsAsync(new List<TabEntity>
        {
            tab,
            new() { TabId = 11, PortalId = 1, TabName = "Other", ParentId = 99, TabOrder = 3 },
        });

        var result = await _sut.DeleteAsync(1, 10);

        result.IsSuccess.Should().BeTrue();
        _tabRepo.Verify(r => r.DeleteAsync(1, 10), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
