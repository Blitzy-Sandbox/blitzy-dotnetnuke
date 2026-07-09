using System.Globalization;
using System.Security.Claims;
using DnnMigration.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// Standard success envelope for all API responses: <c>{ "data": {...}, "meta": {...} }</c>.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: replaces the legacy Web Forms page-render output (ViewState/postback HTML) with a
/// uniform JSON contract mandated by AAP §0.7.2 ("API response standards — preserve exactly").
/// Every successful response emitted by a controller deriving from <see cref="ApiControllerBase"/>
/// is shaped as this envelope so that clients (the Angular SPA and any other consumer) receive a
/// predictable structure.
/// </para>
/// <para>
/// The property names are intentionally PascalCase (<c>Data</c>/<c>Meta</c>). <c>Program.cs</c>
/// configures <c>System.Text.Json</c> with the camelCase naming policy, so these serialize to the
/// lowercase keys <c>data</c>/<c>meta</c> automatically. No <c>[JsonPropertyName]</c> attributes are
/// applied here on purpose: it keeps this file free of any <c>System.Text.Json</c> dependency and
/// avoids duplicating the globally configured naming behaviour.
/// </para>
/// <para>
/// The record is <see langword="sealed"/> and immutable (positional/value semantics) because a
/// response envelope is a transient data-transfer shape that should never be mutated after
/// construction. Error responses are NOT wrapped in this envelope — those are produced centrally as
/// RFC 7807 Problem Details by the exception-handling middleware.
/// </para>
/// </remarks>
/// <typeparam name="T">The payload type — a DTO or a collection of DTOs. EF entities are never used here.</typeparam>
/// <param name="Data">The response payload (the projected DTO or DTO collection).</param>
/// <param name="Meta">
/// Response metadata. Typed as <see cref="object"/> so callers can supply a small anonymous object
/// (for example <c>new { count = 5 }</c>) or the default meta produced by the base controller.
/// It is never <see langword="null"/>: <see cref="ApiControllerBase"/> always substitutes a default
/// meta object so the serialized <c>meta</c> key is always present.
/// </param>
public sealed record ApiResponse<T>(T Data, object Meta);

