namespace DnnMigration.Application.DTOs.Auth;

// MIGRATION: New auth-contract DTO with no 1:1 legacy class. Projects the authenticated user
//            returned by the legacy UserController.GetCurrentUserInfo() (Library/Components/Users/
//            UserController.vb L381), which returned a UserInfo (Library/Components/Users/UserInfo.vb).
//            Backs GET /api/auth/me (AAP §0.3.4).
// MIGRATION: Self-contained by design — does NOT reuse Application/DTOs/User/* so the auth contract
//            stays decoupled from the User CRUD contract (folder requirements).
// MIGRATION: Contains NO credential fields (no Password / PasswordAnswer / PasswordQuestion). Legacy
//            credential material lived on UserMembership.vb and must NEVER appear on a response DTO
//            (AAP §0.7.6 security rule).
public record CurrentUserDto
{
    // MIGRATION: UserInfo.UserID (Integer) -> int. DNN user identifier.
    public int UserId { get; init; }

    // MIGRATION: UserInfo.Username (String).
    public string Username { get; init; } = string.Empty;

    // MIGRATION: UserInfo.Email (String).
    public string Email { get; init; } = string.Empty;

    // MIGRATION: UserInfo.DisplayName (String).
    public string DisplayName { get; init; } = string.Empty;

    // MIGRATION: UserInfo.FirstName (String).
    public string FirstName { get; init; } = string.Empty;

    // MIGRATION: UserInfo.LastName (String).
    public string LastName { get; init; } = string.Empty;

    // MIGRATION: UserInfo.FullName (String). Legacy property is <Obsolete> ("deprecated in favour of
    //            Display Name") and was computed as FirstName & " " & LastName when empty. Retained
    //            verbatim for parity; the VALUE is supplied by the mapping layer — NO computation
    //            logic lives in this DTO.
    public string FullName { get; init; } = string.Empty;

    // MIGRATION: UserInfo.IsSuperUser (Boolean) -> host/super-user flag.
    public bool IsSuperUser { get; init; }

    // MIGRATION: UserInfo.PortalID (Integer) -> tenant scope (AAP §0.7.1 multi-tenant isolation).
    public int PortalId { get; init; }

    // MIGRATION: UserInfo.Roles (String()) -> role NAME strings (legacy hydrated via
    //            RoleController.GetRolesByUser(UserID, PortalID)). Exposed read-only; population
    //            happens in the mapping/service layer, not here.
    public IReadOnlyList<string> Roles { get; init; } = [];
}
