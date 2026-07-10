namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Port exposing the TRUSTED ambient portal for the current request — the portal id derived server-side
/// from the request host / portal alias, never from client-supplied input. Consumed by <c>AuthService</c>
/// to scope authentication to the correct portal, and implemented in
/// <c>DnnMigration.Infrastructure.Identity</c> (dependency inversion keeps the Application layer free of
/// ASP.NET Core / <c>HttpContext</c> references).
/// </summary>
/// <remarks>
/// MIGRATION: replaces the legacy DotNetNuke portal resolution performed by
/// <c>PortalAliasController.GetPortalAliasInfo</c> / <c>Globals.GetPortalId</c>, which mapped the
/// inbound host header (e.g. <c>www.contoso.com</c>) to a <c>PortalID</c> via the <c>PortalAlias</c>
/// table. In the modern stack that host-to-portal mapping is a request-context concern resolved at the
/// edge (host/alias), so the Application layer consumes it through this abstraction rather than reading
/// <c>HttpContext</c> directly.
/// <para>
/// SECURITY (finding F3): the login and current-user flows must NOT trust a client-provided portal id.
/// The ambient portal returned here is authoritative; when it is available it overrides (and is
/// cross-checked against) any <c>PortalId</c> the client sends, closing the portal-scoping hole where a
/// caller could authenticate against, or enumerate, an arbitrary portal by passing its id.
/// </para>
/// <para>
/// DOWNSTREAM WIRING NOTE (CP4 / Api composition root, recorded here because this layer cannot create
/// <c>Program.cs</c>): the concrete host-aware implementation belongs in the Api layer (it needs
/// <c>IHttpContextAccessor</c> + the <c>PortalAlias</c> lookup) and should be registered as SCOPED —
/// <c>builder.Services.AddScoped&lt;IPortalContextAccessor, HttpPortalContextAccessor&gt;();</c>. The
/// Infrastructure default (<c>PortalContextAccessor</c>) returns <c>null</c> (no ambient portal), which
/// preserves the legacy behaviour of honouring the request-supplied portal id when no host mapping is
/// wired, and keeps Infrastructure free of an ASP.NET Core dependency.
/// </para>
/// </remarks>
public interface IPortalContextAccessor
{
    /// <summary>
    /// Returns the trusted portal id for the current request as resolved from the request host / portal
    /// alias, or <c>null</c> when no ambient portal can be determined (in which case the caller falls
    /// back to the request-supplied portal id).
    /// </summary>
    /// <returns>The server-derived portal id, or <c>null</c> when unavailable.</returns>
    int? GetPortalId();
}
