/**
 * Portal Feature TypeScript Model Definitions
 *
 * MIGRATION: VB.NET PortalInfo.vb and PortalAliasInfo.vb entities converted to TypeScript
 * interfaces for Angular 19 frontend. All types derived from legacy DotNetNuke 4.x entities.
 *
 * Source files:
 * - Library/Components/Portal/PortalInfo.vb (lines 33-70, 85-402)
 * - Library/Components/Portal/PortalAliasInfo.vb (lines 32-60)
 *
 * @fileoverview Portal-related interfaces and enums for type safety, API request/response
 * typing, and form data handling throughout the portal feature module.
 */

// ============================================================================
// ENUMS
// ============================================================================

/**
 * User Registration Type enumeration
 *
 * MIGRATION: Derived from PortalInfo.vb UserRegistration property (Integer type)
 * Maps to the registration mode setting for a portal, controlling how new users
 * can register for accounts.
 *
 * @enum {number}
 */
export enum UserRegistrationType {
  /** No registration allowed - users cannot self-register */
  None = 0,
  /** Private registration - requires administrator approval */
  Private = 1,
  /** Public registration - open to all users */
  Public = 2,
  /** Verified registration - requires email verification */
  Verified = 3,
}

/**
 * Banner Advertising Type enumeration
 *
 * MIGRATION: Derived from PortalInfo.vb BannerAdvertising property (Integer type)
 * Controls the banner advertising configuration for a portal.
 *
 * @enum {number}
 */
export enum BannerType {
  /** No banner advertising enabled */
  None = 0,
  /** Site-level banners managed by site administrators */
  Site = 1,
  /** Vendor banners from external advertising sources */
  Vendor = 2,
}

// ============================================================================
// INTERFACES
// ============================================================================

/**
 * Portal interface representing a DotNetNuke portal/site entity
 *
 * MIGRATION: Converted from VB.NET PortalInfo.vb class
 * - VB.NET Integer → TypeScript number
 * - VB.NET String → TypeScript string
 * - VB.NET Single (HostFee) → TypeScript number
 * - VB.NET Date (ExpiryDate) → TypeScript string (ISO 8601 format)
 * - VB.NET Guid → TypeScript string
 * - Applied camelCase naming convention per TypeScript standards
 * - ProcessorPassword excluded (sensitive field)
 *
 * Maps to backend PortalDto for API response typing.
 */
export interface Portal {
  /**
   * Unique portal identifier
   * MIGRATION: From _PortalID (Integer)
   */
  portalId: number;

  /**
   * Display name of the portal
   * MIGRATION: From _PortalName (String)
   */
  portalName: string;

  /**
   * Path to the portal logo file
   * MIGRATION: From _LogoFile (String)
   */
  logoFile?: string;

  /**
   * Footer text displayed at bottom of portal pages
   * MIGRATION: From _FooterText (String)
   */
  footerText?: string;

  /**
   * Portal expiration date in ISO 8601 format
   * MIGRATION: From _ExpiryDate (Date)
   */
  expiryDate?: string;

  /**
   * User registration mode for the portal
   * MIGRATION: From _UserRegistration (Integer) - use UserRegistrationType enum
   */
  userRegistration: UserRegistrationType;

  /**
   * Banner advertising configuration
   * MIGRATION: From _BannerAdvertising (Integer) - use BannerType enum
   */
  bannerAdvertising: BannerType;

  /**
   * User ID of the portal administrator
   * MIGRATION: From _AdministratorId (Integer)
   */
  administratorId: number;

  /**
   * Default currency code for the portal (e.g., "USD", "EUR")
   * MIGRATION: From _Currency (String)
   */
  currency?: string;

  /**
   * Hosting fee for the portal
   * MIGRATION: From _HostFee (Single)
   */
  hostFee: number;

  /**
   * Disk space allocation in MB
   * MIGRATION: From _HostSpace (Integer)
   */
  hostSpace: number;

  /**
   * Maximum number of pages allowed
   * MIGRATION: From _PageQuota (Integer)
   */
  pageQuota: number;

  /**
   * Maximum number of users allowed
   * MIGRATION: From _UserQuota (Integer)
   */
  userQuota: number;

  /**
   * Role ID for administrators
   * MIGRATION: From _AdministratorRoleId (Integer)
   */
  administratorRoleId: number;

