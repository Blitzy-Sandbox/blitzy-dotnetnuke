// MIGRATION: replaces Website/Global.asax (which inherited DotNetNuke.Common.Global) and
// Website/Default.aspx.vb (CDefault : IClientAPICallbackEventHandler) — the legacy DotNetNuke
// ASP.NET Web Forms bootstrap and page shell. The Web Forms request/postback/ViewState lifecycle and
// the DNN HttpModule pipeline are ELIMINATED, not ported: this file rebuilds the request pipeline in
// the ASP.NET Core 8 minimal-hosting model (WebApplicationBuilder) as a stateless, JSON-only
// Backend-for-Frontend (BFF). There is no server-side HTML/Razor rendering, no ViewState, and no
// postbacks. Every dependency is wired through the built-in DI container (the legacy static
// "Public Shared" controller/provider model is gone), and Forms Authentication + PortalSecurity DES
// are replaced by JWT bearer authentication + BCrypt password hashing.
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
using DnnMigration.Api.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
    var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                      ?? throw new InvalidOperationException(
                          $"Missing '{JwtSettings.SectionName}' configuration section.");

    // MIGRATION: HS256 signing key used to VALIDATE incoming bearer tokens (replaces DNN's DES/Forms
    // authentication secret). The production 256-bit secret is supplied out-of-band via environment
    // variable / user-secrets (see MIGRATION_NOTES.md and the empty "Jwt:SecretKey" in appsettings.json).
    // SymmetricSecurityKey rejects a zero-length key, and the JWT bearer handler materializes these
    // options on the FIRST request through the authentication middleware — including the anonymous
    // /health probe. This development-only fallback (>= 256 bits) therefore lets the host boot when no
    // secret is configured (the WebApplicationFactory integration tests of Gate 5 and the /health
    // container probe of Gate 7), and is never used once a real "Jwt:SecretKey" is present.
    var signingKey = string.IsNullOrWhiteSpace(jwtSettings.SecretKey)
        ? "dev-only-insecure-signing-key-change-me-please-0123456789"
        : jwtSettings.SecretKey;

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
    // MIGRATION: AuthService (registered above) additionally depends on a server-side refresh-token
    // store and an ambient-portal accessor (both realized in Infrastructure). They are registered here
    // so the composition root's DI graph is complete and resolvable at host build — WebApplicationFactory
    // enables ValidateOnBuild in Development, so an unregistered dependency of AuthService would fail
    // every integration test at startup. The in-memory refresh-token store MUST be a singleton so
    // issued/rotated tokens survive across requests (swap for a distributed store in production). The
    // portal-context accessor is the safe null-object fallback (reports "no ambient portal"), registered
    // scoped per its own migration note pending a host/alias-aware accessor at the API edge.
    builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
    builder.Services.AddScoped<IPortalContextAccessor, PortalContextAccessor>();

    // ===== 4.6 AutoMapper + FluentValidation — both scan the DnnMigration.Application assembly. =====
    builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
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
    builder.Services.AddAuthorization();

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
        options.AddFixedWindowLimiter("auth", limiterOptions =>
        {
            limiterOptions.PermitLimit = 5;
            limiterOptions.Window = TimeSpan.FromSeconds(30);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 0;
        });
    });

    var app = builder.Build();

    // ===== Request pipeline (ORDER MATTERS). Stateless JSON; no ViewState/postback. =====

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
