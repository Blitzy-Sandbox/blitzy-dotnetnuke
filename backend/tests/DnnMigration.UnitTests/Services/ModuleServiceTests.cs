using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using ModuleEntity = DnnMigration.Domain.Entities.Module;

namespace DnnMigration.UnitTests.Services;

// MIGRATION: parity tests for ModuleService (derived from ModuleController.vb). Soft-delete preserved.
public sealed class ModuleServiceTests
{
    private readonly Mock<IModuleRepository> _moduleRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMapper> _mapper = new();
    // MIGRATION: CP1 review (ModuleService) - portal settings dependency (default-module + display propagation rules).
    private readonly Mock<IPortalSettingsService> _portalSettings = new();
    private readonly ModuleService _sut;

    public ModuleServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _sut = new ModuleService(_moduleRepo.Object, _uow.Object, _mapper.Object, _portalSettings.Object);
    }

    private static ModuleEntity NewModule(int id, int? tabId = 1, bool deleted = false) =>
        new() { ModuleId = id, PortalId = 1, TabId = tabId, IsDeleted = deleted, ModuleTitle = $"Module {id}" };

    private void MapResponseByIdentity() =>
        _mapper.Setup(m => m.Map<ModuleResponse>(It.IsAny<ModuleEntity>()))
               .Returns((ModuleEntity e) => new ModuleResponse { ModuleId = e.ModuleId, TabId = e.TabId, IsDeleted = e.IsDeleted });

    // ---------- GetByPortalAsync (paged, excludes deleted) ----------
    // MIGRATION: CP1 review (IModuleRepository #paging) - the !IsDeleted filter and the page window now live in the
    // repository's server-side GetByPortalPagedAsync; the service surfaces that page as-is. The mock returns the
    // already-filtered/paged result the repository contract guarantees.
    [Fact]
    public async Task GetByPortalAsync_ExcludesDeletedModules()
    {
        var page = new List<ModuleEntity> { NewModule(1), NewModule(2), NewModule(4) };
        _moduleRepo.Setup(r => r.GetByPortalPagedAsync(1, 0, 10))
                   .ReturnsAsync(((IEnumerable<ModuleEntity>)page, 3));
        MapResponseByIdentity();

        var result = await _sut.GetByPortalAsync(1, 0, 10);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(3);
        result.Value.Items.Should().HaveCount(3);
        result.Value.Items.Select(i => i.ModuleId).Should().NotContain(3);
    }

    [Fact]
    public async Task GetByPortalAsync_AppliesZeroBasedPaging()
    {
        var page = new List<ModuleEntity> { NewModule(3), NewModule(4) };
        _moduleRepo.Setup(r => r.GetByPortalPagedAsync(1, 1, 2))
                   .ReturnsAsync(((IEnumerable<ModuleEntity>)page, 5));
        MapResponseByIdentity();

        var result = await _sut.GetByPortalAsync(1, 1, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(5);
        result.Value.Items.Select(i => i.ModuleId).Should().Equal(3, 4);
    }

    // ---------- GetByTabAsync (unpaged, excludes deleted) ----------
    [Fact]
    public async Task GetByTabAsync_ReturnsUnpagedResults_ExcludingDeleted()
    {
        var modules = new List<ModuleEntity> { NewModule(1), NewModule(2, deleted: true), NewModule(3) };
        // MIGRATION: CP1 review (IModuleRepository #1) - portal-scoped (portalId, tabId).
        _moduleRepo.Setup(r => r.GetByTabIdAsync(1, 5)).ReturnsAsync(modules);
        MapResponseByIdentity();

        var result = await _sut.GetByTabAsync(1, 5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(i => i.ModuleId).Should().Equal(1, 3);
    }

    // ---------- GetByIdAsync ----------
    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsMappedResponse()
    {
        var module = NewModule(2);
        var dto = new ModuleResponse { ModuleId = 2 };
        _moduleRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(module);
        _mapper.Setup(m => m.Map<ModuleResponse>(module)).Returns(dto);

        var result = await _sut.GetByIdAsync(1, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsFailureWithExactMessage()
    {
        _moduleRepo.Setup(r => r.GetByIdAsync(1, 9)).ReturnsAsync((ModuleEntity?)null);

        var result = await _sut.GetByIdAsync(1, 9);

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested module was not found.");
    }

    // ---------- CreateAsync ----------
    [Fact]
    public async Task CreateAsync_SetsIsDeletedFalse_AndSavesOnce()
    {
        var entity = new ModuleEntity { ModuleId = 1, PortalId = 1, TabId = 1, IsDeleted = true };
        var request = new CreateModuleRequest();
        _mapper.Setup(m => m.Map<ModuleEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<ModuleResponse>(entity)).Returns(new ModuleResponse { ModuleId = 1 });

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.IsDeleted.Should().BeFalse();
        _moduleRepo.Verify(r => r.AddAsync(entity), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ReturnsMappedResponse()
    {
        var entity = new ModuleEntity { ModuleId = 1, PortalId = 1, TabId = 1 };
        var dto = new ModuleResponse { ModuleId = 1 };
        var request = new CreateModuleRequest();
        _mapper.Setup(m => m.Map<ModuleEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<ModuleResponse>(entity)).Returns(dto);

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    // ---------- UpdateAsync ----------
    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsFailure_AndDoesNotSave()
    {
        _moduleRepo.Setup(r => r.GetByIdAsync(1, 9)).ReturnsAsync((ModuleEntity?)null);

        var result = await _sut.UpdateAsync(1, 9, new UpdateModuleRequest());

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested module was not found.");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_MapsAndSavesOnce()
    {
        var entity = NewModule(2);
        var request = new UpdateModuleRequest();
        _moduleRepo.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(entity);
        _mapper.Setup(m => m.Map(request, entity)).Returns(entity);
        _mapper.Setup(m => m.Map<ModuleResponse>(entity)).Returns(new ModuleResponse { ModuleId = 2 });

        var result = await _sut.UpdateAsync(1, 2, request);

        result.IsSuccess.Should().BeTrue();
        _moduleRepo.Verify(r => r.UpdateAsync(entity), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- DeleteAsync (SOFT delete) ----------
    [Fact]
    public async Task DeleteAsync_PerformsSoftDelete_SettingIsDeletedTrueAndTabIdNull()
    {
        var module = new ModuleEntity { ModuleId = 9, PortalId = 1, TabId = 5, IsDeleted = false };
        _moduleRepo.Setup(r => r.GetByIdAsync(1, 9)).ReturnsAsync(module);

        var result = await _sut.DeleteAsync(1, 9);

        result.IsSuccess.Should().BeTrue();
        module.IsDeleted.Should().BeTrue();
        module.TabId.Should().BeNull();
        _moduleRepo.Verify(r => r.UpdateAsync(module), Times.Once);
        _moduleRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never); // NOT a hard delete
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsFailure_AndDoesNothing()
    {
        _moduleRepo.Setup(r => r.GetByIdAsync(1, 9)).ReturnsAsync((ModuleEntity?)null);

        var result = await _sut.DeleteAsync(1, 9);

        result.IsFailure.Should().BeTrue();
        // MIGRATION: CP1 review - opaque not-found message (no raw id echoed back).
        result.Errors.Should().Contain("The requested module was not found.");
        _moduleRepo.Verify(r => r.UpdateAsync(It.IsAny<ModuleEntity>()), Times.Never);
        _moduleRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
