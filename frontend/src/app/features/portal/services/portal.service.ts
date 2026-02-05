/**
 * Portal Service - Angular 19 HTTP Service for Portal API Operations
 *
 * MIGRATION: Converted from DotNetNuke 4.x VB.NET data access patterns:
 * - Library/Components/Portal/PortalController.vb (lines 1224-1265, 980-1176, 1524-1575)
 * - Library/Components/Portal/PortalAliasController.vb (lines 28-98)
 *
 * This service replaces the legacy SqlHelper/DataProvider patterns with modern
 * Angular HTTP client operations. All methods return RxJS Observables for
 * reactive data handling.
 *
 * Key transformations:
 * - VB.NET DataProvider.Instance().GetPortals() → HttpClient.get()
 * - VB.NET FillPortalInfoCollection(dr) → JSON deserialization
 * - VB.NET DataCache patterns → Angular service singleton pattern
 * - VB.NET Null.NullInteger handling → TypeScript optional properties
 *
 * @fileoverview HTTP service for portal CRUD operations and alias management
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  Portal,
  PortalAlias,
  CreatePortalRequest,
  UpdatePortalRequest,
  PortalTemplate,
  PagedResult,
} from '../models/portal.model';
import { environment } from '../../../../environments/environment';

/**
 * Portal Service
 *
 * Angular 19 HTTP service providing CRUD operations for portal management
 * and portal alias management. Uses the inject() function for dependency
 * injection per Angular 19 standards.
 *
 * @Injectable with providedIn: 'root' ensures singleton pattern application-wide.
 *
 * MIGRATION: Replaces the following DNN components:
 * - PortalController.vb - Portal CRUD operations
 * - PortalAliasController.vb - Portal alias management
 *
 * API Endpoints:
 * - GET    /api/portals            - List portals (paginated)
 * - GET    /api/portals/:id        - Get portal by ID
 * - POST   /api/portals            - Create portal
 * - PUT    /api/portals/:id        - Update portal
 * - DELETE /api/portals/:id        - Delete portal
 * - GET    /api/portals/:id/aliases - Get portal aliases
 * - POST   /api/portals/:id/aliases - Add portal alias
 * - PUT    /api/portals/aliases/:aliasId - Update portal alias
 * - DELETE /api/portals/aliases/:aliasId - Delete portal alias
 * - GET    /api/portals/templates  - Get available templates
 * - GET    /api/portals/check-alias - Check alias availability
 */
@Injectable({
  providedIn: 'root',
})
export class PortalService {
  /**
   * Angular HttpClient instance injected via inject() function.
   * MIGRATION: Angular 19 prefers inject() over constructor injection.
   * @private
   */
  private readonly http = inject(HttpClient);

  /**
   * Base URL for portal API endpoints.
   * Constructed from environment configuration.
   * MIGRATION: Replaces legacy web.config connectionStrings and appSettings.
   * @private
   */
  private readonly apiUrl = `${environment.apiBaseUrl}/portals`;

  // ==========================================================================
  // PORTAL CRUD OPERATIONS
  // ==========================================================================

