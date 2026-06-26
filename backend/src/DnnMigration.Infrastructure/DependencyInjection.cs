using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using DnnMigration.Infrastructure.Identity;
using DnnMigration.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DnnMigration.Infrastructure;

// MIGRATION: Replaces the legacy reflection-instantiated provider singleton
// (Framework.Reflection.CreateObject in Library/Components/Providers/Data/DataProvider.vb L44, selected by the
// <data defaultProvider="SqlDataProvider"> block in Website/release.config) with explicit, constructor-injectable
// service registrations. DataProvider.Instance() callers become constructor-injected I*Repository / IUnitOfWork.
//
// MIGRATION (port/adapter coordination): IPasswordHasher and IJwtService are Application-layer PORTS that live in
// DnnMigration.Application.Interfaces (the Application layer owns the abstractions per Clean/Onion architecture and
// references Domain only). Their concrete ADAPTERS (DnnMigration.Infrastructure.Identity.PasswordHasher /
// .JwtService) live here in Infrastructure and are bound to those ports below. This file is the single composition
// point that connects Application abstractions to their Infrastructure implementations; the dependency direction
// (Infrastructure -> Application) is the correct one. This cross-project coordination is recorded in MIGRATION_NOTES.md.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // MIGRATION: EF Core DnnDbContext replaces DataProvider/SqlDataProvider/SqlHelper (ADO.NET + stored procs).
        // Connection string moved from <connectionStrings name="SiteSqlServer"> (Website/release.config L36-L38)
        // to ConnectionStrings:DefaultConnection in appsettings.json.
        services.AddDbContext<DnnDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // MIGRATION: each DataProvider.Instance() data operation becomes a scoped repository sharing the request's DnnDbContext.
        services.AddScoped<IPortalRepository, PortalRepository>();
        services.AddScoped<IModuleRepository, ModuleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ITabRepository, TabRepository>();

        // MIGRATION: the DataProvider transaction trio (GetTransaction/CommitTransaction/RollbackTransaction, DataProvider.vb L71-74)
        // becomes IUnitOfWork over DnnDbContext.Database transactions + SaveChangesAsync. Scoped so repos + UoW share one DbContext.
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // MIGRATION: PortalSecurity DES Encrypt/Decrypt -> one-way BCrypt hashing. Stateless -> singleton-safe.
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // MIGRATION: AspNetSqlMembershipProvider + Forms-auth ticket -> JWT issuance (60-min access token + refresh rotation, AAP 0.7.6).
        // Registered as a SINGLETON because JwtService owns an in-memory refresh-token store that must survive across requests for
        // ValidateRefreshToken/RevokeRefreshToken to function. If a future phase moves the refresh store to a DB-backed repository,
        // change this to AddScoped (a singleton may not capture a scoped DbContext-backed dependency).
        services.AddSingleton<IJwtService, JwtService>();

        return services;
    }
}
