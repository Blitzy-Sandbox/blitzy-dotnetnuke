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
 * the modern `UserService` now exposes list/get/create/update/delete AND `forcePasswordChange`. Three of
 * the four legacy Membership.ascx transitions have a persistable contract: Authorize / Unauthorize via the
 * `approved` flag on UpdateUserRequest (PUT /api/v1/users/{id}), and Force Password Change via the real
 * `dbo.Users.UpdatePassword` column through POST /api/v1/users/{id}/force-password-change
 * (UserService.forcePasswordChange, reversible require/clear). Both are reproduced end-to-end, OVERTURNING
 * the prior "later checkpoint" deferral now that the backend endpoint and UserDto.UpdatePassword exist.
 * Only Unlock stays unimplemented (lockout lives on the out-of-scope, GUID-keyed aspnet_Membership table;
 * ADR-002 / AAP 0.2.2 forbid the schema change), shown read-only. These tests spy on the methods the
 * component actually calls (`getUser`, `updateUser`, `forcePasswordChange`) and assert the optimistic
 * `approved` / `updatePassword` snapshots flip, faithful to the surviving legacy command-button behavior.
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
    updatePassword: false,
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
    // The component touches getUser (load), updateUser (Authorize/Unauthorize) and forcePasswordChange
    // (Force / Clear password-change); spy on exactly those.
    userService = jasmine.createSpyObj<UserService>('UserService', ['getUser', 'updateUser', 'forcePasswordChange']);
    userService.getUser.and.returnValue(of(target));
    userService.updateUser.and.returnValue(of(target));
    userService.forcePasswordChange.and.returnValue(of(target));

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

  it('hides both actions when editing your OWN account (DataBind L135-145)', () => {
    currentUser.set(makeUser({ userID: 5, isSuperUser: true })); // same id as the loaded target
    component.membership.set({ approved: false, lockedOut: true, updatePassword: false });
    expect(component.isOwnAccount()).toBeTrue();
    expect(component.canAuthorize()).toBeFalse();
    expect(component.canUnauthorize()).toBeFalse();
  });

  it('shows Authorize (and hides Unauthorize) for a fresh non-own account', () => {
    component.membership.set({ approved: false, lockedOut: false, updatePassword: false });
    expect(component.canAuthorize()).toBeTrue();
    expect(component.canUnauthorize()).toBeFalse();
  });

  it('shows Unauthorize (and hides Authorize) when the account is approved', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: false });
    expect(component.canUnauthorize()).toBeTrue();
    expect(component.canAuthorize()).toBeFalse();
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

  // MIGRATION (D-034): Force Password Change visibility mirrors the legacy cmdPassword button (L213) —
  // shown when the user is NOT yet required to change, with the inverse "Clear" button when they are.
  it('shows Force Password Change (and hides Clear) when updatePassword is not set', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: false });
    expect(component.canForcePasswordChange()).toBeTrue();
    expect(component.canClearForcePasswordChange()).toBeFalse();
  });

  it('shows Clear (and hides Force) when updatePassword is already set', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: true });
    expect(component.canClearForcePasswordChange()).toBeTrue();
    expect(component.canForcePasswordChange()).toBeFalse();
  });

  it('hides both password-change actions when editing your OWN account (DataBind L135-145)', () => {
    currentUser.set(makeUser({ userID: 5, isSuperUser: true })); // same id as the loaded target
    component.membership.set({ approved: true, lockedOut: false, updatePassword: false });
    expect(component.isOwnAccount()).toBeTrue();
    expect(component.canForcePasswordChange()).toBeFalse();
    expect(component.canClearForcePasswordChange()).toBeFalse();
  });

  it('force-password-change calls forcePasswordChange(id, true) and optimistically sets updatePassword', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: false });
    component.requestAction('force-password-change');
    expect(component.dialogOpen()).toBeTrue();
    component.confirmAction();
    expect(userService.forcePasswordChange).toHaveBeenCalledWith(5, true);
    expect(userService.updateUser).not.toHaveBeenCalled();
    expect(component.membership()?.updatePassword).toBeTrue();
    expect(component.dialogOpen()).toBeFalse();
  });

  it('clear-force-password-change calls forcePasswordChange(id, false) and optimistically clears updatePassword', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: true });
    component.requestAction('clear-force-password-change');
    component.confirmAction();
    expect(userService.forcePasswordChange).toHaveBeenCalledWith(5, false);
    expect(userService.updateUser).not.toHaveBeenCalled();
    expect(component.membership()?.updatePassword).toBeFalse();
  });

  it('cancelAction closes the dialog without calling the service', () => {
    component.membership.set({ approved: false, lockedOut: false, updatePassword: false });
    component.requestAction('authorize');
    component.cancelAction();
    expect(component.dialogOpen()).toBeFalse();
    expect(userService.updateUser).not.toHaveBeenCalled();
  });
});
