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

    /// <summary>
    /// Retrieves a single bounded page of roles whose name or description contains <paramref name="query"/>
    /// (case-insensitive substring), ordered by <c>RoleID</c>, together with the total count of matches, for
    /// server-side search of <c>GET /api/roles?query=...</c>.
    /// </summary>
    // MIGRATION (QA finding - R10 Issue 13): the roles list previously had NO server-side search, so the SPA
    // could only filter the first bounded page in-memory and a newly created role beyond that page was
    // undiscoverable. This is the BOUNDED, searchable counterpart of GetPagedAsync mirroring the Portal /
    // Module / User SearchPagedAsync repositories: the SAME case-insensitive RoleName/Description substring
    // predicate is applied to both the COUNT and the Skip/Take window so paging over search results is honest.
    Task<PagedResult<Role>> SearchPagedAsync(string query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single bounded page of roles belonging to the specified portal whose name or description
    /// contains <paramref name="query"/> (case-insensitive substring), ordered by <c>RoleID</c>, together
    /// with the total count of matches within that portal.
    /// </summary>
    // MIGRATION (QA finding - R10 Issue 13): portal-scoped counterpart of <see cref="SearchPagedAsync"/>,
    // used when a non-host caller (confined to its own portal) supplies a ?query=.
    Task<PagedResult<Role>> SearchByPortalPagedAsync(int portalId, string query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the role with the given name within the specified portal, matched case-insensitively, or
    /// <c>null</c> if no such role exists. Used to enforce per-portal role-name uniqueness before a create
    /// or a rename.
    /// </summary>
    // MIGRATION (QA finding - R10 Issue 12): role names are unique per portal. The legacy DNN Roles schema
    // stores a LoweredRoleName companion column and the aspnet_Roles provider enforced a UNIQUE index on
    // (ApplicationId, LoweredRoleName) [InstallRoles.sql L86], performing all role-name lookups via
    // LOWER(@RoleName) = LoweredRoleName. This finder reproduces that case-insensitive, portal-scoped lookup
    // so RoleService can reject a duplicate name with a 409 Conflict rather than persisting a second row.
    Task<Role?> GetByNameAsync(int portalId, string roleName, CancellationToken cancellationToken = default);
}
