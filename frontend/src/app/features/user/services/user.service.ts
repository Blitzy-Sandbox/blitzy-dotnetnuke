import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService, PagedResponse, QueryParams } from '../../../core/services/api.service';
import { User } from '../../../core/models/user.model';
import { CreateUserDto, UpdateUserDto, UserListItem, UserSearchQuery } from '../models';

/**
 * UserService - typed HTTP facade for the User administration feature.
 *
 * A THIN pass-through over the core {@link ApiService}: it never touches
 * `HttpClient` directly, contains no business logic, and adds no error handling
 * (the `ApiService` already normalizes failures into RFC 7807 ProblemDetails and
 * rethrows, so callers can map `problem.errors` to form-level server errors).
 * Its sole responsibility is to resolve feature-shaped `/api/v1/users` URLs and
 * delegate to the appropriate generic verb.
 *
 * Behavior-parity source (read-as-reference, NOT a 1:1 conversion):
 * `Library/Components/Users/UserController.vb` data methods, re-expressed as REST
 * for UI parity. Per the Minimal Change Clause, every method whose REST shape
 * diverges from the legacy signature carries a `// MIGRATION:` annotation.
 *
 * Contract fidelity: the five CRUD methods (GET list, GET by id, POST, PUT, DELETE) map 1:1 to the
 * processed `UsersController` / `IUserService` routes, plus the force-password-change membership
 * transition exposed as a dedicated POST sub-resource call (`POST /api/v1/users/{id}/force-password-change`).
 * MIGRATION: of the four legacy membership-state transitions on `Membership.ascx.vb`, only
 * force-password-change is implemented here — it sets the REAL mapped `[Users].UpdatePassword` column, so it
 * has a durable Phase-1 home and a matching backend route. The other three (approve / unauthorize / unlock)
 * mutate the `Approved` and `LockedOut` fields, which live in `aspnet_Membership` (NOT the `[Users]` table)
 * and are EF-`Ignore()`d per ADR-002 / §0.6.2; they have no Phase-1 persistence target and no backend route,
 * so they are DEFERRED (intentionally NOT exposed) until that subsystem is modeled. The legacy online-users
 * and unauthorized-users LISTING operations likewise remain out of scope (no list consumer at this
 * milestone). See root MIGRATION_NOTES.md.
 */
@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly api = inject(ApiService);

  /** REST resource segment for the User endpoints (`/api/v1/users`). */
  private readonly resource = 'users';

  // MIGRATION: UserController.GetUsers / GetUsersBy{Email,UserName} (portalId + ByRef totalRecords + ArrayList)
  //            collapse into one paged GET /api/v1/users. portalId is REQUIRED by the backend contract
  //            (UsersController.Get returns HTTP 400 ProblemDetails when it is absent); it is carried on
  //            UserSearchQuery and forwarded by toQueryParams. totalRecords surfaces as
  //            PagedResponse.meta.totalCount.
  getUsers(query: UserSearchQuery): Observable<PagedResponse<UserListItem>> {
    return this.api.getList<UserListItem>(this.api.resourceUrl(this.resource), this.toQueryParams(query));
  }

  // MIGRATION: UserController.GetUser(portalId, userId, ...) As UserInfo -> GET /api/v1/users/{id};
  //            ApiService unwraps the { data } envelope to a single User.
  getUser(id: number): Observable<User> {
    return this.api.get<User>(this.api.resourceUrl(this.resource, id));
  }

  // MIGRATION: UserController.CreateUser(ByRef objUser) As UserCreateStatus -> POST /api/v1/users (HTTP 201).
  //            Server-side auto-role assignment + cache invalidation are NOT reproduced client-side.
  createUser(request: CreateUserDto): Observable<User> {
    return this.api.post<User>(this.api.resourceUrl(this.resource), request);
  }

  // MIGRATION: UserController.UpdateUser(portalId, objUser) (Sub/void + cache clear) -> PUT /api/v1/users/{id} (HTTP 200)
  //            returning the updated User.
  updateUser(id: number, request: UpdateUserDto): Observable<User> {
    return this.api.put<User>(this.api.resourceUrl(this.resource, id), request);
  }

  // MIGRATION: UserController.DeleteUser(objUser, notify, deleteAdmin) As Boolean (admin-protection + cascade)
  //            -> DELETE /api/v1/users/{id} (HTTP 204). Server enforces the delete strategy and admin protections.
  deleteUser(id: number): Observable<void> {
    return this.api.delete(this.api.resourceUrl(this.resource, id));
  }

  // MIGRATION: Membership.ascx.vb cmdPassword_Click (L216-228) set UpdatePassword = True and called
  //            UserController.UpdateUser, forcing a password change on next login. Re-expressed as
  //            POST /api/v1/users/{id}/force-password-change -> updated User. Backed by
  //            UsersController.ForcePasswordChange / IUserService.ForcePasswordChangeAsync, which set the
  //            REAL mapped [Users].UpdatePassword column. ApiService unwraps the { data } envelope.
  //
  //            DEFERRED (intentionally NOT exposed): approve / unauthorize / unlock. Those legacy handlers
  //            (cmdAuthorize_Click / cmdUnAuthorize_Click / cmdUnLock_Click) mutate Approved / LockedOut,
  //            which live in aspnet_Membership (NOT the [Users] table) and are EF-Ignore()d per ADR-002 /
  //            §0.6.2 -> no Phase-1 persistence target and no matching backend route. See MIGRATION_NOTES.md.
  forcePasswordChange(id: number): Observable<User> {
    return this.api.post<User>(`${this.api.resourceUrl(this.resource, id)}/force-password-change`);
  }

  // MIGRATION: the legacy UserController paging surface collapses into the backend's GET /api/v1/users query
  //            params. Only the parameters the controller actually binds are sent: portalId (REQUIRED),
  //            pageIndex, and pageSize. A named interface (UserSearchQuery) is NOT assignable to QueryParams
  //            (it lacks the required string index signature), so an explicit object literal is built here.
  //            `undefined` values are allowed by QueryParams; ApiService strips empty params when composing
  //            HttpParams.
  private toQueryParams(query: UserSearchQuery): QueryParams {
    return {
      portalId: query.portalId,
      pageIndex: query.pageIndex,
      pageSize: query.pageSize,
    };
  }
}
