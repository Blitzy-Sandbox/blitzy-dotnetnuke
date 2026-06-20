// =============================================================================
// DnnMigration.Api — Program.cs
// =============================================================================
// DI composition root, middleware pipeline, and host bootstrap for the ASP.NET
// Core 8 Web API (Backend-for-Frontend). This file is the modern replacement for
// the legacy DotNetNuke application lifecycle + configuration:
//   - Website/App_Code/Global.asax.vb          (ASP.NET HttpApplication lifecycle)
//   - Website/development.config / release.config (connection string + provider model)
//
// MIGRATION: The legacy Global.asax.vb pipeline — Application_Start (set ServerName),
// Global_BeginRequest (Initialize.Init(app) + Initialize.RunSchedule(Request)), and
// Application_End (Initialize.StopScheduler() + Initialize.LogEnd()) — is INTENTIONALLY
// NOT PORTED. The DNN-specific bootstrap and the Scheduler are out of scope (AAP §0.2.2
// excludes DotNetNuke.Services.Scheduling); ASP.NET Core's built-in generic host owns the
// application start/stop lifetime, and IHostedService/Hangfire would replace scheduling
// only if a need arises. Recorded in root MIGRATION_NOTES.md.
//
// MIGRATION: The development.config/release.config <connectionStrings>/<appSettings> and the
// DNN provider-model wiring are replaced by appsettings.json (ConnectionStrings:Default + a
// strongly-typed Jwt/Cors/Serilog configuration) and the built-in DI container below; the
// legacy Forms-auth timeout="60" maps to the 60-minute JWT access-token lifetime.
// =============================================================================

using System.Text;
using System.Threading.RateLimiting;
using DnnMigration.Api.Authorization;
using DnnMigration.Api.Middleware;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Identity;
using DnnMigration.Infrastructure.Persistence;
using DnnMigration.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// Phase 1 — Structured logging (Serilog)
// -----------------------------------------------------------------------------
// Read the "Serilog" section from configuration (console sink + level overrides)
// and enrich every event from the ambient LogContext so correlation ids flow
// through the request. MIGRATION: replaces the legacy DNN EventLog logging provider
// (Website/.../*.config <logging> section) with structured ILogger/Serilog (AAP NFR).
builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

// -----------------------------------------------------------------------------
// Phase 2 — Configuration (Options pattern)
// -----------------------------------------------------------------------------
// Bind the "Jwt" section so IOptions<JwtSettings> can be injected (consumed by the
// Infrastructure JwtService, which also validates the signing key length at construction).
//
// MIGRATION (Finding INC-1 O2): symmetric STARTUP fail-fast for the JWT signing key, matching the
// ConnectionStrings:Default guard below. Previously only Configure<JwtSettings>() was registered, so a
// missing/short/empty Jwt:Key was not caught until the JwtBearer options or JwtService first resolved —
// letting the app start "healthy" and then fail every request (including /health) with an opaque
// IDX10703/IDX10653. AddOptions().Validate(...).ValidateOnStart() now eagerly validates at host start, so a
// misconfigured key aborts startup with a clear OptionsValidationException instead of degrading at runtime.
// This realizes the design intent documented in JwtService's constructor ("Program.cs adds
// AddOptions().Validate(...) when it enters scope"); the JwtService ctor guard remains as defense-in-depth.
// The 32-character (256-bit) minimum mirrors JwtService.MinimumKeyLength (HMAC-SHA256 requirement).
builder.Services
    .AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Key) && settings.Key.Length >= 32,
        "JWT signing key (Jwt:Key) must be configured with at least 32 characters (256 bits) for HMAC-SHA256.")
    .ValidateOnStart();

// The database connection string is mandatory. Guard for a NULL (missing) value with a
// clear fail-fast error. An empty value is deliberately tolerated: the integration-test
// host (Gate 5) substitutes the EF Core InMemory provider after this registration, so a
// non-functional connection string never opens a real connection there.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

// Read the "Jwt" section into a strongly-typed instance for the JwtBearer validation
// parameters below. JwtSettings exposes non-nullable members (Issuer/Audience/Key default
// to non-null values), so once the bound object itself is confirmed non-null there are no
// further nullable-dereference (CS8602) or possible-null-argument (CS8604) warnings.
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing or invalid.");

