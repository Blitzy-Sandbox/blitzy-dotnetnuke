// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// ASP.NET Core 8 application entry point implementing the minimal hosting model.
// MIGRATION: Replaces legacy DNN 4.x Global.asax Application_Start and web.config configuration.
//
// This file configures:
// - Dependency injection for all application services (repositories, services, DbContext)
// - Serilog structured logging replacing legacy DNN logging
// - JWT Bearer authentication replacing Forms Authentication
// - Swagger/OpenAPI documentation for REST API endpoints
// - CORS policy for Angular SPA frontend communication
// - Middleware pipeline for the BFF (Backend-for-Frontend) pattern
//
// Section 0.4.1 - Program.cs (CREATE) - Application entry point
// Section 0.3.3 - Dependency Injection pattern with AddScoped registrations
// Section 0.5.1 - Microsoft.AspNetCore.Authentication.JwtBearer, Swashbuckle.AspNetCore, Serilog.AspNetCore
// Section 0.3.5 - Target appsettings.json structure for JWT configuration
// Section 0.7.2 - C# 12 coding standards
// Section 0.1.1 - ASP.NET Core 8 BFF pattern implementation
// -----------------------------------------------------------------------------

using System.Reflection;
using System.Text;
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
using Microsoft.OpenApi.Models;
using Serilog;

// -----------------------------------------------------------------------------
// Configure Serilog for structured logging
// MIGRATION: Replaces DotNetNuke.Services.Log.EventLog and legacy logging providers
// -----------------------------------------------------------------------------
// Check if we're running in a testing environment to avoid bootstrap logger issues
// with parallel test execution (the bootstrap logger can only be frozen once).
// Multiple detection methods for reliability:
// 1. Environment variable check (for explicit override)
// 2. Entry assembly name check (for xUnit/test runner detection)
// 3. Check if already running under a test host
var isTestEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing"
    || Assembly.GetEntryAssembly()?.GetName().Name?.Contains("testhost", StringComparison.OrdinalIgnoreCase) == true
    || AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName?.Contains("xunit", StringComparison.OrdinalIgnoreCase) == true);

if (!isTestEnvironment)
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateBootstrapLogger();
}
else
{
    // In testing environment, use a simple logger without bootstrap/freeze semantics
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();
}

