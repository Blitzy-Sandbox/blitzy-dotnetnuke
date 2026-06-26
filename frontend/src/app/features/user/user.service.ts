// MIGRATION: User feature service — replaces the data-access / orchestration portions of the legacy DNN
// Admin→Users Web Forms controls: Website/admin/Users/Users.ascx.vb (listing/paging/filter modes —
// UserController.GetUsers/GetUsersByEmail/GetUsersByUserName/GetUsersByProfileProperty, L259-274),
// ManageUsers.ascx.vb (orchestration + PortalId scoping, L213), and User.ascx.vb (create/edit/delete,
// L147-191). The reflection-instantiated ADO.NET DataProvider singleton (DataProvider.Instance()) is
// replaced by the injected ApiService over REST /api/v1/users. This service performs API communication +
// client-side signal state ONLY (AAP §0.7.3): NO business rules beyond shaping requests, NO presentation logic.
// It mirrors the canonical signal-service pattern established for features/portal/portal.service.ts.
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { finalize, tap } from 'rxjs/operators';

// MIGRATION: ApiService is the SINGLE HTTP gateway (the frontend analog of the legacy DataProvider singleton).
// This service NEVER injects HttpClient directly. `ApiQueryParams` is imported type-only via the inline modifier.
import { ApiService, type ApiQueryParams } from '../../core/services/api.service';
import type { User, Paged } from '../../core/models';

// MIGRATION: credential separation (AAP §0.7.6) — the core `User` model carries ZERO credential fields.
// Password / question / answer / authorize / notify / captcha live ONLY on these write-only request DTOs and are
// NEVER read back from `User`. The legacy `UserMembership` object hung off UserInfo (set in User.ascx.vb Validate
// L147-191); it is replaced here by dedicated request DTOs plus the backend Identity layer (PasswordHasher / JwtService).
// Align names with the backend CreateUserDto/UpdateUserDto; flagged for reconciliation if the backend differs.

/** Payload for creating a user. Maps to the backend `CreateUserDto`; credentials are write-only. */
export interface CreateUserRequest {
  username: string;
  email: string;
  displayName?: string; // MIGRATION: auto-generated from Security_DisplayNameFormat when configured (User.ascx.vb UpdateDisplayName L117-123)
  firstName?: string;
  lastName?: string;
  // credentials (write-only; legacy User.ascx.vb Validate L147-191):
  password?: string; // omitted when randomPassword = true
  confirmPassword?: string; // client-side match only; not necessarily sent
  randomPassword?: boolean; // chkRandom → UserController.GeneratePassword() server-side
  passwordQuestion?: string; // only when the provider RequiresQuestionAndAnswer
  passwordAnswer?: string;
  authorize?: boolean; // chkAuthorize → Membership.Approved (admin add); registration auto-approves per portal type
  notify?: boolean; // chkNotify → send notification email
  verificationCode?: string; // CAPTCHA, only on public self-registration (UseCaptcha)
}

/** Payload for editing an existing user. Maps to the backend `UpdateUserDto`. */
export interface UpdateUserRequest {
  email?: string;
  displayName?: string;
  firstName?: string;
  lastName?: string;
  authorize?: boolean; // edit may toggle the approved state
  // MIGRATION: password changes are CHANGE-ONLY and route through the features/auth password feature
  // (legacy Password.ascx.vb), NOT through UpdateUserRequest; the backend UpdateUserDto does not accept a new
  // password on this endpoint. No credential fields are included here by design.
}

/**
 * A single profile property value for the profile editor.
 * MIGRATION: projects ProfilePropertyDefinition + its user value (legacy Profile.ascx.vb / ProfileDefinitions.ascx.vb).
 */
export interface ProfilePropertyValue {
  propertyDefinitionId: number;
  propertyName: string;
  propertyCategory?: string;
  propertyValue: string | null;
  required: boolean;
  visible: boolean;
  viewOrder: number;
  validationExpression?: string | null; // per-property regex (ProfilePropertyDefinition.ValidationExpression)
  dataType?: number;
  length?: number;
  visibility?: number; // UserVisibilityMode (AdminOnly default)
}

@Injectable({ providedIn: 'root' })
export class UserService {
  // MIGRATION: DataProvider.Instance() reflection singleton → constructor-injected ApiService (AAP §0.3.3).
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
   * MIGRATION: Users.ascx.vb BindGrid (L259-274) — GetUsers / GetUsersByEmail / GetUsersByUserName /
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
      // GetUsersByProfileProperty (Users.ascx.vb L274). The backend `UsersController` must accept this as
      // `searchField`; reconcile the query-param name if the backend route differs.
      params['searchField'] = searchField;
    }

    // MIGRATION: PortalId scoping (AAP §0.7.1, CRITICAL). Legacy `UsersPortalId` (Users.ascx.vb L144-152) derived
    // the tenant PortalId from PortalSettings, using Null.NullInteger only for the host/superuser context. In the
    // BFF this tenant scoping is enforced SERVER-SIDE from the authenticated JWT principal and is NOT passed as a
    // query param. The optional `portalId` below is appended ONLY when the backend explicitly requires an override
    // (e.g. host managing another portal's users); flagged as a backend coordination point.
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

  /** MIGRATION: UserController.GetUser(userId, portalId) → GET /users/{id}; caches the result in `selected`. */
  getById(id: number): Observable<User> {
    return this.api.get<User>(`users/${id}`).pipe(tap((user) => this._selected.set(user)));
  }

  /** MIGRATION: User.ascx.vb CreateUser (ctlUser.CreateUser, L605) → POST /users (201 Created + body, Gate 5). */
  create(dto: CreateUserRequest): Observable<User> {
    return this.api.post<User>('users', dto);
  }

  /**
   * MIGRATION: User.ascx.vb UpdateUser → PUT /users/{id} (200 OK + body, Gate 5).
   * Refreshes the `selected` signal with the server's canonical copy (client-side state, in scope per AAP §0.7.3).
   */
  update(id: number, dto: UpdateUserRequest): Observable<User> {
    return this.api.put<User>(`users/${id}`, dto).pipe(tap((user) => this._selected.set(user)));
  }

  /**
   * MIGRATION: ManageUsers.ascx.vb DeleteUser → DELETE /users/{id} (204 No Content, Gate 5).
   * Keeps client state coherent by dropping the row from `users` and clearing `selected` when it matches.
   */
  delete(id: number): Observable<void> {
    return this.api.delete(`users/${id}`).pipe(
      tap(() => {
        this._users.update((current) => current.filter((user) => user.userId !== id));
        if (this._selected()?.userId === id) {
          this._selected.set(null);
        }
      }),
    );
  }

  // --- Profile endpoints (for the profile/ ProfileComponent) -------------------------------------------------

  /**
   * MIGRATION: replaces ProfileController.GetPropertyDefinitionsByPortal + the per-user profile read
   * (legacy Profile.ascx.vb L162-226). The exact sub-path `users/{id}/profile` must be reconciled with the
   * backend UsersController/AuthController route; flagged as a backend coordination point.
   */
  getProfile(id: number): Observable<ProfilePropertyValue[]> {
    return this.api.get<ProfilePropertyValue[]>(`users/${id}/profile`);
  }

  /**
   * MIGRATION: replaces ProfileController.UpdateUserProfile (legacy Profile.ascx.vb L162-226). The exact
   * sub-path `users/{id}/profile` must be reconciled with the backend route (see getProfile).
   */
  updateProfile(id: number, properties: ProfilePropertyValue[]): Observable<ProfilePropertyValue[]> {
    return this.api.put<ProfilePropertyValue[]>(`users/${id}/profile`, properties);
  }
}
