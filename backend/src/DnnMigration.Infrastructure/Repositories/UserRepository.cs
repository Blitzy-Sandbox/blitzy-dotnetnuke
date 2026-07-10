using System.Text.Json;
using DnnMigration.Domain.Common;
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
// CBO hydration) is dropped in favour of DnnDbContext DbSet<User> queries. No business logic (no
// password hashing/verification) lives here — that is AuthService/UserService's responsibility.
//
// MIGRATION (SCHEMA FIDELITY — finding #1): the user aggregate spans THREE real tables — [Users] (int
// UserID identity), [UserPortals] (the portal-association junction), and [aspnet_Membership] (the
// GUID-keyed credential table). The previous model invented a [Users].[PortalID] column and mapped
// membership as an int-owned aspnet_Membership row, which cannot run against the existing schema. This
// repository therefore acts as the BRIDGE that keeps the model schema-faithful while preserving the
// runtime contract the Application layer depends on:
//   * PortalID — there is no [Users].[PortalID]; the value is written to / read from the [UserPortals]
//     junction and projected onto the transient User.PortalID carrier.
//   * Membership — User.Membership is not an EF navigation; the credential row is persisted to /
//     hydrated from [aspnet_Membership] using a DETERMINISTIC [UserId] projection of the DNN integer
//     UserID (MembershipKey). This invents no column and keeps the credential round-trip that
//     UserService.CreateAsync/ChangePasswordAsync (write) and AuthService.LoginAsync (read+verify) rely
//     on. Because GetByIdAsync/GetByUsernameAsync hydrate the REAL stored membership, a subsequent
//     UpdateAsync copies back the already-correct credential rather than wiping it.
public class UserRepository : IUserRepository
{
    private readonly DnnDbContext _context;

    // MIGRATION (SCHEMA FIDELITY — finding #1): [aspnet_Membership].[ApplicationId] is NOT NULL (FK ->
    // aspnet_Applications). Users created by the modern stack are stamped with this stable default
    // application id so the required column is always populated. See MIGRATION_NOTES.md.
    private static readonly Guid DefaultApplicationId = new("d2d0a9e4-9e5c-4c7b-9e4c-000000000001");

    // MIGRATION QA finding G (profile persistence): the real [aspnet_Profile] table stores profile data as
    // a serialized name/value blob (PropertyNames / PropertyValuesString / PropertyValuesBinary). The
    // modern stack persists the strongly-typed address/contact/locale fields as a compact JSON document in
    // [PropertyValuesString] and stamps [PropertyNames] with this sentinel so a read can positively
    // distinguish a blob written by this stack (parseable JSON) from a legacy DNN-format blob (which is
    // left untouched). See MIGRATION_NOTES.md.
    private const string ProfileBlobMarker = "__DnnMigration.ProfileJson.v1__";

