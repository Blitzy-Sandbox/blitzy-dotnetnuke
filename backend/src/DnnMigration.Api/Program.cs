// =============================================================================
//  DnnMigration.Api — Program.cs
//  -----------------------------------------------------------------------------
//  Composition root, HTTP middleware pipeline and host bootstrap for the
//  ASP.NET Core 8 Web API (Backend-for-Frontend) that replaces the legacy
//  DotNetNuke 4.9.0.85 VB.NET / ASP.NET Web Forms application.
//
//  Source lineage:
//    * Website/App_Code/Global.asax.vb            — ASP.NET application lifecycle
//    * Website/development.config / release.config — configuration + provider model
//
//  MIGRATION: The legacy Global.asax.vb lifecycle is intentionally NOT ported.
//  Application_Start (set ServerName), Global_BeginRequest (Initialize.Init +
//  Initialize.RunSchedule) and Application_End (Initialize.StopScheduler +
//  Initialize.LogEnd) drove the DNN bootstrap and the DotNetNuke.Services.Scheduling
//  subsystem, both of which are OUT OF SCOPE (AAP §0.2.2). ASP.NET Core's built-in
//  generic host lifetime, the (optional) IHostedService model and the DI container
//  replace the DNN provider-model reflection wiring. Every deviation below is also
//  recorded in the root MIGRATION_NOTES.md.
// =============================================================================

using System.Text;
using DnnMigration.Api.Middleware;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Application.Validators;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Identity;
using DnnMigration.Infrastructure.Persistence;
using DnnMigration.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// Phase 1 — Structured logging (Serilog)
// -----------------------------------------------------------------------------
// MIGRATION: replaces the legacy DNN Logging provider model. Serilog is configured
// entirely from the "Serilog" section of appsettings.json (sinks, minimum levels,
// overrides, enrichers) so logging behaviour is environment-driven; FromLogContext
// enrichment carries scoped properties (e.g. per-request correlation ids) into every
// emitted event for the container's stdout sink.
builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

// MIGRATION: AutoMapper 15.x (a security-driven upgrade from the 12.x baseline) is
// commercially licensed by Lucky Penny Software, but license enforcement is LOG-ONLY
// — it never throws, calls out, or disables features. Mute that log category so the
// container's structured logs are not polluted with license notices.
builder.Logging.AddFilter("LuckyPennySoftware.AutoMapper.License", LogLevel.None);

// -----------------------------------------------------------------------------
// Phase 2 — Configuration (Options pattern)
// -----------------------------------------------------------------------------
// Bind the strongly-typed JwtSettings options POCO (defined in the Infrastructure
// layer — imported here, never redefined) from the "Jwt" configuration section so
// JwtService can consume it via IOptions<JwtSettings>.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// MIGRATION: the legacy <connectionStrings> "SiteSqlServer" entry (web.config /
// development.config) is replaced by ConnectionStrings:Default in appsettings.json.
// Fail fast with an actionable message when it is absent; the null-coalescing throw
// also collapses the nullable flow so UseSqlServer receives a non-null string.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. Set it in appsettings.json or via the ConnectionStrings__Default environment variable.");

// Read a local copy of the JWT settings for the JwtBearer wiring below. The throw
// removes the nullable warning on the subsequent reads, and the explicit key-length
// guard mirrors the fail-fast contract of JwtService (a >= 256-bit signing key).
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing or invalid.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key) || Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be configured with a signing key of at least 32 bytes (256 bits). Provide it via the Jwt__Key environment variable.");
}

// -----------------------------------------------------------------------------
// Phase 3 — Persistence (EF Core 8)
// -----------------------------------------------------------------------------
// MIGRATION: the legacy ADO.NET data layer — Microsoft.ApplicationBlocks.Data
// SqlHelper stored-procedure calls in SqlDataProvider.vb together with the
// reflection-based CBO.FillObject / FillCollection hydration — is replaced wholesale
// by EF Core 8 entity materialization. The existing DNN 4.9.0.85 schema is mapped
// UNCHANGED (ADR-002): no schema changes and no EF migrations. The provider and
// connection string are supplied here (DnnDbContext has no OnConfiguring), which also
// lets the integration tests substitute the EF Core InMemory provider through
// WebApplicationFactory without touching this composition root.
builder.Services.AddDbContext<DnnDbContext>(options =>
    options.UseSqlServer(connectionString));

// -----------------------------------------------------------------------------
// Phase 4 — Application & Infrastructure services (Dependency Injection)
// -----------------------------------------------------------------------------
// MIGRATION: replaces DNN's provider-model reflection wiring with the built-in
// ASP.NET Core DI container. Repository abstractions live in the Domain layer; their
// EF Core implementations live in the Infrastructure layer (scoped per request to
// share the DbContext unit-of-work).
builder.Services.AddScoped<IPortalRepository, PortalRepository>();
builder.Services.AddScoped<IModuleRepository, ModuleRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<ITabRepository, TabRepository>();

// Application services — one per aggregate root, plus the JWT authentication
// orchestrator. Each encapsulates the ported business rules of the corresponding
// legacy *Controller.vb class.
builder.Services.AddScoped<IPortalService, PortalService>();
builder.Services.AddScoped<IModuleService, ModuleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ITabService, TabService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// MIGRATION: identity ports (declared in the Application layer) -> Infrastructure
// implementations. JwtService replaces the Forms-auth token logic and PasswordHasher
// replaces the 56-bit DES Encrypt/Decrypt of PortalSecurity.vb with adaptive BCrypt.
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// -----------------------------------------------------------------------------
// Phase 5 — AutoMapper + FluentValidation (assembly scan of the Application layer)
// -----------------------------------------------------------------------------
// MIGRATION: AutoMapper 15.x requires the configuration delegate as the FIRST
// argument of every AddAutoMapper overload (a breaking change from the 12.x API that
// the original AAP example predates). The PortalProfile marker identifies the
// DnnMigration.Application assembly, whose scan registers all entity<->DTO profiles
// (Portal/Module/User/UserRole/Role/Tab).
builder.Services.AddAutoMapper(_ => { }, typeof(PortalProfile));

