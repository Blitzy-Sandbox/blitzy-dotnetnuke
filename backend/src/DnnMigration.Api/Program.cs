using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using DnnMigration.Api.Authorization;
using DnnMigration.Api.Middleware;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// MIGRATION: Composition root replacing the legacy DotNetNuke startup. The Website/release.config
// connection string (SiteSqlServer), the <membership> AspNetSqlMembershipProvider + Forms-auth chain,
// and the Framework.Reflection provider bootstrapping are all replaced here by Microsoft DI:
//   - AddInfrastructure(...) wires EF Core (DnnDbContext), repositories, IUnitOfWork, IPasswordHasher, IJwtService.
//   - Application services / AutoMapper / FluentValidation are registered inline (no AddApplication() exists).
//   - JWT Bearer auth replaces Forms auth; Serilog replaces the DNN Logging Provider; middleware replaces
//     the Web Forms global error handling.

// Two-stage Serilog: a bootstrap logger captures any failure during host construction (before the
// "Serilog" configuration section has been read). It is replaced below by the fully-configured logger.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ----- Structured logging (replaces the DNN Logging Provider). Reads the "Serilog" config section. -----
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ----- MVC controllers + API explorer + ProblemDetails (RFC 7807 infrastructure) -----
    // MIGRATION: (QA-1 Issue #3, error-envelope consistency) Unify the model-validation 400 with the
    // ExceptionHandlingMiddleware RFC 7807 envelope. The default [ApiController]/FluentValidation auto-validation
    // response emitted a framework ValidationProblemDetails that deviated from the middleware envelope in three
    // ways: Content-Type "application/json" (not "application/problem+json"); a "rfc9110" type URI (not the
    // project's "urn:dnnmigration:error:*" scheme); and no body "correlationId" (only the W3C activity id in
    // "traceId"). The custom InvalidModelStateResponseFactory below produces a single, uniform error envelope:
    // same content type, the "urn:dnnmigration:error:validation" type, and the same correlationId/traceId
    // extensions the middleware sets (resolved from the SAME HttpContext.Items["CorrelationId"] key written by
    // CorrelationIdMiddleware, falling back to TraceIdentifier). The field-level "errors" map (populated from
    // ModelState by FluentValidation auto-validation) is preserved unchanged, so existing clients keep working.
    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                HttpContext httpContext = context.HttpContext;

                // Resolve the correlation id identically to ExceptionHandlingMiddleware so every error envelope
                // (validation, exception, and Result failure) carries the SAME id in the body and the
                // X-Correlation-ID response header. CorrelationIdMiddleware runs first and sets both
                // HttpContext.Items["CorrelationId"] and HttpContext.TraceIdentifier to that id.
                string correlationId =
                    httpContext.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var stored)
                    && stored is string storedId
                        ? storedId
                        : httpContext.TraceIdentifier;

                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Type = "urn:dnnmigration:error:validation",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest
                };
                problemDetails.Extensions["correlationId"] = correlationId;
                problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

                // Return a ContentResult with an explicit ContentType (not an ObjectResult) so the response is
                // ALWAYS "application/problem+json": MVC content negotiation would otherwise let the JSON output
                // formatter emit its default "application/json" even when ObjectResult.ContentTypes requests
                // problem+json. Serializing here with the shared ExceptionHandlingMiddleware options
                // (camelCase + ignore-null) makes this envelope byte-identical to the exception path.
                return new ContentResult
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentType = ExceptionHandlingMiddleware.ProblemJsonContentType,
                    Content = JsonSerializer.Serialize(problemDetails, ExceptionHandlingMiddleware.ProblemJsonOptions)
                };
            };
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddProblemDetails();

    // ----- OpenAPI 3.0 (Swashbuckle) with a JWT Bearer security definition -----
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "DnnMigration API",
            Version = "v1",
            Description = "BFF Web API migrated from DotNetNuke 4.x (VB.NET / .NET 2.0) to ASP.NET Core 8."
        });

        var jwtScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter the JWT access token as: Bearer {token}",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        };
        options.AddSecurityDefinition("Bearer", jwtScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            { jwtScheme, Array.Empty<string>() }
        });
    });

    // ----- Application layer DI (registered inline; no AddApplication() extension exists) -----
    // MIGRATION: The DnnMigration.Application project exposes no DependencyInjection.cs / AddApplication(),
    // and Infrastructure.AddInfrastructure() intentionally does NOT register Application concerns. The
    // composition root therefore registers them here. If an AddApplication() extension is later introduced
    // in DnnMigration.Application, replace this entire block with a single builder.Services.AddApplication();
    var applicationAssembly = typeof(IPortalService).Assembly;
    // MIGRATION/SECURITY (QA Checkpoint F5 — finding F-1; AutoMapper CVE-2026-32933 / GHSA-rvv3-g6hj-g44x, HIGH):
    // AutoMapper 12.0.1 leaves TypeMap.MaxDepth unbounded by default, so a deeply nested/cyclic graph can exhaust
    // the stack (uncatchable StackOverflowException -> process-wide DoS). The patched line is paid/commercial and
    // the AAP (§0.5.1) pins 12.0.1, so per rule D1 the version stays pinned and the advisory-endorsed MaxDepth bound
    // is applied here instead. ApplyRecursionGuard runs AFTER assembly scanning, bounding EVERY discovered map (the
    // migrated maps are flat, so legitimate mapping output is unaffected). See MIGRATION_NOTES.md §19.1.
    builder.Services.AddAutoMapper(MappingConfiguration.ApplyRecursionGuard, applicationAssembly);
    builder.Services.AddValidatorsFromAssembly(applicationAssembly);

    // MIGRATION/SECURITY (CP2 review — Program.cs #3, runtime input validation): registering the validators is
    // not sufficient — they must execute on every request. AddFluentValidationAutoValidation() hooks
    // FluentValidation into MVC model validation, so an invalid [FromBody] request DTO populates ModelState and
    // the [ApiController] convention returns an RFC 7807 ValidationProblemDetails (400) BEFORE the action (and
    // therefore the Application service) runs. This closes the gap where extensively unit-tested validators were
    // never invoked at the API boundary.
    builder.Services.AddFluentValidationAutoValidation();

    builder.Services.AddScoped<IPortalService, PortalService>();
    builder.Services.AddScoped<IModuleService, ModuleService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IRoleService, RoleService>();
    builder.Services.AddScoped<ITabService, TabService>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    // MIGRATION: reusable authorization evaluator that re-expresses the legacy PortalSecurity helpers
    // (IsInRole / IsInRoles / HasNecessaryPermission) and ModulePermissionController.HasModulePermission. It is
    // stateless and has no dependencies, so it is registered as a singleton; controllers build its SecurityContext
    // input from the request principal via ClaimsPrincipal.ToSecurityContext() for resource-level permission checks
    // (the coarse Host/PortalAdministrator route policies remain for endpoint-level authorization).
    builder.Services.AddSingleton<IPermissionEvaluator, PermissionEvaluator>();

    // ----- Infrastructure layer DI: EF Core DnnDbContext, repositories, IUnitOfWork, IPasswordHasher, IJwtService -----
    builder.Services.AddInfrastructure(builder.Configuration);

    // ----- Authentication / Authorization (JWT Bearer) -----
    // MIGRATION: replaces Forms authentication + AspNetSqlMembershipProvider (Website/release.config).
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwtSection["Key"];

    // MIGRATION/SECURITY (CP2 review — Program.cs #2 + #5, JwtService #1): FAIL FAST instead of falling back to a
    // hardcoded signing key. A known, source-committed fallback key would let an attacker forge tokens that pass
    // validation, so it is never acceptable — not even to keep the anonymous /health endpoint reachable. The signing
    // key MUST be supplied via configuration: appsettings.Development.json for local development, or the 'Jwt__Key'
    // environment variable / a secret manager in every other environment (AAP 0.7.6). The same 'Jwt:Key' name is read
    // by the JwtService issuer, so issuance and validation share one key.
    if (string.IsNullOrWhiteSpace(jwtKey))
    {
        throw new InvalidOperationException(
            "JWT signing key is not configured. Set 'Jwt:Key' in configuration " +
            "(appsettings.Development.json for local development, or the 'Jwt__Key' environment variable / a secret " +
            "manager otherwise). The application will not start without a signing key.");
    }

    // MIGRATION/SECURITY (CP2 review — Program.cs #5): HMAC-SHA256 requires a key of at least 256 bits (32 bytes).
    // Reject a present-but-weak key at startup rather than silently weakening every token signature. The key VALUE is
    // never logged or echoed in the message (AAP 0.7.6) — only its byte length is reported.
    var jwtKeyByteLength = Encoding.UTF8.GetByteCount(jwtKey);
    if (jwtKeyByteLength < 32)
    {
        throw new InvalidOperationException(
            $"JWT signing key 'Jwt:Key' is too short ({jwtKeyByteLength} bytes); it must be at least 32 bytes " +
            "(256 bits) for HMAC-SHA256. Configure a longer key.");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSection["Audience"],
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
    // MIGRATION (CP2 review — resource-controller authorization findings): replaces the implicit DNN admin-page
    // access control with explicit policies. The legacy Host > Portals page was SuperUser-only; the
    // Admin > Users/Roles/Pages/Modules pages required the portal's "Administrators" role. These policies
    // reproduce that model from the claims JwtService issues ("isSuperUser"; one ClaimTypes.Role per role).
    // Authentication alone ([Authorize]) is NOT sufficient authorization, so resource controllers require these.
    builder.Services.AddAuthorization(options =>
    {
        // Host-level administration (portal CRUD): DNN host SuperUsers only.
        options.AddPolicy(AuthorizationPolicies.HostAdministrator, policy =>
            policy.RequireAssertion(context =>
                bool.TryParse(context.User.FindFirst(DnnClaims.IsSuperUser)?.Value, out bool isSuperUser) && isSuperUser));

        // Portal-level administration (users/roles/tabs/modules): the portal "Administrators" role OR a host SuperUser.
        options.AddPolicy(AuthorizationPolicies.PortalAdministrator, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole(DnnClaims.AdministratorRole) ||
                (bool.TryParse(context.User.FindFirst(DnnClaims.IsSuperUser)?.Value, out bool isSuperUser) && isSuperUser)));
    });

    // ----- CORS restricted to the Angular SPA origin -----
    const string corsPolicyName = "AngularSpa";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? new[] { "http://localhost:4200" };
    builder.Services.AddCors(options =>
    {
        // MIGRATION (CP-final security review): AllowCredentials() removed. The SPA authenticates with
        // memory-only JWT Bearer tokens (Authorization header), NOT cookies, so credentialed CORS is
        // unnecessary and over-permissive; omitting it keeps the policy least-privilege. Reinstate only if
        // httpOnly-cookie auth with anti-forgery (XSRF) is introduced.
        options.AddPolicy(corsPolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
    });

    // ----- Rate limiting on authentication endpoints (AAP 0.7.6) -----
    // A named "auth" fixed-window policy (5 requests / minute). Applied selectively to the auth endpoints
    // via [EnableRateLimiting("auth")] on AuthController; no global limiter is configured, so CRUD and
    // /health endpoints remain unthrottled.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("auth", limiter =>
        {
            limiter.PermitLimit = 5;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });
    });

    var app = builder.Build();

    // ===== Middleware pipeline (ORDER IS DELIBERATE) =====

    // 1) Correlation ID first, so every downstream log entry carries it (works with Serilog FromLogContext).
    app.UseMiddleware<CorrelationIdMiddleware>();

    // 2) Centralized exception -> RFC 7807 ProblemDetails (outermost error boundary for the request).
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // 3) Structured per-request logging (observes the final status code set by downstream middleware).
    app.UseSerilogRequestLogging();

    // 4) OpenAPI document + UI (exposed in all environments so the containerized API is explorable).
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "DnnMigration API v1"));

    // 5) HTTPS enforcement. NOTE: in the Alpine container the API listens only on http:8080
    //    (ASPNETCORE_URLS), so no https port is configured and UseHttpsRedirection is a no-op there,
    //    which keeps Gate 7 (`curl -f http://localhost:8080/health`) returning HTTP 200. HSTS is enabled
    //    only outside Development.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
    app.UseHttpsRedirection();

    // 6) CORS before authentication.
    app.UseCors(corsPolicyName);

    // 7) Rate limiter (enforces the named "auth" policy on decorated endpoints).
    app.UseRateLimiter();

    // 8) Authentication then Authorization.
    app.UseAuthentication();
    app.UseAuthorization();

    // 9) Map attribute-routed controllers (Portals, Modules, Users, Roles, Tabs, Auth, Health).
    app.MapControllers();

    Log.Information("DnnMigration API host starting");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown by tooling/WebApplicationFactory to stop the host; let it propagate
    // so integration tests (Gate 5) and `dotnet ef` work. All other startup failures are logged as fatal.
    Log.Fatal(ex, "DnnMigration API host terminated unexpectedly");

    // MIGRATION: (QA-3 Issue #1, configuration fail-fast robustness) Report the fatal, unrecoverable startup
    // failure to the OS with a NON-ZERO exit code. Previously the synthesized top-level Main fell off its end
    // after this catch and the process exited 0, which masked failures — a missing/short 'Jwt:Key', a malformed
    // 'Serilog:MinimumLevel', etc. — from Docker 'restart: on-failure', docker-compose exit reporting, process
    // supervisors, and CI smoke gates (AAP Gate 7 containerized deployment). Environment.ExitCode is set rather
    // than calling Environment.Exit(1) so the finally block below still runs Log.CloseAndFlush() and the buffered
    // [FTL] line is flushed instead of truncated. The 'when (ex is not HostAbortedException)' catch filter still
    // excludes the intentional aborts raised by EF Core design-time tooling and the
    // WebApplicationFactory<Program> integration tests, so those paths never reach here and continue to exit 0.
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

// MIGRATION: expose the implicit top-level-statement Program class as public + partial so the
// DnnMigration.IntegrationTests project can construct WebApplicationFactory<Program> (Gate 5).
public partial class Program { }
