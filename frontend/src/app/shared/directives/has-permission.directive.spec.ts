import { Component, WritableSignal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HasPermissionDirective } from './has-permission.directive';
import { AuthService } from '../../core/auth/auth.service';

/**
 * Dedicated spec for {@link HasPermissionDirective} -- the structural, role-gating
 * UI directive (QA Report 10 Issue 2, coverage priority: the directive sat at
 * ~4% coverage with no dedicated spec).
 *
 * MIGRATION: the directive reproduces the legacy DotNetNuke
 * `PortalSecurity.IsInRoles` decision (Library/Components/Security/PortalSecurity.vb,
 * AAP 0.6.4): show when the user `IsSuperUser` OR is in ANY required role, and
 * fail-closed otherwise. These tests assert that decision across the super-user
 * override, single/array/';'-/','-delimited role inputs, the fail-closed empty
 * case, and reactive re-evaluation when auth state changes -- proving the
 * show/hide branches directly (the finding flagged `has-permission` as
 * security-adjacent and specifically in need of assertion).
 *
 * `AuthService` is stubbed with writable signals for its two read selectors
 * (`isSuperUser`, `roles`) -- the only members the directive consumes -- so state
 * transitions are driven deterministically and the directive's own reactive
 * `effect`/`computed` wiring is exercised. Contributes to Gate 4
 * (ng test --code-coverage, 100% pass).
 */
@Component({
  imports: [HasPermissionDirective],
  template: `<div *appHasPermission="required" data-testid="gated">Gated content</div>`,
})
class HostComponent {
  required: string | string[] = '';
}

describe('HasPermissionDirective', () => {
  let fixture: ComponentFixture<HostComponent>;
  let isSuperUser: WritableSignal<boolean>;
  let roles: WritableSignal<string[]>;

  /** The gated element while rendered, or null when the directive has cleared the view. */
  function gatedEl(): HTMLElement | null {
    return fixture.nativeElement.querySelector('[data-testid="gated"]');
  }

  beforeEach(() => {
    isSuperUser = signal(false);
    roles = signal<string[]>([]);
    // The directive reads ONLY authService.isSuperUser() and authService.roles();
    // a minimal signal-backed stub exercises its reactive decision path.
    const authStub = { isSuperUser, roles } as unknown as AuthService;

    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [{ provide: AuthService, useValue: authStub }],
    });
    fixture = TestBed.createComponent(HostComponent);
  });

  it('renders for a super user even with no required roles (super-user override)', () => {
    isSuperUser.set(true);
    fixture.componentInstance.required = '';
    fixture.detectChanges();
    expect(gatedEl()).not.toBeNull();
  });

  it('renders when the user holds the single required role', () => {
    roles.set(['Administrators']);
    fixture.componentInstance.required = 'Administrators';
    fixture.detectChanges();
    expect(gatedEl()).not.toBeNull();
  });

  it('hides when the user lacks the required role', () => {
    roles.set(['Editors']);
    fixture.componentInstance.required = 'Administrators';
    fixture.detectChanges();
    expect(gatedEl()).toBeNull();
  });

  it('fail-closed: hides when no roles are required and the user is not a super user', () => {
    fixture.componentInstance.required = '';
    fixture.detectChanges();
    expect(gatedEl()).toBeNull();
  });

  it('array input: renders if the user holds ANY listed role (OR-semantics)', () => {
    roles.set(['Editors']);
    fixture.componentInstance.required = ['Administrators', 'Editors'];
    fixture.detectChanges();
    expect(gatedEl()).not.toBeNull();
  });

  it("';'-delimited string (legacy IsInRoles shape): renders if the user holds any role", () => {
    roles.set(['Editors']);
    fixture.componentInstance.required = 'Administrators;Editors';
    fixture.detectChanges();
    expect(gatedEl()).not.toBeNull();
  });

  it("','-delimited string: renders if the user holds any role", () => {
    roles.set(['Administrators']);
    fixture.componentInstance.required = 'Administrators,Editors';
    fixture.detectChanges();
    expect(gatedEl()).not.toBeNull();
  });

  it('reacts to auth-state changes (hidden -> shown when a granting role arrives)', () => {
    fixture.componentInstance.required = 'Administrators';
    fixture.detectChanges();
    expect(gatedEl()).toBeNull();

    // A login that grants the required role must reveal the gated view reactively.
    roles.set(['Administrators']);
    fixture.detectChanges();
    expect(gatedEl()).not.toBeNull();
  });

  it('reacts to auth-state changes (shown -> hidden on logout / role loss)', () => {
    roles.set(['Administrators']);
    fixture.componentInstance.required = 'Administrators';
    fixture.detectChanges();
    expect(gatedEl()).not.toBeNull();

    // Logout clears roles -> the gated view must be removed.
    roles.set([]);
    fixture.detectChanges();
    expect(gatedEl()).toBeNull();
  });
});