  /**
   * Display name of the administrator role
   * MIGRATION: From _AdministratorRoleName (String)
   */
  administratorRoleName?: string;

  /**
   * Role ID for registered users
   * MIGRATION: From _RegisteredRoleId (Integer)
   */
  registeredRoleId: number;

  /**
   * Display name of the registered users role
   * MIGRATION: From _RegisteredRoleName (String)
   */
  registeredRoleName?: string;

  /**
   * Portal description for SEO and display
   * MIGRATION: From _Description (String)
   */
  description?: string;

  /**
   * SEO keywords for the portal
   * MIGRATION: From _KeyWords (String)
   */
  keyWords?: string;

  /**
   * Path to background image file
   * MIGRATION: From _BackgroundFile (String)
   */
  backgroundFile?: string;

  /**
   * Number of days to retain site logs
   * MIGRATION: From _SiteLogHistory (Integer)
   */
  siteLogHistory: number;

  /**
   * Contact email address for the portal
   * MIGRATION: From _Email (String)
   */
  email?: string;

  /**
   * Tab ID for the admin page
   * MIGRATION: From _AdminTabId (Integer)
   */
  adminTabId: number;

  /**
   * Tab ID for the super user page
   * MIGRATION: From _SuperTabId (Integer)
   */
  superTabId: number;

  /**
   * Current number of users in the portal
   * MIGRATION: From _Users (Integer)
   */
  users: number;

  /**
   * Current number of pages in the portal
   * MIGRATION: From _Pages (Integer)
   */
  pages: number;

  /**
   * Tab ID for the splash/landing page
   * MIGRATION: From _SplashTabId (Integer)
   */
  splashTabId?: number;

  /**
   * Tab ID for the home page
   * MIGRATION: From _HomeTabId (Integer)
   */
  homeTabId?: number;

  /**
   * Tab ID for the login page
   * MIGRATION: From _LoginTabId (Integer)
   */
  loginTabId?: number;

  /**
   * Tab ID for the user profile page
   * MIGRATION: From _UserTabId (Integer)
   */
  userTabId?: number;

  /**
   * Default language code (e.g., "en-US")
   * MIGRATION: From _DefaultLanguage (String)
   */
  defaultLanguage?: string;

  /**
   * Time zone offset in minutes from UTC
   * MIGRATION: From _TimeZoneOffset (Integer)
   */
  timeZoneOffset: number;

  /**
   * Relative path to the portal's home directory
   * MIGRATION: From _HomeDirectory (String)
   */
  homeDirectory?: string;

  /**
   * Portal version string
   * MIGRATION: From _Version (String)
   */
  version?: string;

  /**
   * Globally unique identifier for the portal
   * MIGRATION: From _GUID (Guid) - stored as string representation
   */
  guid: string;
}

/**
 * Portal Alias interface representing a domain/URL alias for a portal
 *
 * MIGRATION: Converted from VB.NET PortalAliasInfo.vb class (lines 32-60)
 * A portal can have multiple aliases (domain names) that point to it.
 *
 * Maps to backend PortalAliasDto for API response typing.
 */
export interface PortalAlias {
  /**
   * Unique portal alias identifier
   * MIGRATION: From _PortalAliasID (Integer)
   */
  portalAliasId: number;

  /**
   * Associated portal ID
   * MIGRATION: From _PortalID (Integer)
   */
  portalId: number;

  /**
   * HTTP alias/domain name (e.g., "www.example.com")
   * MIGRATION: From _HTTPAlias (String)
   */
  httpAlias: string;
}

/**
 * Create Portal Request interface for portal creation API calls
 *
 * MIGRATION: Aligned with backend CreatePortalRequest.cs
 * Captures all required and optional fields for creating a new portal,
 * including administrator account information.
 *
 * Derived from Website/admin/Portal/Signup.ascx.vb form fields.
 */
export interface CreatePortalRequest {
  /**
   * Primary domain/URL alias for the new portal
   * @required
   */
  portalAlias: string;

  /**
   * Display title for the portal
   * @required
   */
  title: string;

  /**
   * Portal description
   * @optional
   */
  description?: string;

  /**
   * SEO keywords for the portal
   * @optional
   */
  keyWords?: string;

  /**
   * Administrator's first name
   * @required
   */
  firstName: string;

  /**
   * Administrator's last name
   * @required
   */
  lastName: string;

