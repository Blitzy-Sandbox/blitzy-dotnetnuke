using Xunit;
using Moq;
using FluentAssertions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// xUnit unit tests for <see cref="ModuleService"/>, asserting behavioral equivalence with the legacy
/// <c>Library/Components/Modules/ModuleController.vb</c> business surface: read-scope projection
/// (<c>GetByIdAsync</c>/<c>GetByTabAsync</c>/<c>GetByPortalAsync</c>), validate-then-persist on create,
/// validate-then-load-then-update on update (with a <see cref="KeyNotFoundException"/> guard when the
/// target module is missing), and the SOFT-delete pass-through to the repository.
/// <para>
/// Collaborators are isolated with Moq: <see cref="IModuleRepository"/> is a strict mock (every invoked
/// member is explicitly set up, so an unexpected call fails the test), and the two FluentValidation
/// validators are loose mocks. The <see cref="IMapper"/>, by contrast, is a REAL mapper built from a
/// <see cref="MapperConfiguration"/> that registers the production <see cref="ModuleProfile"/>, so the
/// entity&lt;-&gt;DTO projection the service relies on is exercised end to end rather than stubbed.
/// </para>
/// This fixture contributes to Validation Gate 2 (<c>dotnet test</c>) and must build clean under Gate 1
/// (<c>--warnaserror</c>, CS8618 exempt).
/// </summary>
public class ModuleServiceTests
{
    // Strict so any repository call the service makes that is NOT explicitly arranged below fails the test,
    // pinning the exact data-access surface each operation touches (parity with ModuleController.vb).
    private readonly Mock<IModuleRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<IValidator<CreateModuleDto>> _createValidator = new();
    private readonly Mock<IValidator<UpdateModuleDto>> _updateValidator = new();

    // MIGRATION: the AAP §0.5.1 AutoMapper pin (12.0.1) was upgraded to the patched 15.x line to remediate
    // advisory GHSA-rvv3-g6hj-g44x (see DnnMigration.Application.csproj and MIGRATION_NOTES.md §7.1). From
    // AutoMapper 13+ the parameterless-logger MapperConfiguration constructor is gone, so the configuration is
    // built with the (Action<IMapperConfigurationExpression>, ILoggerFactory) overload and a no-op
    // NullLoggerFactory for unit tests — matching the sibling ModuleProfileTests fixture.
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<ModuleProfile>(), NullLoggerFactory.Instance).CreateMapper();

    /// <summary>Builds the system under test with the mocked collaborators and the real mapper.</summary>
    private ModuleService CreateSut() => new(_repo.Object, _mapper, _createValidator.Object, _updateValidator.Object);

    // ModuleService.CreateAsync calls IValidator.ValidateAndThrowAsync, which internally delegates to the
    // ValidateAsync(IValidationContext, CancellationToken) interface overload — so that is the member arranged.
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
