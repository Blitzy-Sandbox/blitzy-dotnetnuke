using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using PortalEntity = DnnMigration.Domain.Entities.Portal;
using UserEntity = DnnMigration.Domain.Entities.User;

namespace DnnMigration.UnitTests.Services;

// MIGRATION: parity tests for PortalService (derived from PortalController.vb + PortalSettings.vb).
public sealed class PortalServiceTests
{
    private readonly Mock<IPortalRepository> _portalRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMapper> _mapper = new();
    // MIGRATION: CP1 review (PortalService) - the optional portal-administrator bootstrap hashes (BCrypt) and persists
    // the admin credential; these collaborators are required by the constructor.
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ICredentialStore> _credentialStore = new();
    private readonly PortalService _sut;

    public PortalServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _sut = new PortalService(
            _portalRepo.Object,
            _userRepo.Object,
            _uow.Object,
            _mapper.Object,
            _passwordHasher.Object,
            _credentialStore.Object);
    }

    private static PortalEntity NewPortal(int id, string currency = "USD", string homeDir = "Portals/1") =>
        new() { PortalId = id, PortalName = $"Portal {id}", Currency = currency, HomeDirectory = homeDir };

    private void MapPortalListItemByIdentity() =>
        _mapper.Setup(m => m.Map<PortalListItemDto>(It.IsAny<PortalEntity>()))
               .Returns((PortalEntity p) => new PortalListItemDto { PortalId = p.PortalId, PortalName = p.PortalName });

    // ---------- GetAllAsync ----------
    // MIGRATION: CP1 review (IPortalRepository #paging) - paging is performed server-side by GetPagedAsync; the
    // service surfaces the repository's page as-is. The mock returns the already-paged result the repository yields.
    [Fact]
    public async Task GetAllAsync_ReturnsPagedMappedItems()
    {
        var page = new List<PortalEntity> { NewPortal(1), NewPortal(2), NewPortal(3) };
        _portalRepo.Setup(r => r.GetPagedAsync(0, 10)).ReturnsAsync(((IEnumerable<PortalEntity>)page, 3));
        MapPortalListItemByIdentity();

        var result = await _sut.GetAllAsync(0, 10);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(3);
        result.Value.Items.Should().HaveCount(3);
        result.Value.PageIndex.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_AppliesZeroBasedPaging()
    {
        // pageIndex=1, pageSize=2 => the repository returns the second page (ids 3,4) and the full total (5).
        var page = new List<PortalEntity> { NewPortal(3), NewPortal(4) };
        _portalRepo.Setup(r => r.GetPagedAsync(1, 2)).ReturnsAsync(((IEnumerable<PortalEntity>)page, 5));
        MapPortalListItemByIdentity();

        var result = await _sut.GetAllAsync(1, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(5);
        result.Value.PageIndex.Should().Be(1);
        result.Value.Items.Select(i => i.PortalId).Should().Equal(3, 4);
    }

    // ---------- GetByIdAsync ----------
    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsMappedDto()
    {
        var portal = NewPortal(1);
        var dto = new PortalDto { PortalId = 1 };
        _portalRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(portal);
        _mapper.Setup(m => m.Map<PortalDto>(portal)).Returns(dto);

        var result = await _sut.GetByIdAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsFailureWithExactMessage()
    {
        _portalRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync((PortalEntity?)null);

        var result = await _sut.GetByIdAsync(5);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Portal 5 was not found.");
    }

    // ---------- CreateAsync ----------
    [Fact]
    public async Task CreateAsync_DefaultsCurrencyToUsd_WhenEmpty()
    {
        var entity = new PortalEntity { PortalId = 1, Currency = string.Empty, HomeDirectory = "Portals/1" };
        var request = new CreatePortalRequest();
        _mapper.Setup(m => m.Map<PortalEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<PortalDto>(entity)).Returns(new PortalDto { PortalId = 1 });

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task CreateAsync_KeepsProvidedCurrency()
    {
        var entity = new PortalEntity { PortalId = 1, Currency = "GBP", HomeDirectory = "Portals/1" };
        var request = new CreatePortalRequest();
        _mapper.Setup(m => m.Map<PortalEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<PortalDto>(entity)).Returns(new PortalDto { PortalId = 1 });

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task CreateAsync_DefaultsHomeDirectory_AndSavesTwice_WhenHomeDirEmpty()
    {
        var entity = new PortalEntity { PortalId = 42, Currency = "USD", HomeDirectory = string.Empty };
        var request = new CreatePortalRequest();
        _mapper.Setup(m => m.Map<PortalEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<PortalDto>(entity)).Returns(new PortalDto { PortalId = 42 });

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        entity.HomeDirectory.Should().Be("Portals/42");
        _portalRepo.Verify(r => r.AddAsync(entity), Times.Once);
        _portalRepo.Verify(r => r.UpdateAsync(entity), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAsync_SavesOnce_WhenHomeDirectoryProvided()
    {
        var entity = new PortalEntity { PortalId = 7, Currency = "USD", HomeDirectory = "Portals/7" };
        var request = new CreatePortalRequest();
        _mapper.Setup(m => m.Map<PortalEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<PortalDto>(entity)).Returns(new PortalDto { PortalId = 7 });

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        _portalRepo.Verify(r => r.AddAsync(entity), Times.Once);
        _portalRepo.Verify(r => r.UpdateAsync(It.IsAny<PortalEntity>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_AddsAndReturnsDto()
    {
        var entity = new PortalEntity { PortalId = 3, Currency = "USD", HomeDirectory = "Portals/3" };
        var dto = new PortalDto { PortalId = 3 };
        var request = new CreatePortalRequest();
        _mapper.Setup(m => m.Map<PortalEntity>(request)).Returns(entity);
        _mapper.Setup(m => m.Map<PortalDto>(entity)).Returns(dto);

        var result = await _sut.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
        _portalRepo.Verify(r => r.AddAsync(entity), Times.Once);
    }

    // ---------- UpdateAsync ----------
    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsFailure_AndDoesNotSave()
    {
        _portalRepo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync((PortalEntity?)null);

        var result = await _sut.UpdateAsync(7, new UpdatePortalRequest());

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Portal 7 was not found.");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_RouteIdIsAuthoritative_OverridingDtoId()
    {
        var entity = new PortalEntity { PortalId = 999, Currency = "USD", HomeDirectory = "Portals/999" };
        var request = new UpdatePortalRequest();
        _portalRepo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(entity);
        _mapper.Setup(m => m.Map(request, entity)).Returns(entity);
        _mapper.Setup(m => m.Map<PortalDto>(entity)).Returns(new PortalDto { PortalId = 7 });

        var result = await _sut.UpdateAsync(7, request);

        result.IsSuccess.Should().BeTrue();
        entity.PortalId.Should().Be(7);
        _portalRepo.Verify(r => r.UpdateAsync(entity), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsMappedDto()
    {
        var entity = new PortalEntity { PortalId = 7, Currency = "USD", HomeDirectory = "Portals/7" };
        var dto = new PortalDto { PortalId = 7 };
        var request = new UpdatePortalRequest();
        _portalRepo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(entity);
        _mapper.Setup(m => m.Map(request, entity)).Returns(entity);
        _mapper.Setup(m => m.Map<PortalDto>(entity)).Returns(dto);

        var result = await _sut.UpdateAsync(7, request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    // ---------- DeleteAsync ----------
    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsFailure_AndDeletesNothing()
    {
        _portalRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync((PortalEntity?)null);

        var result = await _sut.DeleteAsync(3);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Portal 3 was not found.");
        // MIGRATION: CP1 review (IUserRepository #1) - user delete is portal-scoped (portalId, userId).
        _userRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _portalRepo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_DeletesAllPortalUsersFirst_ThenPortal_WithSingleSave()
    {
        var portal = NewPortal(3);
        var users = new List<UserEntity>
        {
            new() { UserId = 10, PortalId = 3 },
            new() { UserId = 11, PortalId = 3 },
            new() { UserId = 12, PortalId = 3 },
        };
        _portalRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(portal);
        _userRepo.Setup(r => r.GetByPortalIdAsync(3)).ReturnsAsync(users);

        var result = await _sut.DeleteAsync(3);

        result.IsSuccess.Should().BeTrue();
        // MIGRATION: CP1 review (IUserRepository #1) - cascade delete is portal-scoped (portalId, userId).
        _userRepo.Verify(r => r.DeleteAsync(3, 10), Times.Once);
        _userRepo.Verify(r => r.DeleteAsync(3, 11), Times.Once);
        _userRepo.Verify(r => r.DeleteAsync(3, 12), Times.Once);
        _userRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(3));
        _portalRepo.Verify(r => r.DeleteAsync(3), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithNoUsers_DeletesPortal()
    {
        var portal = NewPortal(3);
        _portalRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(portal);
        _userRepo.Setup(r => r.GetByPortalIdAsync(3)).ReturnsAsync(new List<UserEntity>());

        var result = await _sut.DeleteAsync(3);

        result.IsSuccess.Should().BeTrue();
        _userRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _portalRepo.Verify(r => r.DeleteAsync(3), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- HasSpaceAvailableAsync ----------
    [Theory]
    [InlineData(0, 99999999999L, true)]   // hostSpace 0 => unlimited
    [InlineData(100, 1048576L, true)]      // 1 MB <= 100 MB
    [InlineData(1, 1048576L, true)]        // 1 MB <= 1 MB (boundary, inclusive)
    [InlineData(1, 2097152L, false)]       // 2 MB > 1 MB
    public async Task HasSpaceAvailableAsync_AppliesExactFormula(int hostSpace, long fileSizeBytes, bool expected)
    {
        var portal = new PortalEntity { PortalId = 1, HostSpace = hostSpace };
        _portalRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(portal);

        var result = await _sut.HasSpaceAvailableAsync(1, fileSizeBytes);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public async Task HasSpaceAvailableAsync_NegativePortalId_TreatsAsUnlimited()
    {
        var result = await _sut.HasSpaceAvailableAsync(-1, 99999999999L);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        _portalRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task HasSpaceAvailableAsync_WhenPortalNull_TreatsAsUnlimited()
    {
        _portalRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync((PortalEntity?)null);

        var result = await _sut.HasSpaceAvailableAsync(5, 99999999999L);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }
}
