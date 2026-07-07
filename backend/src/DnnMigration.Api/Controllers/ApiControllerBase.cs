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
        => CreatedAtAction(actionName, routeValues, new ApiResponse<T>(data, meta ?? DefaultMeta()));
}
