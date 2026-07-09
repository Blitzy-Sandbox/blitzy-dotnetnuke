// -----------------------------------------------------------------------------
//  PortalServiceTests.cs
//
//  Unit tests for DnnMigration.Application.Services.PortalService - the migrated
//  portal (site) management service that replaces the business surface of the
//  legacy DotNetNuke 4.x PortalController.vb
//  (Library/Components/Portal/PortalController.vb).
//
//  PURPOSE / PARITY
//  ----------------
//  MIGRATION: the legacy PortalController co-mingled business logic with ADO.NET /
//  SqlDataProvider data access and exposed Public [Shared] members. In the target
//  architecture that surface becomes PortalService (Application layer), which is
//  stateless, async-only, dependency-injected, and returns DTOs (never the Domain
//  Portal entity). These tests verify the migrated business behavior against the
//  legacy semantics:
//      * GetPortals()        [L1263] -> GetAllAsync   (collection projection)
//      * GetPortal(id)       [L1224] -> GetByIdAsync  (single, DataCache dropped)
//      * (alias lookup)              -> GetByAliasAsync
//      * CreatePortal(...)   [L980]  -> CreateAsync   (portal + admin + alias; see note below)
//      * UpdatePortalInfo(..)[L1524] -> UpdateAsync   (27-field in-place copy)
//      * DeletePortalInfo(..)[L1191] -> DeleteAsync   (cascade/cache dropped)
//
//  IMPORTANT (scope of CreateAsync): the legacy CreatePortal was an ORCHESTRATION -
//  it added the portal row, created the initial administrator user, set
//  AdministratorId, and registered the initial PortalAlias (ALL IN SCOPE), and
//  additionally performed template / file-system provisioning (OUT OF SCOPE per AAP
//  section 0.2.2). The migrated CreateAsync therefore provisions the portal, its
//  administrator (delegated to IUserService, which hashes the password), the
//  AdministratorId back-reference, and the PortalAlias; template/file-system/cache/
//  email/logging side effects are excluded. The tests assert exactly that contract:
//  the administrator credential fields and PortalAlias on CreatePortalDto ARE
//  consumed (the review flagged that the prior tests did not catch their omission).
//
//  TEST STRATEGY
//  -------------
//  The mocked collaborators are IPortalRepository and IUserService (Moq). The
//  AutoMapper IMapper is REAL, built from the production MappingProfile, so the entity<->DTO
//  projections are exercised end-to-end; mocking IMapper would hide mapping
//  regressions. Repositories deal in Domain entities (Portal), never DTOs, so the
//  repository mock is set up and verified with Portal instances. Every repository
//  setup/verify matches the cancellation token with It.IsAny<CancellationToken>()
//  because the service forwards its (defaulted) token verbatim.
// -----------------------------------------------------------------------------

using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// xUnit test-suite verifying the business behavior of
/// <see cref="PortalService"/> across all six operations of
/// <c>IPortalService</c>, including the null / not-found branches.
/// </summary>
/// <remarks>
/// Each test follows an Arrange/Act/Assert shape. The repository is a Moq test
/// double (loose behavior) so that unrelated members return harmless defaults,
/// while the mapper is the genuine <see cref="MappingProfile"/>-backed
/// <see cref="IMapper"/>. Not-found repository results are produced with a
/// <c>(Portal?)null</c> cast so overload resolution selects
/// <c>Task&lt;Portal?&gt;</c>, and the non-generic <c>UpdateAsync</c> /
/// <c>DeleteAsync</c> repository methods are stubbed with
/// <see cref="Task.CompletedTask"/>.
/// </remarks>
public class PortalServiceTests
{
    // The repository is the single mocked collaborator. Loose (default) behavior
    // is used deliberately - Strict would force redundant setups for members a
    // given test never touches without improving the assertion's value.
    private readonly Mock<IPortalRepository> _repo = new();

    // IUserService is mocked: PortalService.CreateAsync delegates initial-administrator provisioning to it
    // (the single authoritative user-creation path that hashes the password). These unit tests verify the
    // delegation CONTRACT (called once, with the new portal's id and the DTO's admin fields) without
    // exercising the real UserService, whose own behavior is covered by UserServiceTests.
    private readonly Mock<IUserService> _userService = new();

    // The mapper is REAL (not mocked): built from the production MappingProfile so
    // Portal <-> DTO projections are validated by these tests rather than stubbed.
    private readonly IMapper _mapper;

    // System Under Test.
    private readonly PortalService _sut;

