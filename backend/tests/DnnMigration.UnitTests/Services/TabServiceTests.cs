using Xunit;
using Moq;
using FluentAssertions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

public class TabServiceTests
{
    private const int TabId = 50;
    private const int PortalId = 1;

    private readonly Mock<ITabRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<IValidator<CreateTabDto>> _createValidator = new();
    private readonly Mock<IValidator<UpdateTabDto>> _updateValidator = new();
    // MIGRATION: build a REAL AutoMapper IMapper from the actual TabProfile (projection fidelity), NOT a mock.
    // The resolved AutoMapper is 15.1.1 (security-patched deviation from the AAP §0.5.1 12.0.1 pin remediating
    // advisory GHSA-rvv3-g6hj-g44x; see DnnMigration.Application.csproj and MIGRATION_NOTES.md §7.1). From
    // AutoMapper 14+ the parameterless-logger MapperConfiguration constructor is gone, so the configuration is
    // built with the (Action<IMapperConfigurationExpression>, ILoggerFactory) overload using a no-op
    // NullLoggerFactory for unit tests — identical to the sibling *ServiceTests/*ProfileTests in this project.
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<TabProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private TabService CreateSut() => new(_repo.Object, _mapper, _createValidator.Object, _updateValidator.Object);
    private void SetupValidCreate() =>
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
    private void SetupValidUpdate() =>
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task GetByIdAsync_is_portal_scoped_and_maps_when_found()
    {
        _repo.Setup(r => r.GetByIdAsync(TabId, PortalId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Tab { TabID = TabId, PortalID = PortalId, TabName = "Home" });
        var dto = await CreateSut().GetByIdAsync(TabId, PortalId);
        dto!.TabID.Should().Be(TabId);
        dto.TabName.Should().Be("Home");
        _repo.Verify(r => r.GetByIdAsync(TabId, PortalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_null_when_missing()
    {
        _repo.Setup(r => r.GetByIdAsync(TabId, PortalId, It.IsAny<CancellationToken>())).ReturnsAsync((Tab?)null);
        (await CreateSut().GetByIdAsync(TabId, PortalId)).Should().BeNull();
    }

    [Fact]
    public async Task GetByPortalAsync_maps_all()
    {
        _repo.Setup(r => r.GetByPortalAsync(PortalId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Tab> { new() { TabID = 1 }, new() { TabID = 2 }, new() { TabID = 3 } });
        (await CreateSut().GetByPortalAsync(PortalId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByParentAsync_is_portal_scoped()
    {
        _repo.Setup(r => r.GetByParentAsync(9, PortalId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Tab> { new() { TabID = 10, ParentId = 9 } });
        (await CreateSut().GetByParentAsync(9, PortalId)).Should().ContainSingle();
    }

    [Fact]
    public async Task GetCountAsync_delegates_to_repository()
    {
        _repo.Setup(r => r.GetCountAsync(PortalId, It.IsAny<CancellationToken>())).ReturnsAsync(42);
        (await CreateSut().GetCountAsync(PortalId)).Should().Be(42);
    }

    [Fact]
    public async Task CreateAsync_persists()
    {
        SetupValidCreate();
        _repo.Setup(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Tab t, CancellationToken _) => { t.TabID = 77; return t; });
        var dto = await CreateSut().CreateAsync(new CreateTabDto { TabName = "New", PortalID = PortalId });
        dto.TabID.Should().Be(77);
        _repo.Verify(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_invalid_throws_and_skips_persist()
    {
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new ValidationException("bad"));
        Func<Task> act = () => CreateSut().CreateAsync(new CreateTabDto());
        await act.Should().ThrowAsync<ValidationException>();
        _repo.Verify(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_updates_existing()
    {
        SetupValidUpdate();
        _repo.Setup(r => r.GetByIdAsync(TabId, PortalId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Tab { TabID = TabId, PortalID = PortalId, TabName = "Old" });
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var dto = await CreateSut().UpdateAsync(new UpdateTabDto { TabID = TabId, PortalID = PortalId, TabName = "New" });
        dto.TabName.Should().Be("New");
    }

    [Fact]
    public async Task UpdateAsync_throws_KeyNotFound_when_missing()
    {
        SetupValidUpdate();
        _repo.Setup(r => r.GetByIdAsync(TabId, PortalId, It.IsAny<CancellationToken>())).ReturnsAsync((Tab?)null);
        Func<Task> act = () => CreateSut().UpdateAsync(new UpdateTabDto { TabID = TabId, PortalID = PortalId });
        await act.Should().ThrowAsync<KeyNotFoundException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_when_no_children()
    {
        // MIGRATION: soft-delete via repository when tab has no children (TabController.DeleteTab L446-457)
        _repo.Setup(r => r.GetByParentAsync(TabId, PortalId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Tab>());
        _repo.Setup(r => r.DeleteAsync(TabId, PortalId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await CreateSut().DeleteAsync(TabId, PortalId);
        _repo.Verify(r => r.DeleteAsync(TabId, PortalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_throws_when_tab_has_children()
    {
        // MIGRATION: parent-with-children guard - "parent tabs can not be deleted" (TabController.DeleteTab L446-457)
        _repo.Setup(r => r.GetByParentAsync(TabId, PortalId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Tab> { new() { TabID = 51, ParentId = TabId } });
        Func<Task> act = () => CreateSut().DeleteAsync(TabId, PortalId);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*child*");
        _repo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
