using DnnMigration.Domain.Common;
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

    /// <summary>
    /// Retrieves a single bounded page of roles (ordered by <c>RoleID</c>) together with the total role
    /// count, for server-side pagination of <c>GET /api/roles</c>.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="IRepository{T}.GetAllAsync"/>.
    // GetAllAsync is left untouched; this fetches only the Skip/Take window (ordered by the RoleID primary
    // key) plus a COUNT so the list endpoint never streams an unbounded body.
    Task<PagedResult<Role>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single bounded page of roles belonging to the specified portal (ordered by
    /// <c>RoleID</c>) together with the total count for that portal.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="GetByPortalAsync"/>.
    Task<PagedResult<Role>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default);
}
