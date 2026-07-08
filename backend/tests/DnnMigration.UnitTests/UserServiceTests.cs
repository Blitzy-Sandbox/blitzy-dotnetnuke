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
/// Unit tests for <see cref="UserService"/>, the Application-layer service that owns user identity,
/// membership and credential business rules.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="IUserRepository"/> (persistence) and <see cref="IPasswordHasher"/> (credential
/// crypto) collaborators are replaced with Moq test doubles, while a REAL AutoMapper instance built
/// from the production <see cref="MappingProfile"/> is used so the tests exercise the exact
/// entity&lt;-&gt;DTO projection the running system uses. This keeps the suite fast and free of I/O
/// while still pinning the mapping contract.
/// </para>
/// <para>
/// MIGRATION: these tests assert behavioural parity with the credential handling of the legacy
/// DotNetNuke <c>UserController.vb</c> (Library/Components/Users/UserController.vb) — most notably
/// <c>CreateUser</c> (L156, membership-provider password hashing), <c>ChangePassword</c> (L103,
/// old-password verification + <c>UpdatePassword=False</c>) and the approval flag on
/// <c>UserMembership</c>. The legacy provider hashed and verified credentials internally; the modern
/// service delegates that to the injected <see cref="IPasswordHasher"/> (BCrypt), which is where the
/// most important assertions focus: a plaintext password is NEVER persisted or projected.
/// </para>
/// <para>
/// The business logic under test mutates the <c>User.Membership</c> value object (hashing, approval,
/// timestamps). Those mutations are captured with Moq <c>Callback</c>/<c>ReturnsAsync</c> hooks on the
/// repository <c>AddAsync</c>/<c>UpdateAsync</c> methods and asserted with FluentAssertions — this is
/// the only place the effects are observable because the read DTOs deliberately omit credential fields.
/// </para>
/// </remarks>
public class UserServiceTests
{
    /// <summary>Mocked persistence port. Loose behaviour: negative expectations are pinned with explicit <c>Verify(..., Times.Never)</c>.</summary>
    private readonly Mock<IUserRepository> _repo = new();

    /// <summary>
    /// Mocked credential hasher. <see cref="IPasswordHasher"/> is deliberately SYNCHRONOUS, so setups use
    /// <c>.Returns(...)</c> (never <c>.ReturnsAsync(...)</c>) and take plain string arguments (no <c>CancellationToken</c>).
    /// </summary>
    private readonly Mock<IPasswordHasher> _hasher = new();

    /// <summary>REAL AutoMapper built from the production <see cref="MappingProfile"/> (no Ignore-based fakery).</summary>
    private readonly IMapper _mapper;

    /// <summary>The system under test.</summary>
    private readonly UserService _sut;

