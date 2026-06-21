import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService, PagedResponse, QueryParams } from '../../../core/services/api.service';
import { User } from '../../../core/models/user.model';
import { CreateUserDto, MembershipDto, UpdateUserDto, UserListItem, UserSearchQuery } from '../models';

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
 * processed `UsersController` / `IUserService` routes, plus the four membership-state transitions exposed as
 * dedicated POST/GET sub-resource calls (force-password-change, authorize, unauthorize, unlock, and the
 * membership-state read).
 * MIGRATION (DEV-067): all four legacy `Membership.ascx.vb` membership-state transitions are now implemented
 * end-to-end. Force-password-change sets the mapped `[Users].UpdatePassword` column; approve / unauthorize /
 * unlock target the `[aspnet_Membership]` approval/lockout state (now mapped per InstallMembership.sql — see
 * `AspNetMembership` / `AspNetMembershipConfiguration`), bridged from `[Users].Username`. `getMembership`
 * reads the consolidated `MembershipDto` projection consumed by the user-profile screen. The legacy
 * online-users and unauthorized-users LISTING operations remain out of scope (no list consumer at this
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
  forcePasswordChange(id: number): Observable<User> {
    return this.api.post<User>(`${this.api.resourceUrl(this.resource, id)}/force-password-change`);
  }

  // MIGRATION (DEV-067): the consolidated membership-state read backing the user-profile screen. Backed by
  //            UsersController.GetMembership / IUserService.GetMembershipAsync, which project the
  //            [aspnet_Membership] approval/lockout state plus the mapped [Users].UpdatePassword bit into a
  //            MembershipDto. GET /api/v1/users/{id}/membership -> MembershipDto (ApiService unwraps { data }).
  getMembership(id: number): Observable<MembershipDto> {
    return this.api.get<MembershipDto>(`${this.api.resourceUrl(this.resource, id)}/membership`);
  }

  // MIGRATION (DEV-067): Membership.ascx.vb cmdAuthorize_Click -> sets [aspnet_Membership].IsApproved = true
  //            (and syncs [UserPortals].Authorised). POST /api/v1/users/{id}/authorize -> refreshed MembershipDto.
  authorizeUser(id: number): Observable<MembershipDto> {
    return this.api.post<MembershipDto>(`${this.api.resourceUrl(this.resource, id)}/authorize`);
  }

  // MIGRATION (DEV-067): Membership.ascx.vb cmdUnAuthorize_Click -> sets [aspnet_Membership].IsApproved = false
  //            (and syncs [UserPortals].Authorised). POST /api/v1/users/{id}/unauthorize -> refreshed MembershipDto.
  unauthorizeUser(id: number): Observable<MembershipDto> {
    return this.api.post<MembershipDto>(`${this.api.resourceUrl(this.resource, id)}/unauthorize`);
  }

  // MIGRATION (DEV-067): Membership.ascx.vb cmdUnLock_Click -> clears [aspnet_Membership].IsLockedOut, resets
  //            FailedPasswordAttemptCount, and resets LastLockoutDate to the "never locked out" sentinel
  //            (aspnet_Membership_UnlockUser). POST /api/v1/users/{id}/unlock -> refreshed MembershipDto.
  unlockUser(id: number): Observable<MembershipDto> {
    return this.api.post<MembershipDto>(`${this.api.resourceUrl(this.resource, id)}/unlock`);
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
