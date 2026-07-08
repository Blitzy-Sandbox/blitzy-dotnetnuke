// MIGRATION: replaces Website/Global.asax (which inherited DotNetNuke.Common.Global) and
// Website/Default.aspx.vb (CDefault : IClientAPICallbackEventHandler) — the legacy DotNetNuke
// ASP.NET Web Forms bootstrap and page shell. The Web Forms request/postback/ViewState lifecycle and
// the DNN HttpModule pipeline are ELIMINATED, not ported: this file rebuilds the request pipeline in
// the ASP.NET Core 8 minimal-hosting model (WebApplicationBuilder) as a stateless, JSON-only
// Backend-for-Frontend (BFF). There is no server-side HTML/Razor rendering, no ViewState, and no
// postbacks. Every dependency is wired through the built-in DI container (the legacy static
// "Public Shared" controller/provider model is gone), and Forms Authentication + PortalSecurity DES
// are replaced by JWT bearer authentication + BCrypt password hashing.
using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Application.Validators;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using DnnMigration.Infrastructure.Identity;
using DnnMigration.Infrastructure.Repositories;
using DnnMigration.Api.Identity;
using DnnMigration.Api.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Security.Claims;

// A Serilog bootstrap logger captures anything logged during host construction (before the
// fully-configured logger is built from configuration). It is swapped out for the real logger by the
// builder.Host.UseSerilog(...) call below. The whole host lifecycle is wrapped in try/catch/finally so
// that a fatal startup error is logged and every buffered log event is flushed on exit.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting DnnMigration.Api host");

    var builder = WebApplication.CreateBuilder(args);

    // ----- Structured logging (Serilog) — replaces the DNN logging providers. Reads the "Serilog"
    //       section from configuration, pulls DI-registered enrichers/sinks, and enriches every event
    //       from the ambient LogContext (CorrelationIdMiddleware pushes the correlation id there). -----
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ===== 4.1 Options: bind the "Jwt" section and materialize a non-null instance for the bearer handler. =====
    var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
    var jwtSettings = jwtSection.Get<JwtSettings>()
                      ?? throw new InvalidOperationException(
                          $"Missing '{JwtSettings.SectionName}' configuration section.");

    // MIGRATION: HS256 signing key used to ISSUE (JwtTokenService) and VALIDATE (JWT bearer handler below)
    // bearer tokens — the single replacement for DNN's DES/Forms authentication secret. HS256 requires a
    // key of at least 256 bits (32 UTF-8 bytes); a shorter/blank key is treated as "not configured".
    //
    // F1 (security fix): the previous implementation substituted a HARDCODED fallback key whenever
    // "Jwt:SecretKey" was blank. That fallback applied in EVERY environment (Production included) and was
    // consumed ONLY by the bearer VALIDATION path here, while JwtTokenService (the ISSUING path) threw on
    // the same blank key — a "split brain" in which a token forged with the well-known fallback key could
    // be accepted in Production. The fallback is removed. A single validated key is resolved ONCE and fed
    // to BOTH consumers (see PostConfigure below and IssuerSigningKey in §4.9), with these rules:
    //   * Non-Development: "Jwt:SecretKey" MUST be present and >= 256 bits, otherwise the host fails fast
    //     at startup — no insecure key path can ever reach a non-Development environment.
    //   * Development ONLY: if no adequate key is configured, a clearly-marked, non-secret development-only
    //     key is used so the host still boots for local runs, the Gate 5 WebApplicationFactory tests (which
    //     run under the "Development" environment and never exercise the login / JwtTokenService path) and
    //     the anonymous Gate 7 /health probe. appsettings.Development.json supplies this key explicitly; the
    //     in-code value is a last-resort safety net and is unreachable outside Development.
    const int minSigningKeyBytes = 32; // 256 bits — the HS256 minimum.
    var signingKey = jwtSettings.SecretKey;
    if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < minSigningKeyBytes)
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development-only, non-secret placeholder (>= 256 bits). NEVER used outside Development.
            signingKey = "dnn-migration-development-only-jwt-signing-key-not-for-production-use";
            Log.Warning(
                "Jwt:SecretKey is not configured (or shorter than 256 bits); falling back to the " +
                "DEVELOPMENT-ONLY signing key. Configure a 256-bit 'Jwt:SecretKey' via environment " +
                "variable or user-secrets for any non-Development environment.");
        }
        else
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey is missing or shorter than 256 bits (32 bytes). Configure a strong " +
                "'Jwt:SecretKey' via environment variable or user-secrets before starting the " +
                "application outside the Development environment.");
        }
    }

    // Bind the options AND force the resolved key onto the bound instance so JwtTokenService (which reads
    // JwtSettings.SecretKey via IOptions) and the JWT bearer handler in §4.9 share ONE validated key. This
    // is what eliminates the F1 split-brain: there is now a single source of truth for the signing key.
    builder.Services.Configure<JwtSettings>(jwtSection);
    builder.Services.PostConfigure<JwtSettings>(options => options.SecretKey = signingKey);

    // ===== 4.2 EF Core 8 DnnDbContext (Infrastructure). =====
    // MIGRATION: replaces the ADO.NET SqlDataProvider/SqlHelper stored-procedure stack. DnnDbContext is
    // provider-agnostic; SQL Server is supplied here against the EXISTING DNN / aspnet_* schema, while
    // the integration tests remove this DbContextOptions<DnnDbContext> descriptor and re-register
    // UseInMemoryDatabase via WebApplicationFactory. Registered plainly (default scoped lifetime) so
    // that standard test override pattern works.
    var connectionString = builder.Configuration.GetConnectionString("Default")
                           ?? throw new InvalidOperationException("Missing 'ConnectionStrings:Default'.");
    builder.Services.AddDbContext<DnnDbContext>(options => options.UseSqlServer(connectionString));

    // ===== 4.3 Repositories (Domain interfaces -> Infrastructure implementations), SCOPED. =====
    // MIGRATION: all data access flows through repository interfaces; services never touch DbContext.
    builder.Services.AddScoped<IPortalRepository, PortalRepository>();
    builder.Services.AddScoped<IModuleRepository, ModuleRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IRoleRepository, RoleRepository>();
    builder.Services.AddScoped<ITabRepository, TabRepository>();
    builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();

    // ===== 4.4 Application services (business rules extracted from the legacy *Controller.vb triad), SCOPED. =====
    builder.Services.AddScoped<IPortalService, PortalService>();
    builder.Services.AddScoped<IModuleService, ModuleService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IRoleService, RoleService>();
    builder.Services.AddScoped<ITabService, TabService>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    // ===== 4.5 Identity ports -> Infrastructure implementations. =====
    // MIGRATION: replaces PortalSecurity DES + FormsAuthentication. JwtTokenService (issue/validate) and
    // PasswordHasher (BCrypt) are stateless/thread-safe, so both are singletons.
    builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
    builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
    // MIGRATION: AuthService (registered above) additionally depends on a server-side refresh-token store
    // and an ambient-portal accessor. They are registered here so the composition root's DI graph is
    // complete and resolvable at host build — WebApplicationFactory enables ValidateOnBuild in Development,
    // so an unregistered dependency of AuthService would fail every integration test at startup. The
    // in-memory refresh-token store MUST be a singleton so issued/rotated tokens survive across requests
    // (swap for a distributed store in production).
    builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();

    // F2 (security fix): resolve the TRUSTED ambient portal from the request host at the API edge. The
    // host/alias-aware HttpPortalContextAccessor (Api/Identity) reads IHttpContextAccessor and maps the
    // request host to a PortalID via the PortalAlias table, REPLACING the Infrastructure
    // PortalContextAccessor null-object (which always returned null and left AuthService trusting the
    // client-supplied PortalId). With a real ambient portal resolved, AuthService now rejects a mismatched
    // client-supplied PortalId, closing the portal-scoping hole. Registered SCOPED so it shares the request
    // scope of the scoped IPortalRepository it depends on. AddHttpContextAccessor() registers the singleton
    // IHttpContextAccessor the accessor needs to read the current request host.
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IPortalContextAccessor, HttpPortalContextAccessor>();

    // ===== 4.6 AutoMapper + FluentValidation — both scan the DnnMigration.Application assembly. =====
    // MIGRATION / F3: AutoMapper 15's AddAutoMapper requires a configuration action (the assembly-only
    // overload was removed at v15). AddMaps(assembly) preserves the previous behaviour of scanning the
    // DnnMigration.Application assembly for Profile types (i.e. MappingProfile).
    builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(MappingProfile).Assembly));
    builder.Services.AddValidatorsFromAssembly(typeof(MappingProfile).Assembly);
    builder.Services.AddFluentValidationAutoValidation();

    // ===== 4.7 Controllers + JSON + ProblemDetails + API explorer. =====
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // MIGRATION: emit enums as readable strings for the Angular client (e.g. UserRegistrationType,
            // BannerType) rather than numeric ordinals. The default camelCase property naming policy is
            // preserved (NOT set to null) so the JSON contract matches Angular conventions and the
            // generated OpenAPI/Swagger schema the frontend models follow.
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddProblemDetails(); // RFC 7807 problem+json support for error responses.

    // ===== 4.8 Swagger / OpenAPI 3.0 with a JWT bearer security scheme. =====
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "DnnMigration API", Version = "v1" });

        // Bearer security definition + requirement so the Swagger UI "Authorize" button works.
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme."
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // ===== 4.9 Authentication (JWT bearer) + Authorization. =====
    // MIGRATION: replaces DNN FormsAuthentication + PortalSecurity DES. SecurityAccessLevel
    // (Anonymous/View/Edit/Admin/Host) becomes [AllowAnonymous]/[Authorize] + role claims. The token
    // validation parameters mirror exactly how JwtTokenService issues tokens: symmetric HS256 over the
    // UTF-8 bytes of the signing key, with matching issuer/audience and zero clock skew.
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false; // MIGRATION: preserve custom claim names (e.g. portalId / isSuperUser) exactly as issued.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.Name
            };
        });
    // MIGRATION: replaces the DNN PortalSecurity.SecurityAccessLevel ladder
    // (Anonymous/View/Edit/Admin/Host) with ASP.NET Core role/claims authorization (AAP §0.6.4).
    // Two things are configured here:
    //   1) "PortalAdministrator" — the VERTICAL gate applied via [Authorize(Policy = ...)] to every
    //      resource controller (Portals/Modules/Users/Roles/Tabs). A caller satisfies it when the JWT
    //      carries the "Administrators" role OR the isSuperUser=true (host) claim. This closes the
    //      broken-access-control gap where ANY authenticated principal — regardless of role, portal,
    //      or super-user status — could perform admin CRUD (vertical privilege escalation).
    //   2) FallbackPolicy — secure-by-default: any endpoint that neither opts out with
    //      [AllowAnonymous] nor declares its own policy still requires an authenticated caller, so a
    //      newly added endpoint cannot silently ship open.
    // The HORIZONTAL (cross-portal) gate — the isSuperUser-exempt "portalId" scoping — is enforced per
    // action in ApiControllerBase (RequirePortalAccess/RequireSuperUser), because it depends on the
    // specific resource's owning portal and cannot be expressed as a static policy.
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("PortalAdministrator", policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Administrators")
                || context.User.HasClaim("isSuperUser", "true")));

        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    // ===== 4.10 CORS restricted to the Angular SPA origin(s). =====
    // Bearer tokens travel in the Authorization header (not cookies), so AllowCredentials is not required.
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? Array.Empty<string>();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AngularSpa", policy =>
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });

    // ===== 4.11 Rate limiting on the authentication endpoints (AAP 0.7.1). =====
    // A named fixed-window limiter the AuthController opts into via [EnableRateLimiting("auth")].
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // MIGRATION: emit a standards-compliant Retry-After header on rejected (429) requests so clients
        // know when to retry (RFC 6585 / RFC 9110). The fixed-window limiter surfaces the time remaining in
        // the current window as MetadataName.RetryAfter; it is written (rounded up to whole seconds, invariant
        // culture) when present. The status code is (re)asserted defensively — the limiter applies
        // RejectionStatusCode before this callback, but setting it here keeps the rejection response correct
        // even if the ordering ever changes.
        options.OnRejected = (context, _) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            return ValueTask.CompletedTask;
        };

        options.AddFixedWindowLimiter("auth", limiterOptions =>
        {
            limiterOptions.PermitLimit = 5;
            limiterOptions.Window = TimeSpan.FromSeconds(30);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 0;
        });
    });

    // ===== 4.12 Kestrel hardening: suppress the "Server: Kestrel" response header (information-
    //       disclosure hardening). No-op under the integration-test TestServer (which does not emit a
    //       Server header); effective for the live Kestrel host and the containerized deployment. =====
    builder.WebHost.ConfigureKestrel(kestrelOptions => kestrelOptions.AddServerHeader = false);

    var app = builder.Build();

    // ===== Request pipeline (ORDER MATTERS). Stateless JSON; no ViewState/postback. =====

    // 5.0 Security response headers — applied to EVERY response, including error/challenge responses.
    // MIGRATION: the legacy DNN Web Forms stack emitted none of these headers. This middleware runs FIRST
    // and registers the headers through Response.OnStarting, which fires just before the response is sent
    // regardless of which downstream component produced it — a normal controller result, the RFC 7807
    // exception handler (500), the JWT bearer challenge (401), the authorization failure (403), or the
    // rate-limiter rejection (429). Registering via OnStarting (rather than setting the headers eagerly)
    // is race-free and guarantees presence even when a downstream component writes the response itself.
    //   * X-Content-Type-Options: nosniff  — the Issue #2 finding: stops MIME-type sniffing.
    //   * X-Frame-Options: DENY             — clickjacking hardening (harmless for a JSON API).
    //   * Referrer-Policy: no-referrer      — do not leak the request URL in the Referer header.
    // Indexer assignment (not Add) is used so the headers are set idempotently without risking duplicates.
    app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            return Task.CompletedTask;
        });

        await next();
    });

    // 5.1 Serilog request logging (one enriched completion event per request).
    app.UseSerilogRequestLogging();

    // 5.2 Correlation id FIRST (so the id is on the LogContext and the response before anything else
    //     runs), then the exception handler that maps unhandled exceptions to RFC 7807 ProblemDetails.
    //     These replace the legacy DNN HttpModules pipeline.
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // 5.3 Swagger / OpenAPI (Development). Enabling it here satisfies local dev and the AAP's
    //     auto-generated OpenAPI deliverable; it may be enabled unconditionally if desired.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // 5.4 Transport security. In the container the app listens on plain HTTP :8080 behind nginx (which
    //     terminates TLS), so there is deliberately NO app.UseHttpsRedirection() — an unconditional
    //     in-app redirect would 307 the GET /health probe and fail Gate 7. HSTS is advertised in
    //     non-development environments only.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts(); // MIGRATION: transport security via HSTS + nginx TLS termination, not an in-app HTTPS redirect.
    }

    // 5.5 CORS -> rate limiter -> authentication -> authorization -> controllers.
    app.UseCors("AngularSpa");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DnnMigration.Api host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// MIGRATION: exposed as a public partial class so DnnMigration.IntegrationTests (Validation Gate 5) can
// bootstrap the API in-memory via WebApplicationFactory<Program>. With top-level statements the compiler
// synthesizes an internal Program class; this declaration promotes it to public so the test project can
// reference it as the generic type argument. Without this line the integration test project fails to compile.
public partial class Program { }
