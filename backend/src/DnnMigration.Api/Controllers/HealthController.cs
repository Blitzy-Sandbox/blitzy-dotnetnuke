using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// Unversioned health/liveness endpoint backing the Docker HEALTHCHECK and validation Gate 7.
/// </summary>
// MIGRATION: New endpoint with no legacy equivalent. DNN 4.x had no health probe; this supports the
// containerized (Docker/nginx) deployment topology. Documented in root MIGRATION_NOTES.md.
[ApiController]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns HTTP 200 with a minimal status payload.</summary>
    [HttpGet("/health")]
    public IActionResult Get()
    {
        return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
    }
}
