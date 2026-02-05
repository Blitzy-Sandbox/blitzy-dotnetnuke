/**
 * @fileoverview Angular 19 Module Feature Routing Configuration
 * @description Defines lazy-loaded routes for the module administration feature.
 * Implements authentication guards for protected routes and provides navigation
 * for module list, creation, editing, settings, export, and import functionality.
 *
 * MIGRATION NOTE:
 * This routing configuration replaces DNN's NavigateURL patterns found in:
 * - Website/admin/Modules/ModuleSettings.ascx.vb (Response.Redirect with NavigateURL)
 * - Website/admin/Modules/Export.ascx.vb (Request.QueryString("moduleid"))
 * - Website/admin/Modules/Import.ascx.vb (Request.QueryString("moduleid"))
 *
 * Original DNN navigation pattern:
 * - Response.Redirect(NavigateURL(TabId, "Module", "moduleid=" & ModuleId))
 * - Navigation relied on querystring parameters: ?moduleid=123&tabid=456
 * - Each module function (settings, export, import) was a separate ASCX control
 *   loaded via ctl= querystring parameter
 *
 * New Angular routing approach:
 * - Uses declarative route configuration with lazy-loaded components
 * - Converts querystring parameter (moduleid) to route parameter (:id)
 * - Module portability features (export/import) are separate routes
 * - All routes protected by authGuard for authenticated access only
 *
 * Route Structure:
 * /modules              -> Redirects to /modules/list
 * /modules/list         -> Module list grid (lazy-loaded)
 * /modules/new          -> Create new module form (lazy-loaded)
 * /modules/:id          -> Edit existing module form (lazy-loaded)
 * /modules/:id/settings -> Module settings configuration (lazy-loaded)
 * /modules/:id/export   -> Module export functionality (lazy-loaded)
 * /modules/:id/import   -> Module import functionality (lazy-loaded)
 *
 * @module features/module/module.routes
 * @see ModuleSettings.ascx.vb - Original DNN module settings
 * @see Export.ascx.vb - Original DNN module export
 * @see Import.ascx.vb - Original DNN module import
 */

import { Routes } from '@angular/router';

import { authGuard } from '../../core/auth/auth.guard';

/**
 * Module feature routes configuration array.
 *
 * Defines all routes for module administration with lazy-loaded components
 * and authentication protection. Each route includes:
 * - Path definition with optional route parameters
 * - Lazy-loaded component using loadComponent for optimal bundle splitting
 * - Authentication guard via canActivate array
 * - Route data with title for browser tab display
 *
 * MIGRATION NOTE:
 * DNN's module administration was accessed via:
 * - Admin menu -> Module Settings (loads ModuleSettings.ascx)
 * - Module Actions dropdown -> Export Content (loads Export.ascx with ?moduleid=x)
 * - Module Actions dropdown -> Import Content (loads Import.ascx with ?moduleid=x)
 *
 * The new Angular routes provide:
 * - Cleaner URLs without querystring parameters
 * - Better browser history support
 * - Improved deep-linking capabilities
 * - Lazy loading for better initial load performance
 *
 * @type {Routes}
 */
