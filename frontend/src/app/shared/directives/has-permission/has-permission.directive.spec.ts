import { Component, WritableSignal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { AuthService } from '../../../core/auth/auth.service';
import { User } from '../../../core/models/user.model';
import { HasPermissionDirective, PermissionKey } from './has-permission.directive';

/**
 * Unit tests for {@link HasPermissionDirective} — the `*appHasPermission` RBAC
 * structural directive. Proves the host element is rendered/hidden according to
 * the current user's authorization and that the directive re-evaluates
 * reactively on login/logout. Targets AAP §0.7.2 Gate 4
 * (`ng test --watch=false --browsers=ChromeHeadless`).
 *
 * MIGRATION: the directive under test replaces the legacy server-side RBAC of
 * `Library/Components/Security/PortalSecurity.vb`
 * (`HasNecessaryPermission` / `IsInRole` / `IsInRoles`, including the
 * `IsSuperUser` shortcut). These specs verify the Angular UI-gating behavior;
 * authoritative authorization remains server-side (MIGRATION_NOTES.md D-003).
 *
 * Design decisions:
 * - The directive is exercised through a STANDALONE host component (standalone
 *   is the Angular 19 default; `standalone` is intentionally never set, and
 *   `strictStandalone` would reject a non-standalone declaration). No NgModule
 *   testing setup is used — `TestBed.configureTestingModule({ imports: [...] })`
 *   pulls the directive in transitively via the host's own `imports`.
 * - `AuthService` is replaced by a precisely typed {@link FakeAuthService}
 *   (no `any`): a real writable `currentUser` signal plus a Jasmine spy for
 *   `hasRole`. The directive only ever touches those two members.
 * - Presence is asserted via `queryAll(By.css('button')).length` (0 or 1),
 *   which avoids the `any`-typed `nativeElement` and the null-typing pitfalls
 *   of `query(...)`.
 * - `fixture.detectChanges()` flushes the directive's view `effect()`. The
 *   mocked `hasRole` return value is non-reactive, so reactive re-evaluation is
 *   driven by mutating the `currentUser` signal (the reactive trigger the
 *   directive explicitly reads) AND adjusting the `hasRole` spy return BEFORE
 *   the next `detectChanges()`.
 */
@Component({
  imports: [HasPermissionDirective],
  template: '<button *appHasPermission="permission" type="button">Action</button>',
})
class HostComponent {
  permission: PermissionKey = 'EDIT';
}

/** Precisely typed stand-in for {@link AuthService} — only the members the directive reads. */
interface FakeAuthService {
  currentUser: WritableSignal<User | null>;
  hasRole: jasmine.Spy;
}

/**
 * Build a {@link User} matching the real `core/models/user.model.ts` contract
 * exactly (every required field, with the backend camelCase + acronym casing:
 * `userID` / `portalID` / `affiliateID`). Only `roles` and `isSuperUser` vary
 * per test; the remaining fields are fixed, parity-safe defaults.
 */
function buildUser(roles: string[], isSuperUser = false): User {
  return {
    userID: 1,
    portalID: 0,
    affiliateID: null,
    username: 'tester',
    displayName: 'Test User',
    email: 'tester@example.com',
    firstName: 'Test',
    lastName: 'User',
    fullName: 'Test User',
    isSuperUser,
    approved: true,
    roles,
    createdDate: null,
    lastLoginDate: null,
    lastPasswordChangeDate: null,
    lastActivityDate: null,
  };
}

describe('HasPermissionDirective', () => {
  let fixture: ComponentFixture<HostComponent>;
  let fakeAuthService: FakeAuthService;

  beforeEach(() => {
    fakeAuthService = {
      currentUser: signal<User | null>(null),
      hasRole: jasmine.createSpy('hasRole').and.returnValue(false),
    };

    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [{ provide: AuthService, useValue: fakeAuthService }],
    });

    fixture = TestBed.createComponent(HostComponent);
  });

  function buttonCount(): number {
    return fixture.debugElement.queryAll(By.css('button')).length;
  }

  it('does not render the host element when the user lacks the permission', () => {
    fakeAuthService.hasRole.and.returnValue(false);
    fixture.detectChanges();
    expect(buttonCount()).toBe(0);
  });

  it('renders the host element when the user holds the permission', () => {
    fakeAuthService.currentUser.set(buildUser(['EDIT']));
    fakeAuthService.hasRole.and.returnValue(true);
    fixture.detectChanges();
    expect(buttonCount()).toBe(1);
  });

  it('passes the bound permission key to AuthService.hasRole', () => {
    fakeAuthService.hasRole.and.returnValue(true);
    fixture.detectChanges();
    expect(fakeAuthService.hasRole).toHaveBeenCalledWith('EDIT');
  });

  it('reactively renders the host element after the user logs in', () => {
    fakeAuthService.hasRole.and.returnValue(false);
    fixture.detectChanges();
    expect(buttonCount()).toBe(0);

    fakeAuthService.hasRole.and.returnValue(true);
    fakeAuthService.currentUser.set(buildUser(['EDIT']));
    fixture.detectChanges();
    expect(buttonCount()).toBe(1);
  });

  it('reactively hides the host element after the user logs out', () => {
    fakeAuthService.hasRole.and.returnValue(true);
    fakeAuthService.currentUser.set(buildUser(['EDIT']));
    fixture.detectChanges();
    expect(buttonCount()).toBe(1);

    fakeAuthService.hasRole.and.returnValue(false);
    fakeAuthService.currentUser.set(null);
    fixture.detectChanges();
    expect(buttonCount()).toBe(0);
  });

  it('does not create duplicate views while the user remains authorized', () => {
    fakeAuthService.hasRole.and.returnValue(true);
    fakeAuthService.currentUser.set(buildUser(['EDIT']));
    fixture.detectChanges();
    fixture.detectChanges();
    expect(buttonCount()).toBe(1);
  });
});