try
{
    Log.Information("Starting DnnMigration API application");

    var builder = WebApplication.CreateBuilder(args);

    // -----------------------------------------------------------------------------
    // Configure Serilog from appsettings.json
    // MIGRATION: Replaces legacy DNN logging configuration from web.config
    // Skip full Serilog configuration in testing to avoid bootstrap logger frozen issues
    // -----------------------------------------------------------------------------
    if (!isTestEnvironment)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        });
    }
    else
    {
        // In testing, just add Serilog without the reloadable logger pattern
        builder.Host.UseSerilog();
    }

    // -----------------------------------------------------------------------------
    // Configure Entity Framework Core DbContext
    // MIGRATION: Replaces SqlDataProvider with EF Core 8 DbContext
    // Source: Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb
    // Connection string pattern matches original _connectionString field usage
    // -----------------------------------------------------------------------------
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' not found in configuration.");

    builder.Services.AddDbContext<DnnDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlServerOptions =>
        {
            // Configure retry on transient failures for production resilience
            sqlServerOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);

            // Configure command timeout for long-running queries
            sqlServerOptions.CommandTimeout(30);
        });

        // Enable sensitive data logging only in development
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    });

    // -----------------------------------------------------------------------------
    // Register Repository Implementations
    // MIGRATION: Replaces DataProvider.Instance() singleton pattern with DI
    // Section 0.3.3 - Dependency Injection pattern with AddScoped registrations
    // -----------------------------------------------------------------------------
    builder.Services.AddScoped<IPortalRepository, PortalRepository>();
    builder.Services.AddScoped<IModuleRepository, ModuleRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IRoleRepository, RoleRepository>();
    builder.Services.AddScoped<ITabRepository, TabRepository>();
    builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();

    // -----------------------------------------------------------------------------
    // Register Application Services
    // MIGRATION: Replaces legacy VB.NET Controller classes with service layer pattern
    // Section 0.4.2 - Service registrations from Application layer
    // -----------------------------------------------------------------------------
    builder.Services.AddScoped<IPortalService, PortalService>();
    builder.Services.AddScoped<IModuleService, ModuleService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IRoleService, RoleService>();
    builder.Services.AddScoped<ITabService, TabService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();

    // -----------------------------------------------------------------------------
    // Register Identity Services
    // MIGRATION: Replaces Forms Authentication and PortalSecurity.vb
    // Section 0.7.7 - Security Rules for JWT authentication
    // -----------------------------------------------------------------------------
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

    // -----------------------------------------------------------------------------
    // Configure AutoMapper
    // MIGRATION: Maps domain entities to DTOs for API responses
    // Section 0.4.2 - MappingProfile.cs as AutoMapper configuration
    // -----------------------------------------------------------------------------
    builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

    // -----------------------------------------------------------------------------
    // Configure FluentValidation
    // MIGRATION: Request DTO validation replacing legacy server-side validation
    // Section 0.5.1 - FluentValidation.AspNetCore 11.3.0
    // -----------------------------------------------------------------------------
    builder.Services.AddValidatorsFromAssembly(typeof(MappingProfile).Assembly);

    // -----------------------------------------------------------------------------
    // Configure Controllers
    // MIGRATION: ASP.NET Core Web API controllers replacing WebForms postback
    // -----------------------------------------------------------------------------
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // Configure JSON serialization to match API conventions
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
            options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    // -----------------------------------------------------------------------------
    // Configure Health Checks
    // MIGRATION: New functionality for container orchestration and monitoring
    // Section 0.1.2 - Success Criteria: Health checks return HTTP 200
    // -----------------------------------------------------------------------------
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<DnnDbContext>("database");

    // -----------------------------------------------------------------------------
    // Configure JWT Bearer Authentication
    // MIGRATION: Replaces Forms Authentication from PortalSecurity.vb
    // Section 0.3.5 - Target appsettings.json structure for JWT configuration
    // Section 0.7.7 - Security Rules for JWT Bearer tokens
    // -----------------------------------------------------------------------------
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtSecret = jwtSection.GetValue<string>("Secret")
        ?? throw new InvalidOperationException("JWT Secret not found in configuration.");
    var jwtIssuer = jwtSection.GetValue<string>("Issuer") ?? "DnnMigration";
    var jwtAudience = jwtSection.GetValue<string>("Audience") ?? "DnnMigration";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew for distributed systems
        };

        // Configure events for logging and debugging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.Headers.Append("Token-Expired", "true");
                }
                Log.Warning("JWT authentication failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Log.Debug("JWT challenge issued for path: {Path}", context.Request.Path);
                return Task.CompletedTask;
            }
        };
    });

    // -----------------------------------------------------------------------------
    // Configure Authorization
    // MIGRATION: Role-based authorization replacing DNN permission system
    // Section 0.7.7 - Authorization rules for role-based access
    // -----------------------------------------------------------------------------
    builder.Services.AddAuthorization(options =>
    {
        // Default policy requires authenticated user
        options.FallbackPolicy = options.DefaultPolicy;

        // Administrator policy for admin-only endpoints
        options.AddPolicy("RequireAdministrator", policy =>
            policy.RequireRole("Administrators", "SuperUsers"));

        // Portal-level admin policy
        options.AddPolicy("RequirePortalAdmin", policy =>
            policy.RequireRole("Administrators"));
    });

    // -----------------------------------------------------------------------------
    // Configure CORS
    // MIGRATION: Cross-Origin Resource Sharing for Angular SPA frontend
    // Section 0.1.1 - ASP.NET Core 8 BFF pattern implementation
    // -----------------------------------------------------------------------------
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:4200"];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AngularSpa", corsBuilder =>
        {
            corsBuilder
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });

        // Development policy for testing
        if (builder.Environment.IsDevelopment())
        {
            options.AddPolicy("Development", corsBuilder =>
            {
                corsBuilder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        }
    });

    // -----------------------------------------------------------------------------
    // Configure Swagger/OpenAPI
    // MIGRATION: API documentation replacing legacy ASPX help pages
    // Section 0.5.1 - Swashbuckle.AspNetCore 6.9.0
    // -----------------------------------------------------------------------------
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Version = "v1",
            Title = "DnnMigration API",
            Description = "REST API for the DotNetNuke Migration project. " +
                          "Provides endpoints for Portal, Module, User, Role, and Tab management.",
            Contact = new OpenApiContact
            {
                Name = "DNN Migration Project",
                Email = "support@dnnmigration.local"
            },
            License = new OpenApiLicense
            {
                Name = "MIT License",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        });

        // Configure JWT authentication in Swagger UI
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. " +
                          "Enter 'Bearer' [space] and then your token in the text input below. " +
                          "Example: 'Bearer 12345abcdef'",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Include XML comments for API documentation
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    // -----------------------------------------------------------------------------
    // Build the application
    // -----------------------------------------------------------------------------
    var app = builder.Build();

    // -----------------------------------------------------------------------------
    // Configure Exception Handling Middleware
    // MIGRATION: Global error handling replacing legacy DNN error pages
    // Section 0.7.6 - Error Handling Standards (RFC 7807)
    // -----------------------------------------------------------------------------
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.ContentType = "application/problem+json";

            var exceptionHandlerFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            if (exceptionHandlerFeature is not null)
            {
                var exception = exceptionHandlerFeature.Error;
                
                // Map exception types to HTTP status codes (RFC 7807)
                var (statusCode, title, type) = exception switch
                {
                    KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource Not Found", "https://dnnmigration.com/errors/not-found"),
                    ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", "https://dnnmigration.com/errors/bad-request"),
                    InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict", "https://dnnmigration.com/errors/conflict"),
                    UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "https://dnnmigration.com/errors/unauthorized"),
                    _ => (StatusCodes.Status500InternalServerError, "An error occurred while processing your request.", "https://tools.ietf.org/html/rfc7807")
                };
                
                context.Response.StatusCode = statusCode;
                
                // Log at appropriate level
                if (statusCode >= 500)
                {
                    Log.Error(exception, "Unhandled exception occurred");
                }
                else
                {
                    Log.Warning(exception, "Request failed with {StatusCode}: {Message}", statusCode, exception.Message);
                }

                var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Type = type,
                    Instance = context.Request.Path,
                    Detail = exception.Message
                };

                if (app.Environment.IsDevelopment())
                {
                    problemDetails.Extensions["exception"] = exception.GetType().Name;
                    problemDetails.Extensions["stackTrace"] = exception.StackTrace;
                }

                await context.Response.WriteAsJsonAsync(problemDetails);
            }
        });
    });

    // -----------------------------------------------------------------------------
    // Configure Serilog Request Logging
    // MIGRATION: Structured request logging replacing IIS logs
    // -----------------------------------------------------------------------------
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    // -----------------------------------------------------------------------------
    // Configure Swagger UI (Development and Staging only)
    // -----------------------------------------------------------------------------
    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "DnnMigration API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "DnnMigration API Documentation";
            options.EnableDeepLinking();
            options.EnablePersistAuthorization();
            options.DisplayRequestDuration();
        });
    }

    // -----------------------------------------------------------------------------
    // Configure HTTPS Redirection
    // -----------------------------------------------------------------------------
    app.UseHttpsRedirection();

    // -----------------------------------------------------------------------------
    // Configure CORS
    // MIGRATION: Enable Angular SPA to communicate with API
    // -----------------------------------------------------------------------------
    app.UseCors(app.Environment.IsDevelopment() ? "Development" : "AngularSpa");

    // -----------------------------------------------------------------------------
    // Configure Authentication & Authorization
    // MIGRATION: JWT Bearer authentication pipeline
    // -----------------------------------------------------------------------------
    app.UseAuthentication();
    app.UseAuthorization();

    // -----------------------------------------------------------------------------
    // Map Controllers
    // MIGRATION: Route API requests to ASP.NET Core controllers
    // -----------------------------------------------------------------------------
    app.MapControllers();

    // -----------------------------------------------------------------------------
    // Map Health Check Endpoints
    // MIGRATION: Container orchestration health probes
    // Section 0.1.2 - Success Criteria: Health checks return HTTP 200
    // -----------------------------------------------------------------------------
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration.TotalMilliseconds
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            };
            await context.Response.WriteAsJsonAsync(response);
        }
    });

    // Simple health endpoint for basic liveness probe
    app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
        .WithTags("Health")
        .AllowAnonymous();

    // Readiness endpoint that checks database connectivity
    app.MapGet("/health/ready", async (DnnDbContext dbContext) =>
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync();
            if (canConnect)
            {
                return Results.Ok(new { status = "ready", database = "connected", timestamp = DateTime.UtcNow });
            }
            return Results.Json(new { status = "not ready", database = "disconnected", timestamp = DateTime.UtcNow }, statusCode: 503);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database readiness check failed");
            return Results.Json(new { status = "not ready", database = "error", error = ex.Message, timestamp = DateTime.UtcNow }, statusCode: 503);
        }
    })
        .WithTags("Health")
        .AllowAnonymous();

    // -----------------------------------------------------------------------------
    // Run the application
    // -----------------------------------------------------------------------------
    Log.Information("DnnMigration API started successfully. Listening on configured endpoints.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// -----------------------------------------------------------------------------
// Program class declaration for integration testing support
// This partial class enables WebApplicationFactory<Program> in test projects
// MIGRATION: Required for ASP.NET Core integration testing pattern
// -----------------------------------------------------------------------------
/// <summary>
/// Program entry point class for the DnnMigration API application.
/// </summary>
/// <remarks>
/// This partial class declaration enables the use of <c>WebApplicationFactory&lt;Program&gt;</c>
/// in integration tests, which is the recommended pattern for testing ASP.NET Core applications.
/// </remarks>
public partial class Program { }
