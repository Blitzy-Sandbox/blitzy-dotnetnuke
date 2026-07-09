// -----------------------------------------------------------------------------
//  HealthController.cs
//
//  MIGRATION: Net-new operational endpoint with NO legacy VB source. The classic
//  DotNetNuke 4.x application (VB.NET / ASP.NET Web Forms) had no dedicated
//  liveness probe. This controller is introduced for the .NET 8 migration to
//  satisfy the container HEALTHCHECK directives in docker/api.Dockerfile and
//  docker/docker-compose.yml, plus Validation Gate 7 - an unauthenticated
//  GET /health must return HTTP 200 with the body
//  {"status":"Healthy","version":"1.0.0.0"} (AAP sec. 0.3.1 / 0.7.2).
// -----------------------------------------------------------------------------

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// Exposes the application's unauthenticated liveness probe at <c>GET /health</c>.
/// </summary>
/// <remarks>
/// <para>
/// This controller intentionally inherits <see cref="ControllerBase"/> directly
/// instead of the solution's shared <c>ApiControllerBase</c>: the health payload
/// is a fixed <c>{ status, version }</c> shape and must NOT be wrapped in the
/// standard <c>{ data, meta }</c> success envelope. Wrapping it would change the
/// exact response body that the container health checks and Validation Gate 7
/// assert byte-for-byte.
/// </para>
/// <para>
/// The endpoint is deliberately dependency-free (no injected service or logger).
/// A liveness probe must respond even when downstream dependencies are degraded,
/// so it performs no I/O and reports a static, well-known payload.
/// </para>
/// </remarks>
[ApiController]
// MIGRATION QA finding F: declare the 200 response for OpenAPI/Swagger. The body is the fixed
// { status, version } liveness shape and is deliberately NOT wrapped in the { data, meta } envelope,
// so no DTO type is attached - only the 200 status is declared (on the action below).
// MIGRATION QA finding F: intentionally NO [Produces("application/json")] here so the shared RFC 7807
// error content-type ("application/problem+json") is never overridden by an MVC result filter. The
// fixed { status, version } 200 liveness response is declared on the action below via [ProducesResponseType].
public sealed class HealthController : ControllerBase
{
    /// <summary>
    /// Returns the service liveness status.
    /// </summary>
    /// <returns>
    /// An HTTP <c>200 OK</c> result whose body serializes, under the camelCase
    /// JSON naming policy configured in <c>Program.cs</c>, to
    /// <c>{"status":"Healthy","version":"1.0.0.0"}</c>.
    /// </returns>
    // MIGRATION: literal absolute route - health lives at /health, not under /api.
    // The leading-slash template stops the attribute-routing convention from
    // prefixing it; [AllowAnonymous] lets the JWT auth middleware pass the probe.
    [HttpGet("/health")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
        => Ok(new { Status = "Healthy", Version = "1.0.0.0" });
}
