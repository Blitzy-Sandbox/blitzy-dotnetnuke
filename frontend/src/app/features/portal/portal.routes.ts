/**
 * @fileoverview Portal Feature Routing Configuration for Angular 19
 * @description Defines lazy-loaded routes for portal management feature including
 * portal list, portal creation/editing form, and portal settings components.
 * Implements authentication guards for route protection.
 *
 * MIGRATION NOTE:
 * This routing configuration replaces DNN's NavigateURL/EditUrl patterns found in:
 * - Website/admin/Portal/Portals.ascx.vb (lines 219-227, 303-311, 435-436)
 * - Website/admin/Portal/SiteSettings.ascx.vb (lines 235-243)
 *
 * Original DNN Navigation Patterns:
 * 1. Portal List Navigation:
 *    - Common.Globals.NavigateURL(TabId, "", "filter=" & Filter, "currentpage=" & CurrentPage)
 *    - Grid edit column: NavigateURL(objTab.TabID, "", "pid=KEYFIELD")
 *
 * 2. Site Settings Navigation:
 *    - Request.QueryString("pid") for portal ID parameter
 *    - Access check: PortalSettings.ActiveTab.ParentId = PortalSettings.SuperTabId Or UserInfo.IsSuperUser
 *
 * 3. Module Actions:
 *    - EditUrl("Signup") for new portal creation
 *    - EditUrl("Template") for template export
 *
 * New Angular Route Mapping:
 * - DNN querystring "pid" → Angular route parameter ":id"
 * - DNN IsHostMenu/IsSuperUser checks → authGuard route protection
 * - DNN NavigateURL patterns → Angular Router declarative routes
 * - DNN filter/currentpage params → Component state/query params
 *
 * @module features/portal/portal.routes
 * @see Portals.ascx.vb - Original DNN portal list navigation
 * @see SiteSettings.ascx.vb - Original DNN portal settings navigation
 * @see AuthGuard - Route protection implementation
 */

import { Routes } from '@angular/router';

import { authGuard } from '../../core/auth/auth.guard';

/**
 * Portal feature route definitions.
 *
 * Route Structure:
 * - '' (default) → Redirects to 'list'
 * - 'list' → Portal list view (replaces Portals.ascx grid view)
 * - 'new' → Create new portal (replaces EditUrl("Signup"))
 * - ':id' → Edit existing portal (replaces NavigateURL with pid=KEYFIELD)
 * - ':id/settings' → Detailed portal settings (replaces SiteSettings.ascx)
 *
 * All routes are protected by authGuard which replaces DNN's security checks:
 * - UserInfo.IsSuperUser (Portals.ascx.vb line 339)
 * - PortalSettings.ActiveTab.ParentId = PortalSettings.SuperTabId (SiteSettings.ascx.vb line 235)
 * - SecurityAccessLevel.Host checks in ModuleActions
 *
 * Lazy loading is implemented using loadComponent for optimal bundle splitting,
 * following Angular 19 standalone component patterns.
 */