/// <summary>
/// Abstract base class for every API controller in the DnnMigration BFF. It centralises the
/// success-response envelope shaping (<c>{ "data": ..., "meta": ... }</c>) so that concrete
/// controllers stay thin and consistent (DRY).
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: the legacy DotNetNuke controllers (for example <c>PortalController.vb</c>,
/// <c>UserController.vb</c>) co-mingled HTTP concerns, business rules, and ADO.NET data access. In the
/// target architecture those responsibilities are split into Repository (data) + Service (business) +
/// Controller (HTTP) triads. This base contributes ONLY the HTTP-response-shaping slice: it holds no
/// business logic, performs no data access, and injects no services.
/// </para>
/// <para>
/// This base intentionally carries NO <c>[ApiController]</c> or <c>[Route]</c> attribute. Those
/// attributes belong on each concrete controller (each declares its own
/// <c>[ApiController]</c> + <c>[Route("api/[controller]")]</c>); placing them here would risk route or
/// attribute-application ambiguity across the derived controllers.
/// </para>
/// <para>
/// Conventions for consuming controllers:
/// <list type="bullet">
///   <item>
///     <description>
///     Single-item <c>GET</c>/<c>PUT</c> (HTTP 200): call <c>OkEnvelope(dto)</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///     Collection <c>GET</c> (HTTP 200): call <c>OkEnvelope(list, new { count = list.Count() })</c>
///     so the item count travels in <c>meta</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///     Create <c>POST</c> (HTTP 201): call
///     <c>CreatedEnvelope(nameof(GetById), new { id = created.XxxID }, created)</c> to emit a
///     <c>Location</c> header alongside the envelope.
///     </description>
///   </item>
///   <item>
///     <description>
///     <c>DELETE</c> (HTTP 204): return <see cref="ControllerBase.NoContent"/> directly — a 204 has no
///     body, so it does NOT use these envelope helpers.
///     </description>
///   </item>
///   <item>
///     <description>
///     Not-found paths (HTTP 404): return <see cref="ControllerBase.NotFound()"/> — error/404 bodies
///     are never wrapped in the success envelope. RFC 7807 Problem Details bodies are produced
///     centrally by the exception-handling middleware + <c>AddProblemDetails()</c>; controllers never
///     hand-format error JSON.
///     </description>
///   </item>
/// </list>
/// </para>
/// </remarks>
public abstract class ApiControllerBase : ControllerBase
{
    // MIGRATION: meta always carries at least a server UTC timestamp so the envelope's "meta" key is
    // never empty. This is a pure HTTP-shaping helper (no domain concern), which is why a trivial
    // static factory is acceptable under the "no statics for domain logic" rule.
    private static object DefaultMeta() => new { timestamp = DateTime.UtcNow };

    /// <summary>
    /// Wraps a payload in the standard success envelope and returns it as an HTTP 200 (OK) response.
    /// </summary>
    /// <typeparam name="T">The payload type (a DTO or collection of DTOs).</typeparam>
    /// <param name="data">The payload to place under the envelope's <c>data</c> key.</param>
    /// <param name="meta">
    /// Optional metadata to place under the envelope's <c>meta</c> key (for example
    /// <c>new { count = list.Count() }</c>). When <see langword="null"/>, a default meta object
    /// containing the server UTC timestamp is substituted so <c>meta</c> is always present.
    /// </param>
    /// <returns>An <see cref="IActionResult"/> producing HTTP 200 with an <see cref="ApiResponse{T}"/> body.</returns>
    protected IActionResult OkEnvelope<T>(T data, object? meta = null)
        => Ok(new ApiResponse<T>(data, meta ?? DefaultMeta()));

    /// <summary>
    /// Wraps a single bounded page of results in the standard success envelope (HTTP 200), placing the
    /// page items under <c>data</c> and the pagination metadata under <c>meta</c>.
    /// </summary>
    /// <typeparam name="T">The element type of the page (a DTO).</typeparam>
    /// <param name="result">The page produced by a paged service method (items + total count).</param>
    /// <param name="paging">The normalized pagination parameters that produced the page.</param>
    /// <returns>An <see cref="IActionResult"/> producing HTTP 200 with the paged envelope.</returns>
    // MIGRATION (QA finding — R6 Issue 1, unbounded list endpoints): the bounded-pagination counterpart of
    // the plain collection OkEnvelope(list, new { count = list.Count }) convention. The emitted meta:
    //   * count      — the number of items IN THIS RESPONSE (the page), preserving the EXACT historical
    //                  semantic of the pre-pagination "count" field (it was always the returned list's size),
    //   * page       — the 1-based page number that was served,
    //   * pageSize   — the effective (clamped) page size,
    //   * totalCount — the total number of matching rows across ALL pages (the field the Angular ApiMeta
    //                  model reads to render its truncation hint / total),
    //   * totalPages — ceil(totalCount / pageSize).
    // Both "count" (returned-items count) and "totalCount" (grand total) travel so existing consumers that
    // read meta.count keep their exact prior semantic while the SPA's server-paging contract is satisfied.
    protected IActionResult PagedEnvelope<T>(PagedResult<T> result, PaginationParameters paging)
    {
        var totalCount = result.TotalCount;
        var totalPages = paging.PageSize > 0
            ? (int)Math.Ceiling(totalCount / (double)paging.PageSize)
            : 0;

        return OkEnvelope(result.Items, new
        {
            count = result.Items.Count,
            page = paging.Page,
            pageSize = paging.PageSize,
            totalCount,
            totalPages,
        });
    }

    /// <summary>
    /// Wraps a newly created payload in the standard success envelope and returns it as an HTTP 201
    /// (Created) response, including a <c>Location</c> header that points at the resource's canonical
    /// GET action.
    /// </summary>
    /// <typeparam name="T">The payload type (the created DTO).</typeparam>
    /// <param name="actionName">
    /// The name of the GET-by-id action used to build the <c>Location</c> header (for example
    /// <c>nameof(GetById)</c>).
    /// </param>
    /// <param name="routeValues">
    /// The route values required by <paramref name="actionName"/> to construct the resource URL
    /// (for example <c>new { id = created.XxxID }</c>).
    /// </param>
    /// <param name="data">The created payload to place under the envelope's <c>data</c> key.</param>
    /// <param name="meta">
    /// Optional metadata to place under the envelope's <c>meta</c> key. When <see langword="null"/>, a
    /// default meta object containing the server UTC timestamp is substituted so <c>meta</c> is always
    /// present.
    /// </param>
    /// <returns>
    /// An <see cref="IActionResult"/> producing HTTP 201 with a <c>Location</c> header and an
    /// <see cref="ApiResponse{T}"/> body.
    /// </returns>
    protected IActionResult CreatedEnvelope<T>(string actionName, object? routeValues, T data, object? meta = null)
    {
        // MIGRATION QA finding (Location header leaks internal host): CreatedAtAction builds an ABSOLUTE
        // Location URL from the current request's scheme+host. When the API container is reached directly
        // (bypassing the nginx reverse proxy), that host is the internal endpoint — e.g.
        // "http://127.0.0.1:8080/api/portals/5" — which needlessly discloses the internal host:port. The
        // Location header is net-new migration plumbing (the legacy Web Forms app had no REST Location
        // semantics, so there is no legacy behaviour to preserve), the Angular SPA never consumes it (it
        // re-fetches the collection after a create), and RFC 7231 §7.1.2 explicitly permits a relative
        // reference. Emit a RELATIVE path (e.g. "/api/portals/5") so no internal host is exposed in ANY
        // deployment topology while the resource remains addressable. Url.Action returns the path portion;
        // for a valid GET-by-id action name it is never null, but the nullable return is guarded defensively
        // (fall back to a bare 201 rather than throwing if the route cannot be resolved).
        var location = Url.Action(actionName, routeValues);
        var body = new ApiResponse<T>(data, meta ?? DefaultMeta());
        return location is not null
            ? Created(location, body)
            : StatusCode(StatusCodes.Status201Created, body);
    }

    // =========================================================================================
    // Authorization helpers (horizontal / cross-portal access control).
    //
    // MIGRATION: these enforce the horizontal slice of the DNN PortalSecurity.SecurityAccessLevel
    // model (AAP §0.6.4). The VERTICAL gate (the caller must be an Administrator or a host) is applied
    // declaratively by [Authorize(Policy = "PortalAdministrator")] on each resource controller (the
    // policy is registered in Program.cs). The HORIZONTAL gate — a non-host caller may act only within
    // the portal named by its own "portalId" claim — is resource-specific (it depends on the portal
    // that owns the target row), so it cannot be expressed as a static policy and is enforced here.
    //
    // Authorization deliberately lives at the API (HTTP) boundary rather than inside the Application
    // services: it is an HTTP/security concern (AAP §0.7.1), and the services are constructed directly
    // (without an HttpContext) by the unit-test suite, so pushing claim inspection into them would both
    // misplace the concern and break those tests.
    //
    // The claim shape is fixed by JwtTokenService.BuildClaims: the caller's portal travels in the
    // "portalId" claim (an invariant-culture integer string) and host status in the "isSuperUser" claim
    // ("true"/"false"). A host (isSuperUser == "true") is exempt from portal scoping, mirroring the
    // legacy Host access level and the existing isSuperUser bypass already implemented in AuthService.
    // =========================================================================================

    /// <summary>The JWT claim type that carries the caller's owning portal id (see <c>JwtTokenService</c>).</summary>
    private const string PortalIdClaimType = "portalId";

    /// <summary>The JWT claim type that carries the caller's host/super-user flag (see <c>JwtTokenService</c>).</summary>
    private const string SuperUserClaimType = "isSuperUser";

    /// <summary>
    /// Indicates whether the authenticated caller is a host (super) user, who is exempt from per-portal
    /// scoping. MIGRATION: mirrors the legacy Host <c>SecurityAccessLevel</c> and the <c>isSuperUser</c>
    /// bypass already implemented in <c>AuthService</c>.
    /// </summary>
    protected bool CallerIsSuperUser()
        => User.HasClaim(SuperUserClaimType, "true");

    /// <summary>
    /// Returns the caller's portal id parsed from the <c>portalId</c> claim, or <see langword="null"/>
    /// when the claim is absent or is not an integer.
    /// </summary>
    protected int? CallerPortalId()
        => int.TryParse(
               User.FindFirstValue(PortalIdClaimType),
               NumberStyles.Integer,
               CultureInfo.InvariantCulture,
               out var portalId)
           ? portalId
           : null;

    /// <summary>
    /// Indicates whether the caller may act on a resource owned by <paramref name="resourcePortalId"/>.
    /// A host (super) user always may; any other caller may only when its own <c>portalId</c> claim
    /// matches the resource's portal.
    /// </summary>
    /// <param name="resourcePortalId">The <c>PortalID</c> that owns the target resource.</param>
    protected bool CallerHasPortalAccess(int resourcePortalId)
        => CallerIsSuperUser() || (CallerPortalId() is int callerPortalId && callerPortalId == resourcePortalId);

    /// <summary>
    /// Guard for horizontal (cross-portal) access. Returns <see langword="null"/> when the caller is
    /// authorized to act on a resource owned by <paramref name="resourcePortalId"/>; otherwise returns
    /// an HTTP 403 (Forbidden) RFC 7807 Problem Details result that the calling action must return
    /// immediately.
    /// </summary>
    /// <param name="resourcePortalId">The <c>PortalID</c> that owns the target resource.</param>
    /// <returns><see langword="null"/> to allow the operation, or a 403 <see cref="IActionResult"/> to deny it.</returns>
    protected IActionResult? RequirePortalAccess(int resourcePortalId)
        => CallerHasPortalAccess(resourcePortalId)
            ? null
            : ForbiddenProblem(
                $"The caller is not authorized to access resources owned by portal {resourcePortalId.ToString(CultureInfo.InvariantCulture)}.");

    /// <summary>
    /// Guard for host-only operations (for example, creating an entirely new portal). Returns
    /// <see langword="null"/> when the caller is a host (super) user; otherwise returns an HTTP 403
    /// (Forbidden) RFC 7807 Problem Details result that the calling action must return immediately.
    /// </summary>
    /// <returns><see langword="null"/> to allow the operation, or a 403 <see cref="IActionResult"/> to deny it.</returns>
    protected IActionResult? RequireSuperUser()
        => CallerIsSuperUser()
            ? null
            : ForbiddenProblem("This operation requires host (super-user) privileges.");

    /// <summary>
    /// Produces a 403 (Forbidden) response as an RFC 7807 Problem Details body, consistent with the
    /// API's central error shaping. Used by the authorization guards above so that a denied caller
    /// receives a structured, leak-free <c>application/problem+json</c> payload rather than an empty 403.
    /// </summary>
    /// <param name="detail">A human-readable, non-sensitive explanation of why access was denied.</param>
    protected IActionResult ForbiddenProblem(string detail)
        => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail);
}
