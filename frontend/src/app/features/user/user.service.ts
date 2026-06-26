// MIGRATION: User feature service -- replaces the data-access / orchestration portions of the legacy DNN
// Admin->Users Web Forms controls: Website/admin/Users/Users.ascx.vb (listing/paging/filter modes --
// UserController.GetUsers/GetUsersByEmail/GetUsersByUserName/GetUsersByProfileProperty, L259-274),
// ManageUsers.ascx.vb (orchestration + PortalId scoping, L213), and User.ascx.vb (create/edit/delete,
// L147-191). The reflection-instantiated ADO.NET DataProvider singleton (DataProvider.Instance()) is
// replaced by the injected ApiService over REST /api/v1/users. This service performs API communication +
// client-side signal state ONLY (AAP Section 0.7.3): NO business rules beyond shaping requests, NO presentation logic.
// It mirrors the canonical signal-service pattern established for features/portal/portal.service.ts.
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { finalize, tap } from 'rxjs/operators';

// MIGRATION: ApiService is the SINGLE HTTP gateway (the frontend analog of the legacy DataProvider singleton).
// This service NEVER injects HttpClient directly. `ApiQueryParams` is imported type-only via the inline modifier.
// The tenant `portalId` query (REQUIRED by the backend UsersController for getById/update/delete) is threaded
// through ApiService.get/put/delete via the `params` argument added in the Module phase (api.service.ts).
import { ApiService, type ApiQueryParams } from '../../core/services/api.service';
import type { User, Paged } from '../../core/models';

// MIGRATION: credential separation (AAP Section 0.7.6) -- the core `User` model carries ZERO credential fields.
// Password / confirm live ONLY on the write-only CreateUserRequest below and are NEVER read back from `User`.
// The DTO shapes are aligned EXACTLY to the frozen backend contract (CP1/CP2, AAP 0.3.4) verified against
// DnnMigration.Application.DTOs.User.CreateUserRequest / UpdateUserRequest and their FluentValidation validators:
//   CreateUserRequest (System.Text.Json camelCase): portalId, username, email, displayName, firstName,
//     lastName, password, confirm. CreateUserValidator REQUIRES portalId(>=0)/username/email/displayName/
//     firstName/lastName; password is OPTIONAL (when supplied: min 7, max 20, and confirm MUST equal password;
//     when omitted, the server generates one -- the legacy chkRandom path).
//   UpdateUserRequest (camelCase): email, displayName, firstName, lastName, isApproved, lockedOut. NO password,
//     NO username (immutable login key, route-bound id only).
// MIGRATION: the legacy DNN membership extras (chkRandom flag, security question/answer, chkNotify email,
// CAPTCHA verification code, and the create-time "Authorize" flag) have NO field in the frozen backend contract.
// They remain CLIENT-ONLY form affordances (see UserFormComponent) and are NOT part of these outbound DTOs;
// the random-password UX simply OMITS `password`/`confirm` so the server generates the password. Recorded in
// MIGRATION_NOTES.md as a documented contract deferral.

/**
 * Payload for creating a user. Maps EXACTLY to the backend `CreateUserRequest` (POST /api/v1/users).
 * `portalId` is the multi-tenant discriminator and travels in the BODY (not the query) for create.
 * `password`/`confirm` are write-only; omit both to request a server-generated password.
 */
export interface CreateUserRequest {
  portalId: number;
  username: string;
  email: string;
  displayName?: string;
  firstName?: string;
  lastName?: string;
  // MIGRATION: write-only credentials (legacy User.ascx.vb Validate L147-191). Optional: when both are omitted
  // the server generates the password (legacy chkRandom path). When `password` is supplied, the backend
  // CreateUserValidator enforces min length 7 / max 20 and `confirm` == `password`.
  password?: string;
  confirm?: string;
}

/**
 * Payload for editing an existing user. Maps EXACTLY to the backend `UpdateUserRequest` (PUT /api/v1/users/{id}).
 * `username` (the immutable login key) and credentials are intentionally absent; password changes route through
 * the features/auth password flow (legacy Password.ascx.vb), never this endpoint.
 */
export interface UpdateUserRequest {
  email?: string;
  displayName?: string;
  firstName?: string;
  lastName?: string;
  // MIGRATION: chkAuthorize -> UserMembership.Approved (admin add/edit). Backend field name is `isApproved`
  // (NOT the legacy frontend `authorize`); the UserFormComponent maps its `authorize` control to this field.
  isApproved: boolean;
  // MIGRATION: legacy UserMembership.LockedOut. Admin-editable (supports the legacy "unlock user" action).
  lockedOut: boolean;
}

@Injectable({ providedIn: 'root' })
export class UserService {
  // MIGRATION: DataProvider.Instance() reflection singleton -> constructor-injected ApiService (AAP Section 0.3.3).
  private readonly api = inject(ApiService);

  // --- Signal state (Angular 19) -----------------------------------------------------------------------------
  // Writable signals are PRIVATE; consumers receive read-only views via asReadonly(). Attempting to call
  // `userService.users.set(...)` from a component will not compile.
  private readonly _users = signal<User[]>([]);
  /** Current page of users from the most recent `list()` call. */
  readonly users = this._users.asReadonly();

  private readonly _loading = signal<boolean>(false);
  /** True while a `list()` request is in flight. */
  readonly loading = this._loading.asReadonly();