    // Case-insensitive, minimal JSON contract for the profile blob (property names match ProfileBlob).
    private static readonly JsonSerializerOptions ProfileJsonOptions = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true
    };

    public UserRepository(DnnDbContext context)
    {
        _context = context;
    }

    // MIGRATION (SCHEMA FIDELITY — finding #1): deterministic projection of the DNN integer UserID onto
    // the uniqueidentifier [aspnet_Membership].[UserId]. It is a pure function (write-with-K, read-with-K),
    // so the credential row round-trips for users created by the modern stack, and it invents no column.
    private static Guid MembershipKey(int userId) => new(userId, 0, 0, new byte[8]);

    // MIGRATION: UserController.GetUser / MembershipProvider.GetUser(userId) single lookup. Hydrates the
    // transient PortalID (from [UserPortals]) and Membership (from [aspnet_Membership]) after the read so
    // the Application/DTO contract and AuthService credential checks see the real stored values.
    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserID == id, cancellationToken);
        if (user is not null)
        {
            await HydrateAsync(user, cancellationToken);
        }
        return user;
    }

    // MIGRATION: aggregate of the legacy per-portal GetUsers readers; unfiltered AsNoTracking projection
    // to satisfy the generic IRepository<User> contract.
    // MIGRATION QA finding F1 (list data fidelity): the list path now hydrates each user's transient
    // PortalID, Membership and Profile carriers (batched via HydrateManyAsync -- three queries total) so a
    // list row observes the SAME real values a single GetByIdAsync returns, rather than DateTime.MinValue
    // membership dates and an empty profile. The DTO boundary still strips the credential secrets
    // (password/salt) from the serialized response.
    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        await HydrateManyAsync(users, cancellationToken);
        return users;
    }

    // MIGRATION: UserController.GetUserByName(portalId, username) [UserController.vb L544] — legacy was
    // Public Shared (static) delegating to memberProvider.GetUserByUserName(portalId, username, False);
    // converted to a DI instance method. SCHEMA FIDELITY: usernames are unique PER PORTAL, so the portal
    // scope is applied through the [UserPortals] junction (there is no [Users].[PortalID] column). The
    // Membership is hydrated so AuthService can verify the stored aspnet_Membership hash.
    public async Task<User?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default)
    {
        var user = await (
            from u in _context.Users.AsNoTracking()
            join up in _context.UserPortals.AsNoTracking() on u.UserID equals up.UserId
            where up.PortalId == portalId && u.Username == username
            select u).FirstOrDefaultAsync(cancellationToken);
        if (user is not null)
        {
            await HydrateAsync(user, cancellationToken);
        }
        return user;
    }

    // MIGRATION: UserController.GetUsers(portalId) [UserController.vb L685] — legacy Public Shared
    // returning an ArrayList of UserInfo for the portal; converted to a DI instance async collection.
    // SCHEMA FIDELITY: portal membership is the [UserPortals] junction.
    // MIGRATION QA finding F1 (list data fidelity): the list path now hydrates each user's transient
    // Membership and Profile carriers (batched) so a list row carries the same real values a single GET
    // returns. PortalID is then pinned to the (known) query scope so it reflects the portal being listed.
    public async Task<IEnumerable<User>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        var users = await (
            from u in _context.Users.AsNoTracking()
            join up in _context.UserPortals.AsNoTracking() on u.UserID equals up.UserId
            where up.PortalId == portalId
            select u).ToListAsync(cancellationToken);

        await HydrateManyAsync(users, cancellationToken);
        foreach (var u in users)
        {
            // The explicit query scope wins over the batched lowest-portal projection so PortalID reflects
            // the portal actually being listed.
            u.PortalID = portalId;
        }
        return users;
    }

    // MIGRATION: Users.ascx.vb ddlSearchType + txtSearch -> UserController.GetUsersByUserName /
    // GetUsersByEmail (field-specific) and the general name search. Re-expressed as a case-insensitive
    // substring filter, optionally scoped to a single portal (preserving UsersController's per-portal
    // authorization scoping). A field-specific request (filterProperty = "Username" | "Email", matching
    // the SPA's ddlSearchType values) restricts matching to that one column; otherwise the free-text
    // query is matched across username/email/display-name/first-name/last-name. AsNoTracking (read path).
    // SCHEMA FIDELITY: when a portal scope is requested it is applied through the [UserPortals] junction
    // (there is no [Users].[PortalID] column). The unscoped (all-portals) path — used by host-level
    // administration — needs no junction row.
    // MIGRATION QA finding F1 (list data fidelity): the materialized result now hydrates each user's
    // transient PortalID, Membership and Profile carriers (batched) so a search-result row carries the same
    // real values a single GET returns.
    public async Task<IEnumerable<User>> SearchAsync(int? portalId, string? query, string? filterProperty, string? filter, CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = _context.Users.AsNoTracking();
        if (portalId.HasValue)
        {
            users =
                from u in users
                join up in _context.UserPortals.AsNoTracking() on u.UserID equals up.UserId
                where up.PortalId == portalId.Value
                select u;
        }

        if (!string.IsNullOrWhiteSpace(filterProperty) && !string.IsNullOrWhiteSpace(filter))
        {
            // Field-specific search takes precedence when both parts are supplied.
            var f = filter.ToLower();
            if (string.Equals(filterProperty, "Username", StringComparison.OrdinalIgnoreCase))
            {
                users = users.Where(u => u.Username != null && u.Username.ToLower().Contains(f));
            }
            else if (string.Equals(filterProperty, "Email", StringComparison.OrdinalIgnoreCase))
            {
                users = users.Where(u => u.Email != null && u.Email.ToLower().Contains(f));
            }
            // Any other filterProperty is unsupported and yields the (portal-scoped) unfiltered set.
        }
        else if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.ToLower();
            users = users.Where(u =>
                (u.Username != null && u.Username.ToLower().Contains(term)) ||
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.DisplayName != null && u.DisplayName.ToLower().Contains(term)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(term)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(term)));
        }

        var results = await users.ToListAsync(cancellationToken);
        await HydrateManyAsync(results, cancellationToken);
        return results;
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetAllAsync. One COUNT over the full [Users] set
    // plus one windowed SELECT ordered by the UserID primary key, then the SAME batched three-query
    // hydration the full-list path runs — so a paged row carries the identical real PortalID / membership
    // dates / profile a single GetByIdAsync returns (QA finding F1 fidelity preserved on the paged path).
    // AsNoTracking (read path).
    public async Task<PagedResult<User>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Users.AsNoTracking();
        var total = await baseQuery.CountAsync(cancellationToken);
        var users = await baseQuery
            .OrderBy(u => u.UserID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        await HydrateManyAsync(users, cancellationToken);
        return new PagedResult<User>(users, total);
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetByPortalAsync. The SAME [UserPortals]-junction
    // scope is applied to the base query (shared by COUNT and the page — the junction's composite key means
    // at most one row per (user, portal), so the count is one-per-user), only the Skip/Take window (ordered
    // by UserID) is materialized and hydrated, and PortalID is then pinned to the listed portal exactly as
    // the full-list GetByPortalAsync does. AsNoTracking (read path).
    public async Task<PagedResult<User>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery =
            from u in _context.Users.AsNoTracking()
            join up in _context.UserPortals.AsNoTracking() on u.UserID equals up.UserId
            where up.PortalId == portalId
            select u;

        var total = await baseQuery.CountAsync(cancellationToken);
        var users = await baseQuery
            .OrderBy(u => u.UserID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        await HydrateManyAsync(users, cancellationToken);
        foreach (var u in users)
        {
            // The explicit query scope wins over the batched lowest-portal projection so PortalID reflects
            // the portal actually being listed (parity with GetByPortalAsync).
            u.PortalID = portalId;
        }
        return new PagedResult<User>(users, total);
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of SearchAsync. The SAME optional portal scope +
    // field-specific / free-text substring predicate is built onto the base query (shared by COUNT and the
    // page), then only the Skip/Take window (ordered by UserID) is materialized and hydrated. AsNoTracking.
    public async Task<PagedResult<User>> SearchPagedAsync(int? portalId, string? query, string? filterProperty, string? filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = _context.Users.AsNoTracking();
        if (portalId.HasValue)
        {
            users =
                from u in users
                join up in _context.UserPortals.AsNoTracking() on u.UserID equals up.UserId
                where up.PortalId == portalId.Value
                select u;
        }

        if (!string.IsNullOrWhiteSpace(filterProperty) && !string.IsNullOrWhiteSpace(filter))
        {
            // Field-specific search takes precedence when both parts are supplied (parity with SearchAsync).
            var f = filter.ToLower();
            if (string.Equals(filterProperty, "Username", StringComparison.OrdinalIgnoreCase))
            {
                users = users.Where(u => u.Username != null && u.Username.ToLower().Contains(f));
            }
            else if (string.Equals(filterProperty, "Email", StringComparison.OrdinalIgnoreCase))
            {
                users = users.Where(u => u.Email != null && u.Email.ToLower().Contains(f));
            }
        }
        else if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.ToLower();
            users = users.Where(u =>
                (u.Username != null && u.Username.ToLower().Contains(term)) ||
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.DisplayName != null && u.DisplayName.ToLower().Contains(term)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(term)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(term)));
        }

        var total = await users.CountAsync(cancellationToken);
        var results = await users
            .OrderBy(u => u.UserID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        await HydrateManyAsync(results, cancellationToken);
        return new PagedResult<User>(results, total);
    }

    // MIGRATION: UserController.AddUser / MembershipProvider.AddUser -> EF Core insert. SCHEMA FIDELITY:
    // the identity row goes to [Users] (int IDENTITY UserID), the credential row to [aspnet_Membership]
    // (GUID key = MembershipKey(UserID)), and the portal association to the [UserPortals] junction.
    public async Task<User> AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        _context.Users.Add(entity);
        // First save assigns the [Users].[UserID] IDENTITY so the membership key projection and the
        // junction row can reference it.
        await _context.SaveChangesAsync(cancellationToken);

        _context.UserMemberships.Add(BuildMembershipRow(entity));
        _context.UserPortals.Add(new UserPortal
        {
            UserId = entity.UserID,
            PortalId = entity.PortalID,
            Authorised = true,
            CreatedDate = DateTime.UtcNow
        });

        // MIGRATION QA finding G (profile persistence): write the profile name/value blob to
        // [aspnet_Profile] so a subsequent read hydrates it. Batched into this second SaveChanges alongside
        // the credential + junction rows.
        await PersistProfileAsync(entity, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return entity;
    }

    // MIGRATION: UserController.UpdateUser / MembershipProvider.UpdateUser -> EF Core update. The [Users]
    // scalar columns are updated; the credential row is upserted from entity.Membership (which callers
    // hydrate via GetByIdAsync before mutating, so this copies back the correct credential rather than
    // wiping it); and the portal junction row is ensured to exist.
    public async Task UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(entity);

        var key = MembershipKey(entity.UserID);
        var existing = await _context.UserMemberships
            .FirstOrDefaultAsync(m => m.MembershipUserId == key, cancellationToken);
        if (existing is null)
        {
            _context.UserMemberships.Add(BuildMembershipRow(entity));
        }
        else
        {
            CopyMembership(entity.Membership, existing);
        }

        var hasPortal = await _context.UserPortals
            .AnyAsync(up => up.UserId == entity.UserID && up.PortalId == entity.PortalID, cancellationToken);
        if (!hasPortal)
        {
            _context.UserPortals.Add(new UserPortal
            {
                UserId = entity.UserID,
                PortalId = entity.PortalID,
                Authorised = true,
                CreatedDate = DateTime.UtcNow
            });
        }

        // MIGRATION QA finding G (profile persistence): upsert the profile name/value blob to
        // [aspnet_Profile]. Callers hydrate the user (and its profile) via GetByIdAsync before mutating,
        // and the AutoMapper UpdateUserDto->User profile mapping applies the flat address/contact/locale
        // fields onto entity.Profile, so this persists the merged (hydrated + updated) profile.
        await PersistProfileAsync(entity, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: UserController.DeleteUser [UserController.vb L200] -> EF Core delete. Removes the [Users]
    // row plus its related [aspnet_Membership] credential row and [UserPortals] junction rows (referential
    // cleanup — data access only; the legacy permission-cascade / event-log / mail side-effects remain a
    // separate behavioural-parity concern). Tracked fetch (no AsNoTracking) so EF can mark rows Deleted.
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users
            .FirstOrDefaultAsync(u => u.UserID == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _context.Users.Remove(entity);

        var key = MembershipKey(id);
        var membership = await _context.UserMemberships
            .FirstOrDefaultAsync(m => m.MembershipUserId == key, cancellationToken);
        if (membership is not null)
        {
            _context.UserMemberships.Remove(membership);
        }

        var portals = await _context.UserPortals
            .Where(up => up.UserId == id)
            .ToListAsync(cancellationToken);
        if (portals.Count > 0)
        {
            _context.UserPortals.RemoveRange(portals);
        }

        // MIGRATION QA finding G (profile persistence): remove the [aspnet_Profile] blob row that
        // AddAsync/UpdateAsync wrote for this user (keyed by the same deterministic MembershipKey
        // projection), so a delete leaves no orphaned profile row.
        var profileKey = MembershipKey(id);
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => EF.Property<Guid>(p, "UserId") == profileKey, cancellationToken);
        if (profile is not null)
        {
            _context.UserProfiles.Remove(profile);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION (QA finding - R10 Issue 2): portal-scoped cascade cleanup. The legacy
    // PortalController.DeletePortalInfo removed a portal's users (UserController.DeleteUsers
    // [PortalController.vb L1199]) before deleting the portal row [L1202]. In the modern schema there is NO
    // [Users].[PortalID] column and NO DB-level FK cascade from [Portals] to [Users]/[UserPortals], so
    // deleting a portal alone strands the admin user PortalService.CreateAsync provisioned (its [Users] +
    // [aspnet_Membership] + [aspnet_Profile] rows) and its [UserPortals] junction. This detaches every user
    // association to the target portal and fully cascade-deletes only the users left orphaned by that
    // detachment (those with no OTHER portal association), mirroring the single-user cascade in DeleteAsync
    // (Users + aspnet_Membership + UserPortals + aspnet_Profile), keyed by the deterministic MembershipKey
    // projection. A user still associated with another portal keeps its identity/credential/profile rows and
    // only loses its junction to this portal. Non-transactional to match AddAsync/DeleteAsync (InMemory has
    // no transaction); a single SaveChanges applies the whole cleanup. Tracked fetches (no AsNoTracking) so
    // EF marks the rows Deleted.
    public async Task DeleteByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // Every junction row binding any user to the target portal.
        var portalJunctions = await _context.UserPortals
            .Where(up => up.PortalId == portalId)
            .ToListAsync(cancellationToken);
        if (portalJunctions.Count == 0)
        {
            // No users are associated with this portal; there is nothing to detach or orphan.
            return;
        }

        var affectedUserIds = portalJunctions
            .Select(up => up.UserId)
            .Distinct()
            .ToList();

        // Of the affected users, those that still hold a junction to a DIFFERENT portal are retained; the
        // remainder are orphaned by removing their association to this portal and must be fully deleted.
        var usersWithOtherPortal = await _context.UserPortals
            .Where(up => up.PortalId != portalId && affectedUserIds.Contains(up.UserId))
            .Select(up => up.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var orphanUserIds = affectedUserIds
            .Except(usersWithOtherPortal)
            .ToList();

        // (1) Detach every affected user's association to THIS portal (retained + orphaned users alike).
        _context.UserPortals.RemoveRange(portalJunctions);

        if (orphanUserIds.Count > 0)
        {
            // (2) Delete the orphaned identity rows from [Users].
            var orphanUsers = await _context.Users
                .Where(u => orphanUserIds.Contains(u.UserID))
                .ToListAsync(cancellationToken);
            if (orphanUsers.Count > 0)
            {
                _context.Users.RemoveRange(orphanUsers);
            }

            // (3) Delete their credential rows from [aspnet_Membership] (GUID-keyed by MembershipKey(UserID)).
            var orphanKeys = orphanUserIds
                .Select(MembershipKey)
                .ToList();
            var memberships = await _context.UserMemberships
                .Where(m => orphanKeys.Contains(m.MembershipUserId))
                .ToListAsync(cancellationToken);
            if (memberships.Count > 0)
            {
                _context.UserMemberships.RemoveRange(memberships);
            }

            // (4) Delete their profile blob rows from [aspnet_Profile] (same deterministic key projection).
            // The keys.Contains(EF.Property<Guid>(p, "UserId")) shadow-property filter mirrors the batched
            // profile hydration query used elsewhere in this repository.
            var orphanProfiles = await _context.UserProfiles
                .Where(p => orphanKeys.Contains(EF.Property<Guid>(p, "UserId")))
                .ToListAsync(cancellationToken);
            if (orphanProfiles.Count > 0)
            {
                _context.UserProfiles.RemoveRange(orphanProfiles);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // --- SCHEMA-FIDELITY bridge helpers (finding #1) ---------------------------------------------------

    // Projects the transient PortalID (from the [UserPortals] junction) and the Membership (from
    // [aspnet_Membership]) back onto a freshly-read User so the Application/DTO contract and AuthService
    // credential checks observe the real stored values. Missing junction/credential rows leave the
    // entity's in-memory defaults untouched (a directly-seeded user hydrates gracefully).
    private async Task HydrateAsync(User user, CancellationToken cancellationToken)
    {
        var portalId = await _context.UserPortals
            .AsNoTracking()
            .Where(up => up.UserId == user.UserID)
            .OrderBy(up => up.PortalId)
            .Select(up => (int?)up.PortalId)
            .FirstOrDefaultAsync(cancellationToken);
        if (portalId.HasValue)
        {
            user.PortalID = portalId.Value;
        }

        var key = MembershipKey(user.UserID);
        var membership = await _context.UserMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MembershipUserId == key, cancellationToken);
        if (membership is not null)
        {
            user.Membership = membership;
        }

        // MIGRATION QA finding G (profile hydration): read the [aspnet_Profile] name/value blob (keyed by
        // the same deterministic MembershipKey projection) and deserialize it back onto the transient
        // User.Profile carrier so the UserDto projection observes the persisted profile. Only rows written
        // by the modern stack (tagged with ProfileBlobMarker in [PropertyNames]) are parsed; a legacy
        // DNN-format blob is left untouched (User.Profile keeps its empty defaults) rather than
        // mis-parsed. AsNoTracking read path.
        var profileRow = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => EF.Property<Guid>(p, "UserId") == key)
            .Select(p => new
            {
                Names = EF.Property<string?>(p, "PropertyNames"),
                Values = EF.Property<string?>(p, "PropertyValuesString")
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (profileRow is not null && profileRow.Names == ProfileBlobMarker)
        {
            ApplyProfileBlob(user.Profile, profileRow.Values);
        }
    }

    // MIGRATION QA finding F1 (list data fidelity): the batched counterpart to HydrateAsync used by the
    // collection read paths (GetAllAsync / GetByPortalAsync / SearchAsync). Running the single-item
    // HydrateAsync in a loop would issue three queries PER user (an N+1). This projects the SAME transient
    // carriers -- PortalID (from [UserPortals]), Membership (from [aspnet_Membership]) and Profile (from the
    // [aspnet_Profile] JSON blob) -- onto every user in the set using exactly THREE queries total, so a list
    // row observes the identical real values a single GET returns. AsNoTracking (read path). The credential
    // secrets remain stripped at the DTO boundary, so hydrating them here does not leak them to the client.
    private async Task HydrateManyAsync(IReadOnlyCollection<User> users, CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return;
        }

        var userIds = users.Select(u => u.UserID).ToList();

        // Map each membership/profile GUID key back to its owning User via the deterministic MembershipKey
        // projection, so the batched credential/profile rows can be re-associated with their users. Built
        // with an indexer (not ToDictionary) so a duplicate UserID from a junction join can never throw.
        var usersByKey = new Dictionary<Guid, User>();
        foreach (var u in users)
        {
            usersByKey[MembershipKey(u.UserID)] = u;
        }
        var keys = usersByKey.Keys.ToList();

        // (1) PortalID -- the lowest associated portal from the [UserPortals] junction, matching the
        //     single-item HydrateAsync (OrderBy PortalId -> First). One grouped query for the whole set.
        var portalLookup = (await _context.UserPortals
                .AsNoTracking()
                .Where(up => userIds.Contains(up.UserId))
                .GroupBy(up => up.UserId)
                .Select(g => new { UserId = g.Key, PortalId = g.Min(x => x.PortalId) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.UserId, x => x.PortalId);
        foreach (var user in users)
        {
            if (portalLookup.TryGetValue(user.UserID, out var portalId))
            {
                user.PortalID = portalId;
            }
        }

        // (2) Membership -- the real [aspnet_Membership] credential rows, one query for the whole key set.
        var memberships = await _context.UserMemberships
            .AsNoTracking()
            .Where(m => keys.Contains(m.MembershipUserId))
            .ToListAsync(cancellationToken);
        foreach (var membership in memberships)
        {
            if (usersByKey.TryGetValue(membership.MembershipUserId, out var user))
            {
                user.Membership = membership;
            }
        }

        // (3) Profile -- the [aspnet_Profile] JSON blob rows (keyed by the same deterministic projection),
        //     one query for the whole key set. Only rows written by the modern stack (tagged with
        //     ProfileBlobMarker in [PropertyNames]) are parsed; a legacy DNN-format blob is left untouched.
        var profileRows = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => keys.Contains(EF.Property<Guid>(p, "UserId")))
            .Select(p => new
            {
                Key = EF.Property<Guid>(p, "UserId"),
                Names = EF.Property<string?>(p, "PropertyNames"),
                Values = EF.Property<string?>(p, "PropertyValuesString")
            })
            .ToListAsync(cancellationToken);
        foreach (var row in profileRows)
        {
            if (row.Names == ProfileBlobMarker && usersByKey.TryGetValue(row.Key, out var user))
            {
                ApplyProfileBlob(user.Profile, row.Values);
            }
        }
    }

    // Builds a schema-valid [aspnet_Membership] row from the User's transient Membership carrier,
    // supplying the deterministic GUID key, the required NOT NULL columns, and coercing default dates to
    // a valid value (avoids the SQL Server datetime lower bound while remaining correct under InMemory).
    private static UserMembership BuildMembershipRow(User entity)
    {
        var src = entity.Membership ?? new UserMembership();
        var now = DateTime.UtcNow;
        var email = string.IsNullOrEmpty(src.Email) ? entity.Email : src.Email;
        return new UserMembership
        {
            MembershipUserId = MembershipKey(entity.UserID),
            ApplicationId = DefaultApplicationId,
            Password = src.Password ?? string.Empty,
            // The stored Password is always a BCrypt hash produced by the Infrastructure PasswordHasher;
            // PasswordFormat = 1 marks it as "hashed" for aspnet_Membership fidelity. Modern verification
            // BCrypt-verifies the hash directly and does not branch on this value.
            PasswordFormat = 1,
            PasswordSalt = src.PasswordSalt ?? string.Empty, // BCrypt embeds its own salt in the hash
            Approved = src.Approved,
            LockedOut = src.LockedOut,
            CreatedDate = src.CreatedDate == default ? now : src.CreatedDate,
            LastLoginDate = src.LastLoginDate == default ? now : src.LastLoginDate,
            LastPasswordChangeDate = src.LastPasswordChangeDate == default ? now : src.LastPasswordChangeDate,
            LastLockoutDate = src.LastLockoutDate == default ? now : src.LastLockoutDate,
            FailedPasswordAttemptCount = src.FailedPasswordAttemptCount,
            FailedPasswordAttemptWindowStart = src.FailedPasswordAttemptWindowStart == default ? now : src.FailedPasswordAttemptWindowStart,
            FailedPasswordAnswerAttemptCount = src.FailedPasswordAnswerAttemptCount,
            FailedPasswordAnswerAttemptWindowStart = src.FailedPasswordAnswerAttemptWindowStart == default ? now : src.FailedPasswordAnswerAttemptWindowStart,
            PasswordQuestion = src.PasswordQuestion,
            PasswordAnswer = src.PasswordAnswer,
            Email = email,
            LoweredEmail = string.IsNullOrEmpty(email) ? null : email.ToLowerInvariant(),
            MobilePIN = src.MobilePIN,
            Comment = src.Comment,
            Username = entity.Username
        };
    }

    // Copies the mutable credential fields from a hydrated-then-mutated Membership carrier onto the
    // tracked [aspnet_Membership] row (used on update / change-password). Callers always hydrate first,
    // so the copied values are the real current credential plus any deliberate change.
    private static void CopyMembership(UserMembership? src, UserMembership dest)
    {
        if (src is null)
        {
            return;
        }

        dest.Password = src.Password ?? string.Empty;
        dest.PasswordSalt = src.PasswordSalt ?? string.Empty;
        dest.Approved = src.Approved;
        dest.LockedOut = src.LockedOut;
        dest.FailedPasswordAttemptCount = src.FailedPasswordAttemptCount;
        dest.FailedPasswordAnswerAttemptCount = src.FailedPasswordAnswerAttemptCount;
        dest.PasswordQuestion = src.PasswordQuestion;
        dest.PasswordAnswer = src.PasswordAnswer;
        if (src.LastPasswordChangeDate != default)
        {
            dest.LastPasswordChangeDate = src.LastPasswordChangeDate;
        }
        if (src.LastLoginDate != default)
        {
            dest.LastLoginDate = src.LastLoginDate;
        }
        if (src.LastLockoutDate != default)
        {
            dest.LastLockoutDate = src.LastLockoutDate;
        }
        if (!string.IsNullOrEmpty(src.Email))
        {
            dest.Email = src.Email;
            dest.LoweredEmail = src.Email.ToLowerInvariant();
        }
    }

    // --- QA finding G: aspnet_Profile blob persistence/hydration helpers -------------------------------

    // MIGRATION QA finding G: the strongly-typed profile fields the modern stack surfaces (the same set
    // exposed by ProfileDto). These are serialized into a single JSON document stored in the real
    // [aspnet_Profile].[PropertyValuesString] blob column (the legacy dynamic ProfilePropertyDefinition
    // store is out of scope). Names match UserProfile / ProfileDto so the JSON is self-describing.
    private sealed record ProfileBlob(
        string Street,
        string Unit,
        string City,
        string Region,
        string Country,
        string PostalCode,
        string Telephone,
        string Cell,
        string Fax,
        string Website,
        string IM,
        int TimeZone,
        string PreferredLocale);

    // MIGRATION QA finding G: upsert the profile blob row for the user into [aspnet_Profile], keyed by the
    // deterministic MembershipKey(UserID) Guid (the same projection the credential bridge uses). Writes the
    // shadow columns via the EntityEntry because UserProfile declares no CLR members for them (they are
    // configured as shadow properties in UserProfileConfiguration). Does NOT call SaveChanges - the caller
    // batches persistence. [aspnet_Profile] has no FK (PK-only, per the InitialCreate migration), so the
    // deterministic-Guid insert is valid without an aspnet_Users row.
    private async Task PersistProfileAsync(User entity, CancellationToken cancellationToken)
    {
        var source = entity.Profile;
        if (source is null)
        {
            return;
        }

        var key = MembershipKey(entity.UserID);
        var json = SerializeProfile(source);
        var now = DateTime.UtcNow;

        var existing = await _context.UserProfiles
            .FirstOrDefaultAsync(p => EF.Property<Guid>(p, "UserId") == key, cancellationToken);
        if (existing is null)
        {
            var entry = _context.UserProfiles.Add(new UserProfile());
            entry.Property("UserId").CurrentValue = key;
            entry.Property("PropertyNames").CurrentValue = ProfileBlobMarker;
            entry.Property("PropertyValuesString").CurrentValue = json;
            entry.Property("PropertyValuesBinary").CurrentValue = Array.Empty<byte>();
            entry.Property("LastUpdatedDate").CurrentValue = now;
        }
        else
        {
            var entry = _context.Entry(existing);
            entry.Property("PropertyNames").CurrentValue = ProfileBlobMarker;
            entry.Property("PropertyValuesString").CurrentValue = json;
            entry.Property("PropertyValuesBinary").CurrentValue = Array.Empty<byte>();
            entry.Property("LastUpdatedDate").CurrentValue = now;
        }
    }

    // Serializes the strongly-typed profile fields into the JSON blob persisted in
    // [aspnet_Profile].[PropertyValuesString].
    private static string SerializeProfile(UserProfile profile)
    {
        var blob = new ProfileBlob(
            profile.Street ?? string.Empty,
            profile.Unit ?? string.Empty,
            profile.City ?? string.Empty,
            profile.Region ?? string.Empty,
            profile.Country ?? string.Empty,
            profile.PostalCode ?? string.Empty,
            profile.Telephone ?? string.Empty,
            profile.Cell ?? string.Empty,
            profile.Fax ?? string.Empty,
            profile.Website ?? string.Empty,
            profile.IM ?? string.Empty,
            profile.TimeZone,
            profile.PreferredLocale ?? string.Empty);
        return JsonSerializer.Serialize(blob, ProfileJsonOptions);
    }

    // Deserializes the JSON blob back onto the transient User.Profile carrier. A null/blank or
    // unparseable blob leaves the profile at its empty defaults (defensive: a foreign/legacy blob that
    // slipped past the marker check must never throw on a read path).
    private static void ApplyProfileBlob(UserProfile target, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        ProfileBlob? blob;
        try
        {
            blob = JsonSerializer.Deserialize<ProfileBlob>(json, ProfileJsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        if (blob is null)
        {
            return;
        }

        target.Street = blob.Street ?? string.Empty;
        target.Unit = blob.Unit ?? string.Empty;
        target.City = blob.City ?? string.Empty;
        target.Region = blob.Region ?? string.Empty;
        target.Country = blob.Country ?? string.Empty;
        target.PostalCode = blob.PostalCode ?? string.Empty;
        target.Telephone = blob.Telephone ?? string.Empty;
        target.Cell = blob.Cell ?? string.Empty;
        target.Fax = blob.Fax ?? string.Empty;
        target.Website = blob.Website ?? string.Empty;
        target.IM = blob.IM ?? string.Empty;
        target.TimeZone = blob.TimeZone;
        target.PreferredLocale = blob.PreferredLocale ?? string.Empty;
    }
}
