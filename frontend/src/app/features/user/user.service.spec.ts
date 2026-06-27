// MIGRATION: Karma/Jasmine unit spec for the NET-NEW UserService (./user.service). These tests cover the CRUD +
// paging/filter behavior migrated from the legacy DNN Admin->Users Web Forms controls
// Website/admin/Users/Users.ascx.vb (Partial Class UserAccounts: GetUsers / GetUsersByEmail / GetUsersByUserName /
// GetUsersByProfileProperty listing, zero-based paging, and the non-empty-only UserFilter querystring) and
// Website/admin/Users/User.ascx.vb (Partial Class User: create / edit / delete plus the write-only credential
// fields such as random password / authorize / notify / CAPTCHA verification code). The reflection-instantiated
// ADO.NET DataProvider singleton is replaced by the injected ApiService over REST /api/v1/users; here the REAL
// ApiService (providedIn: 'root') is exercised end-to-end through HttpTestingController so URL building, the
// { data, meta } success-envelope contract, 204 handling, and the service's signal state are all verified.
// Gate 4: `ng test --watch=false --browsers=ChromeHeadless --code-coverage` must pass 100%.
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';

import {
  UserService,
  type CreateUserRequest,
  type UpdateUserRequest,
} from './user.service';
import type { Paged, User, UserProfile } from '../../core/models';

// `ng test` runs against the DEFAULT (production) environment whose environment.apiUrl === '/api/v1'
// (frontend/src/environments/environment.ts). ApiService roots every request at that value, so the `users`
// resource collection lives at `/api/v1/users`. The literal is asserted directly (matching the folder-spec URL
// expectation) which also keeps this spec's imports limited to the unit under test and the shared models.
const apiUrl = '/api/v1';
const usersUrl = `${apiUrl}/users`;

// MIGRATION: typed fixture matching the User model EXACTLY (17 properties, ZERO credentials -- credentials live
// only on the write-only request DTOs per AAP Section 0.7.6). portalId / userId are non-null per the multi-tenant
// contract (AAP Section 0.7.1).
const baseUser: User = {
  userId: 5,
  username: 'jdoe',
  displayName: 'John Doe',
  email: 'jdoe@example.com',
  firstName: 'John',
  lastName: 'Doe',
  fullName: 'John Doe',
  isSuperUser: false,
  affiliateId: null,
  portalId: 0,
  isApproved: true,
  createdDate: '2024-01-01T00:00:00Z',
  lastLoginDate: '2024-06-01T08:30:00Z',
  lastActivityDate: '2024-06-01T09:00:00Z',
  lastLockoutDate: null,
  lockedOut: false,
  roles: [],
};

const userFive: User = { ...baseUser, userId: 5, username: 'jdoe' };
const userSix: User = {
  ...baseUser,
  userId: 6,
  username: 'asmith',
  displayName: 'Anna Smith',
  email: 'asmith@example.com',
  firstName: 'Anna',
  lastName: 'Smith',
  fullName: 'Anna Smith',
};

/**
 * Builds the list response EXACTLY as ApiService.getPaged expects it: the current page of rows lives on `data`
 * and the paging counters live on `meta` (it is NOT a pre-assembled Paged<T> placed on `data`). ApiService.toPaged()
 * then reassembles a Paged<User> from this envelope, which is what UserService.list() consumes.
 */
function listEnvelope(
  items: User[],
  totalCount: number,
  pageIndex = 0,
  pageSize = 10,
): { data: User[]; meta: Record<string, unknown> } {
  const totalPages = pageSize > 0 ? Math.ceil(totalCount / pageSize) : 0;
  return {
    data: items,
    meta: {
      totalCount,
      pageIndex,
      pageSize,
      totalPages,
      hasPreviousPage: pageIndex > 0,
      hasNextPage: pageIndex + 1 < totalPages,
    },
  };
}

