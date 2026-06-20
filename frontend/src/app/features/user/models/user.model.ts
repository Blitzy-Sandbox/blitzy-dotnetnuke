import { User } from '../../../core/models/user.model';

/**
 * User administration feature — data contracts (status enums + DTO interfaces).
 *
 * This is the most foundational layer of the User feature slice: it depends on
 * nothing within the feature, only on the cross-feature core `User` read model.
 * It is consumed by `services/user.service.ts`, the feature screens
 * (`components/user-list`, `components/user-form`, `components/user-profile`),
 * and re-exported by the feature's `models/index.ts` barrel.
 *
 * MIGRATION: derived (read as REFERENCE ONLY — not a 1:1 line conversion) from
 * the legacy DNN VB.NET sources:
 *   - Library/Components/Users/Membership/UserLoginStatus.vb   (UserLoginStatus)
 *   - Library/Components/Users/Membership/UserCreateStatus.vb  (UserCreateStatus)
 *   - Library/Components/Users/UserInfo.vb                     (core user fields)
 *   - Library/Components/Users/Membership/UserMembership.vb    (membership state)
 *   - Website/admin/Users/{User,Membership,Users}.ascx.vb      (UI create/edit/list flows)
 *
 * Property names are camelCase to match the .NET 8 API's System.Text.Json default
 * (JsonNamingPolicy.CamelCase), which lowercases ONLY the first character of each
 * PascalCase C# property and leaves trailing acronyms intact: a C# `UserID`
 * property is serialized as JSON `userID` (NOT `userId`), which the Angular client
 * consumes verbatim. These names are kept consistent with the core `User` model
 * (`userID` / `portalID` / `affiliateID`) that `UserListItem` extends.
 *
 * MIGRATION (C7/DEV-038): `CreateUserDto` and `UpdateUserDto` below are the EXACT
 * over-the-wire request contracts — their fields and casing match the backend
 * `DnnMigration.Application.DTOs.User.CreateUserDto` / `UpdateUserDto` 1:1 so the
 * `UsersController` validators/route-id checks accept them. Legacy form-only inputs
 * that the API does not accept (confirm-password, random-password toggle, password
 * question/answer, notify) live on the separate `CreateUserForm` UI model and are
 * mapped/stripped down to `CreateUserDto` before submission.
 */

/**
 * Result of an authentication attempt.
 *
 * MIGRATION: the integer values are carried over VERBATIM from
 * `Library/Components/Users/Membership/UserLoginStatus.vb` (L23-31) so that any
 * persisted or compared integer remains valid across the migration boundary
 * (for example, `3` must still mean "user locked out"). Member names retain the
 * legacy UPPER_SNAKE_CASE — they are stable wire/comparison values and must NOT
 * be renamed to PascalCase. A plain numbered `enum` (NOT `const enum`, NOT a
 * string enum) is used so the emitted runtime values are byte-identical.
 */
export enum UserLoginStatus {
  LOGIN_FAILURE = 0,
  LOGIN_SUCCESS = 1,
  LOGIN_SUPERUSER = 2,
  LOGIN_USERLOCKEDOUT = 3,
  LOGIN_USERNOTAPPROVED = 4,
  LOGIN_INSECUREADMINPASSWORD = 5,
  LOGIN_INSECUREHOSTPASSWORD = 6,
}

/**
 * Result of a user-provisioning (create) attempt.
 *
 * MIGRATION: all 18 members and their exact ordinals are carried over VERBATIM
 * from `Library/Components/Users/Membership/UserCreateStatus.vb` (L23-42). A
 * plain numbered `enum` preserves the legacy integer codes byte-for-byte
 * (e.g. `PasswordMismatch = 16`), which the create-user flow compares against.
 */
