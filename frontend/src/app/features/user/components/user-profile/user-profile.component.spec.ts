import { ComponentFixture, TestBed } from '@angular/core/testing';
import { computed, signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { User } from '../../../../core/models/user.model';
import { UserService } from '../../services';
import { UserProfileComponent } from './user-profile.component';

/**
 * Spec for {@link UserProfileComponent} — Gate 4 (`ng test --watch=false --browsers=ChromeHeadless`).
 *
 * Strategy (per the file's agent prompt): assert PARITY on the component's PUBLIC signals/computeds,
 * never on the conditionally-rendered DOM buttons. The legacy `Membership.ascx.vb` DataBind logic
 * (L135-145) gated each command button's visibility; here that logic lives in the `canX()` computeds,
 * which are the artifact under test. The `*appHasPermission` directive double-gates the DOM and its
 * internals are out of this file's control, so the `AuthService` stub merely keeps `detectChanges()`
 * from throwing while the parity assertions read the computeds directly.
 *
 * MIGRATION reconciliation (verified against the real implementation, see MIGRATION_NOTES.md D-034):
 * the modern `UserService` exposes list/get/create/update/delete only — it has NO
 * approve/unauthorize/unlock/force-password methods. The component therefore round-trips EVERY
 * membership transition through `UserService.updateUser(id, UpdateUserRequest)` and reconciles the
 * `lockedOut`/`updatePassword` flags client-side via its optimistic snapshot update. These tests spy
 * on the methods the component actually calls (`getUser` + `updateUser`) and assert the optimistic
 * snapshot flips, faithful to the legacy command-button behavior.
 */

/**
 * Build a fully-populated {@link User}. The core `User` contract has every field required
 * (`T | null` rather than optional), and uses the System.Text.Json camelCase wire names
 * `userID` / `portalID` / `affiliateID` (the leading acronym run is lowercased, the trailing
 * `ID` is not). Overrides are merged last so individual tests can pin only what they exercise.
 */
function makeUser(overrides: Partial<User> = {}): User {
  return {
    userID: 5,
    portalID: 0,
    affiliateID: null,
    username: 'jdoe',
    displayName: 'John Doe',
    email: 'jdoe@example.com',
    firstName: 'John',
    lastName: 'Doe',
    fullName: 'John Doe',
    isSuperUser: false,
    approved: false,
    roles: [],
    createdDate: null,
    lastLoginDate: null,
    lastPasswordChangeDate: null,
    lastActivityDate: null,
    ...overrides,
  };
}

describe('UserProfileComponent', () => {
  let fixture: ComponentFixture<UserProfileComponent>;
  let component: UserProfileComponent;
  let userService: jasmine.SpyObj<UserService>;
  let currentUser: ReturnType<typeof signal<User | null>>;

  // The route resolves id=5, so the loaded target user shares userID 5; `admin` is a distinct
  // acting user (userID 999) so the default (non-own-account) visibility rules apply.
  const target = makeUser({ userID: 5 });
  const admin = makeUser({ userID: 999, username: 'admin', isSuperUser: true, roles: ['Administrators'] });

  beforeEach(async () => {
    // The component touches only getUser (load) and updateUser (every transition); spy on exactly those.
    userService = jasmine.createSpyObj<UserService>('UserService', ['getUser', 'updateUser']);
    userService.getUser.and.returnValue(of(target));
    userService.updateUser.and.returnValue(of(target));

    currentUser = signal<User | null>(admin);
    // Minimal AuthService stub: the component reads `currentUser`, and the rendered
    // `*appHasPermission` directive reads `currentUser()` + `hasRole()`. `hasRole` stays permissive
    // so the directive never blocks rendering; `isAuthenticated` mirrors the real computed shape.
    const authStub = {
      currentUser,
      isAuthenticated: computed(() => currentUser() !== null),
      hasRole: (): boolean => true,
    };

    await TestBed.configureTestingModule({
      imports: [UserProfileComponent],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: AuthService, useValue: authStub },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '5' }) } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges(); // triggers ngOnInit -> getUser(5)
  });

  it('creates', () => {
    expect(component).toBeTruthy();
  });

  it('loads the target user on init via UserService', () => {
    expect(userService.getUser).toHaveBeenCalledWith(5);
    expect(component.user()?.userID).toBe(5);
    expect(component.loading()).toBeFalse();
  });

  it('hides all four actions when editing your OWN account (DataBind L135-145)', () => {
    currentUser.set(makeUser({ userID: 5, isSuperUser: true })); // same id as the loaded target
    component.membership.set({ approved: false, lockedOut: true, updatePassword: false });
    expect(component.isOwnAccount()).toBeTrue();
    expect(component.canAuthorize()).toBeFalse();
    expect(component.canUnauthorize()).toBeFalse();
    expect(component.canUnlock()).toBeFalse();
    expect(component.canForcePassword()).toBeFalse();
  });

  it('shows Authorize + Force-Password (and hides Unauthorize/Unlock) for a fresh non-own account', () => {
    component.membership.set({ approved: false, lockedOut: false, updatePassword: false });
    expect(component.canAuthorize()).toBeTrue();
    expect(component.canForcePassword()).toBeTrue();
    expect(component.canUnauthorize()).toBeFalse();
    expect(component.canUnlock()).toBeFalse();
  });

  it('shows Unauthorize (and hides Authorize) when the account is approved', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: false });
    expect(component.canUnauthorize()).toBeTrue();
    expect(component.canAuthorize()).toBeFalse();
  });

  it('shows Unlock only when the account is locked out', () => {
    component.membership.set({ approved: true, lockedOut: true, updatePassword: true });
    expect(component.canUnlock()).toBeTrue();
  });

  it('hides Force-Password when an update is already required', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: true });
    expect(component.canForcePassword()).toBeFalse();
  });

  it('authorize transition calls updateUser and optimistically marks approved', () => {
    component.membership.set({ approved: false, lockedOut: false, updatePassword: false });
    component.requestAction('authorize');
    expect(component.dialogOpen()).toBeTrue();
    component.confirmAction();
    expect(userService.updateUser).toHaveBeenCalledWith(5, jasmine.objectContaining({ approved: true }));
    expect(component.membership()?.approved).toBeTrue();
    expect(component.dialogOpen()).toBeFalse();
  });

  it('unauthorize transition calls updateUser and clears approved', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: false });
    component.requestAction('unauthorize');
    component.confirmAction();
    expect(userService.updateUser).toHaveBeenCalledWith(5, jasmine.objectContaining({ approved: false }));
    expect(component.membership()?.approved).toBeFalse();
  });

  it('unlock transition calls updateUser and clears lockedOut on success', () => {
    component.membership.set({ approved: true, lockedOut: true, updatePassword: false });
    component.requestAction('unlock');
    component.confirmAction();
    expect(userService.updateUser).toHaveBeenCalledWith(5, jasmine.objectContaining({ userID: 5 }));
    expect(component.membership()?.lockedOut).toBeFalse();
  });

  it('force-password transition calls updateUser and sets updatePassword', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: false });
    component.requestAction('force-password');
    component.confirmAction();
    expect(userService.updateUser).toHaveBeenCalledWith(5, jasmine.objectContaining({ userID: 5 }));
    expect(component.membership()?.updatePassword).toBeTrue();
  });

  it('cancelAction closes the dialog without calling the service', () => {
    component.membership.set({ approved: false, lockedOut: false, updatePassword: false });
    component.requestAction('authorize');
    component.cancelAction();
    expect(component.dialogOpen()).toBeFalse();
    expect(userService.updateUser).not.toHaveBeenCalled();
  });
});
