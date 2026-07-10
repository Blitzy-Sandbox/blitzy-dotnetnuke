// -----------------------------------------------------------------------------
//  RolesTabsValidationApiTests.cs
//
//  MIGRATION / QA REMEDIATION (finding D): Net-new xUnit integration tests that
//  reproduce and lock in the fix for the "Missing server-side validation on Roles
//  and Tabs" finding. Before the fix the Validators folder held only Module / Portal
//  / User validators, so POST /api/roles {"roleName":""} and POST /api/tabs
//  {"tabName":""} were accepted and returned 201 (a persisted resource with an empty
//  required name) instead of the expected 400 with field-level errors.
//
//  The fix added CreateRoleDtoValidator / UpdateRoleDtoValidator and
//  CreateTabDtoValidator / UpdateTabDtoValidator (RoleName / TabName NotEmpty). They
//  auto-register through the existing FluentValidation pipeline
//  (AddValidatorsFromAssembly(typeof(MappingProfile).Assembly) +
//  AddFluentValidationAutoValidation in Program.cs); combined with the controllers'
//  [ApiController] attribute an invalid payload is short-circuited into an RFC 7807
//  ProblemDetails 400 BEFORE the controller action / service runs.
//
//  These tests run the full DnnMigration.Api host in-process (WebApplicationFactory +
//  EF Core InMemory + the permissive test auth scheme, whose default identity is a
//  host/super administrator so the controller authorization gate is cleared and the
//  request reaches model validation). Each empty-name case asserts the 400 +
//  problem+json + the offending field key; a positive-contrast case confirms a
//  well-shaped payload clears validation and reaches the service (201 Created), so the
//  validators gate SHAPE only.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end request-validation tests for the Roles (<c>POST /api/roles</c>) and Tabs
/// (<c>POST /api/tabs</c>) endpoints, exercised against the full <c>DnnMigration.Api</c>
/// host booted in-memory by <see cref="CustomWebApplicationFactory"/> (QA finding D).
/// </summary>
/// <remarks>
/// The 400 outcomes asserted here are produced by the <c>[ApiController]</c> automatic
/// model-state short-circuit fed by FluentValidation auto-validation, so they are decided
/// BEFORE the controller action executes and are independent of the (empty) InMemory
/// database. The factory's default host/super identity clears each controller's
/// <c>PortalAdministrator</c> authorization gate so the request reaches model validation.
/// </remarks>
public class RolesTabsValidationApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initializes the test class with the shared in-memory API host.</summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public RolesTabsValidationApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // =========================================================================================
    //  ROLES — POST /api/roles (CreateRoleDtoValidator)
    // =========================================================================================

    [Fact] // QA finding D repro step 1: POST /api/roles {"roleName":""} -> expected 400 (was 201).
    public async Task PostRole_with_empty_roleName_returns_400_problem_details()
    {
        // portalId defaults to 0 (a valid, non-negative identifier), so ONLY the RoleName NotEmpty
        // rule fires — matching the QA reproduction payload exactly.
        var response = await _client.PostAsJsonAsync("/api/roles", new { roleName = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var errors = await ReadValidationErrorsAsync(response);
        errors.Should().ContainKey("RoleName");
    }

    [Fact] // Positive contrast: a well-shaped role payload clears validation and is created (201).
    public async Task PostRole_with_valid_shape_passes_validation_and_returns_201()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/roles",
            new { portalId = 0, roleName = "QA D Role " + Guid.NewGuid().ToString("N") });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // =========================================================================================
    //  ROLES - per-portal name uniqueness (QA R10 Issue 12) and schema max-length (QA R10 Issue 14)
    // =========================================================================================

    [Fact] // QA R10 Issue 12: a second role with the same name in the same portal must be rejected 409 (was 201).
    public async Task PostRole_with_duplicate_name_in_same_portal_returns_409_conflict()
    {
        // A name unique to THIS test so the shared class InMemory store starts without it. The first create
        // persists it (portalId 0); the second create with the identical name + portal must be rejected by the
        // RoleService per-portal uniqueness guard (ConflictException -> 409), not persisted a second time.
        var roleName = "QA R10 Dup Role " + Guid.NewGuid().ToString("N");

        var first = await _client.PostAsJsonAsync("/api/roles", new { portalId = 0, roleName });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/roles", new { portalId = 0, roleName });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        // The 409 title/detail is the RoleService business message, confirming the duplicate-name path (not some
        // other conflict). ExceptionHandlingMiddleware surfaces ConflictException.Message as the RFC 7807 title.
        var body = await second.Content.ReadAsStringAsync();
        body.Should().Contain("already exists");
    }

    [Fact] // QA R10 Issue 14: a role name longer than the schema column width (nvarchar(50)) must be rejected 400 (was 201).
    public async Task PostRole_with_name_exceeding_50_chars_returns_400_problem_details()
    {
        // 550 chars mirrors the QA reproduction. The CreateRoleDtoValidator MaximumLength(50) rule fires and the
        // [ApiController] model-state short-circuit returns an RFC 7807 400 carrying the RoleName field key BEFORE
        // the controller action / service runs.
        var overlong = new string('R', 550);

        var response = await _client.PostAsJsonAsync("/api/roles", new { portalId = 0, roleName = overlong });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var errors = await ReadValidationErrorsAsync(response);
        errors.Should().ContainKey("RoleName");
    }

    // =========================================================================================
    //  TABS — POST /api/tabs (CreateTabDtoValidator)
    // =========================================================================================

    [Fact] // QA finding D repro step 2: POST /api/tabs {"tabName":""} -> expected 400 (was 201).
    public async Task PostTab_with_empty_tabName_returns_400_problem_details()
    {
        // portalId defaults to 0 (a valid, non-negative identifier), so ONLY the TabName NotEmpty
        // rule fires — matching the QA reproduction payload exactly.
        var response = await _client.PostAsJsonAsync("/api/tabs", new { tabName = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var errors = await ReadValidationErrorsAsync(response);
        errors.Should().ContainKey("TabName");
    }

    [Fact] // Positive contrast: a well-shaped tab payload clears validation and is created (201).
    public async Task PostTab_with_valid_shape_passes_validation_and_returns_201()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tabs",
            new
            {
                portalId = 0,
                tabName = "QA D Tab " + Guid.NewGuid().ToString("N"),
                parentId = 0,
                tabOrder = 1,
                isVisible = true
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // =========================================================================================
    //  Helper — read the RFC 7807 ValidationProblemDetails "errors" dictionary case-insensitively.
    // =========================================================================================

    /// <summary>
    /// Parses the RFC 7807 problem-details body and returns its <c>errors</c> member as a
    /// case-insensitive dictionary of field name -&gt; messages, so assertions are robust to the
    /// JSON property-naming policy applied to the dictionary keys.
    /// </summary>
    private static async Task<IDictionary<string, string[]>> ReadValidationErrorsAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ValidationProblem>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Errors.Should().NotBeNull().And.NotBeEmpty();

        return new Dictionary<string, string[]>(payload.Errors!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Minimal projection of the RFC 7807 validation problem-details envelope.</summary>
    private sealed record ValidationProblem
    {
        public int Status { get; init; }
        public Dictionary<string, string[]>? Errors { get; init; }
    }
}
