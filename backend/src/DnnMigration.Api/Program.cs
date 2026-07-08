// MIGRATION: ASP.NET Core 8 composition root and Backend-for-Frontend (BFF) host. Replaces the
// legacy DotNetNuke Global.asax application lifecycle and the Default.aspx.vb Web Forms entry
// point. This is the single place that binds the Application/Domain interfaces to their
// Infrastructure implementations through the built-in DI container, builds the stateless JSON
// request pipeline, and issues/validates JWTs (superseding Forms Authentication).
using System.Text;
using DnnMigration.Api.Middleware;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using DnnMigration.Infrastructure.Identity;
using DnnMigration.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ----- Structured logging (Serilog), replacing the legacy DNN logging providers. -----
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

// ----- Options binding: JWT issuer/audience/signing key/lifetimes from the "Jwt" section. -----
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

// ----- EF Core 8 DnnDbContext mapped to the existing DNN / aspnet_* schema. -----
builder.Services.AddDbContext<DnnDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default") ?? string.Empty));

// ----- Repositories: all data access flows through repository interfaces. -----
builder.Services.AddScoped<IPortalRepository, PortalRepository>();
builder.Services.AddScoped<IModuleRepository, ModuleRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<ITabRepository, TabRepository>();
builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();

// ----- Identity: JWT issuance/validation + BCrypt hashing (replaces PortalSecurity / DES / Forms auth). -----
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
// In-memory refresh-token store is a singleton so issued tokens survive across requests
// (finding F2: server-side rotation/revocation). Swap for a distributed store in production.
builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPortalContextAccessor, PortalContextAccessor>();

// ----- Application services: business logic extracted from the legacy *Controller.vb triad. -----
builder.Services.AddScoped<IPortalService, PortalService>();
builder.Services.AddScoped<IModuleService, ModuleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ITabService, TabService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ----- AutoMapper + FluentValidation (both reside in the Application assembly). -----
var applicationAssembly = typeof(MappingProfile).Assembly;
builder.Services.AddAutoMapper(applicationAssembly);
builder.Services.AddValidatorsFromAssembly(applicationAssembly);

// ----- MVC controllers + OpenAPI / Swagger generation. -----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ----- JWT bearer authentication; validation parameters mirror JwtSettings exactly. -----
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
// MIGRATION: the real 256-bit signing key is supplied via environment variable / user-secrets
// (see MIGRATION_NOTES.md); this development fallback only lets the host boot when none is configured.
var signingKey = string.IsNullOrWhiteSpace(jwtSettings.SecretKey)
    ? "dev-only-insecure-signing-key-change-me-0123456789"
    : jwtSettings.SecretKey;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// ----- CORS restricted to the Angular SPA origin(s). -----
const string CorsPolicy = "AngularSpa";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// ----- Request pipeline: stateless JSON, no ViewState / postback. -----
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// MIGRATION: exposed as a public partial class so DnnMigration.IntegrationTests can bootstrap the
// API in-memory via WebApplicationFactory<Program> (AAP Validation Gate 5).
public partial class Program { }