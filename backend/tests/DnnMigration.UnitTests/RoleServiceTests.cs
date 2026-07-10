// -----------------------------------------------------------------------------
//  RoleServiceTests.cs
//
//  Unit tests for DnnMigration.Application.Services.RoleService — the migrated
//  security-role "use-case" service that sits between the /api/roles controllers
//  and the persistence port.
//
//  PURPOSE / PARITY
//  ----------------
//  MIGRATION: RoleService was extracted from the legacy DotNetNuke 4.x
//  Library/Components/Security/Roles/RoleController.vb. The legacy controller
//  co-mingled business logic with a provider-backed data store and performed a
//  user-role assignment side effect (AutoAssignUsers) inside AddRole [L100] and
//  UpdateRole [L254]. That side effect is INTENTIONALLY DROPPED in the migrated
//  service (it is out of scope for the role CRUD surface — the service is injected
//  only with IRoleRepository + IMapper). Likewise the legacy by-name / role-group
//  lookups (GetRoleByName [L179], GetRolesByGroup [L224], GetRoleNames) are NOT part
//  of the migrated surface and are therefore NOT exercised here.
//
//  The behavioural contract pinned by this suite mirrors the preserved legacy
//  semantics exactly:
//    * GetRoles()          [RoleController.vb L208] -> GetAllAsync   (all roles)
//    * GetPortalRoles(id)  [RoleController.vb L146] -> GetByPortalAsync
//    * GetRole(id, portal) [RoleController.vb L163] -> GetByIdAsync  (missing -> null)
//    * AddRole(role)       [RoleController.vb L100] -> CreateAsync   (persist + echo new id)
//    * UpdateRole(role)    [RoleController.vb L254] -> UpdateAsync   (missing -> null)
//    * DeleteRole(id, ...) [RoleController.vb L125] -> DeleteAsync   (missing -> false, no-op)
//
//  TEST STRATEGY
//  -------------
//  The System-Under-Test (SUT) is constructed with a MOCKED IRoleRepository (Moq)
//  and a REAL AutoMapper IMapper built from the production MappingProfile, so the
//  entity <-> DTO projection under test is the exact mapping the application uses.
//  Only the repository is faked; the mapper is genuine. CancellationToken matching
//  uses It.IsAny<CancellationToken>() throughout because the token is an opaque
//  pass-through that carries no branch logic.
//
//  KEY MIGRATION NOTE (numeric width): the legacy VB.NET RoleInfo.ServiceFee /
//  RoleInfo.TrialFee columns are VB `Single`, migrated to C# `float` (see
//  Role.cs / RoleDto.cs). The float passthrough is asserted explicitly with
//  `float` literals (e.g. 9.99f) to lock the numeric width against accidental
//  widening to double.
// -----------------------------------------------------------------------------

using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Exceptions;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// xUnit test-suite verifying every public operation of
/// <see cref="RoleService"/> against a mocked <see cref="IRoleRepository"/> and a
/// real AutoMapper <see cref="IMapper"/> (built from the production
/// <see cref="MappingProfile"/>).
/// </summary>
/// <remarks>
/// The suite covers all six service methods and both outcomes of the
/// fetch-then-act operations (found / not-found). It asserts three things per the
/// migrated contract: (1) each method delegates to the correct repository member
/// with the expected arguments, (2) the not-found branches short-circuit without
/// attempting a write (Update / Delete are never invoked), and (3) the
/// <c>float</c> fee fields survive the round-trip through the real mapper.
/// </remarks>
public class RoleServiceTests
{
    // The repository collaborator is faked (loose behaviour): every method the
    // SUT calls is set up explicitly below, and the unset members are never
    // invoked, so loose vs. strict makes no observable difference here.
    private readonly Mock<IRoleRepository> _repo = new();

    // A REAL mapper built from the production profile — the projection logic is
    // part of what these tests exercise, so it must not be mocked.
    private readonly IMapper _mapper;

    // The System-Under-Test.
    private readonly RoleService _sut;

