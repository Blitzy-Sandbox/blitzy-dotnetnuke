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
 *    membership method (`forcePasswordChange`) and the `getUser` loader each return
 *    a synchronous `of(target)`; an {@link AuthService} stub exposing the only
 *    members the rendered template's `*appHasPermission` directive reads
 *    (`currentUser` signal, `isAuthenticated` computed, and a permissive `hasRole`)
 *    so `detectChanges()` never throws; and an {@link ActivatedRoute} stub carrying
 *    `paramMap` id `'5'`.
 *  - Assertions target the component's PUBLIC computeds/signals/methods, not the
 *    conditionally-rendered DOM buttons.
 *
 * MIGRATION: of the four legacy Membership.ascx.vb command handlers
 * (cmdAuthorize/cmdUnAuthorize/cmdUnLock/cmdPassword, L194-269), only
 * force-password-change is wired in Phase 1 — it sets the REAL mapped
 * [Users].UpdatePassword column and has a matching backend route. The authorize /
 * unauthorize / unlock transitions mutate Approved / LockedOut (EF-Ignore()d
 * aspnet_Membership fields per ADR-002 / §0.6.2) with no Phase-1 persistence target
 * and no backend route, so they are intentionally DEFERRED (see root
 * MIGRATION_NOTES.md). These tests verify the surviving force-password transition
 * plus its legacy button-visibility gate (Membership.ascx.vb DataBind L135-145:
 * hidden for your own account and once an update is already required). Property
 * names use the trailing-acronym camelCase the .NET 8 API emits
 * (System.Text.Json JsonNamingPolicy.CamelCase): C# `UserID` -> JSON `userID`.
 */
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { computed, signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { User } from '../../../../core/models/user.model';
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

describe('UserProfileComponent', () => {
  let fixture: ComponentFixture<UserProfileComponent>;
  let component: UserProfileComponent;
  let userService: jasmine.SpyObj<UserService>;
  let currentUser: ReturnType<typeof signal<User | null>>;

  const target = makeUser({ userID: 5 });
  const admin = makeUser({ userID: 999, username: 'admin', isSuperUser: true, roles: ['Administrators'] });

  beforeEach(async () => {
    userService = jasmine.createSpyObj<UserService>('UserService', ['getUser', 'forcePasswordChange']);
    userService.getUser.and.returnValue(of(target));
    userService.forcePasswordChange.and.returnValue(of(target));

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
});
