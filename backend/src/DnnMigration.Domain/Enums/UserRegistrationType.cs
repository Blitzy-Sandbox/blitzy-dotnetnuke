namespace DnnMigration.Domain.Enums;

/// <summary>
/// Defines how users may register on a portal (the portal's user-registration mode).
/// Maps to the integer user-registration field on the Portal entity
/// (see <c>DnnMigration.Domain.Entities.Portal.UserRegistration</c>).
/// </summary>
// MIGRATION: Converted from the DotNetNuke VB.NET `Public Enum PortalRegistrationType` in
// Library/Components/Shared/Globals.vb (L84) and renamed to UserRegistrationType for the
// user-management domain. All 4 members and their explicit integer values (0-3) are preserved
// exactly because these values are a persistence/wire contract mapped to an existing integer
// column by EF Core downstream.
// MIGRATION (reconciliation): The Enums folder brief originally cited
// Library/Components/Users/Membership/*.vb as the source, but that folder defines
// UserRegistrationStatus (AddUser = 0, AddUserRoles = -1, UsernameAlreadyExists = -2,
// UserAlreadyRegistered = -3, UnexpectedError = -4) — an operation-RESULT status, which is a
// DIFFERENT concept from a registration TYPE. The registration-type semantics used here come from
// PortalRegistrationType (the correct source); UserRegistrationStatus is intentionally NOT used.
public enum UserRegistrationType
{
    NoRegistration = 0,
    PrivateRegistration = 1,
    PublicRegistration = 2,
    VerifiedRegistration = 3
}
