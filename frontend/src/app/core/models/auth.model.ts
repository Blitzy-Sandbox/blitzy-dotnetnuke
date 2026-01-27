/**
 * @fileoverview Authentication Model Interfaces for Angular 19 SPA
 * @description Provides TypeScript interfaces and enums for authentication operations.
 * 
 * MIGRATION NOTE: These interfaces replace DNN's Forms Authentication with JWT Bearer token
 * authentication per BFF pattern. Enum values derived from:
 * - Library/Components/Users/Membership/UserLoginStatus.vb
 * - Library/Components/Users/Membership/UserValidStatus.vb
 * 
 * @module core/models/auth.model
 */

import { User } from './user.model';

/**
 * Login request payload for POST /api/auth/login endpoint.
 * 
 * MIGRATION NOTE: Replaces DNN's Login.ascx form submission with:
 * - txtUsername → username
 * - txtPassword → password
 * - chkCookie → rememberMe
 * 
 * @interface LoginRequest
 */
export interface LoginRequest {
  /**
   * Username for authentication.
   * Maps to DNN txtUsername control in Login.ascx.
   */
  username: string;

  /**
   * Password for authentication.
   * Maps to DNN txtPassword control in Login.ascx.
   */
  password: string;

  /**
   * Optional flag for persistent login.
   * Maps to DNN CreatePersistentCookie parameter.
   * When true, uses longer token expiry.
   */
  rememberMe?: boolean;
}

/**
 * Authentication response from POST /api/auth/login and POST /api/auth/refresh endpoints.
 * 
 * MIGRATION NOTE: Replaces FormsAuthentication.SetAuthCookie with JWT tokens.
 * 
 * @interface AuthResponse
 */
export interface AuthResponse {
  /**
   * JWT access token for API authorization.
   * Used in Authorization: Bearer header.
   */
  accessToken: string;

  /**
   * Refresh token for obtaining new access tokens.
   * Used with POST /api/auth/refresh endpoint.
   */
  refreshToken: string;

  /**
   * Token expiration time in seconds.
   * Typically 3600 (60 minutes) per Section 0.7.7.
   */
  expiresIn: number;

  /**
   * Token type - always "Bearer".
   */
  tokenType: string;

  /**
   * Authenticated user information.
   */
  user: User;
}

/**
 * Refresh token request payload for POST /api/auth/refresh endpoint.
 * 
 * @interface RefreshRequest
 */
export interface RefreshRequest {
  /**
   * The refresh token to exchange for new access token.
   */
  refreshToken: string;
}

/**
 * User login status codes.
 * 
 * MIGRATION NOTE: Derived from Library/Components/Users/Membership/UserLoginStatus.vb
 * enum values (lines 23-31).
 * 
 * @enum UserLoginStatus
 */
export enum UserLoginStatus {
  /**
   * Login failed due to invalid credentials.
   */
  LOGIN_FAILURE = 0,

  /**
   * Login succeeded for regular user.
   */
  LOGIN_SUCCESS = 1,

  /**
   * Login succeeded for super user (host).
   */
  LOGIN_SUPERUSER = 2,

  /**
   * User account is locked out.
   */
  LOGIN_USERLOCKEDOUT = 3,

  /**
   * User account is not approved.
   */
  LOGIN_USERNOTAPPROVED = 4,

  /**
   * Admin password is insecure and needs update.
   */
  LOGIN_INSECUREADMINPASSWORD = 5,

  /**
   * Host password is insecure and needs update.
   */
  LOGIN_INSECUREHOSTPASSWORD = 6
}

/**
 * User validation status codes.
 * 
 * MIGRATION NOTE: Derived from Library/Components/Users/Membership/UserValidStatus.vb
 * enum values.
 * 
 * @enum UserValidStatus
 */
export enum UserValidStatus {
  /**
   * User is valid - no action required.
   */
  VALID = 0,

  /**
   * User's password has expired.
   */
  PASSWORDEXPIRED = 1,

  /**
   * User's password is expiring soon.
   */
  PASSWORDEXPIRING = 2,

  /**
   * User must update their profile.
   */
  UPDATEPROFILE = 3,

  /**
   * User must update their password.
   */
  UPDATEPASSWORD = 4
}