    /// <summary>
    /// Wires the SUT with the mocked repository and a real AutoMapper instance
    /// created from <see cref="MappingProfile"/>.
    /// </summary>
    public PortalServiceTests()
    {
        _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        _sut = new PortalService(_repo.Object, _userService.Object, _mapper);

        // MIGRATION (Portal Aliases read model): GetAllAsync / SearchAsync / GetByIdAsync now enrich the
        // projected PortalDto with the portal's HTTP aliases via IPortalRepository.GetAliasesAsync /
        // GetAliasesForPortalAsync. A loose Moq returns a completed Task wrapping null for un-set-up members,
        // which would NRE when the service dereferences the result; these harmless empty defaults keep the
        // pre-existing (alias-agnostic) tests green. The dedicated alias tests below override them.
        _repo.Setup(r => r.GetAliasesAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Dictionary<int, IReadOnlyList<string>>());
        _repo.Setup(r => r.GetAliasesForPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<string>());
    }

    #region Test data builders

    /// <summary>
    /// Builds a fully populated, valid <see cref="CreatePortalDto"/>. The administrator
    /// credential fields and <see cref="CreatePortalDto.PortalAlias"/> ARE consumed by
    /// <see cref="PortalService.CreateAsync"/> (they provision the initial administrator
    /// and register the portal's HTTP alias), so the create tests assert on them.
    /// </summary>
    private static CreatePortalDto NewValidCreateDto() => new()
    {
        PortalName = "Contoso",
        FirstName = "Ada",
        LastName = "Lovelace",
        Username = "ada",
        // Clearly-fake unit-test fixture value; not a real credential.
        Password = "Test!Password1",
        ConfirmPassword = "Test!Password1",
        Email = "ada@contoso.example",
        Description = "Corporate portal",
        KeyWords = "corp,portal",
        HomeDirectory = "Portals/0",
        PortalAlias = "contoso.example",
    };

    #endregion

    #region GetAllAsync