// -----------------------------------------------------------------------------
// Phase 3 — Persistence (EF Core 8)
// -----------------------------------------------------------------------------
// MIGRATION: the legacy ADO.NET data layer — Microsoft.ApplicationBlocks.Data SqlHelper
// stored-procedure calls (SqlDataProvider.vb) and CBO.vb reflection-based IDataReader->object
// hydration — is replaced wholesale by EF Core 8 entity materialization. DnnDbContext maps the
// existing DotNetNuke 4.9.0.85 schema UNCHANGED (ADR-002: no EF migrations). The provider and
// connection are supplied here; the context is provider-agnostic so the test host can swap to
// the InMemory provider.
builder.Services.AddDbContext<DnnDbContext>(options => options.UseSqlServer(connectionString));

// -----------------------------------------------------------------------------
// Phase 4 — Application & Infrastructure services (Dependency Injection)
// -----------------------------------------------------------------------------
// Repositories: Domain interface -> Infrastructure EF Core implementation.
// MIGRATION: extracted from the data-access methods the legacy *Controller.vb classes
// co-mingled with business logic.
builder.Services.AddScoped<IPortalRepository, PortalRepository>();
builder.Services.AddScoped<IModuleRepository, ModuleRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<ITabRepository, TabRepository>();

// Application services: one per aggregate root plus the authentication orchestrator.
builder.Services.AddScoped<IPortalService, PortalService>();
builder.Services.AddScoped<IModuleService, ModuleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ITabService, TabService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Identity ports: Application interface -> Infrastructure implementation.
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// -----------------------------------------------------------------------------
// Phase 5 — AutoMapper + FluentValidation (assembly scan)
// -----------------------------------------------------------------------------
// Register every AutoMapper profile and every FluentValidation validator declared in the
// Application assembly. MIGRATION: AutoMapper replaces CBO.FillObject reflection hydration;
// FluentValidation reproduces the legacy ASCX field-level validation rules and messages. The
// marker types are fully qualified so no additional import is required for the assembly scan.
builder.Services.AddAutoMapper(typeof(DnnMigration.Application.Mapping.PortalProfile).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(DnnMigration.Application.Validators.CreatePortalValidator).Assembly);

// -----------------------------------------------------------------------------
// Phase 6 — Authentication & Authorization (JWT Bearer)
// -----------------------------------------------------------------------------
// MIGRATION: legacy ASP.NET Forms Authentication + 56-bit DES (Library/Components/Security/
// PortalSecurity.vb) is replaced by STATELESS JWT Bearer tokens + BCrypt password hashing. The
// server retains no session — identity travels in signed JWT claims — which permits horizontal
// scaling. Recorded in root MIGRATION_NOTES.md.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            // No clock-skew tolerance: an access token expires exactly at its stated lifetime.
            ClockSkew = TimeSpan.Zero
        };
    });

// MIGRATION (Finding CP4-1 / AAP §0.6.2): server-side authorization is the AUTHORITATIVE enforcement
// point and replaces PortalSecurity.HasNecessaryPermission (PortalSecurity.vb L469-L535). Each permission
// key (VIEW/EDIT/DELETE/MANAGE_SETTINGS) is registered as a NAMED policy that requires an authenticated
// user satisfying a PermissionRequirement; the PermissionAuthorizationHandler grants access only to a
// SuperUser (IsSuperUser JWT claim — the legacy IsSuperUser shortcut) OR a member of the "Administrators"
// portal role. This mirrors the frontend PERMISSION_ROLE_MAP exactly, so an authenticated-but-unauthorized
// caller receives 403 (RequireAuthenticatedUser rejects anonymous callers with 401 first). Resource
// controllers apply these via [Authorize(Policy = Permissions.View/Edit/Delete)].
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(
                new PermissionRequirement(permission, new[] { AuthorizationRoles.Administrators }));
        });
    }
});

// -----------------------------------------------------------------------------
// Phase 7 — CORS (Angular SPA origin only)
// -----------------------------------------------------------------------------
// BFF rule: CORS is restricted to the configured Angular origin(s) ONLY — never AllowAnyOrigin.
// Origins are read from "Cors:AllowedOrigins"; the local Angular dev server is the fallback.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularSpa", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// -----------------------------------------------------------------------------
// Phase 8 — Controllers + Swagger / OpenAPI
// -----------------------------------------------------------------------------
// Attribute-routed controllers: the /api/v1/... resource controllers and the unversioned
// HealthController at /health. Default JSON options are retained (enums serialize as integers),
// preserving the verbatim legacy enum values (AAP §0.6.3).
builder.Services.AddControllers();

