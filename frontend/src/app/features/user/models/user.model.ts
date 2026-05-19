/**
 * @fileoverview User feature TypeScript models for Angular 19 SPA
 * @description Provides compile-time type safety for user management API operations.
 *              Contains DTOs for API requests/responses, search filters, and paginated results.
 *
 * MIGRATION NOTE: These interfaces are derived from DotNetNuke 4.x VB.NET sources:
 * - UserDto from Library/Components/Users/UserInfo.vb and UserMembership.vb
 * - CreateUserRequest from Website/admin/Users/User.ascx.vb (CreateUser, Validate methods)
 * - UpdateUserRequest from Website/admin/Users/User.ascx.vb and Membership.ascx.vb
 * - UserSearchFilter from Website/admin/Users/Users.ascx.vb (BindData method)
 *
 * VB.NET Null.NullInteger sentinel values have been converted to TypeScript null/undefined.
 * VB.NET Date type is mapped to ISO string format for JSON serialization compatibility.
 *
 * All models match backend DTOs in DnnMigration.Application/DTOs/User/ for consistent
 * API contracts across the full-stack application.
 *
 * @module features/user/models/user.model
 */

// ============================================================================
// Re-exports from Core Models
// ============================================================================
// MIGRATION: Re-export core user interfaces for convenience within the user feature module.
// This avoids duplication while providing easy access to foundational types.

export type { User, UserProfile, UserMembership } from '../../../core/models/user.model';

// ============================================================================
// API Response DTOs
// ============================================================================

/**
 * Data Transfer Object for user API responses.
 * Matches backend DnnMigration.Application/DTOs/User/UserDto.cs for consistent API contracts.
 *
 * MIGRATION NOTE: Flattened representation combining properties from:
 * - UserInfo.vb (lines 47-58): _UserID, _Username, _DisplayName, _Email, _PortalID, _IsSuperUser, _AffiliateID, _Roles
 * - UserMembership.vb (lines 44-59): _Approved, _LockedOut, _IsOnLine, _CreatedDate, _LastLoginDate, _LastActivityDate
 * - UserProfile.vb (lines 183-193, 251-261): FirstName, LastName
 *
 * Date properties are ISO 8601 strings as returned from the ASP.NET Core 8 Web API.
 *
 * @interface UserDto
 * @description API response representation of a user with membership status flags.
 */
export interface UserDto {
  /**
   * Unique identifier for the user.
   * MIGRATION: From UserInfo._UserID (line 47)
   */
  userId: number;

  /**
   * Unique username for authentication.
   * MIGRATION: From UserInfo._Username (line 48)
   */
  username: string;

  /**
   * Display name shown in the user interface.
   * MIGRATION: From UserInfo._DisplayName (line 49), MaxLength 128
   */
  displayName: string | null;

  /**
   * User's first name from profile.
   * MIGRATION: From Profile.FirstName (UserInfo.vb lines 144-150), MaxLength 50
   */
  firstName: string | null;

  /**
   * User's last name from profile.
   * MIGRATION: From Profile.LastName (UserInfo.vb lines 178-185), MaxLength 50
   */
  lastName: string | null;

  /**
   * User's email address.
   * MIGRATION: From UserInfo._Email (line 51), MaxLength 256
   */
  email: string;

  /**
   * Portal (tenant) identifier the user belongs to.
   * MIGRATION: From UserInfo._PortalID (line 52)
   */
  portalId: number;

  /**
   * Flag indicating whether the user has super user (host) privileges.
   * MIGRATION: From UserInfo._IsSuperUser (line 53)
   */
  isSuperUser: boolean;

  /**
   * Optional affiliate identifier for tracking referrals.
   * MIGRATION: From UserInfo._AffiliateID (line 54), Null.NullInteger → null
   */
  affiliateId: number | null;

  /**
   * Array of role names the user belongs to.
   * MIGRATION: From UserInfo._Roles (line 57)
   */
  roles: string[] | null;

  /**
   * Indicates whether the user account is approved for login.
   * MIGRATION: From Membership.Approved (UserMembership.vb line 83)
   */
  isApproved: boolean;

  /**
   * Indicates whether the user account is currently locked out.
   * MIGRATION: From Membership.LockedOut (UserMembership.vb line 223)
   */
  isLockedOut: boolean;

  /**
   * Indicates whether the user is currently online/active.
   * MIGRATION: From Membership.IsOnLine (UserMembership.vb line 123)
   */
  isOnline: boolean;