  /**
   * Retrieves a paginated list of portals.
   *
   * MIGRATION: Replaces VB.NET PortalController.GetPortals() and GetPortalsByName()
   * Source: Library/Components/Portal/PortalController.vb lines 1263-1265
   *
   * Original VB.NET pattern:
   * ```vb
   * Public Function GetPortals() As ArrayList
   *     Return FillPortalInfoCollection(DataProvider.Instance().GetPortals())
   * End Function
   * ```
   *
   * @param pageIndex - Zero-based page index for pagination (default: 0)
   * @param pageSize - Number of items per page (default: 10)
   * @param filter - Optional search filter string for portal names
   * @returns Observable<PagedResult<Portal>> - Paginated list of portals
   *
   * @example
   * ```typescript
   * // Get first page with default page size
   * portalService.getPortals().subscribe(result => {
   *   console.log(`Found ${result.totalCount} portals`);
   *   result.items.forEach(portal => console.log(portal.portalName));
   * });
   *
   * // Get second page with custom page size and filter
   * portalService.getPortals(1, 20, 'admin').subscribe(result => {
   *   console.log(`Page 2 of ${result.totalPages}`);
   * });
   * ```
   */
  getPortals(
    pageIndex: number = 0,
    pageSize: number = 10,
    filter?: string
  ): Observable<PagedResult<Portal>> {
    // Build query parameters for pagination and filtering
    let params = new HttpParams()
      .set('pageIndex', pageIndex.toString())
      .set('pageSize', pageSize.toString());

    // Add filter parameter if provided
    // MIGRATION: Replaces GetPortalsByName() which accepted a filter string
    if (filter && filter.trim()) {
      params = params.set('filter', filter.trim());
    }

    return this.http.get<PagedResult<Portal>>(this.apiUrl, { params });
  }

  /**
   * Retrieves a single portal by its unique identifier.
   *
   * MIGRATION: Replaces VB.NET PortalController.GetPortal(ByVal PortalId As Integer)
   * Source: Library/Components/Portal/PortalController.vb lines 1224-1251
   *
   * Original VB.NET pattern:
   * ```vb
   * Public Function GetPortal(ByVal PortalId As Integer) As PortalInfo
   *     Dim dr As IDataReader = DataProvider.Instance().GetPortal(PortalId)
   *     Try
   *         portal = FillPortalInfo(dr)
   *     Finally
   *         If Not dr Is Nothing Then dr.Close()
   *     End Try
   *     Return portal
   * End Function
   * ```
   *
   * @param id - The unique portal ID to retrieve
   * @returns Observable<Portal> - The portal entity
   *
   * @example
   * ```typescript
   * portalService.getPortal(1).subscribe(
   *   portal => console.log(`Retrieved portal: ${portal.portalName}`),
   *   error => console.error('Portal not found', error)
   * );
   * ```
   */
  getPortal(id: number): Observable<Portal> {
    return this.http.get<Portal>(`${this.apiUrl}/${id}`);
  }

  /**
   * Creates a new portal with the specified configuration.
   *
   * MIGRATION: Replaces VB.NET PortalController.CreatePortal() overloads
   * Source: Library/Components/Portal/PortalController.vb lines 980-1176
   *
   * Original VB.NET pattern:
   * ```vb
   * Public Function CreatePortal(ByVal PortalName As String, ByVal FirstName As String,
   *     ByVal LastName As String, ByVal Username As String, ByVal Password As String,
   *     ByVal Email As String, ...) As Integer
   *     ' Creates portal, admin user, applies template
   *     Dim intPortalId As Integer = CreatePortal(PortalName, HomeDirectory)
   *     ' ... additional setup
   *     Return intPortalId
   * End Function
   * ```
   *
   * The backend handles all the complex portal creation logic including:
   * - Creating the portal database record
   * - Creating the administrator user account
   * - Applying the selected template
   * - Setting up the home directory structure
   * - Creating default roles and permissions
   *
   * @param request - CreatePortalRequest containing portal configuration
   * @returns Observable<Portal> - The newly created portal
   *
   * @example
   * ```typescript
   * const newPortal: CreatePortalRequest = {
   *   portalAlias: 'newsite.example.com',
   *   title: 'New Portal',
   *   description: 'A new portal site',
   *   firstName: 'Admin',
   *   lastName: 'User',
   *   username: 'admin',
   *   password: 'SecurePass123!',
   *   email: 'admin@example.com',
   *   template: 'default.template',
   *   isChildPortal: false
   * };
   *
   * portalService.createPortal(newPortal).subscribe(
   *   portal => console.log(`Created portal with ID: ${portal.portalId}`),
   *   error => console.error('Failed to create portal', error)
   * );
   * ```
   */
  createPortal(request: CreatePortalRequest): Observable<Portal> {
    return this.http.post<Portal>(this.apiUrl, request);
  }

