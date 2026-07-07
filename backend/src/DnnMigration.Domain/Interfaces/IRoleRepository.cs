using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository contract for the <see cref="Role"/> aggregate. Extends the generic
/// <see cref="IRepository{T}"/> CRUD surface with role-specific lookups.
/// </summary>
public interface IRoleRepository : IRepository<Role>
{
    /// <summary>Retrieves all roles belonging to the specified portal.</summary>
    // MIGRATION: legacy RoleController.GetPortalRoles(PortalId) [RoleController.vb L146] returned an
    // ArrayList of RoleInfo; converted to an async materialized collection (LINQ over DnnDbContext
    // downstream).
    Task<IEnumerable<Role>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);
}
