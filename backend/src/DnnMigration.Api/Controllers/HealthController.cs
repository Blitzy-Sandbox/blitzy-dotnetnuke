using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// Health check controller for container orchestration and load balancer health probes.
/// Provides a simple endpoint that returns HTTP 200 OK when the service is healthy.
/// </summary>
/// <remarks>
/// This controller is used by:
/// - Docker container health checks (HEALTHCHECK instruction)
/// - Kubernetes liveness and readiness probes
/// - Load balancer health monitoring
/// - Service mesh health verification
/// 
/// The endpoint is intentionally lightweight and does not require authentication
/// to ensure quick response times for infrastructure health monitoring.
/// </remarks>
[ApiController]
[Route("[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Performs a health check and returns the current service status.
    /// </summary>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a JSON object with:
    /// - Status: The current health status ("Healthy")
    /// - Timestamp: The UTC timestamp when the health check was performed
    /// - Version: The current application version information
    /// - Environment: The current hosting environment name
    /// </returns>
    /// <response code="200">Service is healthy and responding to requests</response>
    /// <example>
    /// GET /health
    /// 
    /// Response:
    /// {
    ///     "status": "Healthy",
    ///     "timestamp": "2024-01-15T10:30:00.0000000Z",
    ///     "version": "1.0.0",
    ///     "serviceName": "DnnMigration.Api"
    /// }
    /// </example>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        // Return a comprehensive health check response with diagnostic information
        // for monitoring systems and container orchestrators
        var healthResponse = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetAssemblyVersion(),
            ServiceName = "DnnMigration.Api"
        };

        return Ok(healthResponse);
    }

    /// <summary>
    /// Retrieves the current assembly version for the health check response.
    /// </summary>
    /// <returns>The informational version of the executing assembly, or "1.0.0" if not available.</returns>
    private static string GetAssemblyVersion()
    {
        // Get the informational version which includes semantic versioning
        var assembly = typeof(HealthController).Assembly;
        var version = assembly.GetName().Version;
        
        // Return the version string or a default if not available
        return version?.ToString() ?? "1.0.0";
    }
}