// -----------------------------------------------------------------------------
// Phase 8a — Problem Details for framework-generated status responses (RFC 7807)
// -----------------------------------------------------------------------------
// MIGRATION (Finding INC-1 F1 / AAP §0.7.2): the global ExceptionHandlingMiddleware only converts THROWN
// exceptions into RFC 7807 application/problem+json. Framework-generated status results that do NOT throw —
// route-not-matched / failed {id:int} constraint 404, the JWT Bearer auth-challenge 401, method-not-allowed
// 405, and the rate-limiter 429 — previously returned an EMPTY body. Registering AddProblemDetails() (the
// IProblemDetailsService + default writer) together with app.UseStatusCodePages() in the pipeline below makes
// those empty-body status responses emit the SAME uniform error envelope. AddProblemDetails registers the
// IProblemDetailsService and its default writer; the CustomizeProblemDetails callback below applies a single
// universal enrichment to every emitted ProblemDetails — it populates the numeric "status" and attaches the
// "traceId" correlation id (identical to ExceptionHandlingMiddleware). The stable dnnmigration.com "type" URI
// and the human "title" for each framework status are assigned by the UseStatusCodePages handler in the
// pipeline below (via MapStatusToProblem). MVC's already-populated [ApiController] validation ProblemDetails
// (which carry their own body, title, and "errors" map) are left untouched, because UseStatusCodePages only
// acts on responses that have NOT yet written a body — so the existing 400/validation responses are unchanged.
// Base URI for the Problem Details "type" member, identical to ExceptionHandlingMiddleware.ErrorTypeBaseUri,
// so a framework-generated status response and the equivalent thrown-exception response share one type scheme.
const string errorTypeBaseUri = "https://dnnmigration.com/errors/";

// Maps an HTTP status code to a stable (type-slug, title) pair that mirrors ExceptionHandlingMiddleware's
// vocabulary, so a framework-generated 404/401/405/429 carries the SAME type URI and title as the equivalent
// thrown-exception response (e.g. a routing 404 and a KeyNotFoundException 404 both use ".../errors/not-found").
// Used by the UseStatusCodePages handler in the pipeline below.
static (string Slug, string Title) MapStatusToProblem(int statusCode) => statusCode switch
{
    StatusCodes.Status400BadRequest => ("bad-request", "Bad Request"),
    StatusCodes.Status401Unauthorized => ("unauthorized", "Unauthorized"),
    StatusCodes.Status403Forbidden => ("forbidden", "Forbidden"),
    StatusCodes.Status404NotFound => ("not-found", "Not Found"),
    StatusCodes.Status405MethodNotAllowed => ("method-not-allowed", "Method Not Allowed"),
    StatusCodes.Status406NotAcceptable => ("not-acceptable", "Not Acceptable"),
    StatusCodes.Status409Conflict => ("conflict", "Conflict"),
    StatusCodes.Status415UnsupportedMediaType => ("unsupported-media-type", "Unsupported Media Type"),
    StatusCodes.Status429TooManyRequests => ("too-many-requests", "Too Many Requests"),
    >= 500 => ("internal-server-error", "An unexpected error occurred."),
    _ => ("error", "Error")
};

builder.Services.AddProblemDetails(options =>
{
    // Universal enrichment for every ProblemDetails written through IProblemDetailsService: ensure the numeric
    // status is populated and attach the correlation id at the JSON root, identical to ExceptionHandlingMiddleware.
    // Type/Title for the framework empty-body responses are assigned by the UseStatusCodePages handler below, so
    // MVC's [ApiController] validation ProblemDetails (which carry their own Title + errors map) are left intact.
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Status ??= context.HttpContext.Response.StatusCode;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DnnMigration API",
        Version = "v1",
        Description = "REST API (BFF) for the DotNetNuke migration — Portal, Module, User, Role, and Tab management."
    });

    // Enable the Swagger UI "Authorize" dialog to attach a JWT Bearer token to requests.
    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\".",
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
    options.AddSecurityDefinition("Bearer", jwtSecurityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtSecurityScheme, Array.Empty<string>() }
    });
});