  private readonly _totalCount = signal<number>(0);
  /** Total user count across all pages (sourced from the list envelope `meta`). */
  readonly totalCount = this._totalCount.asReadonly();

  private readonly _selected = signal<User | null>(null);
  /** The user most recently fetched via `getById()` or mutated via `update()`. */
  readonly selected = this._selected.asReadonly();

  // --- CRUD against the `users` resource (ApiService prepends environment.apiUrl = /api/v1) ------------------

  /**
   * MIGRATION: Users.ascx.vb BindGrid (L259-274) -- GetUsers / GetUsersByEmail / GetUsersByUserName /
   * GetUsersByProfileProperty. `pageIndex` is ZERO-BASED and passed straight through; the legacy
   * `CurrentPage - 1` conversion (Users.ascx.vb L265) is already performed by the (zero-based) list component.
   */
  list(
    pageIndex: number,
    pageSize: number,
    filter?: string,
    searchField?: string,
    portalId?: number,
  ): Observable<Paged<User>> {
    const params: ApiQueryParams = { pageIndex, pageSize };

    // MIGRATION: Users.ascx.vb UserFilter (L168-188) only appended `filter`/`filterproperty` querystrings when
    // they were non-empty; mirror that exactly by omitting empty keys. Bracket-notation writes are required
    // because ApiQueryParams is an index-signature type (tsconfig noPropertyAccessFromIndexSignature).
    if (filter) {
      params['filter'] = filter;
    }
    if (searchField) {
      // MIGRATION: maps to the legacy `filterproperty` querystring / `SearchField` argument of
      // GetUsersByProfileProperty (Users.ascx.vb L274).
      params['searchField'] = searchField;
    }

    // MIGRATION: PortalId scoping (AAP Section 0.7.1, CRITICAL). The backend UsersController marks `portalId`
    // as a REQUIRED ([FromQuery, BindRequired]) query parameter for the list endpoint; the UserListComponent
    // sources it from the authenticated principal (auth.currentUser().portalId) and threads it on EVERY list
    // call. It is appended whenever supplied (including 0, a valid portal id); only `undefined`/`null` skip it.
    if (portalId !== undefined && portalId !== null) {
      params['portalId'] = portalId;
    }

    this._loading.set(true);
    return this.api.getPaged<User>('users', params).pipe(
      tap((paged) => {
        this._users.set(paged.items);
        this._totalCount.set(paged.totalCount);
      }),
      finalize(() => this._loading.set(false)),
    );
  }

  /**
   * MIGRATION: UserController.GetUser(userId, portalId) -> GET /users/{id}?portalId=; caches the result in
   * `selected`. The backend requires the tenant `portalId` query for this protected, tenant-scoped read
   * (AAP Section 0.7.1); the caller threads it from the authenticated principal / loaded user.
   */
  getById(id: number, portalId: number): Observable<User> {
    return this.api
      .get<User>(`users/${id}`, { portalId })
      .pipe(tap((user) => this._selected.set(user)));
  }

  /**
   * MIGRATION: User.ascx.vb CreateUser (ctlUser.CreateUser, L605) -> POST /users (201 Created + body, Gate 5).
   * `portalId` travels in the request BODY for create (CreateUserRequest.PortalId), NOT as a query parameter.
   */
  create(dto: CreateUserRequest): Observable<User> {
    return this.api.post<User>('users', dto);
  }

  /**
   * MIGRATION: User.ascx.vb UpdateUser -> PUT /users/{id}?portalId= (200 OK + body, Gate 5).
   * The backend requires the tenant `portalId` query for this protected, tenant-scoped write (AAP Section 0.7.1).
   * Refreshes the `selected` signal with the server's canonical copy (client-side state, in scope per AAP 0.7.3).
   */
  update(id: number, portalId: number, dto: UpdateUserRequest): Observable<User> {
    return this.api
      .put<User>(`users/${id}`, dto, { portalId })
      .pipe(tap((user) => this._selected.set(user)));
  }

  /**
   * MIGRATION: ManageUsers.ascx.vb DeleteUser -> DELETE /users/{id}?portalId= (204 No Content, Gate 5).
   * The backend requires the tenant `portalId` query for this protected, tenant-scoped delete (AAP Section 0.7.1).
   * Keeps client state coherent by dropping the row from `users` and clearing `selected` when it matches.
   */
  delete(id: number, portalId: number): Observable<void> {
    return this.api.delete(`users/${id}`, { portalId }).pipe(
      tap(() => {
        this._users.update((current) => current.filter((user) => user.userId !== id));
        if (this._selected()?.userId === id) {
          this._selected.set(null);
        }
      }),
    );
  }

  // MIGRATION (DEFERRAL, AAP D1 / 0.3.4): the legacy per-user PROFILE workflow (ProfileController
  // GetPropertyDefinitionsByPortal + UpdateUserProfile, Profile.ascx.vb / ProfileDefinitions.ascx.vb) has NO
  // endpoint in the frozen backend contract -- the AAP explicitly defers the profile feature for this phase
  // ("the legacy profile workflow (UserProfileDto) is out of scope for this phase per the AAP"). The previous
  // getProfile()/updateProfile() methods and the ProfilePropertyValue DTO targeted `users/{id}/profile`, which
  // returns 404, so they are REMOVED here (aligning the frontend to the backend rather than adding a backend
  // endpoint, which would contradict the frozen contract). The ProfileComponent is now a deferred-notice screen.
  // Recorded in MIGRATION_NOTES.md.
}
