import { User } from '../../../core/models/user.model';

/**
 * User administration feature — data contracts (status enums + DTO interfaces).
 *
 * This is the most foundational layer of the User feature slice: it depends on
 * nothing within the feature and carries no Angular code (no imports beyond the
 * cross-feature core `User`, no decorators, no classes, no runtime logic beyond
 * the two `enum` declarations). The enums are runtime VALUES (mirroring DNN
 * status codes byte-for-byte); the DTOs are compile-time TYPES.
 *
 * MIGRATION: derived (read as reference only — NOT a 1:1 line conversion) from
 * the legacy DNN VB.NET sources:
 *   - Library/Components/Users/Membership/UserLoginStatus.vb   (UserLoginStatus)
 *   - Library/Components/Users/Membership/UserCreateStatus.vb  (UserCreateStatus)
 *   - Library/Components/Users/UserInfo.vb                     (core user fields)
 *   - Library/Components/Users/Membership/UserMembership.vb    (membership state)
 *   - Website/admin/Users/{User,Membership,Users}.ascx.vb      (UI create/edit/list flows)
 *
 * Property names are camelCase to match the .NET 8 API's System.Text.Json default
 * (JsonNamingPolicy.CamelCase): a C# `UserId` property is emitted on the wire as
 * JSON `userId`, so the Angular client consumes camelCase names.
 */

/**
 * Result of an authentication attempt.
 *
 * MIGRATION: integer values are carried over VERBATIM from
 * Library/Components/Users/Membership/UserLoginStatus.vb (L23-31) so that any
 * persisted or compared integer remains valid (e.g. `3` still means "locked out").
 * Legacy UPPER_SNAKE_CASE member names are retained intentionally (stable
 * wire/comparison values) — they are NOT renamed to PascalCase.
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
 * Result of a user-provisioning attempt.
 *
 * MIGRATION: all 18 members and their integer ordinals are carried over VERBATIM
 * from Library/Components/Users/Membership/UserCreateStatus.vb (L23-42).
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
 * Create-user FORM payload — the reactive-form binding shape for the user-create
 * screen. It deliberately carries form-only affordances (confirm/random password,
 * recovery Q&A, `authorize`, `notify`) that have NO place on the wire. The user-form
 * component adapts this into a {@link CreateUserRequest} (the exact backend contract)
 * before calling `UserService.createUser`: e.g. `authorize` maps to `approved`, and
 * `portalID` / `isSuperUser` are supplied from the admin context. Do NOT send this
 * shape to the API directly — it does not match the backend `CreateUserDto`.
 * MIGRATION: mirrors the create flow of Website/admin/Users/User.ascx.vb (L133-238).
 */
export interface CreateUserDto {
  /** Login name. Required; read-only after creation (legacy UserInfo.Username). */
  username: string;
  /** Required (legacy UserInfo.FirstName). */
  firstName: string;
  /** Required (legacy UserInfo.LastName). */
  lastName: string;
  /** Required; legacy may auto-format via Security_DisplayNameFormat. */
  displayName: string;
  /** Required (legacy UserInfo.Email). */
  email: string;
  /** Plain password; ignored when `randomPassword` is true (legacy txtPassword). */
  password?: string;
  /** Must equal `password` (legacy PasswordMismatch check, txtConfirm L152). */
  confirmPassword?: string;
  /** Server generates a random password (legacy chkRandom L150/L164). */
  randomPassword?: boolean;
  /** Password-recovery question; required only when the provider RequiresQuestionAndAnswer (L168-174). */
  question?: string;
  /** Password-recovery answer; required only when the provider RequiresQuestionAndAnswer (L176-181). */
  answer?: string;
  /** Approved/authorized state (legacy chkAuthorize L223 -> Membership.Approved). */
  authorize?: boolean;
  /** Send the new-user notification email (legacy chkNotify L231). */
  notify?: boolean;
}

/**
 * Edit-user FORM payload — the reactive-form binding shape for the user-edit screen.
 * It carries form-only fields (e.g. `affiliateId`) and omits transport concerns. The
 * user-form component adapts this into an {@link UpdateUserRequest} (the exact backend
 * contract) before calling `UserService.updateUser`. Do NOT send this shape to the API
 * directly — it does not match the backend `UpdateUserDto`.
 * MIGRATION: mirrors the non-AddUser path of cmdUpdate_Click (User.ascx.vb L361-385).
 * `username` is intentionally omitted because UserInfo.Username is IsReadOnly after creation.
 */
export interface UpdateUserDto {
  firstName: string;
  lastName: string;
  displayName: string;
  email: string;
  affiliateId?: number;
}

/**
 * Create-user WIRE request — the exact JSON contract for `POST /api/v1/users`.
 *
 * Field names and types mirror the backend `DnnMigration.Application.DTOs.User.CreateUserDto`
 * 1:1 under System.Text.Json camelCase (e.g. C# `PortalID` -> `portalID`,
 * `IsSuperUser` -> `isSuperUser`). This is the ONLY shape that may be posted to the
 * create endpoint; it intentionally contains NO form-only fields (no `authorize`,
 * `confirmPassword`, `randomPassword`, `question`, `answer`, `notify`).
 * `authorize` from {@link CreateUserDto} maps to `approved` here.
 */
