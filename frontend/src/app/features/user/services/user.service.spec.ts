import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';

import { ApiService, PagedResponse } from '../../../core/services/api.service';
import { User } from '../../../core/models/user.model';
import { CreateUserDto, UpdateUserDto, UserListItem, UserSearchQuery } from '../models';
import { UserService } from './user.service';

/**
 * Unit tests for `UserService` — the THIN, typed HTTP facade over the core
 * `ApiService` for the User administration feature.
 *
 * `UserService` never touches `HttpClient`; it only composes the `/api/v1/users`
 * URLs via `ApiService.resourceUrl` and delegates to the generic `ApiService`
 * verbs. These specs therefore mock `ApiService` with a Jasmine spy object and
 * assert two things per method:
 *   1. DELEGATION — the correct `resourceUrl` entity/id was requested AND the
 *      correct verb was invoked with the correct arguments.
 *   2. IDENTITY   — the service returns the *very* `Observable` that `ApiService`
 *      returned (no internal `subscribe`, no transformation), proven with `toBe`.
 *
 * Deliberately NONE of the Angular HTTP testing utilities (no HTTP testing
 * controller, no test HttpClient provider, no HTTP client testing module) are
 * used here: `UserService` delegates to `ApiService`, so a fake `ApiService` is
 * the correct seam and no real HTTP is exercised.
 */

/**
 * Build a fully-populated `User` that conforms EXACTLY to the on-disk
 * `core/models/user.model.ts` contract.
 *
 * NOTE: the acronym-prefixed identifiers serialize as `userID` / `portalID` /
 * `affiliateID` (the .NET `System.Text.Json` camelCase policy lowercases only the
 * leading acronym run), and every always-present member of the flattened `UserDto`
 * is supplied so the literal satisfies the interface with no missing/excess members.
 */
function makeUser(overrides: Partial<User> = {}): User {
  return {
    userID: 7,
    portalID: 0,
    affiliateID: null,
    username: 'jsmith',
    displayName: 'John Smith',
    email: 'jsmith@example.com',
    firstName: 'John',
    lastName: 'Smith',
    fullName: 'John Smith',
    isSuperUser: false,
    approved: true,
    roles: [],
    createdDate: null,
    lastLoginDate: null,
    lastPasswordChangeDate: null,
    lastActivityDate: null,
    ...overrides,
  };
}

/**
 * Build a user-grid row: a full `User` plus the grid-only membership-status columns
 * (`lockedOut`, `isOnline`) that `UserListItem` adds on top of `User`. `approved` is
 * inherited from `User` (already set by `makeUser`) and is intentionally not redeclared.
 */
function makeListItem(overrides: Partial<UserListItem> = {}): UserListItem {
  return { ...makeUser(), lockedOut: false, isOnline: false, ...overrides };
}

/** Build a paged list envelope exactly as `ApiService.getList` hands it back to callers. */
function makePaged(items: UserListItem[] = [makeListItem()]): PagedResponse<UserListItem> {
  return {
    data: items,
    meta: { pageIndex: 0, pageSize: 10, totalCount: items.length, totalPages: 1 },
  };
}

