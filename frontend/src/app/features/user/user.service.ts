import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService, QueryParams } from '../../core/services/api.service';
import {
  ChangePasswordRequest,
  CreateUserRequest,
  UpdateUserRequest,
  User,
} from '../../core/models';

/**
 * UserService — thin orchestration over the shared ApiService for the
 * `/api/users` resource. API communication ONLY — no business logic, no state
 * (AAP §0.7.1). The app-wide authInterceptor attaches the JWT, and ApiService
 * unwraps the `{ data, meta }` envelope and normalizes RFC 7807 errors, so every
 * method here is a one-line delegation.
 *
 * MIGRATION: replaces the data-access surface of the legacy DNN admin user
 * screens — UserController.GetUsers/GetUsersByEmail/GetUsersByUserName
 * (Website/admin/Users/Users.ascx.vb), CreateUser/UpdateUser/DeleteUser
 * (User.ascx.vb) and ChangePassword (Password.ascx.vb). Postback/ViewState is
 * eliminated in favor of stateless HTTP.
 */
@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly api = inject(ApiService);

  /** Bare resource segment — ApiService prepends the `/api` base URL. */
  private readonly resource = 'users';

  /**
   * GET /api/users (optionally filtered/searched).
   * MIGRATION: Users.ascx.vb BindData → UserController.GetUsers* + letter/search filters.
   * Pass e.g. { query: 'smith' } or { filterProperty: 'Email', filter: 'a' }.
   */
  getUsers(params?: QueryParams): Observable<User[]> {
    return this.api.getList<User>(this.resource, params);
  }

  /** GET /api/users/{id}. MIGRATION: Page_Load → UserController.GetUser. */
  getUser(id: number): Observable<User> {
    return this.api.getById<User>(this.resource, id);
  }

  /** POST /api/users (201). MIGRATION: User.ascx.vb CreateUser → UserController.CreateUser. */
  createUser(body: CreateUserRequest): Observable<User> {
    return this.api.create<User>(this.resource, body);
  }

  /** PUT /api/users/{id} (200). MIGRATION: cmdUpdate_Click → UserController.UpdateUser. */
  updateUser(id: number, body: UpdateUserRequest): Observable<User> {
    return this.api.update<User>(this.resource, id, body);
  }

  /** DELETE /api/users/{id} (204). MIGRATION: cmdDelete_Click → UserController.DeleteUser. */
  deleteUser(id: number): Observable<void> {
    return this.api.delete(this.resource, id);
  }

  /**
   * Change a user's password.
   * MIGRATION: Password.ascx.vb cmdUpdate_Click → UserController.ChangePassword(User, old, new).
   * Route: POST /api/users/{id}/change-password — a dedicated sub-resource issued via
   * the low-level envelope-aware `post` (ApiService prepends the `/api` base URL). The
   * ChangePasswordRequest body shape ({ oldPassword, newPassword }) matches the backend
   * ChangePasswordDto. Auth (old-password verification / claims) is enforced server-side;
   * the JWT is attached by the app-wide authInterceptor.
   */
  changePassword(id: number, body: ChangePasswordRequest): Observable<void> {
    return this.api.post<void>(`${this.resource}/${id}/change-password`, body);
  }
}