// -----------------------------------------------------------------------------
// Phase 9 — Rate limiting (auth endpoints)
// -----------------------------------------------------------------------------
// Built-in ASP.NET Core 8 rate limiter (provided by the shared framework — no NuGet package).
// The "auth" fixed-window policy is opted into by AuthController's login/refresh actions via
// [EnableRateLimiting("auth")] (AAP §0.7.2: auth endpoints rate-limited). Rejected requests
// return HTTP 429 instead of the framework default 503.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});

// =============================================================================
// Build the application and configure the HTTP request pipeline.
// ORDER IS LOAD-BEARING — do not reorder the middleware below.
// =============================================================================
var app = builder.Build();

// Structured request logging (method, path, status, elapsed) with correlation ids.
// MIGRATION (Finding INC-1 O3 / AAP NFR — correlation IDs): enrich the per-request completion event with the
// TraceId (the same ASP.NET Core trace identifier surfaced as the response "traceId" and the error-log
// CorrelationId) and render it inline in the request-summary message, so an operator can correlate the
// console request line with a client-quoted traceId without inspecting the structured properties. Done via
// the request-logging MessageTemplate (not the global Console outputTemplate), so only the request-summary
// line carries it and no other log line is affected.
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms (TraceId: {TraceId})";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
});

// Global exception handling is registered FIRST in the application's own middleware so it wraps
// the entire downstream pipeline and converts any unhandled exception into an RFC 7807 Problem
// Details response. MIGRATION: replaces the legacy Web Forms error page Website/ErrorPage.aspx.vb,
// which is not ported as a page (AAP §0.6.4).
app.UseMiddleware<ExceptionHandlingMiddleware>();

// MIGRATION (Finding INC-1 F1 / AAP §0.7.2): convert framework-generated EMPTY-body status responses into
// the uniform RFC 7807 application/problem+json envelope. Registered immediately inside the exception
// middleware and ABOVE routing/CORS/rate-limiter/authentication, it observes the empty 4xx responses those
// later stages produce — the JWT Bearer challenge 401, route-not-matched/constraint 404, method-not-allowed
// 405, and the rate-limiter 429 — and (through the AddProblemDetails IProblemDetailsService +
// CustomizeProblemDetails registered above) writes the same {type,title,status,traceId} body. Responses that
// already wrote a body (the success {data,meta} envelope, ExceptionHandlingMiddleware's problem+json, and
// MVC [ApiController] validation ProblemDetails) are left untouched because their bodies have already started.
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    var statusCode = httpContext.Response.StatusCode;
    var (slug, title) = MapStatusToProblem(statusCode);

    // Build the envelope with the dnnmigration "type" URI + "title" (the AddProblemDetails CustomizeProblemDetails
    // callback adds the "status" and "traceId"), then write it as application/problem+json through the registered
    // IProblemDetailsService so the framework 401/404/405/429 match the thrown-exception envelope exactly.
    var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        Type = errorTypeBaseUri + slug,
        Title = title,
        Status = statusCode
    };

    await httpContext.RequestServices
        .GetRequiredService<IProblemDetailsService>()
        .WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });
});

// Swagger / OpenAPI UI is exposed only in Development.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enforce HTTPS. NOTE: inside the Linux container the health probe targets the plain-HTTP port
// directly, where no HTTPS port is configured; this middleware then becomes a safe no-op and does
// not break the container/Kubernetes health check (Gate 7). It is likewise a no-op under the
// in-process integration-test server (Gate 5).
app.UseHttpsRedirection();

// CORS must run before authentication so cross-origin pre-flight (OPTIONS) requests succeed.
app.UseCors("AngularSpa");

// Rate limiter runs before authentication so abusive traffic to the auth endpoints is shed early.
app.UseRateLimiter();

// Authentication must precede authorization.
app.UseAuthentication();
app.UseAuthorization();

// Map the attribute-routed controllers. This includes the unversioned HealthController at /health
// (Gate 7) and the /api/v1/... resource controllers. The HealthController owns /health, so
// MapHealthChecks("/health") is intentionally NOT called (it would collide on the same route).
app.MapControllers();

app.Run();

// =============================================================================
// Phase 11 — Integration-test entry-point seam (MANDATORY)
// =============================================================================
// Declaring Program as a public partial class promotes the compiler-synthesized top-level entry
// point from internal to public so that DnnMigration.IntegrationTests can reference it through
// Microsoft.AspNetCore.Mvc.Testing's WebApplicationFactory<Program> (Gate 5). Without this the
// generated Program class is internal and the integration-test project fails to compile.
public partial class Program { }