export enum UserCreateStatus {
  AddUser = 0,
  UsernameAlreadyExists = 1,
  UserAlreadyRegistered = 2,
  DuplicateEmail = 3,
  DuplicateProviderUserKey = 4,
  DuplicateUserName = 5,
  InvalidAnswer = 6,
  InvalidEmail = 7,
  InvalidPassword = 8,
  InvalidProviderUserKey = 9,
  InvalidQuestion = 10,
  InvalidUserName = 11,
  ProviderError = 12,
  Success = 13,
  UnexpectedError = 14,
  UserRejected = 15,
  PasswordMismatch = 16,
  AddUserToPortal = 17,
}

/**
 * Create-user request payload (`POST /api/v1/users`).
 *
 * MIGRATION (C7/DEV-038): this is the EXACT wire contract — field names, casing,
 * and presence match the backend `DnnMigration.Application.DTOs.User.CreateUserDto`
 * 1:1 (`PortalID`, `Username`, `Password`, `DisplayName`, `Email`, `FirstName`,
 * `LastName`, `IsSuperUser`, `Approved`). The legacy `Website/admin/Users/User.ascx.vb`
 * (L133-238) form additionally collected confirm-password, a random-password toggle,
 * a password question/answer pair, and a notify flag; the API does NOT accept those,
 * so they were removed from this DTO and relocated to `CreateUserForm`. The legacy
 * `authorize` checkbox maps to the canonical `approved` field.
 */
export interface CreateUserDto {
  /** Owning portal id. Backend `CreateUserDto.PortalID` (wire `portalID`). */
  portalID: number;
  /** Login name; read-only after creation (legacy `UserInfo.Username`). Backend `Username`. */
  username: string;
  /**
   * Plaintext password — INPUT ONLY. BCrypt-hashed in the service layer before
   * persistence and never echoed in any response. Backend `Password` is nullable;
   * presence/strength is enforced by `CreateUserValidator` (FluentValidation).
   */
  password?: string;
  /** Display name; the legacy form may auto-format via `Security_DisplayNameFormat`. Backend `DisplayName`. */
  displayName: string;
  /** Email address (legacy `txtEmail`). Backend `Email`. */
  email: string;
  /** First name (legacy `txtFirstName`). Backend `FirstName`. */
  firstName: string;
  /** Last name (legacy `txtLastName`). Backend `LastName`. */
  lastName: string;
  /** Host/super-user flag. Backend `IsSuperUser`. */
  isSuperUser: boolean;
  /** Approved/authorized membership state (legacy `chkAuthorize` -> `Membership.Approved`). Backend `Approved`. */
  approved: boolean;
}

/**
 * UI-only create-user form model — the full field set bound by the create screen,
 * a SUPERSET of the wire `CreateUserDto`.
 *
 * MIGRATION (C7/DEV-038): preserves the legacy `Website/admin/Users/User.ascx.vb`
 * form controls that the REST API does not accept. The user-form component binds to
 * this model and maps it down to a `CreateUserDto` (the 9 wire fields only) before
 * calling the API; the fields below are validated client-side and then stripped, so
 * they are never sent over the wire.
 */
export interface CreateUserForm extends CreateUserDto {
  /** Must equal `password` — legacy `PasswordMismatch` check (`txtConfirm`, L152). Stripped before POST. */
  confirmPassword?: string;
  /** Server generates a random password (legacy `chkRandom`, L150/L164). Stripped before POST. */
  randomPassword?: boolean;
  /** Password-recovery question; required only when the provider `RequiresQuestionAndAnswer` (L168-174). Stripped before POST. */
  question?: string;
  /** Password-recovery answer; required only when the provider `RequiresQuestionAndAnswer` (L176-181). Stripped before POST. */
  answer?: string;
  /** Send the new-user notification email (legacy `chkNotify`, L231). Stripped before POST. */
  notify?: boolean;
}

/**
 * Edit-user request payload (`PUT /api/v1/users/{id}`).
 *
 * MIGRATION (C7/DEV-038): EXACT wire contract — matches the backend
 * `DnnMigration.Application.DTOs.User.UpdateUserDto` 1:1 (`UserID`, `DisplayName`,
 * `Email`, `FirstName`, `LastName`, `IsSuperUser`, `Approved`). `userID` is REQUIRED
 * in the body: `UsersController.Update` rejects a mismatch with the route id
 * (`id != request.UserID` -> RFC 7807 400). `username` is OMITTED (legacy
 * `UserInfo.Username` is `IsReadOnly` post-creation), `password` is OMITTED (password
 * changes use a dedicated flow), `portalID` is OMITTED (UserID is the global PK), and
 * the previously-present `affiliateId` is REMOVED — the backend update contract does
 * not accept it (it is read-only on `UserDto`), so sending it would be ignored data.
 */
