/**
 * Gate 4 unit tests for {@link UserProfileComponent} — the Angular reproduction of
 * the legacy DotNetNuke Admin > Users membership control
 * (Website/admin/Users/Membership.ascx.vb).
 *
 * Strategy (robust + deterministic):
 *  - A standalone {@link TestBed} that `imports: [UserProfileComponent]` (the
 *    component's own standalone `imports` — the shared form-controls / dialog /
 *    spinner components and the `*appHasPermission` directive — are pulled in
 *    transitively).
 *  - Typed test doubles (NO `any`): a `jasmine.SpyObj<UserService>` whose mapped
 *    membership methods (`forcePasswordChange` / `authorizeUser` / `unauthorizeUser`
 *    / `unlockUser`), the membership reader (`getMembership`), and the `getUser`
 *    loader each return a synchronous `of(...)`; an {@link AuthService} stub exposing
 *    the only members the rendered template's `*appHasPermission` directive reads
 *    (`currentUser` signal, `isAuthenticated` computed, and a permissive `hasRole`)
 *    so `detectChanges()` never throws; and an {@link ActivatedRoute} stub carrying
 *    `paramMap` id `'5'`.
 *  - Assertions target the component's PUBLIC computeds/signals/methods, not the
 *    conditionally-rendered DOM buttons.
 *
 * MIGRATION (DEV-067): all four legacy Membership.ascx.vb command handlers
 * (cmdAuthorize/cmdUnAuthorize/cmdUnLock/cmdPassword, L194-269) are wired end-to-end.
 * force-password-change sets the mapped [Users].UpdatePassword column; authorize /
 * unauthorize / unlock target the [aspnet_Membership] approval/lockout state (now
 * mapped per InstallMembership.sql, bridged from [Users].Username) via dedicated
 * backend routes. The membership status is read from GET /api/v1/users/{id}/membership
 * on load. These tests verify each transition plus its legacy button-visibility gate
 * (Membership.ascx.vb DataBind L135-145: hidden for your own account; Authorize when
 * not approved, Unauthorize when approved, Unlock when locked out, Force-Password when
 * no change is already required). Property names use the trailing-acronym camelCase
 * the .NET 8 API emits (System.Text.Json JsonNamingPolicy.CamelCase): C# `UserID` ->
 * JSON `userID`.
 */
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { computed, signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { User } from '../../../../core/models/user.model';
import { MembershipDto } from '../../models';
import { UserService } from '../../services';
import { UserProfileComponent } from './user-profile.component';

/**
 * Build a fully-populated {@link User}; override any field via `overrides`.
 * Field names match the real `User` contract: `userID` / `portalID` carry the
 * trailing-acronym casing emitted by the .NET 8 API (NOT `userId` / `portalId`).
 */
function makeUser(overrides: Partial<User> = {}): User {
  return {
    userID: 5,
    username: 'jdoe',
    displayName: 'John Doe',
    firstName: 'John',
    lastName: 'Doe',
    email: 'jdoe@example.com',
    portalID: 0,
    isSuperUser: false,
    roles: [],
    ...overrides,
  };
}

/** Build a {@link MembershipDto} snapshot; override any flag via `overrides`. */
function makeMembership(overrides: Partial<MembershipDto> = {}): MembershipDto {
  return {
    approved: true,
    lockedOut: false,
    updatePassword: false,
    ...overrides,
  };
}

describe('UserProfileComponent', () => {
  let fixture: ComponentFixture<UserProfileComponent>;
  let component: UserProfileComponent;
  let userService: jasmine.SpyObj<UserService>;
  let currentUser: ReturnType<typeof signal<User | null>>;

  const target = makeUser({ userID: 5 });
  const admin = makeUser({ userID: 999, username: 'admin', isSuperUser: true, roles: ['Administrators'] });

  beforeEach(async () => {
    userService = jasmine.createSpyObj<UserService>('UserService', [
      'getUser',
      'getMembership',
      'forcePasswordChange',
      'authorizeUser',
      'unauthorizeUser',
      'unlockUser',
    ]);
    userService.getUser.and.returnValue(of(target));
    // loadUser() forkJoins getUser + getMembership, so getMembership MUST yield an Observable.
    userService.getMembership.and.returnValue(of(makeMembership({ approved: true, lockedOut: false, updatePassword: false })));
    userService.forcePasswordChange.and.returnValue(of(target));
    userService.authorizeUser.and.returnValue(of(makeMembership({ approved: true })));
    userService.unauthorizeUser.and.returnValue(of(makeMembership({ approved: false })));
    userService.unlockUser.and.returnValue(of(makeMembership({ lockedOut: false })));

    currentUser = signal<User | null>(admin);
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
    fixture.detectChanges(); // triggers ngOnInit -> forkJoin(getUser(5), getMembership(5))
  });

  it('creates', () => {
    expect(component).toBeTruthy();
  });

  it('loads the target user on init via UserService', () => {
    expect(userService.getUser).toHaveBeenCalledWith(5);
    expect(component.user()?.userID).toBe(5);
    expect(component.loading()).toBeFalse();
  });

  it('loads the membership state on init from the real getMembership endpoint (DEV-067)', () => {
    expect(userService.getMembership).toHaveBeenCalledWith(5);
    // The displayed flags reflect the persisted [aspnet_Membership] projection, not a hard-coded baseline.
    expect(component.membership()?.approved).toBeTrue();
    expect(component.membership()?.lockedOut).toBeFalse();
  });

  it('hides the force-password action when editing your OWN account (DataBind L135-145)', () => {
    currentUser.set(makeUser({ userID: 5, isSuperUser: true })); // same id as the loaded target
    component.membership.set({ approved: false, lockedOut: false, updatePassword: false });
    expect(component.isOwnAccount()).toBeTrue();
    expect(component.canForcePassword()).toBeFalse();
  });

  it('enables Force-Password for a fresh non-own account', () => {
    component.membership.set({ approved: false, lockedOut: false, updatePassword: false });
    expect(component.isOwnAccount()).toBeFalse();
    expect(component.canForcePassword()).toBeTrue();
  });

  it('hides Force-Password when an update is already required', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: true });
    expect(component.canForcePassword()).toBeFalse();
  });

  it('force-password transition calls forcePasswordChange and sets updatePassword', () => {
    component.membership.set({ approved: true, lockedOut: false, updatePassword: false });
    component.requestAction('force-password');
    expect(component.dialogOpen()).toBeTrue();
    component.confirmAction();
    expect(userService.forcePasswordChange).toHaveBeenCalledWith(5);
    expect(component.membership()?.updatePassword).toBeTrue();
    expect(component.dialogOpen()).toBeFalse();
  });

  it('cancelAction closes the dialog without calling the service', () => {
    component.membership.set({ approved: false, lockedOut: false, updatePassword: false });
    component.requestAction('force-password');
    component.cancelAction();
    expect(component.dialogOpen()).toBeFalse();
    expect(userService.forcePasswordChange).not.toHaveBeenCalled();
  });

  // ---- Authorize / Unauthorize / Unlock transitions (DEV-067) ----

  it('shows Authorize only for a non-approved, non-own account (cmdAuthorize.Visible)', () => {
    component.membership.set(makeMembership({ approved: false }));
    expect(component.canAuthorize()).toBeTrue();
    component.membership.set(makeMembership({ approved: true }));
    expect(component.canAuthorize()).toBeFalse();
  });

  it('shows Unauthorize only for an approved, non-own account (cmdUnAuthorize.Visible)', () => {
    component.membership.set(makeMembership({ approved: true }));
    expect(component.canUnauthorize()).toBeTrue();
    component.membership.set(makeMembership({ approved: false }));
    expect(component.canUnauthorize()).toBeFalse();
  });

  it('shows Unlock only for a locked-out, non-own account (cmdUnLock.Visible)', () => {
    component.membership.set(makeMembership({ lockedOut: true }));
    expect(component.canUnlock()).toBeTrue();
    component.membership.set(makeMembership({ lockedOut: false }));
    expect(component.canUnlock()).toBeFalse();
  });

  it('hides every membership button when editing your OWN account (DataBind L135-145)', () => {
    currentUser.set(makeUser({ userID: 5, isSuperUser: true })); // same id as the loaded target
    component.membership.set(makeMembership({ approved: false, lockedOut: true, updatePassword: false }));
    expect(component.isOwnAccount()).toBeTrue();
    expect(component.canAuthorize()).toBeFalse();
    expect(component.canUnauthorize()).toBeFalse();
    expect(component.canUnlock()).toBeFalse();
    expect(component.canForcePassword()).toBeFalse();
  });

  it('authorize transition calls authorizeUser and sets approved=true', () => {
    component.membership.set(makeMembership({ approved: false }));
    component.requestAction('authorize');
    expect(component.dialogOpen()).toBeTrue();
    component.confirmAction();
    expect(userService.authorizeUser).toHaveBeenCalledWith(5);
    expect(component.membership()?.approved).toBeTrue();
    expect(component.dialogOpen()).toBeFalse();
  });

  it('unauthorize transition calls unauthorizeUser and sets approved=false', () => {
    component.membership.set(makeMembership({ approved: true }));
    component.requestAction('unauthorize');
    component.confirmAction();
    expect(userService.unauthorizeUser).toHaveBeenCalledWith(5);
    expect(component.membership()?.approved).toBeFalse();
  });

  it('unlock transition calls unlockUser and clears lockedOut', () => {
    component.membership.set(makeMembership({ lockedOut: true }));
    component.requestAction('unlock');
    component.confirmAction();
    expect(userService.unlockUser).toHaveBeenCalledWith(5);
    expect(component.membership()?.lockedOut).toBeFalse();
  });

  it('surfaces an error and clears the in-flight flag when a transition fails', () => {
    userService.authorizeUser.and.returnValue(throwError(() => new Error('boom')));
    component.membership.set(makeMembership({ approved: false }));
    component.requestAction('authorize');
    component.confirmAction();
    expect(component.errorMessage()).not.toBeNull();
    expect(component.actionInFlight()).toBeFalse();
    // The optimistic projection is NOT applied on failure.
    expect(component.membership()?.approved).toBeFalse();
  });
});