// Register every FluentValidation validator in the Application assembly so the
// services can resolve IValidator<T> for their manual validate-then-act flow. The
// CreatePortalValidator marker identifies the assembly to scan.
builder.Services.AddValidatorsFromAssembly(typeof(CreatePortalValidator).Assembly);

// -----------------------------------------------------------------------------
// Phase 6 — Authentication & Authorization (JWT Bearer)
// -----------------------------------------------------------------------------
// MIGRATION: ASP.NET Forms Authentication + 56-bit DES (PortalSecurity.vb SignOut /
// Encrypt / Decrypt) are replaced by stateless JWT Bearer tokens. The server retains
// NO session — identity travels in signed JWT claims — which permits horizontal
// scaling. The validation parameters mirror the issuing JwtService exactly, with zero
// clock skew so token lifetimes are honoured to the second.
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
            ClockSkew = TimeSpan.Zero
        };
    });

// Plain authorization: the in-scope permission semantics (VIEW / EDIT / DELETE /
// MANAGE_SETTINGS) are enforced inside the Application services — a denied or unknown
// permission surfaces as a SecurityException, which the exception middleware maps to
// an RFC 7807 403. No custom authorization policies/handlers are provided by the
// Application/Infrastructure layers, so none are registered here. Endpoints opt in
// with [Authorize]; [AllowAnonymous] covers /health and the auth login/refresh.
builder.Services.AddAuthorization();

// -----------------------------------------------------------------------------
// Phase 7 — CORS (Angular SPA origin ONLY — BFF rule)
// -----------------------------------------------------------------------------
// The allowed origins come from Cors:AllowedOrigins (falling back to the Angular dev
// origin). AllowAnyOrigin() is deliberately NEVER used — credentials require an
// explicit origin allow-list, which is the BFF security contract.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
    options.AddPolicy("AngularSpa", policy =>
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

// -----------------------------------------------------------------------------
// Phase 8 — Controllers + OpenAPI / Swagger
// -----------------------------------------------------------------------------
// Attribute-routed controllers: resource endpoints are URL-path versioned under
// /api/v1/...; the HealthController is unversioned at /health.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DnnMigration API",
        Version = "v1",
        Description = "REST API (BFF) for the DotNetNuke 4.x -> .NET 8 migration."
    });

    // Wire JWT Bearer into the Swagger UI so protected endpoints can be exercised
    // from the interactive docs (Authorize button).
    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste the JWT access token below (the 'Bearer ' prefix is added automatically).",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    options.AddSecurityDefinition(bearerScheme.Reference.Id, bearerScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { bearerScheme, Array.Empty<string>() }
    });
});

// -----------------------------------------------------------------------------
// Phase 9 — Rate limiting (auth endpoints)
// -----------------------------------------------------------------------------
// MIGRATION: a hardening measure with no legacy equivalent. Uses the built-in
// ASP.NET Core 8 rate limiter (shared framework — no NuGet package). The "auth"
// fixed-window policy throttles brute-force attempts against the AuthController
// login/refresh actions, which opt in via [EnableRateLimiting("auth")]. Rejected
// requests receive HTTP 429 instead of the default 503.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

var app = builder.Build();

// -----------------------------------------------------------------------------
// Phase 10 — HTTP middleware pipeline (ORDER IS LOAD-BEARING)
// -----------------------------------------------------------------------------
// Request logging first so every request — including those short-circuited later in
// the pipeline — is recorded with its final status code and elapsed time.
app.UseSerilogRequestLogging();

// Global exception handling is registered EARLY so it wraps the entire downstream
// pipeline and converts unhandled exceptions into RFC 7807 Problem Details responses.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger UI is exposed in the Development environment only.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enforce HTTPS. NOTE: inside the Linux container the health probe hits the plain
// HTTP port directly; with no HTTPS port configured this middleware is a no-op for
// that request, so it does NOT break Gate 7's `curl http://localhost:8080/health`.
app.UseHttpsRedirection();

// CORS must run before authentication so CORS pre-flight (OPTIONS) requests succeed.
app.UseCors("AngularSpa");

// Rate limiting runs before authentication so throttling protects the (anonymous)
// auth endpoints from brute-force attempts.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Maps all attribute-routed controllers, including the unversioned HealthController
// at /health (Gate 7). Health checks are intentionally NOT mapped to /health to avoid
// a route collision — the AAP designates the HealthController as the owner of /health.
app.MapControllers();

app.Run();

// =============================================================================
//  Test seam (Phase 11)
//  -----------------------------------------------------------------------------
//  Exposing the implicitly-generated top-level Program class as `public partial`
//  lets DnnMigration.IntegrationTests reference WebApplicationFactory<Program> to
//  boot the real pipeline in-process (Gate 5). Without this the generated Program
//  type is internal and the integration tests fail to compile.
// =============================================================================

/// <summary>
/// Entry-point marker for the API host. Declared <c>public partial</c> so that
/// <c>Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory&lt;Program&gt;</c> in the
/// integration-test project can reference the composition root and boot the full
/// ASP.NET Core pipeline in-process. This class intentionally has no members.
/// </summary>
public partial class Program { }
