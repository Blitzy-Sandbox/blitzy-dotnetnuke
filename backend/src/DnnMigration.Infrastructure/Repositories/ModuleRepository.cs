using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core 8 LINQ implementation of <see cref="IModuleRepository"/>.
/// Replaces the legacy <c>ModuleController.vb</c> data methods and <c>SqlDataProvider.vb</c> Module
/// stored-procedure calls. Reads mirror the legacy <c>vw_Modules</c> view by filtering out soft-deleted rows.
/// </summary>
public class ModuleRepository : IModuleRepository
{
    private readonly DnnDbContext _context;

    public ModuleRepository(DnnDbContext context)
    {
        _context = context;
    }

    public async Task<Module?> GetByIdAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // Single-entity lookup is intentionally unfiltered by IsDeleted so callers (and the soft-delete
        // path below) can still resolve a module by its identifier.
        return await _context.Modules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleID == moduleId, cancellationToken);
    }

    public async Task<IEnumerable<Module>> GetByTabAsync(int tabId, CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Where(m => m.TabID == tabId && !m.IsDeleted)
            .OrderBy(m => m.ModuleOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Module>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Where(m => m.PortalID == portalId && !m.IsDeleted)
            .OrderBy(m => m.ModuleID)
            .ToListAsync(cancellationToken);
    }

    public async Task<Module> AddAsync(Module module, CancellationToken cancellationToken = default)
    {
        await _context.Modules.AddAsync(module, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return module;
    }

    public async Task UpdateAsync(Module module, CancellationToken cancellationToken = default)
    {
        _context.Modules.Update(module);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Module delete is a SOFT delete (IsDeleted = true), matching the Modules table's
        // [IsDeleted] bit column and the legacy DeleteTabModule behavior. The fetch is intentionally
        // unfiltered so an already-loaded module can still be flagged.
        var module = await _context.Modules
            .FirstOrDefaultAsync(m => m.ModuleID == moduleId, cancellationToken);

        if (module is null)
        {
            return;
        }

        module.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