describe('UserService', () => {
  let service: UserService;
  let apiSpy: jasmine.SpyObj<ApiService>;

  // A representative, fully-populated query. Passing every field makes the
  // `UserService.toQueryParams` output deterministic for `toHaveBeenCalledWith`.
  const query: UserSearchQuery = {
    filter: 'A',
    filterProperty: 'City',
    searchText: 'smith',
    searchType: 'email',
    pageIndex: 2,
    pageSize: 25,
  };

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj<ApiService>('ApiService', [
      'resourceUrl',
      'get',
      'getList',
      'post',
      'put',
      'delete',
    ]);
    // Mirror the real `resourceUrl` so URL assertions are meaningful:
    // `/api/v1/<entity>` without an id, `/api/v1/<entity>/<id>` with one.
    apiSpy.resourceUrl.and.callFake((entity: string, id?: string | number) =>
      id === undefined ? `/api/v1/${entity}` : `/api/v1/${entity}/${id}`,
    );

    TestBed.configureTestingModule({
      providers: [UserService, { provide: ApiService, useValue: apiSpy }],
    });

    service = TestBed.inject(UserService);
  });

  it('is created', () => {
    expect(service).toBeTruthy();
  });

  it('getUsers delegates to getList with the mapped query params', () => {
    const expected = of(makePaged());
    apiSpy.getList.and.returnValue(expected);

    const result = service.getUsers(query);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users');
    expect(apiSpy.getList).toHaveBeenCalledWith('/api/v1/users', {
      filter: 'A',
      filterProperty: 'City',
      searchText: 'smith',
      searchType: 'email',
      pageIndex: 2,
      pageSize: 25,
    });
  });

  it('getOnlineUsers adds the online=true flag to the query params', () => {
    const expected = of(makePaged());
    apiSpy.getList.and.returnValue(expected);

    const result = service.getOnlineUsers(query);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users');
    expect(apiSpy.getList).toHaveBeenCalledWith('/api/v1/users', {
      filter: 'A',
      filterProperty: 'City',
      searchText: 'smith',
      searchType: 'email',
      pageIndex: 2,
      pageSize: 25,
      online: true,
    });
  });

  it('getUnauthorizedUsers adds the unauthorized=true flag to the query params', () => {
    const expected = of(makePaged());
    apiSpy.getList.and.returnValue(expected);

    const result = service.getUnauthorizedUsers(query);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users');
    expect(apiSpy.getList).toHaveBeenCalledWith('/api/v1/users', {
      filter: 'A',
      filterProperty: 'City',
      searchText: 'smith',
      searchType: 'email',
      pageIndex: 2,
      pageSize: 25,
      unauthorized: true,
    });
  });

  it('getUser delegates to get with /users/{id}', () => {
    const expected = of(makeUser());
    apiSpy.get.and.returnValue(expected);

    const result = service.getUser(7);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users', 7);
    expect(apiSpy.get).toHaveBeenCalledWith('/api/v1/users/7');
  });

  it('createUser delegates to post with the create payload', () => {
    const dto: CreateUserDto = {
      username: 'new',
      firstName: 'New',
      lastName: 'User',
      displayName: 'New User',
      email: 'new@example.com',
    };
    const expected = of(makeUser());
    apiSpy.post.and.returnValue(expected);

    const result = service.createUser(dto);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users');
    expect(apiSpy.post).toHaveBeenCalledWith('/api/v1/users', dto);
  });

  it('updateUser delegates to put with /users/{id} and the update payload', () => {
    const dto: UpdateUserDto = {
      firstName: 'Up',
      lastName: 'Dated',
      displayName: 'Up Dated',
      email: 'up@example.com',
    };
    const expected = of(makeUser());
    apiSpy.put.and.returnValue(expected);

    const result = service.updateUser(7, dto);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users', 7);
    expect(apiSpy.put).toHaveBeenCalledWith('/api/v1/users/7', dto);
  });

  it('deleteUser delegates to delete with /users/{id}', () => {
    const expected: Observable<void> = of(undefined);
    apiSpy.delete.and.returnValue(expected);

    const result = service.deleteUser(7);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users', 7);
    expect(apiSpy.delete).toHaveBeenCalledWith('/api/v1/users/7');
  });

  it('approveUser PUTs to /users/{id}/approve with no body', () => {
    const expected = of(makeUser());
    apiSpy.put.and.returnValue(expected);

    const result = service.approveUser(7);

    expect(result).toBe(expected);
    expect(apiSpy.put).toHaveBeenCalledWith('/api/v1/users/7/approve');
  });

  it('unauthorizeUser PUTs to /users/{id}/unauthorize with no body', () => {
    const expected = of(makeUser());
    apiSpy.put.and.returnValue(expected);

    const result = service.unauthorizeUser(7);

    expect(result).toBe(expected);
    expect(apiSpy.put).toHaveBeenCalledWith('/api/v1/users/7/unauthorize');
  });

  it('unlockUser PUTs to /users/{id}/unlock with no body', () => {
    const expected = of(makeUser());
    apiSpy.put.and.returnValue(expected);

    const result = service.unlockUser(7);

    expect(result).toBe(expected);
    expect(apiSpy.put).toHaveBeenCalledWith('/api/v1/users/7/unlock');
  });

  it('forcePasswordChange PUTs to /users/{id}/force-password-change with no body', () => {
    const expected = of(makeUser());
    apiSpy.put.and.returnValue(expected);

    const result = service.forcePasswordChange(7);

    expect(result).toBe(expected);
    expect(apiSpy.put).toHaveBeenCalledWith('/api/v1/users/7/force-password-change');
  });
});