const routes: Routes = [
  /**
   * Default route redirect.
   *
   * MIGRATION NOTE:
   * In DNN, the default view was determined by the TabId and module placement.
   * The Portals.ascx module was typically placed on the Host > Portals tab.
   * This redirect ensures users landing on /portals see the list view.
   */
  {
    path: '',
    redirectTo: 'list',
    pathMatch: 'full'
  },

  /**
   * Portal list route - displays all portals with filtering and pagination.
   *
   * MIGRATION NOTE:
   * Replaces Website/admin/Portal/Portals.ascx functionality:
   * - BindData() method (line 131-158) for fetching portal list
   * - grdPortals DataGrid for portal display
   * - CreateLetterSearch() for A-Z filtering
   * - ctlPagingControl for pagination
   *
   * DNN access control (line 339):
   *   If Not UserInfo.IsSuperUser Then
   *       Response.Redirect(NavigateURL("Access Denied"), True)
   *   End If
   *
   * Now handled by authGuard with role checks at API layer.
   */
  {
    path: 'list',
    loadComponent: () => import('./components/portal-list/portal-list.component')
      .then(m => m.PortalListComponent),
    canActivate: [authGuard],
    data: {
      title: 'Portal Management'
    }
  },

  /**
   * New portal creation route.
   *
   * MIGRATION NOTE:
   * Replaces DNN's EditUrl("Signup") pattern found in Portals.ascx.vb (line 435):
   *   Actions.Add(GetNextActionID, Localization.GetString(ModuleActionType.AddContent, LocalResourceFile),
   *               ModuleActionType.AddContent, "", "", EditUrl("Signup"), False, SecurityAccessLevel.Host, True, False)
   *
   * The original Signup.ascx.vb provided a wizard-like interface for:
   * - Portal name and alias configuration
   * - Administrator account creation
   * - Template selection
   * - Resource file copying
   *
   * SecurityAccessLevel.Host in DNN is now handled by authGuard plus API-level authorization.
   */
  {
    path: 'new',
    loadComponent: () => import('./components/portal-form/portal-form.component')
      .then(m => m.PortalFormComponent),
    canActivate: [authGuard],
    data: {
      title: 'Create Portal',
      mode: 'create'
    }
  },

  /**
   * Portal edit route with ID parameter.
   *
   * MIGRATION NOTE:
   * Replaces DNN's NavigateURL pattern for portal editing from Portals.ascx.vb (lines 303-311):
   *   Dim objTab As Entities.Tabs.TabInfo = objTabs.GetTabByName("Site Settings", PortalSettings.PortalId, PortalSettings.AdminTabId)
   *   Dim formatString As String = NavigateURL(objTab.TabID, "", "pid=KEYFIELD")
   *   formatString = formatString.Replace("KEYFIELD", "{0}")
   *   imageColumn.NavigateURLFormatString = formatString
   *
   * The "pid" querystring parameter is now the :id route parameter.
   * Angular route: /portals/:id → DNN: /admin/SiteSettings?pid={portalId}
   *
   * Access control in SiteSettings.ascx.vb (line 235):
   *   If Not (Request.QueryString("pid") Is Nothing) And
   *      (PortalSettings.ActiveTab.ParentId = PortalSettings.SuperTabId Or UserInfo.IsSuperUser) Then
   *       intPortalId = Int32.Parse(Request.QueryString("pid"))
   *   Else
   *       intPortalId = PortalId
   *   End If
   *
   * Now:
   * - Route parameter :id provides the portal ID
   * - authGuard ensures authentication
   * - API layer validates user has permission to edit specific portal
   */
  {
    path: ':id',
    loadComponent: () => import('./components/portal-form/portal-form.component')
      .then(m => m.PortalFormComponent),
    canActivate: [authGuard],
    data: {
      title: 'Edit Portal',
      mode: 'edit'
    }
  },

  /**
   * Portal settings route for detailed configuration.
   *
   * MIGRATION NOTE:
   * Provides extended portal configuration options beyond basic edit.
   * Replaces the tabbed sections in SiteSettings.ascx including:
   * - Basic Settings (portal name, logo, footer)
   * - Advanced Settings (page management, registration)
   * - Host Settings (expiry, host fee, quotas) - SuperUser only
   * - Stylesheet Editor
   * - Skin/Container Configuration
   *
   * In DNN, these were all sections on SiteSettings.ascx with visibility
   * controlled by IsSuperUser() checks (lines 193-197):
   *   Public Function IsSuperUser() As Boolean
   *       Return Me.UserInfo.IsSuperUser
   *   End Function
   *
   * The separate settings route allows for more granular navigation
   * and better separation of concerns in the Angular architecture.
   */
  {
    path: ':id/settings',
    loadComponent: () => import('./components/portal-settings/portal-settings.component')
      .then(m => m.PortalSettingsComponent),
    canActivate: [authGuard],
    data: {
      title: 'Portal Settings'
    }
  }
];

/**
 * Exported portal routes for use in parent routing configuration.
 *
 * @example
 * ```typescript
 * // In app.routes.ts
 * import { portalRoutes } from './features/portal/portal.routes';
 *
 * export const routes: Routes = [
 *   {
 *     path: 'portals',
 *     loadChildren: () => import('./features/portal/portal.routes')
 *       .then(m => m.portalRoutes)
 *   }
 * ];
 * ```
 *
 * Or using loadComponent pattern in parent routes:
 * ```typescript
 * {
 *   path: 'portals',
 *   children: portalRoutes
 * }
 * ```
 *
 * MIGRATION NOTE:
 * This export pattern enables lazy loading of the entire portal feature module,
 * replacing DNN's module-based architecture where the Portals module was loaded
 * dynamically based on TabModules configuration in the database.
 */
export const portalRoutes: Routes = routes;
