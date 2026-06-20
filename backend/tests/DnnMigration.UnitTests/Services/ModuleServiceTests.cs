using Xunit;
using Moq;
using FluentAssertions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

public class ModuleServiceTests
{
    private readonly Mock<IModuleRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<IValidator<CreateModuleDto>> _createValidator = new();
    private readonly Mock<IValidator<UpdateModuleDto>> _updateValidator = new();
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<ModuleProfile>()).CreateMapper();

    private ModuleService CreateSut() => new(_repo.Object, _mapper, _createValidator.Object, _updateValidator.Object);
    private void SetupValidCreate() =>
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
    private void SetupValidUpdate() =>
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task GetByIdAsync_maps_when_found()
    {
        _repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Module { ModuleID = 5, ModuleTitle = "M" });
        var dto = await CreateSut().GetByIdAsync(5);
        dto!.ModuleID.Should().Be(5);
        dto.ModuleTitle.Should().Be("M");
    }

    [Fact]
    public async Task GetByIdAsync_null_when_missing()
    {
        _repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Module?)null);
        (await CreateSut().GetByIdAsync(1)).Should().BeNull();
    }

    [Fact]
    public async Task GetByTabAsync_maps_all()
    {
        _repo.Setup(r => r.GetByTabAsync(8, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Module> { new() { ModuleID = 1 }, new() { ModuleID = 2 } });
        (await CreateSut().GetByTabAsync(8)).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByPortalAsync_maps_all()
    {
        _repo.Setup(r => r.GetByPortalAsync(2, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Module> { new() { ModuleID = 9 } });
        (await CreateSut().GetByPortalAsync(2)).Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_persists()
    {
        SetupValidCreate();
        _repo.Setup(r => r.AddAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Module m, CancellationToken _) => { m.ModuleID = 21; return m; });
        var dto = await CreateSut().CreateAsync(new CreateModuleDto { ModuleTitle = "T" });
        dto.ModuleID.Should().Be(21);
        _repo.Verify(r => r.AddAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_invalid_throws_and_skips_persist()
    {
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new ValidationException("bad"));
        Func<Task> act = () => CreateSut().CreateAsync(new CreateModuleDto());
        await act.Should().ThrowAsync<ValidationException>();
        _repo.Verify(r => r.AddAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_updates_existing()
    {
        SetupValidUpdate();
        _repo.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(new Module { ModuleID = 4, ModuleTitle = "Old" });
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var dto = await CreateSut().UpdateAsync(new UpdateModuleDto { ModuleID = 4, ModuleTitle = "New" });
        dto.ModuleTitle.Should().Be("New");
    }

    [Fact]
    public async Task UpdateAsync_throws_KeyNotFound_when_missing()
    {
        SetupValidUpdate();
        _repo.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync((Module?)null);
        Func<Task> act = () => CreateSut().UpdateAsync(new UpdateModuleDto { ModuleID = 6, ModuleTitle = "x" });
        await act.Should().ThrowAsync<KeyNotFoundException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_delegates_to_repository_soft_delete()
    {
        // MIGRATION: soft-delete handled by repository (sets IsDeleted=true); service is a pass-through.
        _repo.Setup(r => r.DeleteAsync(13, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await CreateSut().DeleteAsync(13);
        _repo.Verify(r => r.DeleteAsync(13, It.IsAny<CancellationToken>()), Times.Once);
    }
}
