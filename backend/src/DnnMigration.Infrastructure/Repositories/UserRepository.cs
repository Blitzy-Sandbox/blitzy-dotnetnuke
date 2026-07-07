using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/> backed by <see cref="DnnDbContext"/>.
/// </summary>
// MIGRATION: Re-expresses the data-access portion of the legacy VB.NET
// DotNetNuke.Entities.Users.UserController (Library/Components/Users/UserController.vb) as EF Core LINQ.
// The legacy provider-based membership access (memberProvider.GetUserByUserName / stored-proc readers +
// CBO hydration) is dropped in favour of DnnDbContext DbSet<User> queries. The composed UserMembership /
// UserProfile owned types are auto-loaded by EF with the User. No business logic (no password
// hashing/verification) lives here — that is AuthService's responsibility.
public class UserRepository : IUserRepository
{
    private readonly DnnDbContext _context;

    public UserRepository(DnnDbContext context)
    {
        _context = context;
    }

    // MIGRATION: UserController.GetUser / MembershipProvider.GetUser(userId) single lookup.
    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserID == id, cancellationToken);
    }

    // MIGRATION: aggregate of the legacy per-portal GetUsers readers; unfiltered AsNoTracking projection
    // to satisfy the generic IRepository<User> contract.
    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: UserController.GetUserByName(portalId, username) [UserController.vb L544] — legacy was
    // Public Shared (static) delegating to memberProvider.GetUserByUserName(portalId, username, False);
    // converted to a DI instance method. The owned Membership is auto-loaded so AuthService can verify
    // the legacy aspnet_Membership hash (forward-hash-on-login handled in the Application layer).
    public async Task<User?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PortalID == portalId && u.Username == username, cancellationToken);
    }

    // MIGRATION: UserController.GetUsers(portalId) [UserController.vb L685] — legacy Public Shared
    // returning an ArrayList of UserInfo for the portal; converted to a DI instance async collection.
    public async Task<IEnumerable<User>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.PortalID == portalId)
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: UserController.AddUser / MembershipProvider.AddUser -> EF Core insert.
    public async Task<User> AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        _context.Users.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    // MIGRATION: UserController.UpdateUser / MembershipProvider.UpdateUser -> EF Core update.
    public async Task UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: UserController.DeleteUser [UserController.vb L200] -> EF Core delete. Tracked fetch
    // (no AsNoTracking) so EF can mark the entity Deleted, then remove.
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users
            .FirstOrDefaultAsync(u => u.UserID == id, cancellationToken);
        if (entity is not null)
        {
            _context.Users.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
