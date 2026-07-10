using DnnMigration.Application.Interfaces;

// MIGRATION / DOWNSTREAM WIRING NOTE (this file cannot reference ASP.NET Core or create Program.cs;
// recorded here so the Api agents wire trusted portal resolution correctly):
//   * The AUTHORITATIVE, host/alias-aware implementation of IPortalContextAccessor belongs in the Api
//     layer, because resolving the portal from the request host requires IHttpContextAccessor plus a
//     PortalAlias lookup (legacy PortalAliasController.GetPortalAliasInfo / Globals.GetPortalId). The
//     Infrastructure layer must not depend on ASP.NET Core (Clean/Onion), so that concrete type cannot
//     live here.
//   * Api Program.cs SHOULD register the host-aware accessor as SCOPED, overriding this default:
//         builder.Services.AddScoped<IPortalContextAccessor, HttpPortalContextAccessor>();
//     where HttpPortalContextAccessor reads HttpContext.Request.Host, maps it to a PortalID via the
//     PortalAlias table, and returns that id.
//   * This Infrastructure default is the SAFE fallback used until (or where) the host-aware accessor is
//     wired: it reports "no ambient portal" (null), which makes AuthService fall back to the
//     request-supplied portal id — preserving legacy behaviour without ever letting a client override a
//     KNOWN trusted portal. See MIGRATION_NOTES.md.

namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// Default, host-agnostic implementation of <see cref="IPortalContextAccessor"/> that reports no ambient
/// portal (<c>null</c>). It exists so the Application layer can depend on the trusted-portal seam without
/// forcing an ASP.NET Core dependency into Infrastructure; the authoritative host/alias-derived accessor
/// is supplied by the Api composition root (see the file header) and overrides this registration.
/// </summary>
/// <remarks>
/// MIGRATION: replaces nothing on its own — it is the null-object fallback for the legacy DNN portal
/// resolution (host header to <c>PortalID</c> via the <c>PortalAlias</c> table), which is re-homed to the
/// Api edge in the modern stack. Returning <c>null</c> makes <c>AuthService</c> honour the
/// request-supplied portal id, matching legacy behaviour, while still refusing any client attempt to
/// override a portal the host-aware accessor has already resolved (when that accessor is wired).
/// </remarks>
public sealed class PortalContextAccessor : IPortalContextAccessor
{
    /// <inheritdoc />
    public int? GetPortalId() => null;
}
