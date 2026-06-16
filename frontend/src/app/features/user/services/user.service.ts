import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService, PagedResponse, QueryParams } from '../../../core/services/api.service';
import { User } from '../../../core/models/user.model';
import { CreateUserDto, UpdateUserDto, UserListItem, UserSearchQuery } from '../models';

/**
 * UserService — typed HTTP facade for the User administration feature.
 *
 * A THIN pass-through over the core `ApiService`: it never touches `HttpClient`
 * directly and carries NO business logic. Its sole responsibility is to resolve
 * the feature's `/api/v1/users` URLs and delegate to `ApiService`, which centralizes
 * URL composition, unwraps the `{ data, meta }` success envelope, and normalizes
 * errors into RFC 7807 ProblemDetails. Errors are intentionally left to propagate so
 * form components can map `problem.errors` -> server-side field errors.
 *
 * Behavior-parity source (read as reference, NOT a 1:1 conversion):
 *   Library/Components/Users/UserController.vb (data methods, re-expressed as REST).
 * Authoritative REST contract: backend UsersController (`/api/v1/users`).
 */
@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly api = inject(ApiService);

  /** REST resource segment for the User endpoints (`/api/v1/users`). */
  private readonly resource = 'users';

  // MIGRATION: UserController.GetUsers/GetUsersBy{Email,UserName,ProfileProperty} (portalId + ByRef totalRecords + ArrayList)
  //            collapse into one GET /api/v1/users; totalRecords -> PagedResponse.meta.totalCount.
  getUsers(query: UserSearchQuery = {}): Observable<PagedResponse<UserListItem>> {
    return this.api.getList<UserListItem>(this.api.resourceUrl(this.resource), this.toQueryParams(query));
  }

  // MIGRATION: UserController.GetOnlineUsers(portalId) -> GET /api/v1/users?online=true.
  getOnlineUsers(query: UserSearchQuery = {}): Observable<PagedResponse<UserListItem>> {
    return this.api.getList<UserListItem>(this.api.resourceUrl(this.resource), this.toQueryParams(query, { online: true }));
  }

  // MIGRATION: UserController.GetUnAuthorizedUsers(portalId) -> GET /api/v1/users?unauthorized=true.
  getUnauthorizedUsers(query: UserSearchQuery = {}): Observable<PagedResponse<UserListItem>> {
    return this.api.getList<UserListItem>(this.api.resourceUrl(this.resource), this.toQueryParams(query, { unauthorized: true }));
  }

  // MIGRATION: UserController.GetUser(portalId, userId, ...) -> GET /api/v1/users/{id}; ApiService unwraps { data }.
  getUser(id: number): Observable<User> {
    return this.api.get<User>(this.api.resourceUrl(this.resource, id));
  }

  // MIGRATION: UserController.CreateUser(ByRef objUser) As UserCreateStatus -> POST /api/v1/users (201).
  //            Server-side auto-role assignment + cache invalidation are not reproduced client-side.
  createUser(request: CreateUserDto): Observable<User> {
    return this.api.post<User>(this.api.resourceUrl(this.resource), request);
  }

  // MIGRATION: UserController.UpdateUser(portalId, objUser) (Sub/void + cache clear) -> PUT /api/v1/users/{id} (200).
  updateUser(id: number, request: UpdateUserDto): Observable<User> {
    return this.api.put<User>(this.api.resourceUrl(this.resource, id), request);
  }

  // MIGRATION: UserController.DeleteUser(objUser, notify, deleteAdmin) -> DELETE /api/v1/users/{id} (204).
  //            Server enforces soft-delete + admin protections.
  deleteUser(id: number): Observable<void> {
    return this.api.delete(this.api.resourceUrl(this.resource, id));
  }

  // MIGRATION: Membership.ascx cmdAuthorize (Membership.Approved = true via UpdateUser) -> PUT /api/v1/users/{id}/approve.
  approveUser(id: number): Observable<User> {
    return this.api.put<User>(`${this.api.resourceUrl(this.resource, id)}/approve`);
  }

  // MIGRATION: Membership.ascx cmdUnAuthorize (Membership.Approved = false) -> PUT /api/v1/users/{id}/unauthorize.
  unauthorizeUser(id: number): Observable<User> {
    return this.api.put<User>(`${this.api.resourceUrl(this.resource, id)}/unauthorize`);
  }

  // MIGRATION: UserController.UnLockUser(user) As Boolean (+ cache clear) -> PUT /api/v1/users/{id}/unlock.
  unlockUser(id: number): Observable<User> {
    return this.api.put<User>(`${this.api.resourceUrl(this.resource, id)}/unlock`);
  }

  // MIGRATION: Membership.ascx cmdPassword (UpdateForcePasswordChange) -> PUT /api/v1/users/{id}/force-password-change.
  forcePasswordChange(id: number): Observable<User> {
    return this.api.put<User>(`${this.api.resourceUrl(this.resource, id)}/force-password-change`);
  }

  // MIGRATION: separate legacy UserController query methods collapse into one query-param surface.
  //            A named interface (UserSearchQuery) is NOT assignable to QueryParams (missing index signature),
  //            so build an explicit object literal here (verified: passing the interface directly fails TS2345).
  private toQueryParams(query: UserSearchQuery, extra: QueryParams = {}): QueryParams {
    return {
      filter: query.filter,
      filterProperty: query.filterProperty,
      searchText: query.searchText,
      searchType: query.searchType,
      pageIndex: query.pageIndex,
      pageSize: query.pageSize,
      ...extra,
    };
  }
}
