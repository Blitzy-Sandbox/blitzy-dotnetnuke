// -----------------------------------------------------------------------------
//  UsersPaginationApiTests.cs
//
//  MIGRATION (QA finding — R6 Issue 1, unbounded list endpoints): net-new xUnit
//  integration-test class proving the migrated list endpoints are now BOUNDED and
//  emit correct pagination metadata, end-to-end against the in-memory host.
//
//  The R6 report reproduced the defect by calling GET /api/users and observing the
//  ENTIRE user set streamed back with no server-side limit. Its "Expected Outcome"
//  accepts either true server-side pagination OR an enforced maximum row cap. These
//  tests exercise the /api/users list (chosen as the representative resource because
//  its repository performs the heaviest per-row hydration) and assert:
//    * a seed of 60 users yields a bounded default page of 50 (not all 60),
//    * meta carries { count, page, pageSize, totalCount, totalPages } with correct values,
//    * ?page=2 returns the remaining rows and reports page=2,
//    * an oversized ?pageSize= is clamped to the hard maximum (200),
//    * a small ?pageSize= is honoured, and
//    * pagination composes correctly with non-host portal scoping (scope BEFORE paging).
//
//  Like the sibling search tests, the class owns an isolated EF Core InMemory store
//  (IClassFixture) seeded through ResetAndSeedAsync, and the default client presents
//  the fixture's synthetic HOST super-user identity (so the unscoped host list path is
//  exercised); the non-host test overrides that via the X-Test-* headers documented on
//  CustomWebApplicationFactory.TestAuthHandler.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end integration tests for bounded server-side pagination of the user list (finding R6 Issue 1),
/// exercised against the in-memory <c>DnnMigration.Api</c> host.
/// </summary>
public class UsersPaginationApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersPaginationApiTests"/> class.
    /// </summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public UsersPaginationApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Seeds <paramref name="count"/> users into <paramref name="portalId"/> with contiguous ids starting at
    /// <paramref name="firstUserId"/>. Ids are deterministic so page boundaries can be asserted precisely.
    /// </summary>
    private Task SeedUsersAsync(int count, int portalId = 0, int firstUserId = 4001) =>
        _factory.ResetAndSeedAsync(async db =>
        {
            for (var i = 0; i < count; i++)
            {
                var id = firstUserId + i;
                db.Users.Add(new User
                {
                    UserID = id,
                    PortalID = portalId,
                    Username = $"user{id}",
                    Email = $"user{id}@contoso.example",
                    FirstName = "User",
                    LastName = id.ToString(),
                    DisplayName = $"User {id}"
                });
            }
            await Task.CompletedTask;
        });

    // -------------------------------------------------------------------------
    //  R6 Issue 1 — bounded default page + meta shape
    // -------------------------------------------------------------------------

    /// <summary>
    /// The core R6 Issue 1 reproduction: with 60 users seeded, an unqualified <c>GET /api/users</c> returns a
    /// BOUNDED default page of 50 (the default page size) rather than all 60, and the pagination meta reports
    /// the full total. This proves the endpoint can no longer stream an unbounded result set.
    /// </summary>
    [Fact]
    public async Task GetAll_DefaultPage_IsBoundedAndReportsMeta()
    {
        await SeedUsersAsync(60);

        var response = await _client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();

        // Bounded: exactly the default page size, strictly fewer than the 60 seeded rows.
        envelope.Data!.Should().HaveCount(PaginationParameters.DefaultPageSize);
        envelope.Data!.Count.Should().BeLessThan(60, "the default page must not return the entire table (R6 Issue 1)");

        // Meta contract: { count (this page), page, pageSize, totalCount (grand total), totalPages }.
        GetInt(envelope.Meta, "count").Should().Be(PaginationParameters.DefaultPageSize);
        GetInt(envelope.Meta, "page").Should().Be(PaginationParameters.DefaultPage);
        GetInt(envelope.Meta, "pageSize").Should().Be(PaginationParameters.DefaultPageSize);
        GetInt(envelope.Meta, "totalCount").Should().Be(60);
        GetInt(envelope.Meta, "totalPages").Should().Be(2, "ceil(60 / 50) == 2");
    }

    /// <summary>
    /// Requesting the second page returns the remaining rows (60 - 50 = 10) and reports <c>page = 2</c>, while
    /// <c>totalCount</c> stays the grand total. Confirms the <c>Skip</c>/<c>Take</c> window advances correctly.
    /// </summary>
    [Fact]
    public async Task GetAll_SecondPage_ReturnsRemainderAndReportsPage()
    {
        await SeedUsersAsync(60);

        var response = await _client.GetAsync("/api/users?page=2&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().HaveCount(10, "page 2 of a 60-row set at pageSize 50 holds the final 10 rows");

        GetInt(envelope.Meta, "page").Should().Be(2);
        GetInt(envelope.Meta, "pageSize").Should().Be(50);
        GetInt(envelope.Meta, "totalCount").Should().Be(60);
        GetInt(envelope.Meta, "count").Should().Be(10);
    }

    /// <summary>
    /// A non-overlapping first and second page prove the window truly moves (no duplicate rows across pages).
    /// </summary>
    [Fact]
    public async Task GetAll_FirstAndSecondPage_DoNotOverlap()
    {
        await SeedUsersAsync(60);

        var firstPage = await (await _client.GetAsync("/api/users?page=1&pageSize=50"))
            .Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        var secondPage = await (await _client.GetAsync("/api/users?page=2&pageSize=50"))
            .Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);

        var firstIds = firstPage!.Data!.Select(u => u.UserID).ToList();
        var secondIds = secondPage!.Data!.Select(u => u.UserID).ToList();

        firstIds.Should().HaveCount(50);
        secondIds.Should().HaveCount(10);
        firstIds.Should().NotIntersectWith(secondIds, "each page must return a distinct window of rows");
    }

    // -------------------------------------------------------------------------
    //  R6 Issue 1 — page-size clamp (the enforced maximum row cap)
    // -------------------------------------------------------------------------

    /// <summary>
    /// An oversized <c>?pageSize=</c> (far larger than the seeded set) is clamped to the hard maximum
    /// (<see cref="PaginationParameters.MaxPageSize"/>) — the enforced row cap that closes R6 Issue 1. The
    /// reported <c>pageSize</c> is the clamp value, not the caller-supplied one.
    /// </summary>
    [Fact]
    public async Task GetAll_OversizedPageSize_IsClampedToMaximum()
    {
        await SeedUsersAsync(60);

        var response = await _client.GetAsync("/api/users?pageSize=100000");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();

        // The effective page size is the hard cap, proving a caller cannot coerce an unbounded fetch.
        GetInt(envelope.Meta, "pageSize").Should().Be(PaginationParameters.MaxPageSize);
        // All 60 seeded rows fit within a single 200-row page, so the whole (bounded) set comes back here.
        envelope.Data!.Should().HaveCount(60);
        envelope.Data!.Count.Should().BeLessOrEqualTo(PaginationParameters.MaxPageSize);
        GetInt(envelope.Meta, "totalCount").Should().Be(60);
    }

    /// <summary>
    /// A small explicit <c>?pageSize=</c> is honoured verbatim, and <c>totalPages</c> is computed from it.
    /// </summary>
    [Fact]
    public async Task GetAll_SmallPageSize_IsHonoured()
    {
        await SeedUsersAsync(60);

        var response = await _client.GetAsync("/api/users?pageSize=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().HaveCount(5);

        GetInt(envelope.Meta, "pageSize").Should().Be(5);
        GetInt(envelope.Meta, "totalCount").Should().Be(60);
        GetInt(envelope.Meta, "totalPages").Should().Be(12, "ceil(60 / 5) == 12");
    }

    // -------------------------------------------------------------------------
    //  R6 Issue 1 — pagination composes with non-host portal scoping
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scoping is applied BEFORE pagination: a non-host caller confined to portal 7 sees only that portal's
    /// users, bounded to the default page, and <c>totalCount</c> reflects the scoped total (60) — never the
    /// cross-portal grand total (70). Every returned row belongs to the caller's portal.
    /// </summary>
    [Fact]
    public async Task GetAll_NonHostCaller_ScopesThenPaginates()
    {
        // 60 users in portal 7 (the caller's portal) + 10 in portal 0 (must never leak). SCHEMA FIDELITY:
        // portal membership is the [UserPortals] junction (there is no [Users].[PortalID] column), so each
        // seeded user needs a matching UserPortal row for the scoped GetByPortalPagedAsync join to see it —
        // exactly what UserRepository.AddAsync writes in production.
        await _factory.ResetAndSeedAsync(async db =>
        {
            for (var i = 0; i < 60; i++)
            {
                var id = 5001 + i;
                db.Users.Add(new User { UserID = id, PortalID = 7, Username = $"p7-{id}", Email = $"{id}@p7.example", DisplayName = $"P7 {id}" });
                db.UserPortals.Add(new UserPortal { UserId = id, PortalId = 7, Authorised = true, CreatedDate = DateTime.UtcNow });
            }
            for (var i = 0; i < 10; i++)
            {
                var id = 6001 + i;
                db.Users.Add(new User { UserID = id, PortalID = 0, Username = $"p0-{id}", Email = $"{id}@p0.example", DisplayName = $"P0 {id}" });
                db.UserPortals.Add(new UserPortal { UserId = id, PortalId = 0, Authorised = true, CreatedDate = DateTime.UtcNow });
            }
            await Task.CompletedTask;
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Add("X-Test-IsSuperUser", "false");
        request.Headers.Add("X-Test-PortalId", "7");
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();

        // Bounded to the default page, scoped strictly to portal 7, and total reflects the scoped set only.
        envelope.Data!.Should().HaveCount(PaginationParameters.DefaultPageSize);
        envelope.Data!.Should().OnlyContain(u => u.PortalID == 7, "a non-host caller must never see another portal's users");
        GetInt(envelope.Meta, "totalCount").Should().Be(60, "totalCount must be the scoped total (portal 7 only), not the cross-portal 70");
    }

    // -------------------------------------------------------------------------
    //  Local helpers + read-models (test-only shapes)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads an integer meta property by name from the raw <see cref="JsonElement"/>, tolerating any casing so
    /// the assertion is robust to the serializer's property-naming policy.
    /// </summary>
    private static int GetInt(JsonElement meta, string name)
    {
        if (meta.TryGetProperty(name, out var exact))
        {
            return exact.GetInt32();
        }

        foreach (var property in meta.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.GetInt32();
            }
        }

        throw new Xunit.Sdk.XunitException($"meta did not contain an integer property named '{name}'. Actual meta: {meta.GetRawText()}");
    }

    private sealed class Envelope<T>
    {
        public T? Data { get; set; }
        public JsonElement Meta { get; set; }
    }

    private sealed class UserRead
    {
        public int UserID { get; set; }
        public int PortalID { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
    }
}