export interface UpdateUserDto {
  /** Target user id; must equal the route id (`UsersController` enforces `id != request.UserID` -> 400). Backend `UserID`. */
  userID: number;
  /** Display name. Backend `DisplayName`. */
  displayName: string;
  /** Email address. Backend `Email`. */
  email: string;
  /** First name. Backend `FirstName`. */
  firstName: string;
  /** Last name. Backend `LastName`. */
  lastName: string;
  /** Host/super-user flag. Backend `IsSuperUser`. */
  isSuperUser: boolean;
  /** Approved/authorized membership state. Backend `Approved`. */
  approved: boolean;
}

/**
 * Read model of a user's membership state, shown on the user-profile screen.
 *
 * MIGRATION: projected from `Library/Components/Users/Membership/UserMembership.vb`
 * and `Website/admin/Users/Membership.ascx.vb`. The read-only timestamp fields
 * are display-only (legacy `IsReadOnly(True)` properties). The sensitive
 * `Password` / `PasswordAnswer` / `PasswordQuestion` members are intentionally
 * NOT exposed to the client.
 */
export interface MembershipDto {
  approved: boolean;
  lockedOut: boolean;
  updatePassword: boolean;
  isOnline?: boolean;
  // MIGRATION: DateTime values serialize as ISO-8601 strings (System.Text.Json
  // default); typed `string` here, compatible with the shared data-table date
  // pipe. These timestamps are display-only (legacy IsReadOnly props).
  createdDate?: string;
  lastLoginDate?: string;
  lastActivityDate?: string;
  lastLockoutDate?: string;
  lastPasswordChangeDate?: string;
}

/**
 * Editable membership transitions issued from the user-profile screen.
 *
 * MIGRATION: mirrors the command buttons in `Website/admin/Users/Membership.ascx.vb`
 * (L194-250): `cmdAuthorize`/`cmdUnAuthorize` toggle `approved`, `cmdUnLock`
 * clears `lockedOut`, and `cmdPassword` forces a password change (`updatePassword`).
 */
export interface UpdateMembershipDto {
  approved: boolean;
  lockedOut: boolean;
  updatePassword: boolean;
}

/**
 * A single row in the user-management grid.
 *
 * MIGRATION: extends the cross-feature core `User` (no field duplication) and
 * adds the membership-status columns rendered by
 * `Website/admin/Users/{Users,ManageUsers}.ascx.vb`. The legacy "authorized"
 * concept is represented by the single canonical `approved` field — no redundant
 * `authorized` alias is added.
 */
export interface UserListItem extends User {
  approved?: boolean;
  lockedOut?: boolean;
  isOnline?: boolean;
  lastLoginDate?: string;
  createdDate?: string;
}

/**
 * Filter / search / paging parameters for `GET /api/v1/users`.
 *
 * MIGRATION: mirrors the list controls in `Website/admin/Users/Users.ascx.vb`
 * (L49-199, 268-275, 577-581). The legacy `ddlSearchType` dropdown
 * (Username / Email / profile-property) is normalized to a precise 3-member
 * string union rather than a bare `string`.
 */
export interface UserSearchQuery {
  /** Letter filter or quick-filter text. */
  filter?: string;
  /** The property the filter applies to (legacy `FilterProperty`). */
  filterProperty?: string;
  /** Free-text search term. */
  searchText?: string;
  /** Which field the search targets (legacy `ddlSearchType`). */
  searchType?: 'email' | 'username' | 'profile';
  /** Zero-based page index. */
  pageIndex?: number;
  /** Page size (rows per page). */
  pageSize?: number;
}
