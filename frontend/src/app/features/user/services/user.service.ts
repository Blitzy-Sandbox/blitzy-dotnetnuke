import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService, PagedResponse, QueryParams } from '../../../core/services/api.service';
import { User } from '../../../core/models/user.model';
import { CreateUserRequest, UpdateUserRequest, UserListItem, UserSearchQuery } from '../models';

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
  //            `request` is the WIRE shape (CreateUserRequest) that mirrors the backend CreateUserDto
  //            1:1; the user-form component adapts its form model (CreateUserDto) into this before calling.
  createUser(request: CreateUserRequest): Observable<User> {
    return this.api.post<User>(this.api.resourceUrl(this.resource), request);
  }

  // MIGRATION: UserController.UpdateUser(portalId, objUser) (Sub/void + cache clear) -> PUT /api/v1/users/{id} (200).
  //            `request` is the WIRE shape (UpdateUserRequest) mirroring the backend UpdateUserDto. The route
  //            id is stamped onto the body's `userID` so it always matches: UsersController.Update returns
  //            HTTP 400 ("Identifier mismatch") when `id != request.UserID`.
  updateUser(id: number, request: UpdateUserRequest): Observable<User> {
    return this.api.put<User>(this.api.resourceUrl(this.resource, id), { ...request, userID: id });
  }

  // MIGRATION: UserController.DeleteUser(objUser, notify, deleteAdmin) -> DELETE /api/v1/users/{id} (204).
  //            Server performs a HARD delete (the DNN 4.9 Users table has no IsDeleted column) and enforces
  //            the portal-administrator protection. See root MIGRATION_NOTES.md §6.3 / D-014.
  deleteUser(id: number): Observable<void> {
    return this.api.delete(this.api.resourceUrl(this.resource, id));
  }

  // MIGRATION: Force Password Change â€” the legacy Membership.ascx `cmdPassword` transition
  //            (Website/admin/Users/Membership.ascx.vb L213; UserController.UpdateForcePasswordChange).
  //            This persists to the REAL `dbo.Users.UpdatePassword` column (one of the 9 physical Users
  //            columns), so it is reproduced end-to-end: POST /api/v1/users/{id}/force-password-change with
  //            `{ require }` returns the updated UserDto (ApiService unwraps { data }). This OVERTURNS the
  //            prior "later checkpoint" deferral now that UsersController exposes the route (D-034).
  forcePasswordChange(id: number, require: boolean): Observable<User> {
    return this.api.post<User>(
      `${this.api.resourceUrl(this.resource, id)}/force-password-change`,
      { require },
    );
  }

  // MIGRATION (SCOPE NOTE): the OTHER legacy Membership.ascx transitions divide as follows:
  //   - Authorize / Unauthorize (cmdAuthorize / cmdUnAuthorize -> Membership.Approved) ARE reproduced
  //     via the `approved` field on PUT /api/v1/users/{id} (see updateUser + user-profile.component).
  //   - Unlock (cmdUnLock -> UserController.UnLockUser) is an AAP-grounded scope reduction: the lockout
  //     state lives on the GUID-keyed `aspnet_Membership` provider table, NOT on the in-scope `dbo.Users`
  //     entity, and ADR-002 / AAP Â§0.2.2 forbid schema changes and exclude the legacy membership-provider
  //     tables. Persisting Unlock would require either a schema change (forbidden) or mapping an
  //     out-of-scope table, so it is intentionally not implemented. See root MIGRATION_NOTES.md (D-034).

  // MIGRATION: separate legacy UserController query methods collapse into one query-param surface.
  //            A named interface (UserSearchQuery) is NOT assignable to QueryParams (missing index signature),
  //            so build an explicit object literal here (verified: passing the interface directly fails TS2345).
  private toQueryParams(query: UserSearchQuery, extra: QueryParams = {}): QueryParams {
    return {
      // MIGRATION: portalId is REQUIRED by UsersController.Get (400 without it). ApiService.toHttpParams
      //            drops null/undefined, so an unset portalId is simply omitted (the caller is responsible
      //            for supplying it — UserListComponent derives it from the authenticated user).
      portalId: query.portalId,
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