  /**
   * Date and time when the user account was created (ISO 8601 string).
   * MIGRATION: From Membership.CreatedDate (UserMembership.vb line 103)
   */
  createdDate: string | null;

  /**
   * Date and time of the user's last successful login (ISO 8601 string).
   * MIGRATION: From Membership.LastLoginDate (UserMembership.vb line 183)
   */
  lastLoginDate: string | null;

  /**
   * Date and time of the user's last activity/interaction (ISO 8601 string).
   * MIGRATION: From Membership.LastActivityDate (UserMembership.vb line 143)
   */
  lastActivityDate: string | null;
}

// ============================================================================
// API Request DTOs
// ============================================================================

/**
 * Data Transfer Object for creating a new user.
 * Matches backend DnnMigration.Application/DTOs/User/CreateUserRequest.cs.
 *
 * MIGRATION NOTE: Derived from User.ascx.vb CreateUser method (lines 209-238)
 * and Validate method (lines 133-195). Form fields include:
 * - UserEditor control: username, email, firstName, lastName
 * - txtPassword, txtConfirm: password fields (line 152)
 * - chkRandom: generate random password checkbox (lines 150, 162-165)
 * - chkAuthorize: authorization checkbox (line 223)
 * - chkNotify: notification checkbox (line 231)
 * - txtQuestion, txtAnswer: security question/answer (lines 169, 176)
 *
 * @interface CreateUserRequest
 * @description Request payload for POST /api/users endpoint.
 */
export interface CreateUserRequest {
  /**
   * Unique username for the new user (required).
   * MIGRATION: From UserEditor control binding
   */
  username: string;

  /**
   * Password for the new user account.
   * Required unless generateRandomPassword is true.
   * MIGRATION: From txtPassword (line 152)
   */
  password: string;

  /**
   * Password confirmation for client-side validation.
   * Must match password field.
   * MIGRATION: From txtConfirm (line 152)
   */
  passwordConfirm?: string;

  /**
   * Email address for the new user (required).
   * MIGRATION: From UserEditor control binding
   */
  email: string;

  /**
   * First name for the user profile (required).
   * MIGRATION: From UserEditor control binding
   */
  firstName: string;

  /**
   * Last name for the user profile (required).
   * MIGRATION: From UserEditor control binding
   */
  lastName: string;

  /**
   * Display name for the user.
   * If not provided, computed via UpdateDisplayName format (User.ascx.vb lines 117-123).
   * MIGRATION: From UserEditor control or computed from format string
   */
  displayName?: string;

  /**
   * Flag indicating if the user should be a super user (host-level admin).
   * Defaults to false if not specified.
   * MIGRATION: Admin-only field
   */
  isSuperUser?: boolean;

  /**
   * Flag indicating if the user account should be authorized/approved immediately.
   * MIGRATION: From chkAuthorize checkbox (User.ascx.vb line 223)
   */
  isAuthorized?: boolean;

  /**
   * Flag indicating if the user should receive a notification email.
   * MIGRATION: From chkNotify checkbox (User.ascx.vb line 231)
   */
  notify?: boolean;

  /**
   * Flag indicating if a random password should be generated.
   * If true, the password field may be ignored.
   * MIGRATION: From chkRandom checkbox (User.ascx.vb lines 150, 162-165)
   */
  generateRandomPassword?: boolean;

  /**
   * Security question for password recovery.
   * Required if MembershipProviderConfig.RequiresQuestionAndAnswer is true.
   * MIGRATION: From txtQuestion (User.ascx.vb line 169)
   */
  passwordQuestion?: string;

  /**
   * Answer to the security question for password recovery.
   * Required if passwordQuestion is provided.
   * MIGRATION: From txtAnswer (User.ascx.vb line 176)
   */
  passwordAnswer?: string;
}

/**
 * Data Transfer Object for updating an existing user.
 * Matches backend DnnMigration.Application/DTOs/User/UpdateUserRequest.cs.
 *
 * MIGRATION NOTE: Derived from User.ascx.vb and Membership.ascx.vb update patterns.
 * All fields are optional to support partial updates (PATCH semantics).
 *
 * @interface UpdateUserRequest
 * @description Request payload for PUT /api/users/{id} endpoint.
 *              All properties are optional for partial update support.
 */
export interface UpdateUserRequest {
  /**
   * Updated display name for the user.
   * MIGRATION: From UserEditor binding
   */
  displayName?: string;

  /**
   * Updated first name for the user profile.
   * MIGRATION: Profile update field
   */
  firstName?: string;

  /**
   * Updated last name for the user profile.
   * MIGRATION: Profile update field
   */
  lastName?: string;

