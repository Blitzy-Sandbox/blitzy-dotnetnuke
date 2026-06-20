import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';

import { ApiService, PagedResponse } from '../../../core/services/api.service';
import { User } from '../../../core/models/user.model';
import { CreateUserDto, UpdateUserDto, UserListItem, UserSearchQuery } from '../models';
import { UserService } from './user.service';

/**
 * Unit test for {@link UserService}.
 *
 * `UserService` is a THIN pass-through over {@link ApiService}: it owns no
 * business logic and performs no HTTP itself. These specs therefore verify two
 * contracts only, with NO real HTTP (no `HttpTestingController`, no
 * `provideHttpClient`, no `HttpClientTestingModule`):
 *   1. DELEGATION — each method resolves the correct `/api/v1/users[/{id}]` URL
 *      (through the faked `resourceUrl`) and calls the correct generic verb with
 *      the correct arguments (asserted with `toHaveBeenCalledWith`).
 *   2. IDENTITY  — each method returns the EXACT `Observable` that `ApiService`
 *      returned, unchanged (asserted with `toBe`), proving there is no internal
 *      `subscribe` and no transformation in the facade.
 *
 * The facade exposes exactly the five REST CRUD operations the backend
 * `UsersController` implements (GET list, GET by id, POST, PUT, DELETE). Legacy
 * UserController membership transitions (online/unauthorized listing, approve /
 * unauthorize / unlock / force-password-change) have no REST counterpart at this
 * milestone and are intentionally absent from the service, so they are not
 * exercised here.
 *
 * `ApiService` is replaced by a Jasmine spy object; `resourceUrl` is faked to
 * mirror the real URL composition so the URL assertions are meaningful.
 */

/** Build a fully-populated {@link User}; override any field via `overrides`. */
function makeUser(overrides: Partial<User> = {}): User {
  return {
    userID: 7,
    username: 'jsmith',
    displayName: 'John Smith',
    firstName: 'John',
    lastName: 'Smith',
    email: 'jsmith@example.com',
    portalID: 0,
    isSuperUser: false,
    roles: [],
    ...overrides,
  };
}

/** Build a {@link UserListItem} grid row (a `User` plus membership-status columns). */
function makeListItem(overrides: Partial<UserListItem> = {}): UserListItem {
  return { ...makeUser(), approved: true, lockedOut: false, isOnline: false, ...overrides };
}

/** Build a paged `{ data, meta }` envelope around the supplied list items. */
function makePaged(items: UserListItem[] = [makeListItem()]): PagedResponse<UserListItem> {
  return {
    data: items,
    meta: { pageIndex: 0, pageSize: 10, totalCount: items.length, totalPages: 1 },
  };
}

describe('UserService', () => {
  let service: UserService;
  let apiSpy: jasmine.SpyObj<ApiService>;

  // A representative query covering every field the REST list contract binds.
  // Passing portalId (REQUIRED), pageIndex, and pageSize makes the service's
  // `toQueryParams` output deterministic for the assertion below.
  const query: UserSearchQuery = {
    portalId: 0,
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

  it('getUsers delegates to getList with the mapped query params and returns the observable unchanged', () => {
    const expected = of(makePaged());
    apiSpy.getList.and.returnValue(expected);

    const result = service.getUsers(query);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users');
    expect(apiSpy.getList).toHaveBeenCalledWith('/api/v1/users', {
      portalId: 0,
      pageIndex: 2,
      pageSize: 25,
    });
  });

  it('getUser delegates to get with the /users/{id} URL', () => {
    const expected = of(makeUser());
    apiSpy.get.and.returnValue(expected);

    const result = service.getUser(7);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users', 7);
    expect(apiSpy.get).toHaveBeenCalledWith('/api/v1/users/7');
  });

  it('createUser delegates to post with the request body', () => {
    const dto: CreateUserDto = {
      portalID: 0,
      username: 'new',
      displayName: 'New User',
      email: 'new@example.com',
      firstName: 'New',
      lastName: 'User',
      isSuperUser: false,
      approved: true,
    };
    const expected = of(makeUser());
    apiSpy.post.and.returnValue(expected);

    const result = service.createUser(dto);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users');
    expect(apiSpy.post).toHaveBeenCalledWith('/api/v1/users', dto);
  });

  it('updateUser delegates to put with the /users/{id} URL and body', () => {
    const dto: UpdateUserDto = {
      userID: 7,
      displayName: 'Up Dated',
      email: 'up@example.com',
      firstName: 'Up',
      lastName: 'Dated',
      isSuperUser: false,
      approved: true,
    };
    const expected = of(makeUser());
    apiSpy.put.and.returnValue(expected);

    const result = service.updateUser(7, dto);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users', 7);
    expect(apiSpy.put).toHaveBeenCalledWith('/api/v1/users/7', dto);
  });

  it('deleteUser delegates to delete with the /users/{id} URL', () => {
    const expected: Observable<void> = of(undefined);
    apiSpy.delete.and.returnValue(expected);

    const result = service.deleteUser(7);

    expect(result).toBe(expected);
    expect(apiSpy.resourceUrl).toHaveBeenCalledWith('users', 7);
    expect(apiSpy.delete).toHaveBeenCalledWith('/api/v1/users/7');
  });
});
