import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { CreateUserRequest, ProblemDetails, UpdateUserRequest, User } from '../../../core/models';
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
      // QA finding F3: createUser now resolves to CreateUserResult { user, generatedPassword? }.
      // No generatedPassword here => the create behaves like a plain save (navigates to the list).
      userServiceSpy.createUser.and.returnValue(of({ user: makeUser() }));
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
      userServiceSpy.createUser.and.returnValue(of({ user: makeUser() }));
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

    // QA finding F3: a random-password create must SURFACE the one-time generated password and
    // DEFER navigation until the admin acknowledges it (the password is never retrievable again).
    it('reveals the generated password and defers navigation until acknowledged', () => {
      configure(null);
      const generated = 'Tmp!Pw9xQ2';
      userServiceSpy.createUser.and.returnValue(
        of({ user: makeUser(), generatedPassword: generated }),
      );
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
      fixture.detectChanges();

      // Dialog open, password captured, navigation NOT yet performed.
      expect(component.showPasswordDialog()).toBeTrue();
      expect(component.generatedPassword()).toBe(generated);
      expect(routerSpy.navigate).not.toHaveBeenCalled();

      // The generated password is rendered (selectable) in the acknowledgement dialog.
      const detail = (fixture.nativeElement as HTMLElement).querySelector('.cdlg-dialog__detail');
      expect(detail).not.toBeNull();
      expect(detail?.textContent).toContain(generated);

      // Acknowledging closes the dialog, clears the password, and continues to the list.
      component.acknowledgeGeneratedPassword();
      fixture.detectChanges();
      expect(component.showPasswordDialog()).toBeFalse();
      expect(component.generatedPassword()).toBeNull();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/users']);
    });

    // QA R10 Issue 11: toggling the random/manual password checkbox must NOT surface premature
    // validation. Every toggle resets the password/confirm controls (value + touched + pristine),
    // so after switching back to manual the fields are empty, UNTOUCHED and PRISTINE (invalid but
    // not yet error-displayed) rather than showing a stale "required"/"mismatch" message.
    it('resets password fields (value + touched/pristine) on every random/manual toggle', () => {
      configure(null);
      const fixture = createComponent();
      const component = fixture.componentInstance;
      const password = component.form.controls.password;
      const confirm = component.form.controls.confirmPassword;

      // Switch to MANUAL, type values, and mark them interacted-with (as a real user would).
      component.form.patchValue({ randomPassword: false });
      fixture.detectChanges();
      password.setValue('secret12');
      confirm.setValue('secret12');
      password.markAsTouched();
      confirm.markAsTouched();
      expect(password.touched).toBeTrue();

      // Toggle back to RANDOM: the fields are cleared and reset (disabled, validator-free).
      component.form.patchValue({ randomPassword: true });
      fixture.detectChanges();
      expect(password.value).toBe('');
      expect(confirm.value).toBe('');
      expect(password.touched).toBeFalse();
      expect(password.pristine).toBeTrue();

      // Toggle to MANUAL again: the fields are fresh (empty, UNTOUCHED, PRISTINE) so no premature
      // "required"/"mismatch" error is shown the instant the checkbox flips.
      component.form.patchValue({ randomPassword: false });
      fixture.detectChanges();
      expect(password.value).toBe('');
      expect(confirm.value).toBe('');
      expect(password.touched).toBeFalse();
      expect(password.pristine).toBeTrue();
      expect(confirm.touched).toBeFalse();
      expect(confirm.pristine).toBeTrue();
      // The control is INVALID (required, empty) but untouched+pristine => FormFieldComponent
      // renders no message yet (validation is deferred to interaction/submit).
      expect(password.invalid).toBeTrue();
    });

    // QA R10 Issue 7: a duplicate-user create is rejected by the backend with a 409 Conflict whose
    // RFC 7807 detail/title carries the SPECIFIC business message. It must be surfaced INLINE on the
    // User Name control (keyed on a custom `duplicate` error) instead of the generic save banner.
    it('maps a duplicate-user 409 to a field-level error on User Name', () => {
      configure(null);
      const dupMessage = "A user with the username 'newuser' already exists in portal 1.";
      userServiceSpy.createUser.and.returnValue(
        throwError(
          () =>
            ({
              type: 'about:blank',
              title: dupMessage,
              status: 409,
              detail: dupMessage,
            }) as ProblemDetails,
        ),
      );
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
      fixture.detectChanges();

      // The specific server message is surfaced INLINE on the username control (not the generic banner).
      expect(component.form.controls.username.hasError('duplicate')).toBeTrue();
      expect(component.usernameServerError()).toBe(dupMessage);
      expect(component.usernameErrorMessages()['duplicate']).toBe(dupMessage);
      expect(component.form.controls.username.touched).toBeTrue();
      expect(component.errorMessage()).toBeNull();
    });

    // QA R10 Issue 9: password-manager autocomplete attributes must be present so Chrome does not
    // warn and credentials pair correctly. Username => "username"; create-mode password/confirm =>
    // "new-password".
    it('sets password-manager autocomplete attributes on identity/password inputs', () => {
      configure(null);
      const fixture = createComponent();
      const component = fixture.componentInstance;
      const host = fixture.nativeElement as HTMLElement;

      expect(host.querySelector('#username')?.getAttribute('autocomplete')).toBe('username');

      // Reveal the manual password fields, then assert their autocomplete tokens.
      component.form.patchValue({ randomPassword: false });
      fixture.detectChanges();
      expect(host.querySelector('#password')?.getAttribute('autocomplete')).toBe('new-password');
      expect(host.querySelector('#confirmPassword')?.getAttribute('autocomplete')).toBe(
        'new-password',
      );
    });

    // QA finding P7-2 (Report 11): the remaining identity/profile inputs each carry an
    // explicit autocomplete token so browsers offer correct field-level autofill; the
    // secret-adjacent password question/answer are opted OUT ("off"). Username and the
    // create-mode password/confirm tokens are covered by the test above.
    it('sets field-level autocomplete tokens on the remaining create-mode inputs (P7-2)', () => {
      configure(null);
      const fixture = createComponent();
      const host = fixture.nativeElement as HTMLElement;

      expect(host.querySelector('#firstName')?.getAttribute('autocomplete')).toBe('given-name');
      expect(host.querySelector('#lastName')?.getAttribute('autocomplete')).toBe('family-name');
      expect(host.querySelector('#displayName')?.getAttribute('autocomplete')).toBe('nickname');
      expect(host.querySelector('#email')?.getAttribute('autocomplete')).toBe('email');
      expect(host.querySelector('#passwordQuestion')?.getAttribute('autocomplete')).toBe('off');
      expect(host.querySelector('#passwordAnswer')?.getAttribute('autocomplete')).toBe('off');
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

    // QA finding F4: the Change Password entry point (previously absent) navigates to the
    // change-password screen for the edited user.
    it('navigates to the change-password screen from edit mode', () => {
      configure('5');
      userServiceSpy.getUser.and.returnValue(of(makeUser()));
      const fixture = createComponent();
      const component = fixture.componentInstance;

      // The Change Password button is rendered in edit mode...
      const buttons = Array.from(
        (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
      );
      const changeBtn = buttons.find((b) => b.textContent?.trim() === 'Change Password');
      expect(changeBtn)
        .withContext('Change Password button should be rendered in edit mode')
        .toBeTruthy();

      // ...and clicking it routes to /users/:id/password (in-app, preserving the JWT session).
      changeBtn!.click();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/users', 5, 'password']);
    });
  });
});
