using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Api.Identity;

/// <summary>
/// Authoritative, host/alias-aware implementation of <see cref="IPortalContextAccessor"/>. It derives the
/// TRUSTED ambient portal for the current request from the request host header by looking that host up in
/// the <c>PortalAlias</c> table (via <see cref="IPortalRepository.GetByAliasAsync"/>), never from
/// client-supplied input.
/// </summary>
/// <remarks>
/// MIGRATION (finding F2): replaces the legacy DotNetNuke host-to-portal resolution
/// (<c>PortalAliasController.GetPortalAliasInfo</c> / <c>Globals.GetPortalId</c>), which mapped the inbound
/// host header (e.g. <c>www.contoso.com</c>) to a <c>PortalID</c> through the <c>PortalAlias</c> table. This
/// concrete accessor lives in the Api layer because resolving the portal from the request requires
/// <see cref="IHttpContextAccessor"/> plus the alias lookup; the Application/Infrastructure layers stay free
/// of an ASP.NET Core dependency (Clean/Onion). It is registered as SCOPED in <c>Program.cs</c>, overriding
/// the Infrastructure <c>PortalContextAccessor</c> null-object default.
/// <para>
/// SECURITY: when this accessor resolves an ambient portal, <c>AuthService</c> treats it as authoritative
/// and REJECTS any request that carries a different client-supplied <c>PortalId</c> — closing the
/// portal-scoping hole where a caller could authenticate against, or enumerate, an arbitrary portal by
/// passing its id. When no host mapping is found the accessor returns <c>null</c>, and the caller falls back
/// to the request-supplied portal id, preserving legacy behaviour.
/// </para>
/// </remarks>
public sealed class HttpPortalContextAccessor : IPortalContextAccessor
{
    // Per-request memoization key: the host->portal resolution runs at most once per HTTP request. Stored in
    // HttpContext.Items so the (possibly-null) result is cached for the lifetime of the request without a
    // repeated database round-trip.
    private const string ResolvedPortalItemKey = "__DnnMigration.ResolvedAmbientPortalId";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPortalRepository _portalRepository;
    private readonly ILogger<HttpPortalContextAccessor> _logger;

    /// <summary>
    /// Creates the accessor. All dependencies are injected by the DI container; the accessor is registered
    /// scoped so <paramref name="portalRepository"/> (also scoped) is resolved within the same request scope.
    /// </summary>
    public HttpPortalContextAccessor(
        IHttpContextAccessor httpContextAccessor,
        IPortalRepository portalRepository,
        ILogger<HttpPortalContextAccessor> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _portalRepository = portalRepository ?? throw new ArgumentNullException(nameof(portalRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int? GetPortalId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            // No active request (e.g. resolved outside the request pipeline): no ambient portal.
            return null;
        }

        // Return the memoized result if this request already resolved the ambient portal (including a
        // memoized null, so an unmapped host does not trigger a repeated lookup).
        if (httpContext.Items.TryGetValue(ResolvedPortalItemKey, out var cached))
        {
            return (int?)cached;
        }

        var resolved = ResolveFromHost(httpContext);
        httpContext.Items[ResolvedPortalItemKey] = resolved;
        return resolved;
    }

    /// <summary>
    /// Resolves the portal id from the request host header, or <c>null</c> when the host is absent or maps to
    /// no portal alias.
    /// </summary>
    private int? ResolveFromHost(HttpContext httpContext)
    {
        var host = httpContext.Request.Host;
        if (!host.HasValue)
        {
            return null;
        }

        // Match against PortalAlias.HTTPAlias using the host exactly as received (host[:port]), mirroring how
        // DNN stored aliases. GetByAliasAsync performs the PortalAlias -> Portal join.
        var httpAlias = host.Value;
        if (string.IsNullOrWhiteSpace(httpAlias))
        {
            return null;
        }

        try
        {
            // IPortalContextAccessor.GetPortalId() is synchronous (AuthService reads it inline while building
            // the authentication result), but the alias lookup is async. Blocking here is safe: ASP.NET Core
            // has no SynchronizationContext (so there is no sync-over-async deadlock), the lookup runs only on
            // the rate-limited authentication path, and it executes at most once per request (memoized by the
            // caller). ConfigureAwait(false) is belt-and-suspenders.
            var portal = _portalRepository
                .GetByAliasAsync(httpAlias, CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            return portal?.PortalID;
        }
        catch (Exception ex)
        {
            // Never let ambient-portal resolution break the request. Log and fall back to "no ambient portal"
            // (null), which makes AuthService honour the request-supplied portal id (legacy behaviour).
            _logger.LogWarning(ex, "Failed to resolve ambient portal from request host {HttpAlias}.", httpAlias);
            return null;
        }
    }
}
