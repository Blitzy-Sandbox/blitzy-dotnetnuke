import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { ChangePasswordRequest, CreateUserRequest, User } from '../../core/models';
import { UserService } from './user.service';

/**
 * Unit spec for {@link UserService}, verifying it is a thin delegator to
 * {@link ApiService} and — critically — that `changePassword()` uses the no-content
 * POST helper.
 *
 * MIGRATION (Checkpoint-8 API-contract finding): `POST /api/users/{id}/change-password`
 * returns HTTP 204 with no `{ data, meta }` envelope. The prior `post<void>()` mapped
 * `res.data` and mis-reported the successful change as a client error; the fix routes it
 * through `postNoContent()`. ApiService is mocked (jasmine.SpyObj) so no real HTTP runs.
 * Contributes to Gate 4 (ng test --code-coverage, 100% pass).
 */
describe('UserService', () => {
  let service: UserService;
  let api: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiService>('ApiService', [
      'getList',
      'delete',
      'postNoContent',
      'createWithMeta',
    ]);

    TestBed.configureTestingModule({
      providers: [UserService, { provide: ApiService, useValue: api }],
    });

    service = TestBed.inject(UserService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('changePassword() delegates to ApiService.postNoContent (204 contract, no envelope)', () => {
    const body: ChangePasswordRequest = { oldPassword: 'old', newPassword: 'new' };
    api.postNoContent.and.returnValue(of(void 0));

    service.changePassword(7, body).subscribe();

    expect(api.postNoContent).toHaveBeenCalledWith('users/7/change-password', body);
  });

  it('getUsers() delegates to ApiService.getList with the "users" resource', () => {
    api.getList.and.returnValue(of([]));

    service.getUsers({ query: 'smith' }).subscribe();

    expect(api.getList).toHaveBeenCalledWith('users', { query: 'smith' });
  });

  it('deleteUser() delegates to ApiService.delete', () => {
    api.delete.and.returnValue(of(void 0));

    service.deleteUser(9).subscribe();

    expect(api.delete).toHaveBeenCalledWith('users', 9);
  });

  // QA finding F3: createUser must PRESERVE the response meta so the one-time generated password
  // (meta.generatedPassword) reaches the create UI. It delegates to createWithMeta (not the
  // envelope-stripping create) and maps { data, meta } -> { user, generatedPassword }.
  it('createUser() maps the createWithMeta envelope to { user, generatedPassword } (F3)', () => {
    const created = { userID: 42 } as User;
    api.createWithMeta.and.returnValue(
      of({ data: created, meta: { generatedPassword: 'Gen!Pw123' } }),
    );
    const body = { username: 'newuser' } as CreateUserRequest;

    let result: { user: User; generatedPassword?: string } | undefined;
    service.createUser(body).subscribe((r) => (result = r));

    expect(api.createWithMeta).toHaveBeenCalledWith('users', body);
    expect(result?.user).toBe(created);
    expect(result?.generatedPassword).toBe('Gen!Pw123');
  });

  it('createUser() yields an undefined generatedPassword when meta omits it (manual password)', () => {
    const created = { userID: 43 } as User;
    api.createWithMeta.and.returnValue(of({ data: created, meta: {} }));

    let result: { user: User; generatedPassword?: string } | undefined;
    service
      .createUser({ username: 'manual' } as CreateUserRequest)
      .subscribe((r) => (result = r));

    expect(result?.user).toBe(created);
    expect(result?.generatedPassword).toBeUndefined();
  });
});
