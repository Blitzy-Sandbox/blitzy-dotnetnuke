/**
 * Gate 4 unit tests for {@link HasPermissionDirective} — the `*appHasPermission`
 * RBAC structural directive that conditionally renders its host element based on
 * the current user's role-based access.
 *
 * Strategy: a standalone {@link HostComponent} embeds the directive through the
 * structural microsyntax `<button *appHasPermission="permission">`. A typed fake
 * {@link AuthService} (NO `any`) supplies the only two members the directive reads
 * — the `currentUser` writable signal (the reactive trigger) and the `hasRole` spy
 * (the authorization decision). Element presence is asserted by counting matching
 * DOM nodes via `fixture.debugElement.queryAll(By.css('button')).length`, which
 * keeps the assertions strictly typed: it deliberately avoids the `any`-typed
 * `fixture.nativeElement` and the null-typing pitfalls of `query(...)`.
 *
 * Effect flushing: `fixture.detectChanges()` flushes the directive's constructor
 * `effect()`. Because the faked `hasRole` return is non-reactive, reactive
 * re-evaluation is driven by mutating the reactive `currentUser` signal (which the
 * directive explicitly reads) AND adjusting the `hasRole` spy return BEFORE the
 * next `detectChanges()`.
 *
 * // MIGRATION: These tests verify the client-side UI gate that replaces the legacy
 * // server-side PortalSecurity.HasNecessaryPermission / IsInRoles checks
 * // (Library/Components/Security/PortalSecurity.vb L115-L135, L469-L535), including
 * // the `objUserInfo.IsSuperUser` short-circuit. UI gating is presentation-only;
 * // authoritative authorization remains enforced server-side.
 */
import { Component, WritableSignal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { AuthService } from '../../../core/auth/auth.service';
import { User } from '../../../core/models/user.model';
import { HasPermissionDirective, PermissionKey } from './has-permission.directive';

/**
 * Standalone host that embeds the directive through the `*appHasPermission`
 * structural microsyntax. `permission` is bound to the directive's required input;
 * `'EDIT'` is the gated key under test. (The `standalone` key is omitted — it is
 * the Angular 19 default and strictStandalone-compliant.)
 */
@Component({
  imports: [HasPermissionDirective],
  template: '<button *appHasPermission="permission" type="button">Action</button>',
})
class HostComponent {
  permission: PermissionKey = 'EDIT';
}

/**
 * Minimal typed surface of {@link AuthService} the directive depends on: the
 * `currentUser` reactive signal and the `hasRole` decision method. Typing the fake
 * explicitly keeps the suite free of `any`.
 */
interface FakeAuthService {
  currentUser: WritableSignal<User | null>;
  hasRole: jasmine.Spy;
}

/**
 * Build a {@link User} fixture. Property names match the real `User` contract:
 * `userID` / `portalID` carry the trailing-acronym casing emitted by the .NET 8
 * API's System.Text.Json camelCase policy (NOT `userId` / `portalId`).
 */
function buildUser(roles: string[], isSuperUser = false): User {
  return {
    userID: 1,
    username: 'tester',
    displayName: 'Test User',
    firstName: 'Test',
    lastName: 'User',
    email: 'tester@example.com',
    portalID: 0,
    isSuperUser,
    roles,
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

  it('maps the bound permission key to its role and passes that to AuthService.hasRole', () => {
    fakeAuthService.hasRole.and.returnValue(true);
    fixture.detectChanges();
    // The 'EDIT' permission key resolves to the 'Administrators' role via the
    // directive's key->role map; the mapped role (not the raw key) is what is
    // checked against AuthService.hasRole.
    expect(fakeAuthService.hasRole).toHaveBeenCalledWith('Administrators');
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
