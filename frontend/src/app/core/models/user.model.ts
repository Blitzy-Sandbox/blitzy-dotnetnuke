/**
 * @fileoverview Core User TypeScript interfaces for Angular 19 SPA
 * @description Provides foundational type definitions for user management,
 *              authentication, and authorization throughout the frontend application.
 *
 * MIGRATION NOTE: These interfaces are derived from DotNetNuke 4.x VB.NET entity classes:
 * - User interface from Library/Components/Users/UserInfo.vb
 * - UserProfile interface from Library/Components/Users/Profile/UserProfile.vb
 * - UserMembership interface from Library/Components/Users/Membership/UserMembership.vb
 *
 * VB.NET Null.NullInteger sentinel values have been converted to optional properties (?)
 * or undefined. VB.NET Date type is mapped to Date | string for JSON serialization
 * compatibility with the ASP.NET Core 8 Web API backend.
 *
 * @module core/models/user.model
 */

/**
 * User profile information interface containing personal and contact details.
 * Derived from DNN UserProfile.vb entity class.
 *
 * MIGRATION NOTE: Property names derived from UserProfile.vb constants (lines 46-71):
 * - cFirstName, cLastName, cStreet, cCity, cRegion, cCountry, cPostalCode
 * - cTelephone, cCell, cWebsite, cTimeZone, cPreferredLocale
 *
 * @interface UserProfile
 * @description Contains user's personal information including name, address,
 *              contact details, and preferences. All properties are optional
 *              to support progressive profile hydration.
 */
export interface UserProfile {
  /**
   * User's first name.
   * MIGRATION: Mapped from UserProfile.FirstName property (line 183-193)
   */
  firstName?: string;

  /**
   * User's last name.
   * MIGRATION: Mapped from UserProfile.LastName property (line 251-261)
   */
  lastName?: string;

  /**
   * Street address.
   * MIGRATION: Mapped from UserProfile.Street property (line 366-376)
   */
  street?: string;

  /**
   * City portion of the address.
   * MIGRATION: Mapped from UserProfile.City property (line 123-133)
   */
  city?: string;

  /**
   * Region/State/Province portion of the address.
   * MIGRATION: Mapped from UserProfile.Region property (line 346-356)
   */
  region?: string;

  /**
   * Country portion of the address.
   * MIGRATION: Mapped from UserProfile.Country property (line 143-153)
   */
  country?: string;

  /**
   * Postal/ZIP code portion of the address.
   * MIGRATION: Mapped from UserProfile.PostalCode property (line 288-298)
   */
  postalCode?: string;

  /**
   * Primary telephone/landline number.
   * MIGRATION: Mapped from UserProfile.Telephone property (line 386-396)
   */
  telephone?: string;

  /**
   * Cell/mobile phone number.
   * MIGRATION: Mapped from UserProfile.Cell property (line 103-113)
   */
  cell?: string;

  /**
   * User's personal website URL.
   * MIGRATION: Mapped from UserProfile.Website property (line 451-461)
   */
  website?: string;

  /**
   * User's preferred timezone offset in minutes.
   * MIGRATION: Mapped from UserProfile.TimeZone property (line 406-421)
   * In VB.NET, this was an Integer with Null.NullInteger sentinel.
   */
  timeZone?: number;

  /**
   * User's preferred locale/language code (e.g., 'en-US', 'fr-FR').
   * MIGRATION: Mapped from UserProfile.PreferredLocale property (line 308-318)
   */
  preferredLocale?: string;
}

/**
 * User membership status and authentication-related information.
 * Derived from DNN UserMembership.vb entity class.
 *
 * MIGRATION NOTE: Property names derived from UserMembership.vb private members (lines 44-59):
 * - _Approved, _LockedOut, _IsOnLine, _CreatedDate, _LastLoginDate
 * - _LastActivityDate, _LastPasswordChangeDate, _LastLockoutDate, _UpdatePassword
 *
 * Date properties use union type Date | string to support both JavaScript Date objects
 * and ISO 8601 date strings from JSON API responses.
 *
 * @interface UserMembership
 * @description Contains membership status flags and date tracking for user
 *              authentication lifecycle management.
 */
export interface UserMembership {
  /**
   * Indicates whether the user account is approved for login.
   * MIGRATION: Mapped from UserMembership.Approved property (line 83-93)
   * Default value in VB.NET was True.
   */
  approved: boolean;

  /**
   * Indicates whether the user account is currently locked out.
   * MIGRATION: Mapped from UserMembership.LockedOut property (line 223-233)
   * Default value in VB.NET was False.
   */
  lockedOut: boolean;

  /**
   * Indicates whether the user is currently online/active.
   * MIGRATION: Mapped from UserMembership.IsOnLine property (line 123-133)
   */
  isOnline: boolean;

