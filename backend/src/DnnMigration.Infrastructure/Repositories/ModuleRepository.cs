using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IModuleRepository"/> backed by <see cref="DnnDbContext"/>.
/// </summary>
// MIGRATION: Re-expresses the data-access portion of the legacy VB.NET
// DotNetNuke.Entities.Modules.ModuleController (Library/Components/Modules/ModuleController.vb) as EF Core
// LINQ. Legacy access went through DataProvider.Instance().GetModules/GetModuleByDefinition (IDataReader)
// with FillModuleInfoCollection/FillModuleInfo (CBO reflection hydration) and DataCache; that
// provider/CBO/cache indirection is dropped in favour of DnnDbContext DbSet<Module> queries. Data access only.
public class ModuleRepository : IModuleRepository
{
    private readonly DnnDbContext _context;

    public ModuleRepository(DnnDbContext context)
    {
        _context = context;
    }

    // MIGRATION: ModuleController.GetModule / DataProvider.GetModule single-reader lookup.
    public async Task<Module?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleID == id, cancellationToken);
    }

    // MIGRATION: no exact 1:1 legacy "all modules across all portals" reader; provided to satisfy the
    // generic IRepository<Module> contract as an unfiltered AsNoTracking projection.
    public async Task<IEnumerable<Module>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: ModuleController.GetModules(PortalID) [ModuleController.vb L915] —
    // FillModuleInfoCollection(DataProvider.Instance().GetModules(PortalID)). Permission-hydration
    // overload (L928) is omitted (hydration is not repository data access).
    public async Task<IEnumerable<Module>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Where(m => m.PortalID == portalId)
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: ModuleController.GetModuleByDefinition(PortalId, FriendlyName) [ModuleController.vb L955].
    // The legacy Dictionary<string, ModuleInfo> DataCache lookup is dropped; the DB fallback query
    // (first module instance in the portal with the given definition friendly name) is preserved.
    public async Task<Module?> GetByDefinitionAsync(int portalId, string friendlyName, CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.PortalID == portalId && m.FriendlyName == friendlyName, cancellationToken);
    }

    // MIGRATION: ModuleController.AddModule -> DataProvider.AddModule stored proc.
    public async Task<Module> AddAsync(Module entity, CancellationToken cancellationToken = default)
    {
        _context.Modules.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    // MIGRATION: ModuleController.UpdateModule -> DataProvider.UpdateModule stored proc.
    public async Task UpdateAsync(Module entity, CancellationToken cancellationToken = default)
    {
        _context.Modules.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: ModuleController.DeleteModule -> DataProvider.DeleteModule stored proc. Tracked fetch
    // (no AsNoTracking) so EF can mark the entity Deleted, then remove.
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Modules
            .FirstOrDefaultAsync(m => m.ModuleID == id, cancellationToken);
        if (entity is not null)
        {
            _context.Modules.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
