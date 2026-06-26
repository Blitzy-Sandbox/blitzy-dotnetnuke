using System.Text;
using System.Threading.RateLimiting;
using DnnMigration.Api.Middleware;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Services;
using DnnMigration.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    builder.Services.AddControllers();
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
    builder.Services.AddAutoMapper(applicationAssembly);
    builder.Services.AddValidatorsFromAssembly(applicationAssembly);
    builder.Services.AddScoped<IPortalService, PortalService>();
    builder.Services.AddScoped<IModuleService, ModuleService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IRoleService, RoleService>();
    builder.Services.AddScoped<ITabService, TabService>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    // ----- Infrastructure layer DI: EF Core DnnDbContext, repositories, IUnitOfWork, IPasswordHasher, IJwtService -----
    builder.Services.AddInfrastructure(builder.Configuration);

    // ----- Authentication / Authorization (JWT Bearer) -----
    // MIGRATION: replaces Forms authentication + AspNetSqlMembershipProvider (Website/release.config).
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwtSection["Key"];
    if (string.IsNullOrWhiteSpace(jwtKey))
    {
        // MIGRATION/SECURITY: no signing key configured. Fall back to a clearly-insecure development key so
        // the host can still start (e.g., the anonymous /health endpoint required by Gate 7), but warn loudly.
        // Production MUST supply Jwt:Key via the environment variable Jwt__Key or a secret manager (AAP 0.7.6);
        // never commit a real signing key.
        jwtKey = "DnnMigration-INSECURE-DEV-FALLBACK-Signing-Key-set-Jwt__Key-in-production-0123456789";
        Log.Warning("Jwt:Key is not configured; using an INSECURE development fallback signing key. Set Jwt__Key in production.");
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
    builder.Services.AddAuthorization();

    // ----- CORS restricted to the Angular SPA origin -----
    const string corsPolicyName = "AngularSpa";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? new[] { "http://localhost:4200" };
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(corsPolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
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
}
finally
{
    Log.CloseAndFlush();
}

// MIGRATION: expose the implicit top-level-statement Program class as public + partial so the
// DnnMigration.IntegrationTests project can construct WebApplicationFactory<Program> (Gate 5).
public partial class Program { }