  /**
   * Administrator's username for login
   * @required
   */
  username: string;

  /**
   * Administrator's password
   * @required
   */
  password: string;

  /**
   * Administrator's email address
   * @required
   */
  email: string;

  /**
   * Portal template file to use for initial setup
   * @optional
   */
  template?: string;

  /**
   * Custom home directory path
   * @optional
   */
  homeDirectory?: string;

  /**
   * Whether this is a child portal (subdirectory) or root portal
   * @required
   */
  isChildPortal: boolean;
}

/**
 * Update Portal Request interface for portal modification API calls
 *
 * MIGRATION: Aligned with backend UpdatePortalRequest.cs
 * Contains all editable portal properties. Derived from
 * Website/admin/Portal/SiteSettings.ascx.vb form fields.
 */
export interface UpdatePortalRequest {
  /**
   * Display name of the portal
   * @required
   */
  portalName: string;

  /**
   * Path to the portal logo file
   * @optional
   */
  logoFile?: string;

  /**
   * Footer text displayed at bottom of portal pages
   * @optional
   */
  footerText?: string;

  /**
   * Portal expiration date in ISO 8601 format
   * @optional
   */
  expiryDate?: string;

  /**
   * User registration mode for the portal
   * @required - use UserRegistrationType enum value
   */
  userRegistration: UserRegistrationType;

  /**
   * Banner advertising configuration
   * @required - use BannerType enum value
   */
  bannerAdvertising: BannerType;

  /**
   * Default currency code for the portal
   * @optional
   */
  currency?: string;

  /**
   * User ID of the portal administrator
   * @required
   */
  administratorId: number;

  /**
   * Hosting fee for the portal
   * @required
   */
  hostFee: number;

  /**
   * Disk space allocation in MB
   * @required
   */
  hostSpace: number;

  /**
   * Maximum number of pages allowed
   * @required
   */
  pageQuota: number;

  /**
   * Maximum number of users allowed
   * @required
   */
  userQuota: number;

  /**
   * Payment processor identifier
   * @optional
   */
  paymentProcessor?: string;

  /**
   * Processor user ID for payment processing
   * @optional
   */
  processorUserId?: string;

  /**
   * Processor password for payment processing
   * @optional - sensitive field, handle with care
   */
  processorPassword?: string;

  /**
   * Portal description for SEO and display
   * @optional
   */
  description?: string;

  /**
   * SEO keywords for the portal
   * @optional
   */
  keyWords?: string;

  /**
   * Path to background image file
   * @optional
   */
  backgroundFile?: string;

  /**
   * Number of days to retain site logs
   * @required
   */
  siteLogHistory: number;

  /**
   * Tab ID for the splash/landing page
   * @optional
   */
  splashTabId?: number;

  /**
   * Tab ID for the home page
   * @optional
   */
  homeTabId?: number;

  /**
   * Tab ID for the login page
   * @optional
   */
  loginTabId?: number;

  /**
   * Tab ID for the user profile page
   * @optional
   */
  userTabId?: number;

  /**
   * Default language code (e.g., "en-US")
   * @optional
   */
  defaultLanguage?: string;

  /**
   * Time zone offset in minutes from UTC
   * @required
   */
  timeZoneOffset: number;

  /**
   * Relative path to the portal's home directory
   * @optional
   */
  homeDirectory?: string;
}

/**
 * Portal Template interface for available portal templates
 *
 * Represents a template that can be used when creating new portals.
 * Templates provide pre-configured pages, modules, and settings.
 */
export interface PortalTemplate {
  /**
   * Display name of the template
   */
  name: string;

  /**
   * Template file name/path
   */
  fileName: string;

  /**
   * Description of what the template includes
   */
  description?: string;
}

/**
 * Generic Paged Result interface for paginated API responses
 *
 * Used to wrap collections returned from the API with pagination metadata.
 * Supports type-safe pagination for any entity type.
 *
 * @template T - The type of items in the result set
 */
export interface PagedResult<T> {
  /**
   * Array of items for the current page
   */
  items: T[];

  /**
   * Total number of items across all pages
   */
  totalCount: number;

  /**
   * Current page index (0-based)
   */
  pageIndex: number;

  /**
   * Number of items per page
   */
  pageSize: number;

  /**
   * Total number of pages
   */
  totalPages: number;
}
