// -----------------------------------------------------------------------------
//  OptionalStringOmittedRegressionTests.cs
//
//  MIGRATION / QA REGRESSION: Net-new xUnit integration tests that lock in the fix
//  for QA finding C1 ("HTTP 500 when an optional string field is omitted on
//  create/update"). The QA report's "Areas of Concern" explicitly noted that the
//  pre-existing CRUD tests MASK the defect because they always send every optional
//  string (ModuleApiTests sends alignment/color/border/... as empty strings and
//  UserApiTests sends a non-null displayName), so a request that OMITS those fields
//  entirely was never exercised end-to-end.
//
//  Root cause of C1 (now fixed at HEAD): the optional string properties on the
//  Module/Role/Tab/Portal domain entities were declared as NON-nullable reference
//  types (string = string.Empty). EF Core treats a non-nullable reference property
//  as REQUIRED, and AutoMapper convention-mapped a null DTO value over the entity's
//  string.Empty default, so SaveChanges threw a "Required properties are missing"
//  DbUpdateException that surfaced as HTTP 500. The fix made the genuinely-NULLable
//  columns nullable reference types (string?) and, for the two columns that are
//  NOT NULL in the existing schema (Users.DisplayName, Portals.DefaultLanguage /
//  Portals.HomeDirectory), guarded them (UserService derives DisplayName; the
//  AutoMapper profile applies NullSubstitute(string.Empty)).
//
//  These tests send DELIBERATELY MINIMAL payloads that OMIT every optional string
//  field and assert the fixed outcome: create -> 201, update -> 200 (never 500).
//  They run against the same in-process host as the sibling CRUD tests
//  (CustomWebApplicationFactory: full DnnMigration.Api pipeline + EF Core InMemory +
//  the permissive test authentication scheme), so they exercise the real controller
//  -> service -> AutoMapper -> repository -> EF Core SaveChanges path over HTTP.
//
//  NOTE on the EF Core InMemory provider: it performs the SAME required-property
//  validation as a relational provider (it throws the identical "Required properties
//  are missing" DbUpdateException on SaveChanges when a required/non-nullable
//  reference property is null), so the create-side regressions (Module/Role/Tab/User)
//  reproduce here without a real SQL Server. The Portal update regression additionally
//  covers the NullSubstitute guard for the NOT NULL DefaultLanguage/HomeDirectory
//  columns.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// Regression tests for QA finding C1: creating a Module/Role/Tab/User or updating a Portal
/// while OMITTING the optional string fields must succeed (201/200), not fail with HTTP 500.
/// </summary>
/// <remarks>
/// Shares one <see cref="CustomWebApplicationFactory"/> instance (and therefore one uniquely
/// named EF Core InMemory database) across the class via <see cref="IClassFixture{TFixture}"/>.
/// The factory's default test authentication scheme satisfies each resource controller's
/// class-level authorization, so the plain <see cref="System.Net.Http.HttpClient"/> reaches the
/// create/update endpoints without a real login round-trip.
/// </remarks>
public class OptionalStringOmittedRegressionTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>
    /// The in-process HTTP client bound to the shared test host. Authenticated by the factory's
    /// default test scheme so the secured CRUD endpoints are reachable without a token.
    /// </summary>
    private readonly HttpClient _client;

    /// <summary>
    /// JSON options mirroring the API host: camelCase-insensitive binding plus the string enum
    /// converter, so response payloads (which may carry string-encoded enums) deserialize cleanly.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OptionalStringOmittedRegressionTests"/> class.
    /// </summary>
    /// <param name="factory">The shared factory that boots the API host with an InMemory database.</param>
    public OptionalStringOmittedRegressionTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// C1 (Module create): POST /api/modules with every optional string field OMITTED
    /// (Alignment, Color, Border, IconFile, Header, Footer, ContainerSrc) must return 201, not 500.
    /// </summary>
    /// <returns>A task that completes when the create assertion has passed.</returns>
    [Fact]
    public async Task CreateModule_WithOptionalStringsOmitted_Returns201()
    {
        // Only the fields REQUIRED by CreateModuleDtoValidator are supplied: ModuleTitle (NotEmpty),
        // ModuleDefID > 0, TabID > 0, CacheTime >= 0, Visibility in 0..2, PortalID >= 0. Every optional
        // string (Alignment/Color/Border/IconFile/Header/Footer/ContainerSrc) — and the optional
        // PaneName — is intentionally omitted so the DTO binds them to null/default; before the C1 fix
        // this produced a "Required properties are missing" 500 on SaveChanges.
        var createBody = new
        {
            portalID = 0,
            tabID = 1,
            moduleDefID = 1,
            moduleTitle = "C1 Module (optional strings omitted)",
            moduleOrder = 1,
            cacheTime = 0,
            visibility = 0
        };

        var response = await _client.PostAsJsonAsync("/api/modules", createBody, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<Envelope<ModuleRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        created.Data!.ModuleID.Should().BeGreaterThan(0);
        created.Data.ModuleTitle.Should().Be("C1 Module (optional strings omitted)");
    }

    /// <summary>
    /// C1 (Role create): POST /api/roles with every optional string field OMITTED
    /// (Description, BillingFrequency, TrialFrequency, RSVPCode, IconFile) must return 201, not 500.
    /// </summary>
    /// <returns>A task that completes when the create assertion has passed.</returns>
    [Fact]
    public async Task CreateRole_WithOptionalStringsOmitted_Returns201()
    {
        // Roles have no FluentValidation validator (by AAP design), so only the required identity of
        // the role is supplied: RoleName (non-nullable) and PortalID. All optional strings are omitted.
        var createBody = new
        {
            portalID = 0,
            roleName = "C1 Role (optional strings omitted)"
        };

        var response = await _client.PostAsJsonAsync("/api/roles", createBody, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<Envelope<RoleRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        created.Data!.RoleID.Should().BeGreaterThan(0);
        created.Data.RoleName.Should().Be("C1 Role (optional strings omitted)");
    }

    /// <summary>
    /// C1 (Tab create): POST /api/tabs with every optional string field OMITTED
    /// (IconFile, Title, Description, KeyWords, Url, SkinSrc, ContainerSrc, PageHeadText) must
    /// return 201, not 500.
    /// </summary>
    /// <returns>A task that completes when the create assertion has passed.</returns>
    [Fact]
    public async Task CreateTab_WithOptionalStringsOmitted_Returns201()
    {
        // Tabs have no FluentValidation validator (by AAP design). Only the tab's required identity is
        // supplied: TabName (non-nullable), PortalID, ParentId, TabOrder, IsVisible. The non-nullable
        // entity columns NOT present on the DTO (TabPath/AuthorizedRoles/AdministratorRoles) keep the
        // entity's string.Empty default because AutoMapper never touches them; every OPTIONAL string is
        // omitted so it binds to null.
        var createBody = new
        {
            portalID = 0,
            tabName = "C1 Tab (optional strings omitted)",
            parentId = 0,
            tabOrder = 1,
            isVisible = true
        };

        var response = await _client.PostAsJsonAsync("/api/tabs", createBody, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<Envelope<TabRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        created.Data!.TabID.Should().BeGreaterThan(0);
        created.Data.TabName.Should().Be("C1 Tab (optional strings omitted)");
    }

    /// <summary>
    /// C1 (User create, NOT NULL column guard): POST /api/users with the optional DisplayName OMITTED
    /// must return 201 (never 500) and the server must DERIVE the display name from first/last name.
    /// Users.DisplayName is NOT NULL in the existing schema, so the fix guards it in UserService rather
    /// than making it nullable.
    /// </summary>
    /// <returns>A task that completes when the create + derivation assertions have passed.</returns>
    [Fact]
    public async Task CreateUser_WithDisplayNameOmitted_Returns201_AndDerivesDisplayName()
    {
        // Supply exactly what CreateUserDtoValidator requires (username, first/last name, valid email,
        // a >= 7-char password with a matching confirm because randomPassword is false) and OMIT the
        // optional displayName. UserService derives "FirstName LastName" (mirroring the legacy
        // UserInfo.UpdateDisplayName) so the NOT NULL Users.DisplayName column is never written null.
        var createBody = new
        {
            username = "c1_user_omitted",
            firstName = "C1",
            lastName = "Minimal",
            email = "c1.minimal@dnnmigration.local",
            password = "P@ssw0rd123",
            confirmPassword = "P@ssw0rd123",
            portalID = 0,
            authorize = true,
            notify = false,
            randomPassword = false
        };

        var response = await _client.PostAsJsonAsync("/api/users", createBody, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        created.Data!.UserID.Should().BeGreaterThan(0);
        created.Data.Username.Should().Be("c1_user_omitted");
        // The derived display name is "FirstName LastName" trimmed.
        created.Data.DisplayName.Should().Be("C1 Minimal");
    }

    /// <summary>
    /// C1 (Portal update): PUT /api/portals/{id} with every optional string field OMITTED
    /// (LogoFile, FooterText, Currency, PaymentProcessor, ProcessorUserId, ProcessorPassword,
    /// Description, KeyWords, BackgroundFile, DefaultLanguage, HomeDirectory) must return 200, not 500.
    /// This exercises both the nullable-column fix and the NullSubstitute guard for the NOT NULL
    /// DefaultLanguage/HomeDirectory columns.
    /// </summary>
    /// <returns>A task that completes when the create-then-minimal-update assertions have passed.</returns>
    [Fact]
    public async Task UpdatePortal_WithOptionalStringsOmitted_Returns200()
    {
        // First create a portal with a fully valid body (the create path requires an initial
        // administrator user + alias). A unique username/alias avoids any collision within the class's
        // shared InMemory store.
        var createBody = new
        {
            portalName = "C1 Portal (create)",
            firstName = "C1",
            lastName = "Admin",
            username = "c1_portal_admin",
            password = "P@ssw0rd123",
            email = "c1.portal.admin@dnnmigration.local",
            description = "Created by C1 regression",
            keyWords = "c1,regression",
            homeDirectory = "Portals/c1",
            portalAlias = "c1-portal.localtest.me"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/portals", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<PortalRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        var id = created.Data!.PortalID;
        id.Should().BeGreaterThan(0);

        // Minimal update: supply only what UpdatePortalDtoValidator requires (PortalName non-empty and
        // the non-negative numeric quotas/fee) and OMIT every optional string. Before the C1 fix this
        // produced HTTP 500 on SQL Server ("Required properties are missing" / a NOT NULL violation on
        // DefaultLanguage/HomeDirectory); the fix makes the genuinely-nullable columns nullable and
        // NullSubstitutes the two NOT NULL columns to string.Empty.
        var updateBody = new
        {
            portalName = "C1 Portal Updated (optional strings omitted)",
            hostFee = 0.0,
            hostSpace = 0,
            pageQuota = 0,
            userQuota = 0
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/portals/{id}", updateBody, JsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<Envelope<PortalRead>>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.Data.Should().NotBeNull();
        updated.Data!.PortalName.Should().Be("C1 Portal Updated (optional strings omitted)");
    }

    // -------------------------------------------------------------------------
    //  Test-only read-models. Each mirrors only the fields these tests assert on;
    //  reference members are nullable so the models never require non-null
    //  construction (no CS8618 under the Gate 1 warnings-as-errors build).
    // -------------------------------------------------------------------------

    /// <summary>The API success envelope <c>{ "data": ..., "meta": ... }</c>.</summary>
    /// <typeparam name="T">The payload type carried under the <c>data</c> key.</typeparam>
    private sealed class Envelope<T>
    {
        /// <summary>The response payload projected from the <c>data</c> key.</summary>
        public T? Data { get; set; }

        /// <summary>The raw response metadata from the <c>meta</c> key (not asserted on).</summary>
        public JsonElement Meta { get; set; }
    }

    /// <summary>Minimal module read model (<c>moduleID</c>/<c>moduleTitle</c>).</summary>
    private sealed class ModuleRead
    {
        /// <summary>The server-assigned module identifier.</summary>
        public int ModuleID { get; set; }

        /// <summary>The module display title.</summary>
        public string? ModuleTitle { get; set; }
    }

    /// <summary>Minimal role read model (<c>roleID</c>/<c>roleName</c>).</summary>
    private sealed class RoleRead
    {
        /// <summary>The server-assigned role identifier.</summary>
        public int RoleID { get; set; }

        /// <summary>The role display name.</summary>
        public string? RoleName { get; set; }
    }

    /// <summary>Minimal tab read model (<c>tabID</c>/<c>tabName</c>).</summary>
    private sealed class TabRead
    {
        /// <summary>The server-assigned tab identifier.</summary>
        public int TabID { get; set; }

        /// <summary>The tab name.</summary>
        public string? TabName { get; set; }
    }

    /// <summary>Minimal user read model (<c>userID</c>/<c>username</c>/<c>displayName</c>).</summary>
    private sealed class UserRead
    {
        /// <summary>The server-assigned user identifier.</summary>
        public int UserID { get; set; }

        /// <summary>The login name.</summary>
        public string? Username { get; set; }

        /// <summary>The (possibly server-derived) display name.</summary>
        public string? DisplayName { get; set; }
    }

    /// <summary>Minimal portal read model (<c>portalID</c>/<c>portalName</c>).</summary>
    private sealed class PortalRead
    {
        /// <summary>The server-assigned portal identifier.</summary>
        public int PortalID { get; set; }

        /// <summary>The portal display name.</summary>
        public string? PortalName { get; set; }
    }
}
