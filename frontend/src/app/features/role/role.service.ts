import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { CreateRoleRequest, Role, UpdateRoleRequest } from '../../core/models';
import { ApiService, QueryParams } from '../../core/services/api.service';

/**
 * RoleService — thin orchestration over ApiService for the 'roles' REST resource.
 *
 * MIGRATION: replaces the data-access calls the legacy Web Forms code-behind made to
 * the static DotNetNuke RoleController (Website/admin/Security/Roles.ascx.vb and
 * EditRoles.ascx.vb called RoleController.GetPortalRoles / GetRoleByName / AddRole /
 * UpdateRole / DeleteRole via postback). Business logic is NOT ported here
 * (AAP §0.7.1: Angular services handle API communication only); the ASP.NET Core API
 * owns all business rules (e.g. the legacy GetRoleByName duplicate-name check and any
 * fee/frequency defaulting live in the backend RoleService/validators). This service
 * only maps CRUD intent to ApiService calls.
 */
@Injectable({ providedIn: 'root' })
export class RoleService {
  // MIGRATION: resource segment only — ApiService.baseUrl already includes '/api'.
  private static readonly RESOURCE = 'roles';

  private readonly api = inject(ApiService);

  /** GET /api/roles — list roles (optional search/paging/sort query params). */
  getRoles(params?: QueryParams): Observable<Role[]> {
    return this.api.getList<Role>(RoleService.RESOURCE, params);
  }

  /** GET /api/roles/{id} — fetch a single role (edit-form prefill). */
  getRole(id: number): Observable<Role> {
    return this.api.getById<Role>(RoleService.RESOURCE, id);
  }

  /** POST /api/roles — create a new role. */
  createRole(body: CreateRoleRequest): Observable<Role> {
    return this.api.create<Role>(RoleService.RESOURCE, body);
  }

  /** PUT /api/roles/{id} — update an existing role. */
  updateRole(id: number, body: UpdateRoleRequest): Observable<Role> {
    return this.api.update<Role>(RoleService.RESOURCE, id, body);
  }

  /** DELETE /api/roles/{id} — delete a role. */
  deleteRole(id: number): Observable<void> {
    return this.api.delete(RoleService.RESOURCE, id);
  }
}