export interface CreateUserRequest {
  /** Owning portal (admin context). Backend `CreateUserDto.PortalID`. */
  portalID: number;
  /** Login name. Backend `CreateUserDto.Username`. */
  username: string;
  /** Plaintext password (input only; BCrypt-hashed server-side). Optional for the
   *  server-generated-password path. Backend `CreateUserDto.Password`. */
  password?: string;
  /** Backend `CreateUserDto.DisplayName`. */
  displayName: string;
  /** Backend `CreateUserDto.Email`. */
  email: string;
  /** Backend `CreateUserDto.FirstName`. */
  firstName: string;
  /** Backend `CreateUserDto.LastName`. */
  lastName: string;
  /** Host-level super user flag. Backend `CreateUserDto.IsSuperUser`. */
  isSuperUser: boolean;
  /** Approved/authorized membership state. Backend `CreateUserDto.Approved`
   *  (maps from the form's `authorize`). */
  approved: boolean;
}

/**
 * Edit-user WIRE request — the exact JSON contract for `PUT /api/v1/users/{id}`.
 *
 * Field names and types mirror the backend `DnnMigration.Application.DTOs.User.UpdateUserDto`
 * 1:1 under System.Text.Json camelCase. `userID` is REQUIRED and MUST equal the route
 * id: `UsersController.Update` returns HTTP 400 ("Identifier mismatch") when
 * `id != request.UserID`. `UserService.updateUser` enforces this by stamping the route
 * id onto the body. Username, password, and portalID are intentionally absent (username
 * is read-only post-creation; password changes use a dedicated flow; UserID is the global PK).
 */
export interface UpdateUserRequest {
  /** Global user PK; MUST equal the route id. Backend `UpdateUserDto.UserID`. */
  userID: number;
  /** Backend `UpdateUserDto.DisplayName`. */
  displayName: string;
  /** Backend `UpdateUserDto.Email`. */
  email: string;
  /** Backend `UpdateUserDto.FirstName`. */
  firstName: string;
  /** Backend `UpdateUserDto.LastName`. */
  lastName: string;
  /** Host-level super user flag. Backend `UpdateUserDto.IsSuperUser`. */
  isSuperUser: boolean;
  /** Approved/authorized membership state. Backend `UpdateUserDto.Approved`. */
  approved: boolean;
}

/**
 * Read model of a user's membership state (user-profile screen).
 * MIGRATION: from Library/Components/Users/Membership/UserMembership.vb +
 * Website/admin/Users/Membership.ascx.vb.
 *
 * MIGRATION: the read-only timestamp fields are display-only (legacy IsReadOnly(True)
 * properties); the sensitive `password` / `passwordAnswer` / `passwordQuestion`
 * fields are intentionally NOT exposed to the client.
 */
export interface MembershipDto {
  approved: boolean;
  lockedOut: boolean;
  updatePassword: boolean;
  isOnline?: boolean;
  // MIGRATION: DateTime serialized as an ISO-8601 string (System.Text.Json) — typed
  // `string` for compatibility with the shared data-table date pipe. Applies to all
  // timestamp fields below.
  createdDate?: string;
  lastLoginDate?: string;
  lastActivityDate?: string;
  lastLockoutDate?: string;
  lastPasswordChangeDate?: string;
}

/**
 * Editable membership transitions.
 * MIGRATION: from the Membership.ascx.vb command buttons (L194-250):
 * cmdAuthorize->approved=true / cmdUnAuthorize->approved=false; cmdUnLock->lockedOut=false;
 * cmdPassword->updatePassword=true (force password change).
 */
export interface UpdateMembershipDto {
  approved: boolean;
  lockedOut: boolean;
  updatePassword: boolean;
}

/**
 * A row in the user-management grid.
 * MIGRATION: extends the core `User` (no field duplication) with the membership-status
 * columns shown by Website/admin/Users/{Users,ManageUsers}.ascx.vb. "Authorized" is
 * represented by the single canonical `approved` field — no redundant `authorized` alias.
 */
export interface UserListItem extends User {
  // MIGRATION: `approved`, `createdDate`, and `lastLoginDate` are inherited from the core `User`
  // (where they are `boolean` / `string | null`); they are intentionally NOT redeclared here, as
  // redeclaring them as optional would widen the inherited types and break `extends User` (TS2430).
  // Only the grid-only membership-status columns absent from `User` are added below.
  lockedOut?: boolean;
  isOnline?: boolean;
}

/**
 * Filter/search/paging parameters for GET /api/v1/users.
 * MIGRATION: from Website/admin/Users/Users.ascx.vb (L49-199, 268-275, 577-581).
 */
export interface UserSearchQuery {
  /** Letter filter or quick-filter text. */
  filter?: string;
  /** The property the filter applies to. */
  filterProperty?: string;
  /** Free-text search term. */
  searchText?: string;
  /** Legacy ddlSearchType: Username (L577) / Email (L578) / profile-property (L581),
   *  normalized to a 3-member string union. */
  searchType?: 'email' | 'username' | 'profile';
  pageIndex?: number;
  pageSize?: number;
}
