import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { CreateUserRequest, UpdateUserRequest, User } from '../../../core/models';
import { UserService } from '../user.service';
import { UserFormComponent } from './user-form.component';

/** Builds a complete User read model (no secrets) for edit-mode / auth-context mocks. */
function makeUser(overrides: Partial<User> = {}): User {
  return {
    userID: 5,
    portalID: 1,
    username: 'jdoe',
    displayName: 'John Doe',
    firstName: 'John',
    lastName: 'Doe',
    email: 'jdoe@example.com',
    isSuperUser: false,
    affiliateID: 0,
    roles: [],
    membership: {
      approved: true,
      lockedOut: false,
      isOnLine: false,
      updatePassword: false,
      createdDate: '2020-01-01T00:00:00Z',
      lastLoginDate: '2020-01-01T00:00:00Z',
      lastActivityDate: '2020-01-01T00:00:00Z',
      lastLockoutDate: '2020-01-01T00:00:00Z',
      lastPasswordChangeDate: '2020-01-01T00:00:00Z',
    },
    profile: {
      street: '1 Main St',
      unit: '',
      city: 'Townsville',
      region: 'CA',
      country: 'US',
      postalCode: '90210',
      telephone: '555-1234',
      cell: '',
      fax: '',
      website: '',
      im: '',
      timeZone: -480,
      preferredLocale: 'en-US',
    },
    ...overrides,
  };
}

describe('UserFormComponent', () => {
  let userServiceSpy: jasmine.SpyObj<UserService>;
  let routerSpy: jasmine.SpyObj<Router>;

  /** Configure TestBed for a given route mode: null id => create, '5' => edit. */
  function configure(idParam: string | null): void {
    userServiceSpy = jasmine.createSpyObj<UserService>('UserService', [
      'getUser',
      'createUser',
      'updateUser',
      'deleteUser',
    ]);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    const paramMap = convertToParamMap(idParam === null ? {} : { id: idParam });

    TestBed.configureTestingModule({
      imports: [UserFormComponent],
      providers: [
        { provide: UserService, useValue: userServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap } } },
        // AuthService: only currentUser() is read; a signal-backed stub suffices.
        { provide: AuthService, useValue: { currentUser: signal<User | null>(makeUser({ userID: 1 })) } },
      ],
    });
  }

  function createComponent(): ComponentFixture<UserFormComponent> {
    const fixture = TestBed.createComponent(UserFormComponent);
    fixture.detectChanges(); // triggers ngOnInit
    return fixture;
  }

  // ---------------- CREATE MODE ----------------
  describe('create mode (no :id)', () => {
    it('creates the component and reports create mode', () => {
      configure(null);
      const fixture = createComponent();
      expect(fixture.componentInstance).toBeTruthy();
      expect(fixture.componentInstance.isEditMode()).toBeFalse();
      expect(userServiceSpy.getUser).not.toHaveBeenCalled();
    });

    it('posts a valid CreateUserRequest with a random password (password empty, randomPassword true)', () => {
      configure(null);
      userServiceSpy.createUser.and.returnValue(of(makeUser()));
      const fixture = createComponent();
      const component = fixture.componentInstance;

      component.form.patchValue({
        username: 'newuser',
        firstName: 'New',
        lastName: 'User',
        email: 'new@example.com',
        randomPassword: true,
      });
      fixture.detectChanges();

      component.onSubmit();

      expect(userServiceSpy.createUser).toHaveBeenCalledTimes(1);
      const body = userServiceSpy.createUser.calls.mostRecent().args[0] as CreateUserRequest;
      expect(body.username).toBe('newuser');
      expect(body.randomPassword).toBeTrue();
      expect(body.password).toBe('');
      expect(body.portalID).toBe(1);
      expect(body.authorize).toBeTrue();
      expect(body.notify).toBeTrue();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/users']);
    });

    it('enforces password match when randomPassword is false', () => {
      configure(null);
      userServiceSpy.createUser.and.returnValue(of(makeUser()));
      const fixture = createComponent();
      const component = fixture.componentInstance;

      component.form.patchValue({
        username: 'newuser',
        firstName: 'New',
        lastName: 'User',
        email: 'new@example.com',
        randomPassword: false, // enables + requires password/confirm via applyPasswordMode
      });
      fixture.detectChanges();

      component.form.patchValue({ password: 'secret12', confirmPassword: 'different' });
      fixture.detectChanges();

      component.onSubmit();
      expect(userServiceSpy.createUser).not.toHaveBeenCalled(); // mismatch => invalid

      component.form.patchValue({ confirmPassword: 'secret12' });
      fixture.detectChanges();

      component.onSubmit();
      expect(userServiceSpy.createUser).toHaveBeenCalledTimes(1);
      const body = userServiceSpy.createUser.calls.mostRecent().args[0] as CreateUserRequest;
      expect(body.password).toBe('secret12');
      expect(body.randomPassword).toBeFalse();
    });
  });

  // ---------------- EDIT MODE ----------------
  describe('edit mode (:id present)', () => {
    it('loads the user and patches the form (no secrets)', () => {
      configure('5');
      userServiceSpy.getUser.and.returnValue(of(makeUser()));
      const fixture = createComponent();
      const component = fixture.componentInstance;

      expect(component.isEditMode()).toBeTrue();
      expect(userServiceSpy.getUser).toHaveBeenCalledWith(5);
      expect(component.form.controls.firstName.value).toBe('John');
      expect(component.form.controls.email.value).toBe('jdoe@example.com');
      expect(component.form.controls.approved.value).toBeTrue();
      expect(component.form.controls.city.value).toBe('Townsville');
    });

    it('puts a valid UpdateUserRequest with NO password fields', () => {
      configure('5');
      userServiceSpy.getUser.and.returnValue(of(makeUser()));
      userServiceSpy.updateUser.and.returnValue(of(makeUser()));
      const fixture = createComponent();
      const component = fixture.componentInstance;

      component.form.patchValue({ firstName: 'Johnny' });
      fixture.detectChanges();

      component.onSubmit();

      expect(userServiceSpy.updateUser).toHaveBeenCalledTimes(1);
      const call = userServiceSpy.updateUser.calls.mostRecent();
      expect(call.args[0]).toBe(5);
      const body = call.args[1] as UpdateUserRequest;
      expect(body.firstName).toBe('Johnny');
      expect(body.timeZone).toBe(-480);
      // No password on the update contract. UpdateUserRequest is a closed interface
      // (no index signature), so a direct `as Record<string, unknown>` is rejected by
      // strict TS (TS2352); route through `unknown` — the compiler-suggested, `any`-free
      // remedy — to probe for the absent property via bracket notation
      // (satisfies noPropertyAccessFromIndexSignature).
      expect((body as unknown as Record<string, unknown>)['password']).toBeUndefined();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/users']);
    });

    it('deletes the user after confirmation', () => {
      configure('5');
      userServiceSpy.getUser.and.returnValue(of(makeUser()));
      userServiceSpy.deleteUser.and.returnValue(of(undefined as void));
      const fixture = createComponent();
      const component = fixture.componentInstance;

      component.requestDelete();
      expect(component.showDeleteDialog()).toBeTrue();

      component.confirmDelete();

      expect(userServiceSpy.deleteUser).toHaveBeenCalledWith(5);
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/users']);
    });
  });
});