  /**
   * Updated email address with validation.
   * MIGRATION: From UserEditor binding with email regex validation
   */
  email?: string;

  /**
   * Flag to update super user status (admin-only operation).
   * MIGRATION: Admin-only field for privilege escalation/demotion
   */
  isSuperUser?: boolean;

  /**
   * Flag to update the approved/authorized status.
   * MIGRATION: From Membership.ascx.vb chkAuthorize
   */
  isApproved?: boolean;

  /**
   * Flag to update the locked out status.
   * Set to false to unlock a user account.
   * MIGRATION: Unlock user capability from Membership management
   */
  isLockedOut?: boolean;

  /**
   * Flag indicating if the user must change password on next login.
   * MIGRATION: From chkUpdatePassword, Membership.UpdatePassword property
   */
  forcePasswordUpdate?: boolean;

  /**
   * Updated affiliate identifier for referral tracking.
   * MIGRATION: Affiliate update field
   */
  affiliateId?: number;
}

// ============================================================================
// Search and Filtering
// ============================================================================

/**
 * Filter options for searching and listing users.
 * Matches backend pagination and filtering parameters.
 *
 * MIGRATION NOTE: Derived from Users.ascx.vb BindData method (lines 231-291)
 * and filter properties (lines 49-86):
 * - _Filter: search text string
 * - _FilterProperty: property to filter on (Email, Username, or profile property)
 * - _CurrentPage: current page number (1-based)
 * - PageSize: records per page from portal settings
 *
 * Special filter values from BindData:
 * - 'OnLine' (line 261): shows online users via GetOnlineUsers
 * - 'Unauthorized' (line 258): shows unapproved users via GetUnAuthorizedUsers
 *
 * @interface UserSearchFilter
 * @description Query parameters for GET /api/users endpoint with filtering and pagination.
 */
export interface UserSearchFilter {
  /**
   * Search text to filter users.
   * MIGRATION: From _Filter (Users.ascx.vb line 49), txtSearch.Text (line 172)
   */
  filter?: string;

  /**
   * Property to search on.
   * Values: 'Email', 'Username', or a profile property name.
   * MIGRATION: From _FilterProperty (Users.ascx.vb line 50), ddlSearchType.SelectedValue (line 173)
   */
  filterProperty?: string;

  /**
   * Page index for pagination (1-based).
   * MIGRATION: From _CurrentPage (Users.ascx.vb line 51), default 1
   */
  pageIndex?: number;

  /**
   * Number of records per page.
   * MIGRATION: From PageSize property (Users.ascx.vb lines 114-119)
   */
  pageSize?: number;

  /**
   * Column name to sort results by.
   * MIGRATION: Grid sorting support
   */
  sortColumn?: string;

  /**
   * Sort direction flag.
   * True for ascending, false for descending.
   * MIGRATION: Grid sort direction support
   */
  sortAscending?: boolean;

  /**
   * Flag to filter for online users only.
   * MIGRATION: From SearchText == 'OnLine' (Users.ascx.vb line 261)
   * Triggers GetOnlineUsers query
   */
  showOnline?: boolean;

  /**
   * Flag to filter for unauthorized/unapproved users only.
   * MIGRATION: From SearchText == 'Unauthorized' (Users.ascx.vb line 258)
   * Triggers GetUnAuthorizedUsers query
   */
  showUnauthorized?: boolean;
}

// ============================================================================
// Paginated Response
// ============================================================================

/**
 * Paginated response wrapper for user list queries.
 * Matches backend PagedResult<UserDto> response structure.
 *
 * MIGRATION NOTE: Pagination metadata derived from Users.ascx.vb:
 * - TotalRecords (line 59): total count of matching users
 * - TotalPages (line 58): computed total page count
 * - CurrentPage, PageSize: pagination parameters
 *
 * @interface UserListResponse
 * @description Response structure for GET /api/users with pagination metadata.
 */
export interface UserListResponse {
  /**
   * Array of user DTOs for the current page.
   */
  users: UserDto[];

  /**
   * Total count of users matching the filter criteria.
   * MIGRATION: From TotalRecords (Users.ascx.vb line 59)
   */
  totalRecords: number;

  /**
   * Total number of pages based on totalRecords and pageSize.
   * MIGRATION: From TotalPages (Users.ascx.vb line 58)
   */
  totalPages: number;

  /**
   * Current page index (1-based).
   */
  pageIndex: number;

  /**
   * Number of records per page.
   */
  pageSize: number;
}