  /**
   * Updates an existing portal's configuration.
   *
   * MIGRATION: Replaces VB.NET PortalController.UpdatePortalInfo()
   * Source: Library/Components/Portal/PortalController.vb lines 1524-1575
   *
   * Original VB.NET pattern:
   * ```vb
   * Public Sub UpdatePortalInfo(ByVal Portal As PortalInfo)
   *     UpdatePortalInfo(Portal.PortalID, Portal.PortalName, ...)
   * End Sub
   *
   * Public Sub UpdatePortalInfo(ByVal PortalId As Integer, ByVal PortalName As String, ...)
   *     DataProvider.Instance().UpdatePortalInfo(PortalId, PortalName, ...)
   *     DataCache.ClearPortalCache(PortalId, True)
   * End Sub
   * ```
   *
   * The backend handles cache invalidation automatically after update.
   *
   * @param id - The portal ID to update
   * @param request - UpdatePortalRequest containing updated portal properties
   * @returns Observable<Portal> - The updated portal entity
   *
   * @example
   * ```typescript
   * const updates: UpdatePortalRequest = {
   *   portalName: 'Updated Portal Name',
   *   description: 'New description',
   *   userRegistration: UserRegistrationType.Public,
   *   bannerAdvertising: BannerType.None,
   *   administratorId: 1,
   *   hostFee: 0,
   *   hostSpace: 100,
   *   pageQuota: 50,
   *   userQuota: 100,
   *   siteLogHistory: 30,
   *   timeZoneOffset: -300
   * };
   *
   * portalService.updatePortal(1, updates).subscribe(
   *   portal => console.log(`Portal updated: ${portal.portalName}`),
   *   error => console.error('Update failed', error)
   * );
   * ```
   */
  updatePortal(id: number, request: UpdatePortalRequest): Observable<Portal> {
    return this.http.put<Portal>(`${this.apiUrl}/${id}`, request);
  }

