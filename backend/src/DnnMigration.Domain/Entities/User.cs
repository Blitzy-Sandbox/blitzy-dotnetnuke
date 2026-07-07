namespace DnnMigration.Domain.Entities;

/// <summary>
/// User identity entity. Composes <see cref="UserMembership"/> and <see cref="UserProfile"/>.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Users.UserInfo
// (Library/Components/Users/UserInfo.vb, L41). The IPropertyAccess implementation (GetProperty /
// Cacheability), the IsInRole/UpdateDisplayName helper methods, and the legacy progressive-hydration
// logic in the Membership/Profile/Roles getters (which called UserController/ProfileController/
// RoleController) are dropped — hydration/population is a repository/service concern downstream.
//
// MIGRATION (Minimal Change / entity fidelity): legacy UserInfo did NOT hold FirstName/LastName/
// Email/Username as fully-independent scalar state. It PROXIED FirstName/LastName straight through the
// composed Profile (get AND set) and PROPAGATED Email/Username into the composed Membership on set.
// Those proxy/propagation semantics are preserved below so that User.FirstName == Profile.FirstName,
// User.LastName == Profile.LastName, and User.Email/User.Username stay in sync with Membership
// (behavioral equivalence). This keeps the composed UserProfile/UserMembership authoritative and
// prevents the divergence (e.g. User.Email != Membership.Email) that fully-independent properties
// would allow.
//
// MIGRATION (EF Core, CP3): because FirstName/LastName are pass-through proxies of Profile (no User
// backing storage) they must NOT be mapped as columns on the Users table by the CP3 Fluent
// configuration — they persist via UserProfile (aspnet_Profile). Email/Username remain first-class
// Users columns; their setters additionally mirror the value onto the owned Membership object.
public class User
{
    // MIGRATION: private backing fields for the members whose legacy setters carried side effects
    // (Email/Username propagate into Membership) or that cache a lazily-derived value (FullName).
    private string _email = string.Empty;
    private string _username = string.Empty;
    private string _fullName = string.Empty;

    public int AffiliateID { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    // MIGRATION: UserInfo.Email setter (UserInfo.vb L113-133) stored the value AND propagated it into
    // Membership.Email so the composed membership stayed authoritative ("Continue to set the
    // membership Property in case developers have used this in their own code"). Preserved verbatim.
    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            Membership.Email = value;
        }
    }

    // MIGRATION: UserInfo.FirstName (UserInfo.vb L135-149) proxied straight through Profile.FirstName
    // on BOTH get and set — it was never independent User state. Profile remains authoritative.
    public string FirstName
    {
        get => Profile.FirstName;
        set => Profile.FirstName = value;
    }

    public bool IsSuperUser { get; set; }

    // MIGRATION: UserInfo.LastName (UserInfo.vb L151-177) proxied straight through Profile.LastName on
    // BOTH get and set — it was never independent User state. Profile remains authoritative.
    public string LastName
    {
        get => Profile.LastName;
        set => Profile.LastName = value;
    }

    // MIGRATION: composition — legacy lazy-hydrated Membership property, now a plain owned object.
    // Initialized to a non-null instance so the Email/Username propagation setters above never
    // dereference null.
    public UserMembership Membership { get; set; } = new();

    public int PortalID { get; set; }

    // MIGRATION: composition — legacy lazy-hydrated Profile property, now a plain owned object.
    // Initialized to a non-null instance so the FirstName/LastName proxy accessors above never
    // dereference null.
    public UserProfile Profile { get; set; } = new();

    // MIGRATION: legacy String() array, lazy-hydrated from RoleController; now plain state.
    public string[] Roles { get; set; } = Array.Empty<string>();

    public int UserID { get; set; }

    // MIGRATION: UserInfo.Username setter (UserInfo.vb L353-380) stored the value AND propagated it
    // into Membership.Username so either surface exposed the same value. Preserved verbatim.
    public string Username
    {
        get => _username;
        set
        {
            _username = value;
            Membership.Username = value;
        }
    }

    // MIGRATION: UserInfo.FullName (deprecated in favour of DisplayName; UserInfo.vb L384-402) was a
    // SETTABLE property backed by _FullName that, when unset, lazily built "FirstName & \" \" &
    // LastName" (NO trimming) and cached it. Preserved verbatim for behavioral equivalence: the getter
    // builds-and-caches only when the backing field is empty, and an explicit set overrides it.
    // Substituting a pure computed getter or adding .Trim() would change observable output.
    public string FullName
    {
        get
        {
            if (string.IsNullOrEmpty(_fullName))
            {
                _fullName = FirstName + " " + LastName;
            }

            return _fullName;
        }
        set => _fullName = value;
    }
}