  /**
   * Date and time when the user account was created.
   * MIGRATION: Mapped from UserMembership.CreatedDate property (line 103-113)
   */
  createdDate: Date | string;

  /**
   * Date and time of the user's last successful login.
   * MIGRATION: Mapped from UserMembership.LastLoginDate property (line 183-193)
   */
  lastLoginDate: Date | string;

  /**
   * Date and time of the user's last activity/interaction.
   * MIGRATION: Mapped from UserMembership.LastActivityDate property (line 143-153)
   */
  lastActivityDate: Date | string;

  /**
   * Date and time when the user last changed their password.
   * MIGRATION: Mapped from UserMembership.LastPasswordChangeDate property (line 203-213)
   */
  lastPasswordChangeDate: Date | string;

  /**
   * Date and time when the user was last locked out.
   * MIGRATION: Mapped from UserMembership.LastLockoutDate property (line 163-173)
   */
  lastLockoutDate: Date | string;

  /**
   * Flag indicating whether the user must update their password on next login.
   * MIGRATION: Mapped from UserMembership.UpdatePassword property (line 323-330)
   */
  updatePassword: boolean;
}

/**
 * Main User interface representing a user entity in the system.
 * Derived from DNN UserInfo.vb entity class.
 *
 * MIGRATION NOTE: Property names derived from UserInfo.vb private members (lines 47-58):
 * - _UserID, _Username, _DisplayName, _Email, _PortalID
 * - _IsSuperUser, _AffiliateID, _Membership, _Profile, _Roles
 *
 * FirstName and LastName are delegated properties that reference Profile.FirstName
 * and Profile.LastName respectively (see UserInfo.vb lines 144-185).
 *
 * VB.NET Null.NullInteger sentinel values (-1) converted to optional properties (?)
 * for affiliateId and other nullable integer fields.
 *
 * @interface User
 * @description Core user entity containing identity, portal association,
 *              authorization flags, profile information, and membership status.
 */
export interface User {
  /**
   * Unique identifier for the user.
   * MIGRATION: Mapped from UserInfo.UserID property (line 284-291)
   * VB.NET initialized this to Null.NullInteger (-1) for new users.
   */
  userId: number;

  /**
   * Unique username for authentication.
   * MIGRATION: Mapped from UserInfo.Username property (line 301-312)
   * This is a required, read-only field in the original implementation.
   */
  username: string;

  /**
   * Display name shown in the user interface.
   * MIGRATION: Mapped from UserInfo.DisplayName property (line 104-111)
   * Maximum length of 128 characters in the original implementation.
   */
  displayName: string;

  /**
   * User's first name.
   * MIGRATION: Mapped from UserInfo.FirstName property (line 144-151)
   * This is a delegated property that references Profile.FirstName.
   * Maximum length of 50 characters.
   */
  firstName: string;

  /**
   * User's last name.
   * MIGRATION: Mapped from UserInfo.LastName property (line 178-185)
   * This is a delegated property that references Profile.LastName.
   * Maximum length of 50 characters.
   */
  lastName: string;

  /**
   * User's email address.
   * MIGRATION: Mapped from UserInfo.Email property (line 121-134)
   * Maximum length of 256 characters with email regex validation.
   */
  email: string;

  /**
   * Portal (tenant) identifier the user belongs to.
   * MIGRATION: Mapped from UserInfo.PortalID property (line 219-226)
   * VB.NET initialized this to Null.NullInteger for new users.
   * A value of -1 typically indicates a host/super user.
   */
  portalId: number;

  /**
   * Flag indicating whether the user has super user (host) privileges.
   * MIGRATION: Mapped from UserInfo.IsSuperUser property (line 161-168)
   * Super users have administrative access across all portals.
   */
  isSuperUser: boolean;

  /**
   * Optional affiliate identifier for tracking referrals.
   * MIGRATION: Mapped from UserInfo.AffiliateID property (line 87-94)
   * VB.NET initialized this to Null.NullInteger; optional in TypeScript.
   */
  affiliateId?: number;

  /**
   * Array of role names the user belongs to.
   * MIGRATION: Mapped from UserInfo.Roles property (line 261-274)
   * In VB.NET, this was a String() array that was lazily hydrated.
   */
  roles: string[];

  /**
   * Optional user profile containing personal and contact information.
   * MIGRATION: Mapped from UserInfo.Profile property (line 236-250)
   * In VB.NET, this was lazily hydrated via ProfileController.GetUserProfile().
   */
  profile?: UserProfile;

  /**
   * Optional membership information containing authentication status and dates.
   * MIGRATION: Mapped from UserInfo.Membership property (line 195-209)
   * In VB.NET, this was lazily hydrated via UserController.GetUserMembership().
   */
  membership?: UserMembership;
}
