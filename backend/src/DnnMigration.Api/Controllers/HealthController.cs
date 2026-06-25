using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

// MIGRATION: New liveness endpoint with no legacy DotNetNuke analog (AAP 0.3.4). Consumed by Gate 7
// (curl -f http://localhost:8080/health). Program.cs does not register MapHealthChecks, so this
// controller owns /health.
// MIGRATION: The response is intentionally RAW ({ status, version }) and is NOT wrapped in the standard
// { data, meta } success envelope, because Gate 7 expects this literal shape.
[ApiController]
[AllowAnonymous]
[Route("health")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    /// <summary>GET /health — liveness probe used by Docker/Gate 7.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new { status = "Healthy", version = "1.0.0.0" });
    }
}