describe('UserService', () => {
  let service: UserService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    // provideHttpClient() + provideHttpClientTesting() wire HttpClient to the mock backend so the REAL ApiService
    // that UserService injects is driven through HttpTestingController (no service is stubbed).
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(UserService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    // Fails the test if any test left an unmatched / outstanding HTTP request behind.
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('exposes the expected initial signal state', () => {
    expect(service.users()).toEqual([]);
    expect(service.totalCount()).toBe(0);
    expect(service.selected()).toBeNull();
    expect(service.loading()).toBeFalse();
  });

  describe('list()', () => {
    it('GETs /users with pageIndex/pageSize, toggles loading, and populates users()/totalCount()', () => {
      const items: User[] = [userFive, userSix];
      let emitted: Paged<User> | undefined;

      expect(service.loading()).toBeFalse();

      service.list(0, 10).subscribe((paged) => (emitted = paged));

      // list() flips loading -> true synchronously and the request is now in flight.
      expect(service.loading()).toBeTrue();

      const req = httpMock.expectOne((r) => r.url === usersUrl);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('pageIndex')).toBe('0');
      expect(req.request.params.get('pageSize')).toBe('10');

      req.flush(listEnvelope(items, 2, 0, 10));

      // tap() copied the page into the signals; finalize() cleared loading on completion.
      expect(emitted?.items).toEqual(items);
      expect(emitted?.totalCount).toBe(2);
      expect(service.users()).toEqual(items);
      expect(service.totalCount()).toBe(2);
      expect(service.loading()).toBeFalse();
    });

    it('captures the error into error(), resets loading() to false, and emits a safe empty page when the request errors', () => {
      // MIGRATION: [QA F3 #4] The list stream no longer propagates the error to the subscriber. To surface the
      // backend RFC 7807 ProblemDetails (AAP Section 0.7.5) in the shared DataTableComponent banner instead of
      // silently falling back to the empty state, list() now captures the problem into the error() signal,
      // clears the row/count signals, and emits a safe empty page so the subscriber completes normally.
      let emittedPage: Paged<User> | undefined;

      service.list(0, 10).subscribe({
        next: (page) => (emittedPage = page),
        error: () =>
          fail('list() should capture the error into error(), not propagate it to the subscriber'),
      });

      expect(service.loading()).toBeTrue();

      const req = httpMock.expectOne((r) => r.url === usersUrl);
      req.flush(
        { title: 'Internal Server Error' },
        { status: 500, statusText: 'Server Error' },
      );

      expect(service.loading()).toBeFalse();
      // The RFC 7807 problem is captured for the data-table error banner.
      expect(service.error()).not.toBeNull();
      expect(service.error()?.title).toBe('Internal Server Error');
      expect(service.error()?.status).toBe(500);
      // Row/count signals are cleared and a safe empty page is emitted to the subscriber.
      expect(service.users()).toEqual([]);
      expect(service.totalCount()).toBe(0);
      expect(emittedPage?.items).toEqual([]);
      expect(emittedPage?.totalCount).toBe(0);
    });

    it('appends filter and searchField query params when both are provided', () => {
      service.list(1, 25, 'A', 'Username').subscribe();

      const req = httpMock.expectOne((r) => r.url === usersUrl);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('pageIndex')).toBe('1');
      expect(req.request.params.get('pageSize')).toBe('25');
      expect(req.request.params.get('filter')).toBe('A');
      expect(req.request.params.get('searchField')).toBe('Username');

      req.flush(listEnvelope([], 0, 1, 25));
    });

    it('omits filter and searchField when they are empty (legacy non-empty-only querystring)', () => {
      service.list(0, 10, '', '').subscribe();

      const req = httpMock.expectOne((r) => r.url === usersUrl);
      expect(req.request.params.has('filter')).toBeFalse();
      expect(req.request.params.has('searchField')).toBeFalse();

      req.flush(listEnvelope([], 0));
    });

    it('appends portalId only when an explicit override is supplied', () => {
      service.list(0, 10, undefined, undefined, 7).subscribe();

      const req = httpMock.expectOne((r) => r.url === usersUrl);
      expect(req.request.params.get('portalId')).toBe('7');

      req.flush(listEnvelope([], 0));
    });
  });

  describe('getById()', () => {
    it('GETs /users/5?portalId=1 and caches the result in selected()', () => {
      let emitted: User | undefined;

      service.getById(5, 1).subscribe((user) => (emitted = user));

      const req = httpMock.expectOne((r) => r.url === `${usersUrl}/5`);
      expect(req.request.method).toBe('GET');
      // MIGRATION: the protected, tenant-scoped read requires the portalId query (AAP Section 0.7.1).
      expect(req.request.params.get('portalId')).toBe('1');

      req.flush({ data: userFive });

      expect(emitted).toEqual(userFive);
      expect(service.selected()).toEqual(userFive);
    });
  });

  describe('create()', () => {
    it('POSTs the backend-shaped CreateUserRequest body and returns the created user (201)', () => {
      // MIGRATION: aligned to the frozen backend CreateUserRequest -- portalId travels in the BODY, the
      // confirmation field is `confirm` (NOT the legacy `confirmPassword`), and the unsupported DNN membership
      // extras (randomPassword/authorize/notify/verificationCode) are NOT part of the contract.
      const request: CreateUserRequest = {
        portalId: 0,
        username: 'newuser',
        email: 'newuser@example.com',
        displayName: 'New User',
        firstName: 'New',
        lastName: 'User',
        password: 'P@ssw0rd!',
        confirm: 'P@ssw0rd!',
      };
      const created: User = {
        ...baseUser,
        userId: 42,
        username: 'newuser',
        email: 'newuser@example.com',
        displayName: 'New User',
        firstName: 'New',
        lastName: 'User',
      };
      let emitted: User | undefined;

      service.create(request).subscribe((user) => (emitted = user));

      const req = httpMock.expectOne((r) => r.url === usersUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(request);

      // The write-only credential fields ride along on the POST body (they never appear on the User model),
      // and portalId is in the body (not the query) for create.
      const body = req.request.body as CreateUserRequest;
      expect(body.portalId).toBe(0);
      expect(body.username).toBe('newuser');
      expect(body.email).toBe('newuser@example.com');
      expect(body.password).toBe('P@ssw0rd!');
      expect(body.confirm).toBe('P@ssw0rd!');

      req.flush({ data: created }, { status: 201, statusText: 'Created' });

      expect(emitted).toEqual(created);
    });
  });

  describe('update()', () => {
    it('PUTs the backend-shaped UpdateUserRequest to /users/5?portalId=1 and refreshes selected() (200)', () => {
      // MIGRATION: aligned to the frozen backend UpdateUserRequest -- the approval flag is `isApproved` (NOT the
      // legacy `authorize`) and `lockedOut` is the admin unlock flag. portalId is the required tenant query.
      const request: UpdateUserRequest = {
        email: 'updated@example.com',
        displayName: 'Updated Name',
        firstName: 'Up',
        lastName: 'Dated',
        isApproved: false,
        lockedOut: false,
      };
      const updated: User = {
        ...baseUser,
        userId: 5,
        email: 'updated@example.com',
        displayName: 'Updated Name',
        firstName: 'Up',
        lastName: 'Dated',
      };
      let emitted: User | undefined;

      service.update(5, 1, request).subscribe((user) => (emitted = user));

      const req = httpMock.expectOne((r) => r.url === `${usersUrl}/5`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(request);
      expect(req.request.params.get('portalId')).toBe('1');

      req.flush({ data: updated });

      expect(emitted).toEqual(updated);
      expect(service.selected()).toEqual(updated);
    });
  });

  describe('delete()', () => {
    it('DELETEs /users/5?portalId=1 and completes on a 204 No Content', () => {
      let completed = false;
      let result: unknown = 'sentinel';

      service.delete(5, 1).subscribe({
        next: (value) => (result = value),
        complete: () => (completed = true),
      });

      const req = httpMock.expectOne(
        (r) => r.url === `${usersUrl}/5` && r.method === 'DELETE',
      );
      // MIGRATION: the protected, tenant-scoped delete requires the portalId query (AAP Section 0.7.1).
      expect(req.request.params.get('portalId')).toBe('1');
      req.flush(null, { status: 204, statusText: 'No Content' });

      expect(result).toBeNull();
      expect(completed).toBeTrue();
    });

    it('removes the deleted row from users() and clears selected() when it matches', () => {
      // Seed users() via list() ...
      service.list(0, 10).subscribe();
      httpMock
        .expectOne((r) => r.url === usersUrl)
        .flush(listEnvelope([userFive, userSix], 2, 0, 10));

      // ... and seed selected() via getById().
      service.getById(5, 1).subscribe();
      httpMock
        .expectOne((r) => r.url === `${usersUrl}/5`)
        .flush({ data: userFive });

      expect(service.users()).toEqual([userFive, userSix]);
      expect(service.selected()).toEqual(userFive);

      service.delete(5, 1).subscribe();
      httpMock
        .expectOne((r) => r.url === `${usersUrl}/5` && r.method === 'DELETE')
        .flush(null, { status: 204, statusText: 'No Content' });

      // The deleted row is dropped from the cached page and the matching selection is cleared.
      expect(service.users()).toEqual([userSix]);
      expect(service.selected()).toBeNull();
    });

    it('leaves selected() intact when a different user is deleted', () => {
      service.list(0, 10).subscribe();
      httpMock
        .expectOne((r) => r.url === usersUrl)
        .flush(listEnvelope([userFive, userSix], 2, 0, 10));

      service.getById(5, 1).subscribe();
      httpMock
        .expectOne((r) => r.url === `${usersUrl}/5`)
        .flush({ data: userFive });

      service.delete(6, 1).subscribe();
      httpMock
        .expectOne((r) => r.url === `${usersUrl}/6` && r.method === 'DELETE')
        .flush(null, { status: 204, statusText: 'No Content' });

      // userSix is dropped from the page; the still-selected userFive (id 5 !== 6) is left untouched.
      expect(service.users()).toEqual([userFive]);
      expect(service.selected()).toEqual(userFive);
    });
  });

  // MIGRATION: profile endpoints (getProfile/updateProfile against /users/{id}/profile). The backend now maps the
  // DNN profile EAV schema ([ProfilePropertyDefinition] + [UserProfile]) to a flat UserProfileDto, so these
  // service methods are implemented and covered here. Recorded in MIGRATION_NOTES.md.
  describe('profile endpoints', () => {
    function makeProfile(overrides: Partial<UserProfile> = {}): UserProfile {
      return {
        firstName: 'John',
        lastName: 'Doe',
        fullName: 'John Doe',
        cell: null,
        telephone: null,
        fax: null,
        im: null,
        street: null,
        unit: null,
        city: null,
        region: null,
        country: null,
        postalCode: null,
        preferredLocale: 'en-US',
        timeZone: -1,
        website: null,
        ...overrides,
      };
    }

    it('getProfile() GETs /users/5/profile with the required portalId query and returns the profile', () => {
      const profile = makeProfile();
      let emitted: UserProfile | undefined;

      service.getProfile(5, 1).subscribe((p) => (emitted = p));

      const req = httpMock.expectOne((r) => r.url === `${usersUrl}/5/profile`);
      expect(req.request.method).toBe('GET');
      // MIGRATION: the protected, tenant-scoped read requires the portalId query (AAP Section 0.7.1).
      expect(req.request.params.get('portalId')).toBe('1');
      req.flush({ data: profile });

      expect(emitted).toEqual(profile);
    });

    it('updateProfile() PUTs /users/5/profile with the body and portalId query and returns the saved profile', () => {
      const dto = makeProfile({ city: 'Seattle', timeZone: 2 });
      const saved = makeProfile({ city: 'Seattle', timeZone: 2, fullName: 'John Doe' });
      let emitted: UserProfile | undefined;

      service.updateProfile(5, 1, dto).subscribe((p) => (emitted = p));

      const req = httpMock.expectOne((r) => r.url === `${usersUrl}/5/profile`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.params.get('portalId')).toBe('1');
      expect(req.request.body).toEqual(dto);
      req.flush({ data: saved });

      expect(emitted).toEqual(saved);
    });
  });

  describe('error propagation', () => {
    it('does not swallow non-2xx responses (the error reaches the subscriber)', () => {
      let errorResponse: HttpErrorResponse | undefined;

      service.getById(999, 1).subscribe({
        next: () => fail('expected the request to error, not succeed'),
        error: (err: HttpErrorResponse) => (errorResponse = err),
      });

      const req = httpMock.expectOne((r) => r.url === `${usersUrl}/999`);
      req.flush(
        {
          type: 'about:blank',
          title: 'Not Found',
          status: 404,
          detail: 'User not found',
        },
        { status: 404, statusText: 'Not Found' },
      );

      expect(errorResponse).toBeDefined();
      expect(errorResponse?.status).toBe(404);
    });
  });
});