  /**
   * Deletes a portal and all associated data.
   *
   * MIGRATION: Replaces VB.NET PortalController.DeletePortal()
   * Source: Library/Components/Portal/PortalController.vb lines 1195-1207
   *
   * Original VB.NET pattern:
   * ```vb
   * Public Sub DeletePortal(ByVal PortalId As Integer)
   *     ' Delete child portals, folders, files, tabs, modules
   *     DataProvider.Instance().DeletePortalInfo(PortalId)
   *     DataCache.ClearHostCache(True)
   * End Sub
   * ```
   *
   * WARNING: This operation is destructive and cannot be undone.
   * The backend handles cascade deletion of all related entities.
   *
   * @param id - The portal ID to delete
   * @returns Observable<void> - Completes on successful deletion
   *
   * @example
   * ```typescript
   * // Confirm before deletion
   * if (confirm('Are you sure you want to delete this portal?')) {
   *   portalService.deletePortal(portalId).subscribe(
   *     () => console.log('Portal deleted successfully'),
   *     error => console.error('Delete failed', error)
   *   );
   * }
   * ```
   */
  deletePortal(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  // ==========================================================================
  // PORTAL ALIAS MANAGEMENT
  // ==========================================================================

  /**
   * Retrieves all domain aliases for a specific portal.
   *
   * MIGRATION: Replaces VB.NET PortalAliasController.GetPortalAliasArrayByPortalID()
   * Source: Library/Components/Portal/PortalAliasController.vb lines 44-61
   *
   * Original VB.NET pattern:
   * ```vb
   * Public Function GetPortalAliasArrayByPortalID(ByVal PortalID As Integer) As ArrayList
   *     Dim dr As IDataReader = DataProvider.Instance().GetPortalAliasByPortalID(PortalID)
   *     Try
   *         Dim arr As New ArrayList
   *         While dr.Read
   *             Dim objPortalAliasInfo As New PortalAliasInfo
   *             objPortalAliasInfo.PortalAliasID = Convert.ToInt32(dr("PortalAliasID"))
   *             objPortalAliasInfo.HTTPAlias = Convert.ToString(dr("HTTPAlias")).ToLower
   *             arr.Add(objPortalAliasInfo)
   *         End While
   *         Return arr
   *     Finally
   *         If Not dr Is Nothing Then dr.Close()
   *     End Try
   * End Function
   * ```
   *
   * @param portalId - The portal ID to retrieve aliases for
   * @returns Observable<PortalAlias[]> - Array of portal aliases
   *
   * @example
   * ```typescript
   * portalService.getPortalAliases(1).subscribe(aliases => {
   *   aliases.forEach(alias => console.log(`Alias: ${alias.httpAlias}`));
   * });
   * ```
   */
  getPortalAliases(portalId: number): Observable<PortalAlias[]> {
    return this.http.get<PortalAlias[]>(`${this.apiUrl}/${portalId}/aliases`);
  }

  /**
   * Adds a new domain alias to a portal.
   *
   * MIGRATION: Replaces VB.NET PortalAliasController.AddPortalAlias()
   * Source: Library/Components/Portal/PortalAliasController.vb lines 28-32
   *
   * Original VB.NET pattern:
   * ```vb
   * Public Function AddPortalAlias(ByVal objPortalAliasInfo As PortalAliasInfo) As Integer
   *     DataCache.ClearHostCache(False)
   *     Return DataProvider.Instance().AddPortalAlias(
   *         objPortalAliasInfo.PortalID,
   *         objPortalAliasInfo.HTTPAlias.ToLower)
   * End Function
   * ```
   *
   * @param portalId - The portal ID to add the alias to
   * @param httpAlias - The domain/URL alias string (e.g., "www.example.com")
   * @returns Observable<PortalAlias> - The newly created portal alias
   *
   * @example
   * ```typescript
   * portalService.addPortalAlias(1, 'subdomain.example.com').subscribe(
   *   alias => console.log(`Created alias: ${alias.httpAlias}`),
   *   error => console.error('Failed to add alias', error)
   * );
   * ```
   */
  addPortalAlias(portalId: number, httpAlias: string): Observable<PortalAlias> {
    // Send the alias as a request body object
    const request = { httpAlias: httpAlias.toLowerCase() };
    return this.http.post<PortalAlias>(
      `${this.apiUrl}/${portalId}/aliases`,
      request
    );
  }

  /**
   * Updates an existing portal alias.
   *
   * MIGRATION: Replaces VB.NET PortalAliasController.UpdatePortalAliasInfo()
   * Source: Library/Components/Portal/PortalAliasController.vb lines 94-98
   *
   * Original VB.NET pattern:
   * ```vb
   * Public Sub UpdatePortalAliasInfo(ByVal objPortalAliasInfo As PortalAliasInfo)
   *     DataCache.ClearHostCache(False)
   *     DataProvider.Instance().UpdatePortalAliasInfo(
   *         objPortalAliasInfo.PortalAliasID,
   *         objPortalAliasInfo.PortalID,
   *         objPortalAliasInfo.HTTPAlias.ToLower)
   * End Sub
   * ```
   *
   * @param aliasId - The portal alias ID to update
   * @param httpAlias - The new domain/URL alias string
   * @returns Observable<PortalAlias> - The updated portal alias
   *
   * @example
   * ```typescript
   * portalService.updatePortalAlias(5, 'new-alias.example.com').subscribe(
   *   alias => console.log(`Updated alias to: ${alias.httpAlias}`),
   *   error => console.error('Update failed', error)
   * );
   * ```
   */
  updatePortalAlias(aliasId: number, httpAlias: string): Observable<PortalAlias> {
    // Send the alias as a request body object
    const request = { httpAlias: httpAlias.toLowerCase() };
    return this.http.put<PortalAlias>(
      `${this.apiUrl}/aliases/${aliasId}`,
      request
    );
  }

  /**
   * Deletes a portal alias.
   *
   * MIGRATION: Replaces VB.NET PortalAliasController.DeletePortalAlias()
   * Source: Library/Components/Portal/PortalAliasController.vb lines 34-38
   *
   * Original VB.NET pattern:
   * ```vb
   * Public Sub DeletePortalAlias(ByVal PortalAliasID As Integer)
   *     DataCache.ClearHostCache(False)
   *     DataProvider.Instance().DeletePortalAlias(PortalAliasID)
   * End Sub
   * ```
   *
   * WARNING: Ensure the portal has at least one alias remaining before deletion.
   *
   * @param aliasId - The portal alias ID to delete
   * @returns Observable<void> - Completes on successful deletion
   *
   * @example
   * ```typescript
   * portalService.deletePortalAlias(5).subscribe(
   *   () => console.log('Alias deleted successfully'),
   *   error => console.error('Delete failed', error)
   * );
   * ```
   */
  deletePortalAlias(aliasId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/aliases/${aliasId}`);
  }

  // ==========================================================================
  // PORTAL TEMPLATE MANAGEMENT
  // ==========================================================================

  /**
   * Retrieves available portal templates for portal creation.
   *
   * MIGRATION: Provides template listing functionality that was handled
   * server-side in the legacy DNN application through file system enumeration.
   *
   * Templates are pre-configured portal setups that include pages, modules,
   * and default settings. When creating a new portal, users can select a
   * template to use as the starting point.
   *
   * @returns Observable<PortalTemplate[]> - Array of available templates
   *
   * @example
   * ```typescript
   * portalService.getTemplates().subscribe(templates => {
   *   templates.forEach(t => console.log(`${t.name}: ${t.description}`));
   * });
   * ```
   */
  getTemplates(): Observable<PortalTemplate[]> {
    return this.http.get<PortalTemplate[]>(`${this.apiUrl}/templates`);
  }

  // ==========================================================================
  // ALIAS VALIDATION
  // ==========================================================================

  /**
   * Checks if a portal alias already exists in the system.
   *
   * MIGRATION: Replaces VB.NET PortalAliasController.GetPortalAlias() check
   * Source: Library/Components/Portal/PortalAliasController.vb lines 40-42
   *
   * Original VB.NET pattern:
   * ```vb
   * Public Function GetPortalAlias(ByVal PortalAlias As String, ByVal PortalID As Integer) As PortalAliasInfo
   *     Return CType(CBO.FillObject(DataProvider.Instance().GetPortalAlias(PortalAlias, PortalID),
   *         GetType(PortalAliasInfo)), PortalAliasInfo)
   * End Function
   * ```
   *
   * This method is used for validation during portal creation or alias
   * addition to ensure uniqueness of domain aliases across the system.
   *
   * @param httpAlias - The domain/URL alias to check
   * @param excludePortalId - Optional portal ID to exclude from the check
   *                          (useful when updating an existing portal)
   * @returns Observable<boolean> - true if alias exists, false otherwise
   *
   * @example
   * ```typescript
   * // Check if alias is available during form validation
   * portalService.checkAliasExists('newsite.example.com').subscribe(exists => {
   *   if (exists) {
   *     this.form.get('alias')?.setErrors({ aliasExists: true });
   *   }
   * });
   *
   * // Exclude current portal when updating
   * portalService.checkAliasExists('site.example.com', currentPortalId).subscribe(exists => {
   *   // Handle result
   * });
   * ```
   */
  checkAliasExists(httpAlias: string, excludePortalId?: number): Observable<boolean> {
    let params = new HttpParams().set('alias', httpAlias.toLowerCase());

    if (excludePortalId !== undefined && excludePortalId !== null) {
      params = params.set('excludePortalId', excludePortalId.toString());
    }

    return this.http.get<boolean>(`${this.apiUrl}/check-alias`, { params });
  }
}