    /// <summary>
    /// Wires the SUT with the two mocked ports and the real mapper.
    /// </summary>
    /// <remarks>
    /// The mapper is created from the whole <see cref="MappingProfile"/> but <c>AssertConfigurationIsValid()</c>
    /// is intentionally NOT called here: that global validation covers unrelated Portal/Module/Role/Tab maps
    /// which are the responsibility of their own dedicated test fixtures. Only the User-related maps
    /// (<c>User&lt;-&gt;UserDto</c>, <c>CreateUserDto-&gt;User</c>, <c>UpdateUserDto-&gt;User</c>) are exercised here.
    /// </remarks>
    public UserServiceTests()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        _mapper = configuration.CreateMapper();
        _sut = new UserService(_repo.Object, _hasher.Object, _mapper);
    }

    /// <summary>
    /// Builds a minimal, fully-populated <see cref="User"/> entity for a read/lookup scenario.
    /// </summary>
    /// <remarks>
    /// <c>Membership</c> and <c>Profile</c> are left at their non-null property defaults (<c>new()</c>),
    /// mirroring how a hydrated entity looks; <c>UserMembership.Approved</c> defaults to <see langword="true"/>
    /// per the legacy default, so approval scenarios set it explicitly.
    /// </remarks>
    private static User MakeUser(int id, string username) => new()
    {
        UserID = id,
        Username = username,
        Email = $"{username}@example.com",
        FirstName = "First",
        LastName = "Last",
        DisplayName = username,
        PortalID = 0
    };

    // ---------------------------------------------------------------------------------------------
    // Read surface: GetAllAsync / GetByPortalAsync / GetByIdAsync / GetByUsernameAsync
    // (AAP agent_prompt Phase 2)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_maps_all_users()
    {
        var users = new List<User> { MakeUser(1, "alice"), MakeUser(2, "bob") };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users);

        var result = (await _sut.GetAllAsync()).ToList();

        // The service returns IEnumerable<UserDto>, so a count of 2 confirms two projected DTOs;
        // the username set confirms the real mapper actually copied the identity data.
        result.Should().HaveCount(2);
        result.Select(u => u.Username).Should().BeEquivalentTo(new[] { "alice", "bob" });
        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByPortalAsync_forwards_portalId_and_maps()
    {
        var users = new List<User> { MakeUser(1, "alice"), MakeUser(2, "bob"), MakeUser(3, "carol") };
        _repo.Setup(r => r.GetByPortalAsync(0, It.IsAny<CancellationToken>())).ReturnsAsync(users);

        var result = (await _sut.GetByPortalAsync(0)).ToList();

        result.Should().HaveCount(3);
        // The portalId argument must be forwarded verbatim to the repository.
        _repo.Verify(r => r.GetByPortalAsync(0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_found_returns_dto()
    {
        _repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(MakeUser(7, "carol"));

        var result = await _sut.GetByIdAsync(7);

        result.Should().NotBeNull();
        result!.UserID.Should().Be(7);
        result.Username.Should().Be("carol");
    }

    [Fact]
    public async Task GetByIdAsync_not_found_returns_null()
    {
        _repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _sut.GetByIdAsync(99);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUsernameAsync_found_returns_dto()
    {
        _repo.Setup(r => r.GetByUsernameAsync(0, "admin", It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeUser(1, "admin"));

        var result = await _sut.GetByUsernameAsync(0, "admin");

        result.Should().NotBeNull();
        result!.Username.Should().Be("admin");
        // Both the portalId and the username must be forwarded to the repository lookup.
        _repo.Verify(r => r.GetByUsernameAsync(0, "admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByUsernameAsync_not_found_returns_null()
    {
        _repo.Setup(r => r.GetByUsernameAsync(0, "ghost", It.IsAny<CancellationToken>()))
             .ReturnsAsync((User?)null);

        var result = await _sut.GetByUsernameAsync(0, "ghost");

        result.Should().BeNull();
        _repo.Verify(r => r.GetByUsernameAsync(0, "ghost", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------------------------------
    // CreateAsync: password hashing + membership field population (AAP agent_prompt Phase 3)
    // MIGRATION: parity with UserController.CreateUser (L156). The legacy membership provider hashed
    // the password internally; here the service hashes via IPasswordHasher and stamps the membership.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_hashes_password_and_sets_membership_fields()
    {
        // Arrange: the hasher turns any plaintext into the sentinel "HASHED"; the repository echoes the
        // entity back and assigns a database id, capturing the exact instance the service persisted.
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("HASHED");

        User? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((User u, CancellationToken _) =>
             {
                 captured = u;
                 u.UserID = 100;
                 return u;
             });

        var create = new CreateUserDto
        {
            Username = "jdoe",
            Email = "j@x.com",
            Password = "Secret1!",
            Authorize = true,
            FirstName = "Jane",
            LastName = "Doe"
        };

        // Act
        var dto = await _sut.CreateAsync(create);

        // Assert: the returned projection reflects the persisted entity.
        dto.Should().NotBeNull();
        dto.Username.Should().Be("jdoe");
        dto.UserID.Should().Be(100);

        // Assert: the credential + membership mutations the service performed on the persisted entity.
        captured.Should().NotBeNull();
        captured!.Membership.Password.Should().Be("HASHED", "the plaintext password must be hashed before persistence");
        captured.Membership.Approved.Should().BeTrue("Authorize=true maps to Membership.Approved");
        captured.Membership.Username.Should().Be("jdoe", "the credential record mirrors the identity username");
        captured.Membership.Email.Should().Be("j@x.com", "the credential record mirrors the identity email");
        captured.Membership.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Assert: the plaintext was hashed exactly once and the entity was persisted exactly once.
        _hasher.Verify(h => h.Hash("Secret1!"), Times.Once);
        _repo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);

        // SECURITY: the plaintext password must NEVER leak onto the returned projection. This is a
        // structural guarantee — UserDto (and its nested membership projection) declares no credential
        // member — asserted here via reflection so the contract stays locked even if the DTO evolves.
        typeof(UserDto).GetProperty("Password").Should().BeNull("UserDto must not expose a password");
        dto.Membership.GetType().GetProperty("Password").Should()
           .BeNull("the membership projection must not expose a password");
    }

    // ---------------------------------------------------------------------------------------------
    // UpdateAsync: the Approved state transition is applied MANUALLY by the service (never by the
    // mapper) and only when the client actually supplies a value (AAP agent_prompt Phase 4).
    // MIGRATION: parity with UserController.UpdateUser (L963) — approval is a membership state flag.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_applies_Approved_when_HasValue()
    {
        // Existing user starts NOT approved; the update supplies Approved=true.
        var existing = MakeUser(5, "existing");
        existing.Membership.Approved = false;
        _repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        User? captured = null;
        _repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
             .Callback<User, CancellationToken>((u, _) => captured = u)
             .Returns(Task.CompletedTask);

        var update = new UpdateUserDto
        {
            Approved = true,
            FirstName = "Edited",
            LastName = "User",
            Email = "edited@x.com"
        };

        var result = await _sut.UpdateAsync(5, update);

        result.Should().NotBeNull();
        captured.Should().NotBeNull();
        captured!.Membership.Approved.Should().BeTrue("the service applies dto.Approved when it HasValue");
        _repo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_leaves_Approved_unchanged_when_null()
    {
        // Existing user is already approved; the update omits Approved (null) -> the guard skips it.
        var existing = MakeUser(6, "existing");
        existing.Membership.Approved = true;
        _repo.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        User? captured = null;
        _repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
             .Callback<User, CancellationToken>((u, _) => captured = u)
             .Returns(Task.CompletedTask);

        var update = new UpdateUserDto
        {
            Approved = null,
            FirstName = "Edited",
            LastName = "User",
            Email = "edited@x.com"
        };

        var result = await _sut.UpdateAsync(6, update);

        result.Should().NotBeNull();
        captured.Should().NotBeNull();
        captured!.Membership.Approved.Should()
                .BeTrue("the `if (dto.Approved.HasValue)` guard must skip the assignment when Approved is null");
        _repo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_not_found_returns_null_and_skips_update()
    {
        _repo.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var update = new UpdateUserDto { FirstName = "Edited", LastName = "User", Email = "edited@x.com" };

        var result = await _sut.UpdateAsync(404, update);

        result.Should().BeNull();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------------------------
    // DeleteAsync: delete-by-id when found, no-op when missing (AAP agent_prompt Phase 5).
    // MIGRATION: parity with UserController.DeleteUser (L200); the legacy cascade/notify/cache
    // side-effects are dropped — only the user record is removed. Note IRepository.DeleteAsync
    // takes the integer id (not the entity).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_found_returns_true_and_deletes()
    {
        _repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(MakeUser(5, "victim"));
        _repo.Setup(r => r.DeleteAsync(5, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.DeleteAsync(5);

        result.Should().BeTrue();
        _repo.Verify(r => r.DeleteAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_not_found_returns_false_and_skips_delete()
    {
        _repo.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _sut.DeleteAsync(404);

        result.Should().BeFalse();
        _repo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------------------------
    // ChangePasswordAsync: all four branches (AAP agent_prompt Phase 6).
    // MIGRATION: parity with UserController.ChangePassword (L103). The legacy provider verified the
    // old password internally and the caller validated the new one (throwing "Invalid Password");
    // here an empty/whitespace new password and an old-password mismatch both return false (a
    // documented throw->return-false divergence), and success stamps the new hash + UpdatePassword=false
    // + LastPasswordChangeDate.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ChangePasswordAsync_not_found_returns_false()
    {
        _repo.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _sut.ChangePasswordAsync(404, new ChangePasswordDto { OldPassword = "old", NewPassword = "NewPass1!" });

        result.Should().BeFalse();
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_whitespace_new_password_returns_false()
    {
        // The whitespace guard runs BEFORE the old-password verification; asserting Verify is never
        // called locks that ordering (a regression that reordered the checks would fail here).
        _repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(MakeUser(5, "user"));

        var result = await _sut.ChangePasswordAsync(5, new ChangePasswordDto { OldPassword = "old", NewPassword = "  " });

        result.Should().BeFalse();
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_wrong_old_password_returns_false()
    {
        var user = MakeUser(5, "user");
        user.Membership.Password = "STORED";
        _repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrongOld", "STORED")).Returns(false);

        var result = await _sut.ChangePasswordAsync(5, new ChangePasswordDto { OldPassword = "wrongOld", NewPassword = "NewPass1!" });

        result.Should().BeFalse();
        // A failed verification must short-circuit before hashing or persisting the new password.
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_success_sets_new_hash_and_flags()
    {
        var user = MakeUser(5, "user");
        user.Membership.Password = "STORED";
        _repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("goodOld", "STORED")).Returns(true);
        _hasher.Setup(h => h.Hash("NewPass1!")).Returns("NEWHASH");

        User? captured = null;
        _repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
             .Callback<User, CancellationToken>((u, _) => captured = u)
             .Returns(Task.CompletedTask);

        var result = await _sut.ChangePasswordAsync(5, new ChangePasswordDto { OldPassword = "goodOld", NewPassword = "NewPass1!" });

        result.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Membership.Password.Should().Be("NEWHASH", "the verified change stores the newly hashed password");
        captured.Membership.UpdatePassword.Should().BeFalse("a completed change clears the force-change flag");
        captured.Membership.LastPasswordChangeDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        _hasher.Verify(h => h.Verify("goodOld", "STORED"), Times.Once);
        _hasher.Verify(h => h.Hash("NewPass1!"), Times.Once);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------------------------------
    // SearchAsync
    // MIGRATION: the legacy Users.ascx "search-by-field" (Username/Email) and "All Fields" filter
    // (Website/admin/Users/**) -> AAP §0.7.2 server-side
    // GET /api/users?query=... | ?filterProperty=&filter= contract. The service is a thin,
    // portal-scoped pass-through to IUserRepository.SearchAsync that projects entities to DTOs.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_field_specific_forwards_all_arguments_and_projects_to_dtos()
    {
        // A field-specific search (filterProperty + filter) must forward the portal scope, the null
        // free-text query, and BOTH filter arguments verbatim, then project to UserDto via the real mapper.
        _repo.Setup(r => r.SearchAsync(0, null, "Username", "ali", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<User> { MakeUser(1, "alice") });

        var result = (await _sut.SearchAsync(0, null, "Username", "ali")).ToList();

        result.Should().ContainSingle().Which.Username.Should().Be("alice");
        _repo.Verify(r => r.SearchAsync(0, null, "Username", "ali", It.IsAny<CancellationToken>()), Times.Once,
            "the service must delegate the field-specific search to the repository with every argument intact");
    }

    [Fact]
    public async Task SearchAsync_free_text_forwards_query_with_null_field_filter()
    {
        // An "All Fields" search supplies only the free-text query; filterProperty/filter are null and
        // must be forwarded as null so the repository performs the multi-column match.
        _repo.Setup(r => r.SearchAsync(0, "smith", null, null, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<User> { MakeUser(2, "bsmith"), MakeUser(3, "csmith") });

        var result = (await _sut.SearchAsync(0, "smith", null, null)).ToList();

        result.Should().HaveCount(2);
        result.Select(u => u.Username).Should().BeEquivalentTo(new[] { "bsmith", "csmith" });
        _repo.Verify(r => r.SearchAsync(0, "smith", null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_with_null_portal_scope_forwards_null_for_host_superuser()
    {
        // A HOST superuser lists across all portals: portalId is null and must be forwarded verbatim.
        _repo.Setup(r => r.SearchAsync(null, "admin", null, null, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<User> { MakeUser(9, "admin") });

        var result = (await _sut.SearchAsync(null, "admin", null, null)).ToList();

        result.Should().ContainSingle().Which.Username.Should().Be("admin");
        _repo.Verify(r => r.SearchAsync(null, "admin", null, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
