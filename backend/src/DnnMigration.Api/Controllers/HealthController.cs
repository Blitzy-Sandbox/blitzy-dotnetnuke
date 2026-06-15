using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// Unversioned health/liveness endpoint backing the Docker HEALTHCHECK and validation Gate 7.
/// </summary>
/// <remarks>
/// Exposed at the absolute path <c>/health</c> (intentionally NOT under the versioned
/// <c>/api/v1/...</c> prefix) and decorated with <see cref="AllowAnonymousAttribute"/> so the probe
/// remains reachable even when a global fallback authorization policy is configured in
/// <c>Program.cs</c>. The response is a minimal operational status payload and is deliberately not
/// wrapped in the standard <c>{ data, meta }</c> resource envelope.
/// </remarks>
// MIGRATION: New endpoint with no legacy equivalent. DNN 4.x had no health probe; this supports the
// containerized (Docker/nginx) deployment topology. Documented in root MIGRATION_NOTES.md.
[ApiController]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    /// <summary>
    /// Liveness probe. Returns HTTP 200 with a minimal status payload to indicate the API process is
    /// up and able to serve requests.
    /// </summary>
    /// <returns>
    /// <c>200 OK</c> with body <c>{ "status": "Healthy", "timestamp": "&lt;ISO-8601 UTC&gt;" }</c>.
    /// </returns>
    [HttpGet("/health")]
    public IActionResult Get()
    {
        return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
    }
}
