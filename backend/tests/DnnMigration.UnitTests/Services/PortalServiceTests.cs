using Xunit;
using Moq;
using FluentAssertions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

public class PortalServiceTests
{
    private readonly Mock<IPortalRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<IValidator<CreatePortalDto>> _createValidator = new();
    private readonly Mock<IValidator<UpdatePortalDto>> _updateValidator = new();
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<PortalProfile>()).CreateMapper();

    private PortalService CreateSut() =>
        new(_repo.Object, _mapper, _createValidator.Object, _updateValidator.Object);

    private void SetupValidCreate() =>
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new ValidationResult());
    private void SetupValidUpdate() =>
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task GetByIdAsync_returns_mapped_dto_when_found()
    {
        _repo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Portal { PortalID = 3, PortalName = "Acme" });
        var dto = await CreateSut().GetByIdAsync(3);
        dto.Should().NotBeNull();
        dto!.PortalID.Should().Be(3);
        dto.PortalName.Should().Be("Acme");
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_not_found()
    {
        _repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Portal?)null);
        (await CreateSut().GetByIdAsync(99)).Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_maps_all()
    {
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Portal> { new() { PortalID = 1 }, new() { PortalID = 2 } });
        (await CreateSut().GetAllAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByNameAsync_normalizes_minus_one_pageIndex_to_zero_and_maxpagesize()
    {
        // MIGRATION: legacy -1 sentinel => pageIndex=0, pageSize=int.MaxValue
        _repo.Setup(r => r.GetByNameAsync("ac", 0, int.MaxValue, It.IsAny<CancellationToken>()))
             .ReturnsAsync((new List<Portal> { new() { PortalID = 1 } }, 1));
        var page = await CreateSut().GetByNameAsync("ac", -1, 25);
        page.PageIndex.Should().Be(0);
        page.PageSize.Should().Be(int.MaxValue);
        page.TotalCount.Should().Be(1);
        page.Items.Should().HaveCount(1);
        _repo.Verify(r => r.GetByNameAsync("ac", 0, int.MaxValue, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByNameAsync_passes_through_normal_paging()
    {
        _repo.Setup(r => r.GetByNameAsync("x", 2, 10, It.IsAny<CancellationToken>()))
             .ReturnsAsync((new List<Portal>(), 42));
        var page = await CreateSut().GetByNameAsync("x", 2, 10);
        page.PageIndex.Should().Be(2);
        page.PageSize.Should().Be(10);
        page.TotalCount.Should().Be(42);
    }

    [Fact]
    public async Task GetByAliasAsync_returns_null_when_missing()
    {
        _repo.Setup(r => r.GetByAliasAsync("a.com", It.IsAny<CancellationToken>())).ReturnsAsync((Portal?)null);
        (await CreateSut().GetByAliasAsync("a.com")).Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_validates_maps_and_persists()
    {
        SetupValidCreate();
        _repo.Setup(r => r.AddAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Portal p, CancellationToken _) => { p.PortalID = 11; return p; });
        var dto = await CreateSut().CreateAsync(new CreatePortalDto { PortalName = "New" });
        dto.PortalID.Should().Be(11);
        dto.PortalName.Should().Be("New");
        _repo.Verify(r => r.AddAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_throws_ValidationException_and_does_not_persist()
    {
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("PortalName", "required") }));
        Func<Task> act = () => CreateSut().CreateAsync(new CreatePortalDto());
        await act.Should().ThrowAsync<ValidationException>();
        _repo.Verify(r => r.AddAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_updates_existing()
    {
        SetupValidUpdate();
        _repo.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Portal { PortalID = 4, PortalName = "Old" });
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var dto = await CreateSut().UpdateAsync(new UpdatePortalDto { PortalID = 4, PortalName = "Updated" });
        dto.PortalName.Should().Be("Updated");
        _repo.Verify(r => r.UpdateAsync(It.Is<Portal>(p => p.PortalID == 4 && p.PortalName == "Updated"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_throws_KeyNotFound_when_missing()
    {
        SetupValidUpdate();
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((Portal?)null);
        Func<Task> act = () => CreateSut().UpdateAsync(new UpdatePortalDto { PortalID = 7, PortalName = "x" });
        await act.Should().ThrowAsync<KeyNotFoundException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_hard_deletes_when_more_than_one_portal()
    {
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Portal> { new() { PortalID = 1 }, new() { PortalID = 2 } });
        // The last-portal delete guard counts portals via the count-only CountAsync; two portals -> deletion proceeds.
        _repo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _repo.Setup(r => r.DeleteAsync(2, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await CreateSut().DeleteAsync(2);
        _repo.Verify(r => r.DeleteAsync(2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_throws_when_last_portal()
    {
        // MIGRATION: legacy "LastPortal" guard -> InvalidOperationException
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Portal> { new() { PortalID = 1 } });
        // The last-portal delete guard counts portals via the count-only CountAsync; a single (last) portal -> guard throws.
        _repo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        Func<Task> act = () => CreateSut().DeleteAsync(1);
        await act.Should().ThrowAsync<InvalidOperationException>();
        _repo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