    /// <summary>
    /// Builds the real AutoMapper instance from <see cref="MappingProfile"/> and
    /// wires the SUT with the mocked repository. <see cref="IMapperConfigurationExpression.AddProfile{TProfile}()"/>
    /// registers the production profile so the Role &lt;-&gt; DTO maps are the genuine ones.
    /// </summary>
    /// <remarks>
    /// MIGRATION: <see cref="MapperConfiguration.AssertConfigurationIsValid()"/> is
    /// intentionally NOT called — that would validate every map in the shared
    /// profile (Portal / Module / User / Tab), which are outside the scope of these
    /// role tests. The Role maps are compiled lazily on first use, which is all this
    /// suite requires.
    /// </remarks>
    public RoleServiceTests()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        _mapper = configuration.CreateMapper();
        _sut = new RoleService(_repo.Object, _mapper);
    }

    #region Test-data builders

    /// <summary>
    /// Creates a <see cref="Role"/> domain entity with the fields relevant to these
    /// tests, defaulting the remainder. Individual tests override only what they assert.
    /// </summary>
    private static Role NewRole(
        int roleId,
        string roleName,
        int portalId = 0,
        float serviceFee = 0f,
        bool isPublic = false,
        bool autoAssignment = false) => new()
        {
            RoleID = roleId,
            RoleName = roleName,
            PortalID = portalId,
            ServiceFee = serviceFee,
            IsPublic = isPublic,
            AutoAssignment = autoAssignment,
        };

    /// <summary>
    /// Builds a fully populated, valid <see cref="CreateRoleDto"/> baseline used by
    /// the create test. String fields are non-null so the mapped entity carries real
    /// values through the round-trip.
    /// </summary>
    private static CreateRoleDto ValidCreate() => new()
    {
        PortalID = 1,
        RoleGroupID = -1,
        RoleName = "Contributors",
        Description = "Content contributors",
        IsPublic = true,
        AutoAssignment = false,
        ServiceFee = 19.95f,
        BillingFrequency = "M",
        BillingPeriod = 1,
        TrialFee = 4.5f,
        TrialPeriod = 7,
        TrialFrequency = "D",
        RSVPCode = "RSVP123",
        IconFile = "role.gif",
    };

    /// <summary>
    /// Builds a fully populated <see cref="UpdateRoleDto"/> baseline used by the
    /// update test. Note: <see cref="UpdateRoleDto"/> carries neither RoleID nor
    /// PortalID (identity is immutable / taken from the route).
    /// </summary>
    private static UpdateRoleDto ValidUpdate() => new()
    {
        RoleName = "New",
        Description = "Updated description",
        RoleGroupID = -1,
        IsPublic = true,
        AutoAssignment = false,
        ServiceFee = 5.5f,
        BillingFrequency = "Y",
        BillingPeriod = 1,
        TrialFee = 0f,
        TrialPeriod = 0,
        TrialFrequency = "N",
        RSVPCode = "NEWCODE",
        IconFile = "new.gif",
    };

    #endregion

    #region GetAllAsync

    /// <summary>
    /// GetAllAsync fetches every role from the repository and projects each to a
    /// <see cref="RoleDto"/> — preserving order, count and (crucially) the
    /// <c>float</c> ServiceFee value.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ReturnsAllRolesProjectedToDtos()
    {
        var roles = new List<Role>
        {
            NewRole(1, "Administrators", portalId: 0, serviceFee: 0f, isPublic: false),
            NewRole(2, "Subscribers", portalId: 0, serviceFee: 9.99f, isPublic: true),
        };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(roles);

        var result = await _sut.GetAllAsync(CancellationToken.None);

        var list = result.ToList();
        list.Should().HaveCount(2);

        list[0].RoleID.Should().Be(1);
        list[0].RoleName.Should().Be("Administrators");
        list[0].PortalID.Should().Be(0);
        list[0].ServiceFee.Should().Be(0f);
        list[0].IsPublic.Should().BeFalse();

        list[1].RoleID.Should().Be(2);
        list[1].RoleName.Should().Be("Subscribers");
        // MIGRATION: VB Single -> float; assert the exact float value (no widening).
        list[1].ServiceFee.Should().Be(9.99f);
        list[1].IsPublic.Should().BeTrue();

        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetByPortalAsync

    /// <summary>
    /// GetByPortalAsync delegates to the portal-scoped repository finder with the
    /// supplied portal id and projects the returned roles to DTOs.
    /// </summary>
    [Fact]
    public async Task GetByPortalAsync_DelegatesWithPortalId_AndProjectsToDtos()
    {
        const int portalId = 3;
        var roles = new List<Role>
        {
            NewRole(10, "Portal3 Admin", portalId: portalId, serviceFee: 1.5f, isPublic: false),
            NewRole(11, "Portal3 Member", portalId: portalId, serviceFee: 0f, isPublic: true),
        };
        _repo.Setup(r => r.GetByPortalAsync(portalId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(roles);

        var result = await _sut.GetByPortalAsync(portalId, CancellationToken.None);

        var list = result.ToList();
        list.Should().HaveCount(2);
        list.Should().OnlyContain(dto => dto.PortalID == portalId);
        list[0].RoleID.Should().Be(10);
        list[0].RoleName.Should().Be("Portal3 Admin");
        list[0].ServiceFee.Should().Be(1.5f);

        // The exact portal id (3) must reach the repository finder.
        _repo.Verify(r => r.GetByPortalAsync(3, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// MIGRATION (QA finding - R10 Issue 13): SearchPagedAsync forwards the free-text query and the
    /// skip/take window to the repository's server-side substring search, projects the returned page to
    /// DTOs, and carries the repository's grand total through unchanged so the controller can build honest
    /// pagination meta. This is the delegation that gives the roles list the search parity the SPA needs to
    /// make a role beyond the first page discoverable.
    /// </summary>
    [Fact]
    public async Task SearchPagedAsync_DelegatesQueryAndWindow_AndProjectsPageWithTotal()
    {
        const string query = "editor";
        const int skip = 20;
        const int take = 20;
        var page = new PagedResult<Role>(
            new List<Role> { NewRole(4025, "BrandNewRole2025", portalId: 0) },
            totalCount: 25);
        _repo.Setup(r => r.SearchPagedAsync(query, skip, take, It.IsAny<CancellationToken>()))
             .ReturnsAsync(page);

        var result = await _sut.SearchPagedAsync(query, skip, take, CancellationToken.None);

        // The page is projected to DTOs and the grand total is preserved verbatim.
        result.TotalCount.Should().Be(25);
        result.Items.Should().ContainSingle()
            .Which.RoleName.Should().Be("BrandNewRole2025");

        // The exact query + window must reach the repository search (not the unfiltered GetPaged path).
        _repo.Verify(r => r.SearchPagedAsync(query, skip, take, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// MIGRATION (QA finding - R10 Issue 13): the portal-scoped search delegates the portal id, query, and
    /// window to <c>IRoleRepository.SearchByPortalPagedAsync</c> and projects the page to DTOs.
    /// </summary>
    [Fact]
    public async Task SearchByPortalPagedAsync_DelegatesPortalQueryAndWindow_AndProjectsPage()
    {
        const int portalId = 7;
        const string query = "member";
        var page = new PagedResult<Role>(
            new List<Role> { NewRole(51, "Portal7 Members", portalId: portalId) },
            totalCount: 1);
        _repo.Setup(r => r.SearchByPortalPagedAsync(portalId, query, 0, 20, It.IsAny<CancellationToken>()))
             .ReturnsAsync(page);

        var result = await _sut.SearchByPortalPagedAsync(portalId, query, 0, 20, CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle()
            .Which.PortalID.Should().Be(portalId);
        _repo.Verify(r => r.SearchByPortalPagedAsync(portalId, query, 0, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetByIdAsync

    /// <summary>
    /// GetByIdAsync returns the mapped DTO when the repository finds the role.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsMappedDto()
    {
        var role = NewRole(42, "Editors", portalId: 1, serviceFee: 4.25f, isPublic: false);
        _repo.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
             .ReturnsAsync(role);

        var result = await _sut.GetByIdAsync(42, CancellationToken.None);

        result.Should().NotBeNull();
        result!.RoleID.Should().Be(42);
        result.RoleName.Should().Be("Editors");
        result.PortalID.Should().Be(1);
        result.ServiceFee.Should().Be(4.25f);

        _repo.Verify(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// GetByIdAsync returns <c>null</c> when the repository has no matching role
    /// (the API layer turns this into a 404). MIGRATION: mirrors the legacy
    /// GetRole returning Nothing for an unknown id.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Role?)null);

        var result = await _sut.GetByIdAsync(999, CancellationToken.None);

        result.Should().BeNull();
        _repo.Verify(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region CreateAsync

    /// <summary>
    /// CreateAsync maps the request DTO to a <see cref="Role"/>, persists it via
    /// the repository, and projects the persisted (id-assigned) entity back to a
    /// <see cref="RoleDto"/>. The repository echo-back simulates the store
    /// assigning the identity on insert.
    /// </summary>
    /// <remarks>
    /// MIGRATION: the legacy AddRole [RoleController.vb L100] returned the new
    /// integer id after a successful CreateRole and then ran AutoAssignUsers.
    /// AutoAssignUsers is DROPPED; the created role is simply projected back with
    /// its assigned RoleID.
    /// </remarks>
    [Fact]
    public async Task CreateAsync_MapsPersistsAndReturnsCreatedDtoWithAssignedId()
    {
        var dto = ValidCreate();

        // Echo the entity back with a store-assigned identity, mirroring an insert.
        _repo.Setup(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Role entity, CancellationToken _) =>
             {
                 entity.RoleID = 100;
                 return entity;
             });

        var result = await _sut.CreateAsync(dto, CancellationToken.None);

        result.RoleName.Should().Be("Contributors");
        result.RoleID.Should().Be(100);
        result.PortalID.Should().Be(1);
        result.IsPublic.Should().BeTrue();
        // MIGRATION: VB Single -> float passthrough on create.
        result.ServiceFee.Should().Be(19.95f);
        result.TrialFee.Should().Be(4.5f);

        // The mapped entity that reached the repository must carry the request fields.
        _repo.Verify(
            r => r.AddAsync(
                It.Is<Role>(x => x.RoleName == "Contributors" && x.PortalID == 1 && x.ServiceFee == 19.95f),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// MIGRATION (QA finding - R10 Issue 12): CreateAsync enforces per-portal role-name uniqueness. When a
    /// role with the same (case-insensitive) name already exists in the target portal, the create is
    /// rejected with a <see cref="ConflictException"/> (surfaced as HTTP 409) BEFORE any row is written, so
    /// AddAsync is never called and no duplicate is persisted.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenDuplicateNameInPortal_ThrowsConflict_AndDoesNotPersist()
    {
        var dto = ValidCreate(); // PortalID = 1, RoleName = "Contributors"

        // An existing role already occupies this portal under the same name.
        _repo.Setup(r => r.GetByNameAsync(1, "Contributors", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Role { RoleID = 55, PortalID = 1, RoleName = "Contributors" });

        var act = async () => await _sut.CreateAsync(dto, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _repo.Verify(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// MIGRATION (finding #7 — AUTO-ASSIGNMENT EXCLUSION boundary): CreateAsync persists the role's
    /// <c>AutoAssignment</c> flag faithfully (schema fidelity) but performs NO user-role assignment
    /// fan-out. The legacy AddRole ran AutoAssignUsers, which enumerated the portal's users and wrote a
    /// membership row for each; that behavior is out of the AAP §0.3.1 design (no user-role
    /// junction/repository/endpoint). The exclusion is structurally guaranteed here — RoleService is
    /// injected with only IRoleRepository + IMapper — so this test pins that the flag round-trips AND
    /// that exactly one write (AddAsync) occurs with no portal-scoped user enumeration.
    /// </summary>
    [Fact]
    public async Task CreateAsync_persists_AutoAssignment_flag_without_assigning_users()
    {
        var dto = ValidCreate() with { AutoAssignment = true };

        Role? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Role entity, CancellationToken _) =>
             {
                 captured = entity;
                 entity.RoleID = 200;
                 return entity;
             });

        var result = await _sut.CreateAsync(dto, CancellationToken.None);

        // The flag round-trips through the real mapper onto both the persisted entity and the DTO.
        captured.Should().NotBeNull();
        captured!.AutoAssignment.Should().BeTrue("the AutoAssignment column is persisted (schema fidelity)");
        result.AutoAssignment.Should().BeTrue();

        // Exactly one write (the role row); the AutoAssignUsers fan-out is excluded, so there is no
        // additional repository interaction (no user enumeration, no second write).
        _repo.Verify(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region UpdateAsync

    /// <summary>
    /// UpdateAsync fetches the existing role, applies the request fields in place
    /// via the real mapper, persists the change, and returns the updated DTO. The
    /// role identity (RoleID) and owning portal (PortalID) are preserved because
    /// <see cref="UpdateRoleDto"/> does not carry them.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenFound_AppliesChangesPersistsAndReturnsDto()
    {
        var existing = NewRole(7, "Old", portalId: 2, serviceFee: 1f, isPublic: false);
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
             .ReturnsAsync(existing);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        var dto = ValidUpdate(); // RoleName "New", ServiceFee 5.5f, IsPublic true

        var result = await _sut.UpdateAsync(7, dto, CancellationToken.None);

        result.Should().NotBeNull();
        result!.RoleName.Should().Be("New");
        result.IsPublic.Should().BeTrue();
        // MIGRATION: VB Single -> float passthrough on update.
        result.ServiceFee.Should().Be(5.5f);
        // Identity + portal are NOT on UpdateRoleDto, so the in-place map preserves them.
        result.RoleID.Should().Be(7);
        result.PortalID.Should().Be(2);

        _repo.Verify(
            r => r.UpdateAsync(It.Is<Role>(x => x.RoleID == 7 && x.RoleName == "New"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// UpdateAsync returns <c>null</c> and performs NO write when the role does not
    /// exist. MIGRATION: the missing-role branch short-circuits before UpdateRole.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsNull_AndDoesNotPersist()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Role?)null);

        var result = await _sut.UpdateAsync(123, ValidUpdate(), CancellationToken.None);

        result.Should().BeNull();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// MIGRATION (QA finding - R10 Issue 12): UpdateAsync enforces the same per-portal uniqueness on rename.
    /// Renaming a role to a name already held by a DIFFERENT role in the same portal is rejected with a
    /// <see cref="ConflictException"/> (HTTP 409) and no update is persisted.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenRenameCollidesWithAnotherRole_ThrowsConflict_AndDoesNotPersist()
    {
        var existing = NewRole(9, "Members", portalId: 1, autoAssignment: false);
        _repo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        // ValidUpdate().RoleName == "New"; a DIFFERENT role (id 42) in portal 1 already owns "New".
        _repo.Setup(r => r.GetByNameAsync(1, "New", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Role { RoleID = 42, PortalID = 1, RoleName = "New" });

        var act = async () => await _sut.UpdateAsync(9, ValidUpdate(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// MIGRATION (QA finding - R10 Issue 12): renaming a role to its OWN current name (the uniqueness lookup
    /// matches the role itself) is permitted - the guard excludes the role's own id - so the update proceeds
    /// and persists normally.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenMatchedRoleIsItself_Succeeds_AndPersists()
    {
        var existing = NewRole(9, "New", portalId: 1, autoAssignment: false);
        _repo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        // The only role owning "New" in portal 1 IS role 9 itself, so this is not a collision.
        _repo.Setup(r => r.GetByNameAsync(1, "New", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Role { RoleID = 9, PortalID = 1, RoleName = "New" });

        var result = await _sut.UpdateAsync(9, ValidUpdate(), CancellationToken.None);

        result.Should().NotBeNull();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// MIGRATION (finding #7 — AUTO-ASSIGNMENT EXCLUSION boundary): UpdateAsync persists a changed
    /// <c>AutoAssignment</c> flag but performs no user-role reassignment fan-out (the legacy UpdateRole
    /// re-ran AutoAssignUsers). No user port exists on this service, so the exclusion is structural;
    /// the test pins the flag persistence AND the single write with no user enumeration.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_persists_AutoAssignment_flag_without_assigning_users()
    {
        var existing = NewRole(9, "Members", portalId: 1, autoAssignment: false);
        _repo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        Role? captured = null;
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
             .Callback<Role, CancellationToken>((r, _) => captured = r)
             .Returns(Task.CompletedTask);

        var dto = ValidUpdate() with { AutoAssignment = true };

        var result = await _sut.UpdateAsync(9, dto, CancellationToken.None);

        result.Should().NotBeNull();
        captured.Should().NotBeNull();
        captured!.AutoAssignment.Should().BeTrue("the updated AutoAssignment flag is persisted");
        result!.AutoAssignment.Should().BeTrue();

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DeleteAsync

    /// <summary>
    /// DeleteAsync deletes the role (by id) and returns <c>true</c> when it exists.
    /// MIGRATION: the legacy DeleteRole [RoleController.vb L125] first fetched the
    /// role and only called the provider delete when it was found.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WhenFound_DeletesByIdAndReturnsTrue()
    {
        var existing = NewRole(5, "Temp", portalId: 1);
        _repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
             .ReturnsAsync(existing);
        _repo.Setup(r => r.DeleteAsync(5, It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        var result = await _sut.DeleteAsync(5, CancellationToken.None);

        result.Should().BeTrue();
        // IRepository.DeleteAsync takes the integer id (not the entity).
        _repo.Verify(r => r.DeleteAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// DeleteAsync returns <c>false</c> and performs NO delete when the role does
    /// not exist. MIGRATION: preserves the legacy "no-op when Nothing" behaviour.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsFalse_AndDoesNotDelete()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Role?)null);

        var result = await _sut.DeleteAsync(404, CancellationToken.None);

        result.Should().BeFalse();
        _repo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
