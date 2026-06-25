namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Users.UserRoleInfo (Library/Components/Users/UserRoleInfo.vb).
// STRUCTURAL CHANGE: legacy "UserRoleInfo Inherits RoleInfo" leaked the full Role definition into the join row.
// Restructured into a clean many-to-many join entity (User <-> Role) carrying only its own keys and the
// membership lifecycle fields. Role attributes are now reached via the Role navigation, not inheritance.
// MIGRATION: Denormalized display fields FullName and Email dropped — reachable via the User navigation.
public class UserRole
{
    public int UserRoleId { get; set; }

    public int UserId { get; set; }

    // MIGRATION: Previously inherited from RoleInfo.RoleID; now an explicit FK because inheritance was removed.
    public int RoleId { get; set; }

    // MIGRATION: VB Date -> nullable DateTime (effective/expiry windows may be unset).
    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool IsTrialUsed { get; set; }

    public bool Subscribed { get; set; }

    // Optional navigations for the EF Core many-to-many-with-payload relationship (configured in Infrastructure).
    public User? User { get; set; }

    public Role? Role { get; set; }
}