const routes: Routes = [
  /**
   * Default route - redirects to module list.
   *
   * MIGRATION NOTE:
   * Replaces DNN's default admin module tab navigation which showed
   * the module management grid by default.
   */
  {
    path: '',
    redirectTo: 'list',
    pathMatch: 'full'
  },

  /**
   * Module list route - displays grid of all modules.
   *
   * MIGRATION NOTE:
   * Replaces the module list functionality from DNN's admin pages that
   * displayed installed modules. Uses Angular's lazy loading pattern
   * for optimal bundle splitting.
   *
   * Protected by authGuard requiring authentication to access.
   */
  {
    path: 'list',
    loadComponent: () =>
      import('./components/module-list/module-list.component').then(
        (m) => m.ModuleListComponent
      ),
    canActivate: [authGuard],
    data: {
      title: 'Modules'
    }
  },

  /**
   * Module creation route - displays form for new module.
   *
   * MIGRATION NOTE:
   * Replaces DNN's module creation wizard functionality. In DNN,
   * new modules were added via the control panel or page editing mode.
   * The new route provides a dedicated form for module creation.
   *
   * Protected by authGuard requiring authentication to access.
   */
  {
    path: 'new',
    loadComponent: () =>
      import('./components/module-form/module-form.component').then(
        (m) => m.ModuleFormComponent
      ),
    canActivate: [authGuard],
    data: {
      title: 'New Module'
    }
  },

  /**
   * Module edit route - displays form for editing existing module.
   *
   * MIGRATION NOTE:
   * Replaces DNN's module editing functionality accessed via module actions.
   * The :id parameter replaces the querystring ?moduleid=x pattern used in
   * ModuleSettings.ascx.vb (line 49: Private Shadows ModuleId As Integer = -1).
   *
   * Route parameter :id maps to the module's unique identifier.
   * Protected by authGuard requiring authentication to access.
   */
  {
    path: ':id',
    loadComponent: () =>
      import('./components/module-form/module-form.component').then(
        (m) => m.ModuleFormComponent
      ),
    canActivate: [authGuard],
    data: {
      title: 'Edit Module'
    }
  },

  /**
   * Module settings route - displays detailed module configuration.
   *
   * MIGRATION NOTE:
   * Directly derived from Website/admin/Modules/ModuleSettings.ascx.vb.
   * This route provides access to module-specific settings including:
   * - Module title and icon
   * - Cache configuration
   * - Visibility settings (Maximized/Minimized/None)
   * - Permission settings (view/edit roles)
   * - Container and styling options
   * - Start/End date configuration
   *
   * Original DNN access pattern:
   * - Module Actions dropdown -> Settings
   * - URL: /admin/Modules/ModuleSettings.aspx?moduleid=123&tabid=456
   *
   * The :id/settings path provides cleaner URL structure.
   * Protected by authGuard requiring authentication to access.
   */
  {
    path: ':id/settings',
    loadComponent: () =>
      import('./components/module-settings/module-settings.component').then(
        (m) => m.ModuleSettingsComponent
      ),
    canActivate: [authGuard],
    data: {
      title: 'Module Settings'
    }
  },

  /**
   * Module export route - provides module content export functionality.
   *
   * MIGRATION NOTE:
   * Directly derived from Website/admin/Modules/Export.ascx.vb.
   * Enables exporting module content to XML file for portability.
   *
   * Original DNN implementation (Export.ascx.vb, lines 65-67):
   * ```vb
   * If Not Request.QueryString("moduleid") Is Nothing Then
   *     ModuleId = Int32.Parse(Request.QueryString("moduleid"))
   * End If
   * ```
   *
   * The :id parameter replaces the querystring moduleid parameter.
   * Export functionality allows administrators to:
   * - Select target folder for export file
   * - Specify export filename
   * - Export module content as XML
   *
   * Protected by authGuard requiring authentication to access.
   */
  {
    path: ':id/export',
    loadComponent: () =>
      import('./components/module-export/module-export.component').then(
        (m) => m.ModuleExportComponent
      ),
    canActivate: [authGuard],
    data: {
      title: 'Export Module'
    }
  },

  /**
   * Module import route - provides module content import functionality.
   *
   * MIGRATION NOTE:
   * Directly derived from Website/admin/Modules/Import.ascx.vb.
   * Enables importing module content from XML file for portability.
   *
   * Original DNN implementation (Import.ascx.vb, lines 67-69):
   * ```vb
   * If Not Request.QueryString("moduleid") Is Nothing Then
   *     ModuleId = Int32.Parse(Request.QueryString("moduleid"))
   * End If
   * ```
   *
   * The :id parameter replaces the querystring moduleid parameter.
   * Import functionality allows administrators to:
   * - Select source folder containing import files
   * - Choose XML file to import
   * - Import content into the target module
   *
   * Protected by authGuard requiring authentication to access.
   */
  {
    path: ':id/import',
    loadComponent: () =>
      import('./components/module-import/module-import.component').then(
        (m) => m.ModuleImportComponent
      ),
    canActivate: [authGuard],
    data: {
      title: 'Import Module'
    }
  }
];

/**
 * Exported module routes for use in parent routing configuration.
 *
 * This constant is imported by the parent app.routes.ts to enable
 * lazy loading of the entire module feature at the /modules path.
 *
 * @example
 * ```typescript
 * // In app.routes.ts
 * import { moduleRoutes } from './features/module/module.routes';
 *
 * export const routes: Routes = [
 *   {
 *     path: 'modules',
 *     children: moduleRoutes
 *   }
 *   // Or with loadChildren for lazy loading:
 *   // {
 *   //   path: 'modules',
 *   //   loadChildren: () => import('./features/module/module.routes')
 *   //     .then(m => m.moduleRoutes)
 *   // }
 * ];
 * ```
 *
 * MIGRATION NOTE:
 * This replaces DNN's DesktopModules registration and admin tab navigation
 * with Angular's declarative routing configuration. The routes are designed
 * to work with Angular's lazy loading capabilities for optimal performance.
 *
 * @type {Routes}
 */
export const moduleRoutes: Routes = routes;
