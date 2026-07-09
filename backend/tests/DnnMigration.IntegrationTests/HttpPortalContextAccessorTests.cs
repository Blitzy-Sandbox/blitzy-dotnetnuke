// MIGRATION (finding F2): net-new tests for the host/alias-aware ambient-portal accessor introduced at the
// API edge. The legacy DotNetNuke stack resolved the portal from the request host via the PortalAlias table
// (PortalAliasController.GetPortalAliasInfo / Globals.GetPortalId); the modern stack re-homes that resolution
// to HttpPortalContextAccessor (DnnMigration.Api.Identity), which reads the request host from
// IHttpContextAccessor and maps it to a PortalID through IPortalRepository.GetByAliasAsync. These tests pin
// the security-critical behaviour the fix restores: a host that maps to a portal yields that portal id
// (which AuthService then treats as authoritative and cross-checks against any client-supplied PortalId),
// an unmapped host yields null (caller falls back to the request-supplied id, legacy behaviour), no active
// request yields null, and the resolution is memoized once per request.
using DnnMigration.Api.Identity;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// Focused unit-style tests for <see cref="HttpPortalContextAccessor"/>. They exercise the accessor directly
/// against a hand-built <see cref="DefaultHttpContext"/> and a lightweight fake repository (no Moq, no HTTP
/// host required), keeping this integration-tier project dependency-free while still validating the F2 fix.
/// </summary>
public class HttpPortalContextAccessorTests
{
    private const string MappedHost = "portal7.example.com";
    private const int MappedPortalId = 7;

    [Fact]
    public void GetPortalId_WhenRequestHostMatchesAnAlias_ReturnsTheMappedPortalId()
    {
        var repository = new FakePortalRepository(MappedHost, MappedPortalId);
        var sut = CreateSut(repository, MappedHost);

        var portalId = sut.GetPortalId();

        portalId.Should().Be(MappedPortalId);
        repository.GetByAliasCallCount.Should().Be(1);
        repository.LastRequestedAlias.Should().Be(MappedHost);
    }

    [Fact]
    public void GetPortalId_WhenRequestHostMatchesAliasCaseInsensitively_ReturnsTheMappedPortalId()
    {
        var repository = new FakePortalRepository(MappedHost, MappedPortalId);
        var sut = CreateSut(repository, MappedHost.ToUpperInvariant());

        var portalId = sut.GetPortalId();

        portalId.Should().Be(MappedPortalId);
    }

    [Fact]
    public void GetPortalId_WhenRequestHostMatchesNoAlias_ReturnsNull()
    {
        var repository = new FakePortalRepository(MappedHost, MappedPortalId);
        var sut = CreateSut(repository, "unknown-host.example.org");

        var portalId = sut.GetPortalId();

        portalId.Should().BeNull();
    }

    [Fact]
    public void GetPortalId_WhenNoActiveHttpContext_ReturnsNullWithoutRepositoryLookup()
    {
        var repository = new FakePortalRepository(MappedHost, MappedPortalId);
        var sut = CreateSut(repository, host: null);

        var portalId = sut.GetPortalId();

        portalId.Should().BeNull();
        repository.GetByAliasCallCount.Should().Be(0, "no request means there is no host to resolve");
    }

    [Fact]
    public void GetPortalId_IsMemoizedForTheLifetimeOfTheRequest()
    {
        var repository = new FakePortalRepository(MappedHost, MappedPortalId);
        var sut = CreateSut(repository, MappedHost);

        var first = sut.GetPortalId();
        var second = sut.GetPortalId();

        first.Should().Be(MappedPortalId);
        second.Should().Be(MappedPortalId);
        repository.GetByAliasCallCount.Should().Be(1, "the host->portal resolution must run at most once per request");
    }

    /// <summary>
    /// Builds the accessor over a real <see cref="HttpContextAccessor"/>. When <paramref name="host"/> is
    /// non-null a <see cref="DefaultHttpContext"/> carrying that request host is attached; when null the
    /// accessor observes no active request.
    /// </summary>
    private static HttpPortalContextAccessor CreateSut(FakePortalRepository repository, string? host)
    {
        var httpContextAccessor = new HttpContextAccessor();
        if (host is not null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString(host);
            httpContextAccessor.HttpContext = httpContext;
        }

        return new HttpPortalContextAccessor(
            httpContextAccessor,
            repository,
            NullLogger<HttpPortalContextAccessor>.Instance);
    }

    /// <summary>
    /// Minimal <see cref="IPortalRepository"/> test double. Only <see cref="GetByAliasAsync"/> is exercised by
    /// the accessor; the remaining CRUD surface throws so an unexpected call is caught loudly. Records the
    /// alias lookup count and the last requested alias so the tests can assert on memoization and casing.
    /// </summary>
    private sealed class FakePortalRepository : IPortalRepository
    {
        private readonly string _knownAlias;
        private readonly int _knownPortalId;

        public FakePortalRepository(string knownAlias, int knownPortalId)
        {
            _knownAlias = knownAlias;
            _knownPortalId = knownPortalId;
        }

        public int GetByAliasCallCount { get; private set; }

        public string? LastRequestedAlias { get; private set; }

        public Task<Portal?> GetByAliasAsync(string httpAlias, CancellationToken cancellationToken = default)
        {
            GetByAliasCallCount++;
            LastRequestedAlias = httpAlias;
            var matches = string.Equals(httpAlias, _knownAlias, StringComparison.OrdinalIgnoreCase);
            return Task.FromResult<Portal?>(matches ? new Portal { PortalID = _knownPortalId } : null);
        }

        // Unused CRUD surface — not reached by HttpPortalContextAccessor.
        public Task<Portal?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<Portal>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Portal> AddAsync(Portal entity, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(Portal entity, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        // Unused search / alias read surface — not reached by HttpPortalContextAccessor.
        public Task<IEnumerable<Portal>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetAliasesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAliasesForPortalAsync(int portalId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        // This fake exercises only GetByAliasAsync (portal-context resolution); the alias WRITE port is not
        // used by these tests, so it is stubbed like the other unused members.
        public Task<PortalAlias> AddAliasAsync(PortalAlias alias, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
