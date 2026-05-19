/**
 * @fileoverview Authentication Feature Routes Configuration
 * 
 * This module defines the routing configuration for the authentication feature
 * in the Angular 19 SPA application. It implements lazy-loaded routing using
 * the standalone component pattern introduced in Angular 14+ and made default in Angular 19.
 * 
 * MIGRATION NOTE:
 * This routing configuration replaces the DNN (DotNetNuke) page-based navigation system.
 * The following legacy routes have been converted to Angular Router paths:
 * - Login.aspx → /auth/login (LoginComponent)
 * - SendPassword.ascx → /auth/forgot-password (ForgotPasswordComponent)
 * 
 * Uses Angular 19 standalone component lazy loading pattern (loadComponent) instead of
 * the deprecated loadChildren with NgModules approach. This provides better tree-shaking
 * and reduced initial bundle size.
 * 
 * @module auth.routes
 * @see {@link https://angular.dev/guide/routing} Angular Router Documentation
 */

import { Routes } from '@angular/router';

/**
 * Authentication feature routes configuration.
 * 
 * Defines all routes related to user authentication workflows including:
 * - User login
 * - Password recovery/reset
 * 
 * These routes are designed to be lazy-loaded when integrated with the main
 * application routes, typically under the '/auth' path prefix.
 * 
 * @example
 * // Integration in app.routes.ts:
 * {
 *   path: 'auth',
 *   loadChildren: () => import('./features/auth/auth.routes').then(m => m.routes)
 * }
 * 
 * @example
 * // Or direct component loading:
 * {
 *   path: 'auth',
 *   children: routes
 * }
 */
export const routes: Routes = [
  {
    /**
     * Default route - redirects empty path to login page.
     * When users navigate to '/auth' or '/auth/', they are automatically
     * redirected to the login component.
     */
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  },
  {
    /**
     * Login route - primary authentication entry point.
     * 
     * MIGRATION: Replaces the legacy DNN Login.aspx page functionality.
     * Implements JWT-based authentication instead of Forms Authentication.
     * 
     * @lazy Loaded on demand using Angular 19 loadComponent pattern
     */
    path: 'login',
    loadComponent: () => import('./login/login.component').then(m => m.LoginComponent),
    title: 'Login'
  },
  {
    /**
     * Forgot Password route - password recovery workflow.
     * 
     * MIGRATION: Replaces the legacy DNN SendPassword.ascx functionality
     * from Website/admin/Security/SendPassword.ascx.vb which provided:
     * - Username/email-based password retrieval
     * - Security question verification (if configured)
     * - CAPTCHA validation for security
     * - Email-based password reset functionality
     * 
     * @lazy Loaded on demand using Angular 19 loadComponent pattern
     */
    path: 'forgot-password',
    loadComponent: () => import('./forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent),
    title: 'Forgot Password'
  }
];