    /// <summary>
    /// GetAllAsync maps every entity returned by the repository into a
    /// <see cref="PortalDto"/> sequence, preserving ids and names.
    /// MIGRATION: PortalController.GetPortals() [L1263].
    /// </summary>
    [Fact]
    public async Task GetAllAsync_MapsEveryRepositoryEntity_ToPortalDto()
    {
        // Arrange
        var entities = new List<Portal>
        {
            new() { PortalID = 1, PortalName = "P1" },
            new() { PortalID = 2, PortalName = "P2" },
        };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(entities);

        // Act
        var result = (await _sut.GetAllAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllBeOfType<PortalDto>();
        result.Should().ContainSingle(p => p.PortalID == 1 && p.PortalName == "P1");
        result.Should().ContainSingle(p => p.PortalID == 2 && p.PortalName == "P2");
        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// GetAllAsync returns an empty (non-null) sequence when the repository has no
    /// portals, rather than throwing.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WhenRepositoryEmpty_ReturnsEmptySequence()
    {
        // Arrange
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Portal>());

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync

    /// <summary>
    /// GetByIdAsync projects the found entity to a non-null <see cref="PortalDto"/>
    /// carrying the same identity/field values.
    /// MIGRATION: PortalController.GetPortal(PortalId) [L1224] (DataCache dropped).
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsMappedDto()
    {
        // Arrange
        var entity = new Portal { PortalID = 42, PortalName = "Answer", Email = "admin@answer.example" };
        _repo.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
             .ReturnsAsync(entity);

        // Act
        var result = await _sut.GetByIdAsync(42);

        // Assert
        result.Should().NotBeNull();
        result!.PortalID.Should().Be(42);
        result.PortalName.Should().Be("Answer");
        result.Email.Should().Be("admin@answer.example");
        _repo.Verify(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// GetByIdAsync returns <c>null</c> (not an empty DTO, not an exception) when
    /// the repository has no matching portal.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange - (Portal?)null cast so ReturnsAsync binds to Task<Portal?>.
        _repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
             .ReturnsAsync((Portal?)null);

        // Act
        var result = await _sut.GetByIdAsync(99);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByAliasAsync

    /// <summary>
    /// GetByAliasAsync projects the alias-matched entity to a non-null DTO.
    /// MIGRATION: mirrors the legacy PortalAliasController/GetPortalByAlias lookup.
    /// </summary>
    [Fact]
    public async Task GetByAliasAsync_WhenFound_ReturnsMappedDto()
    {
        // Arrange
        var entity = new Portal { PortalID = 3, PortalName = "AliasSite" };
        _repo.Setup(r => r.GetByAliasAsync("site.com", It.IsAny<CancellationToken>()))
             .ReturnsAsync(entity);

        // Act
        var result = await _sut.GetByAliasAsync("site.com");

        // Assert
        result.Should().NotBeNull();
        result!.PortalID.Should().Be(3);
        result.PortalName.Should().Be("AliasSite");
        _repo.Verify(r => r.GetByAliasAsync("site.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// GetByAliasAsync returns <c>null</c> when no portal matches the alias.
    /// </summary>
    [Fact]
    public async Task GetByAliasAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        _repo.Setup(r => r.GetByAliasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Portal?)null);

        // Act
        var result = await _sut.GetByAliasAsync("missing.example");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Portal Aliases read model (MIGRATION: FormatPortalAliases grid column)

    /// <summary>
    /// GetAllAsync enriches each projected <see cref="PortalDto"/> with the HTTP aliases returned by
    /// <c>GetAliasesAsync</c>, keyed by portal id; a portal with no aliases keeps an empty list.
    /// MIGRATION: the legacy Portals.ascx.vb grid "Portal Aliases" column (FormatPortalAliases).
    /// </summary>
    [Fact]
    public async Task GetAllAsync_PopulatesAliases_FromAliasLookup()
    {
        // Arrange
        var entities = new List<Portal>
        {
            new() { PortalID = 1, PortalName = "P1" },
            new() { PortalID = 2, PortalName = "P2" },
        };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entities);
        _repo.Setup(r => r.GetAliasesAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Dictionary<int, IReadOnlyList<string>>
             {
                 [1] = new[] { "p1.example", "www.p1.example" },
                 // portal 2 intentionally has no aliases entry
             });

        // Act
        var result = (await _sut.GetAllAsync()).ToList();

        // Assert
        result.Single(p => p.PortalID == 1).Aliases.Should().BeEquivalentTo("p1.example", "www.p1.example");
        result.Single(p => p.PortalID == 2).Aliases.Should().BeEmpty();
        _repo.Verify(r => r.GetAliasesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// GetByIdAsync enriches the single projected <see cref="PortalDto"/> with the aliases returned by
    /// <c>GetAliasesForPortalAsync</c>.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_PopulatesAliases_FromPerPortalLookup()
    {
        // Arrange
        var entity = new Portal { PortalID = 7, PortalName = "Seven" };
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _repo.Setup(r => r.GetAliasesForPortalAsync(7, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { "seven.example" });

        // Act
        var result = await _sut.GetByIdAsync(7);

        // Assert
        result.Should().NotBeNull();
        result!.Aliases.Should().ContainSingle().Which.Should().Be("seven.example");
        _repo.Verify(r => r.GetAliasesForPortalAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SearchAsync (MIGRATION: PortalController.GetPortalsByName / AAP §0.7.2 server-side search)

    /// <summary>
    /// SearchAsync delegates the free-text term to <c>IPortalRepository.SearchAsync</c>, projects the
    /// matched entities to DTOs, and enriches them with aliases exactly as the list path does.
    /// </summary>
    [Fact]
    public async Task SearchAsync_DelegatesToRepository_AndMapsWithAliases()
    {
        // Arrange
        var matches = new List<Portal> { new() { PortalID = 5, PortalName = "Contoso" } };
        _repo.Setup(r => r.SearchAsync("cont", It.IsAny<CancellationToken>())).ReturnsAsync(matches);
        _repo.Setup(r => r.GetAliasesAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Dictionary<int, IReadOnlyList<string>> { [5] = new[] { "contoso.example" } });

        // Act
        var result = (await _sut.SearchAsync("cont")).ToList();

        // Assert
        result.Should().ContainSingle();
        result[0].PortalID.Should().Be(5);
        result[0].PortalName.Should().Be("Contoso");
        result[0].Aliases.Should().BeEquivalentTo("contoso.example");
        _repo.Verify(r => r.SearchAsync("cont", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region CreateAsync

    /// <summary>
    /// CreateAsync maps the request DTO to a Portal entity and persists the portal record via
    /// <c>AddAsync</c> exactly once, returning the DTO projection of the persisted entity (including
    /// the id the repository assigns).
    /// MIGRATION: PortalController.CreatePortal(...) [L980] - the portal INSERT portion of the
    /// orchestration (the administrator / alias / AdministratorId steps are asserted separately below).
    /// </summary>
    [Fact]
    public async Task CreateAsync_PersistsPortalRecord_AndReturnsMappedDto()
    {
        // Arrange
        var create = NewValidCreateDto();
        Portal? persisted = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Portal p, CancellationToken _) =>
             {
                 // Echo the entity back with a repository-assigned identity, mirroring
                 // an INSERT that yields the new PortalID.
                 persisted = p;
                 p.PortalID = 10;
                 return p;
             });
        // The create is now a full orchestration; stub the admin/update/alias collaborators so the
        // whole use case runs without asserting on them in this (portal-record-focused) test.
        StubAdminCreation(adminUserId: 5);

        // Act
        var dto = await _sut.CreateAsync(create);

        // Assert - the returned DTO reflects the persisted entity.
        dto.Should().NotBeNull();
        dto.PortalName.Should().Be(create.PortalName);
        dto.PortalID.Should().Be(10);

        // Assert - the DTO->entity projection carried the descriptive fields onto
        // the entity that was actually handed to the repository.
        persisted.Should().NotBeNull();
        persisted!.PortalName.Should().Be(create.PortalName);

        // Assert - the portal row is inserted exactly once.
        _repo.Verify(r => r.AddAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// CreateAsync provisions the portal's initial administrator (delegating to
    /// <see cref="IUserService.CreateAsync"/>, which hashes the password), wires the new admin's id onto
    /// <c>Portal.AdministratorId</c> via <c>UpdateAsync</c>, and registers the initial <c>PortalAlias</c>
    /// so the portal is resolvable by host alias.
    /// MIGRATION: PortalController.CreatePortal(...) - the admin user [L1013], the AdministratorId
    /// back-reference, and PortalAliasController.AddPortalAlias. This is the regression guard the code
    /// review flagged as MISSING: the prior implementation dropped the administrator and alias entirely,
    /// producing portals that could not be administered or resolved by host alias.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ProvisionsAdministrator_WiresAdministratorId_AndRegistersAlias()
    {
        // Arrange
        const int newPortalId = 42;
        const int adminUserId = 777;
        var create = NewValidCreateDto();

        _repo.Setup(r => r.AddAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Portal p, CancellationToken _) => { p.PortalID = newPortalId; return p; });

        // Capture the CreateUserDto handed to the user service so the delegation contract can be asserted.
        CreateUserDto? adminRequest = null;
        _userService.Setup(s => s.CreateAsync(It.IsAny<CreateUserDto>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((CreateUserDto d, CancellationToken _) =>
                    {
                        adminRequest = d;
                        // MIGRATION QA finding K: IUserService.CreateAsync returns a CreateUserResult wrapper.
                        // The portal admin supplies its own password, so GeneratedPassword is null here.
                        return new CreateUserResult(
                            new UserDto { UserID = adminUserId, Username = d.Username, PortalID = d.PortalID },
                            null);
                    });

        // Capture the portal handed to UpdateAsync so the AdministratorId back-reference can be asserted.
        Portal? updatedPortal = null;
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
             .Callback((Portal p, CancellationToken _) => updatedPortal = p)
             .Returns(Task.CompletedTask);

        // Capture the alias handed to AddAliasAsync so its portal id / host alias can be asserted.
        PortalAlias? registeredAlias = null;
        _repo.Setup(r => r.AddAliasAsync(It.IsAny<PortalAlias>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((PortalAlias a, CancellationToken _) => { registeredAlias = a; return a; });

        // Act
        await _sut.CreateAsync(create);

        // Assert - the administrator is provisioned exactly once, associated with the NEW portal, approved,
        // and carrying the DTO's admin fields (the credential-hashing path is UserService's, not duplicated).
        _userService.Verify(s => s.CreateAsync(It.IsAny<CreateUserDto>(), It.IsAny<CancellationToken>()), Times.Once);
        adminRequest.Should().NotBeNull();
        adminRequest!.Username.Should().Be(create.Username);
        adminRequest.Email.Should().Be(create.Email);
        adminRequest.Password.Should().Be(create.Password);
        adminRequest.PortalID.Should().Be(newPortalId);
        adminRequest.Authorize.Should().BeTrue();

        // Assert - the portal's AdministratorId back-reference is wired to the new admin and persisted once.
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()), Times.Once);
        updatedPortal.Should().NotBeNull();
        updatedPortal!.AdministratorId.Should().Be(adminUserId);

        // Assert - the initial HTTP alias is registered once against the new portal.
        _repo.Verify(r => r.AddAliasAsync(It.IsAny<PortalAlias>(), It.IsAny<CancellationToken>()), Times.Once);
        registeredAlias.Should().NotBeNull();
        registeredAlias!.PortalID.Should().Be(newPortalId);
        registeredAlias.HTTPAlias.Should().Be(create.PortalAlias);
    }

    /// <summary>
    /// When no <c>PortalAlias</c> is supplied, CreateAsync still provisions the portal and its
    /// administrator but does NOT write an (empty) alias row - documenting the guard on the alias write.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenPortalAliasBlank_DoesNotRegisterAlias()
    {
        // Arrange - a create request with a blank alias (all other required fields present). CreatePortalDto
        // is a record, so the blank-alias variant is produced with a non-destructive `with` expression.
        var create = NewValidCreateDto() with { PortalAlias = "  " };
        _repo.Setup(r => r.AddAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Portal p, CancellationToken _) => { p.PortalID = 7; return p; });
        StubAdminCreation(adminUserId: 9);

        // Act
        await _sut.CreateAsync(create);

        // Assert - the administrator is still provisioned, but no alias row is written.
        _userService.Verify(s => s.CreateAsync(It.IsAny<CreateUserDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.AddAliasAsync(It.IsAny<PortalAlias>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Stubs the mocked user-service and the repository write calls (UpdateAsync / AddAliasAsync) that
    // CreateAsync now performs, so a test not asserting on those collaborators still runs the full
    // orchestration without NullReferenceExceptions. Returns an admin UserDto carrying the supplied id.
    private void StubAdminCreation(int adminUserId)
    {
        _userService.Setup(s => s.CreateAsync(It.IsAny<CreateUserDto>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((CreateUserDto d, CancellationToken _) =>
                        // MIGRATION QA finding K: IUserService.CreateAsync returns a CreateUserResult wrapper.
                        new CreateUserResult(
                            new UserDto { UserID = adminUserId, Username = d.Username, PortalID = d.PortalID },
                            null));
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        _repo.Setup(r => r.AddAliasAsync(It.IsAny<PortalAlias>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((PortalAlias a, CancellationToken _) => a);
    }

    #endregion

    #region UpdateAsync

    /// <summary>
    /// UpdateAsync fetches the existing entity, applies the request DTO in-place
    /// (AutoMapper's Map(source, destination) overload), persists via
    /// <c>UpdateAsync</c> exactly once, and returns the updated projection. The
    /// route id is not part of the DTO, so the entity's <c>PortalID</c> is
    /// preserved while the mapped fields (e.g. PortalName) are overwritten.
    /// MIGRATION: PortalController.UpdatePortalInfo(PortalInfo) [L1524 -> L1568]
    /// (the 27-field copy is preserved; the DataCache clear is dropped).
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenFound_AppliesInPlaceMap_PersistsOnce_AndReturnsUpdatedDto()
    {
        // Arrange
        var existing = new Portal { PortalID = 5, PortalName = "Old" };
        _repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
             .ReturnsAsync(existing);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        var update = new UpdatePortalDto { PortalName = "New" };

        // Act
        var result = await _sut.UpdateAsync(5, update);

        // Assert - returned DTO reflects the in-place mapped entity.
        result.Should().NotBeNull();
        result!.PortalID.Should().Be(5);
        result.PortalName.Should().Be("New");

        // Assert - persisted the same entity instance (id preserved, name updated).
        _repo.Verify(
            r => r.UpdateAsync(It.Is<Portal>(p => p.PortalID == 5 && p.PortalName == "New"),
                               It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// UpdateAsync returns <c>null</c> and NEVER persists when the target portal
    /// does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsNull_AndDoesNotPersist()
    {
        // Arrange
        _repo.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>()))
             .ReturnsAsync((Portal?)null);
        var update = new UpdatePortalDto { PortalName = "Irrelevant" };

        // Act
        var result = await _sut.UpdateAsync(404, update);

        // Assert
        result.Should().BeNull();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DeleteAsync

    /// <summary>
    /// DeleteAsync deletes the existing portal via <c>DeleteAsync(id)</c> exactly
    /// once and returns <c>true</c>.
    /// MIGRATION: PortalController.DeletePortalInfo(PortalId) [L1191] (the cascade
    /// user-deletion, skin cleanup, and cache clear are dropped).
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WhenFound_DeletesOnce_AndReturnsTrue()
    {
        // Arrange
        var existing = new Portal { PortalID = 7, PortalName = "Doomed" };
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
             .ReturnsAsync(existing);
        _repo.Setup(r => r.DeleteAsync(7, It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.DeleteAsync(7);

        // Assert
        result.Should().BeTrue();
        _repo.Verify(r => r.DeleteAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// DeleteAsync returns <c>false</c> and NEVER calls the repository delete when
    /// the target portal does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsFalse_AndDoesNotDelete()
    {
        // Arrange
        _repo.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>()))
             .ReturnsAsync((Portal?)null);

        // Act
        var result = await _sut.DeleteAsync(8);

        // Assert
        result.Should().BeFalse();
        _repo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
