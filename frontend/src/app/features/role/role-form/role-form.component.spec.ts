import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { CurrentUser, Role } from '../../../core/models';
import { AuthService } from '../../../core/auth/auth.service';
import { RoleService } from '../role.service';
import { RoleFormComponent } from './role-form.component';

/**
 * Unit spec for {@link RoleFormComponent} — the migrated Angular 19 replacement for
 * the legacy DotNetNuke `Website/admin/Security/editroles.ascx` "Edit Role" Web Forms
 * screen (AAP §0.4.1 role feature). It exercises the component purely through its
 * PUBLIC API and its collaborators' spies, satisfying validation Gate 4
 * (`ng test --watch=false --browsers=ChromeHeadless --code-coverage`, 100% pass).
 *
 * Testing stack: Jasmine + Karma (the Angular default, AAP §0.5.1). The three
 * collaborators the component injects — `RoleService`, `Router`, and `ActivatedRoute`
 * — are fully mocked so NO real HTTP call and NO real navigation ever occurs; every
 * observable emits synchronously via `of(...)`, so no `fakeAsync`/`tick` is required.
 *
 * The child components the standalone `RoleFormComponent` imports (FormFieldComponent,
 * LoadingSpinnerComponent, ConfirmationDialogComponent) are presentation-only and
 * dependency-light, so they render live in TestBed (no override) for higher coverage.
 *
 * Lock-step rule (AAP §0.7.1 UI functional parity): the assertions below mirror the
 * component's verified contract — the dual create/edit behavior, the read-only role
 * name in edit mode, and the legacy `greaterThan(0)`/`required` validator keys derived
 * verbatim from `editroles.ascx`. If the component's public API changes, update this
 * spec in the same edit so the two never drift.
 */
describe('RoleFormComponent', () => {
  let roleServiceSpy: jasmine.SpyObj<RoleService>;
  let routerSpy: jasmine.SpyObj<Router>;

  /**
   * A complete `Role` read model used for edit-mode prefill. Field casing is preserved
   * EXACTLY as declared on the `Role` interface (acronyms `roleID`, `portalID`,
   * `roleGroupID`, `rsvpCode`) so the object satisfies the contract and compiles under
   * strict TypeScript.
   */
  const mockRole: Role = {
    roleID: 5,
    portalID: 0,
    roleGroupID: -1,
    roleName: 'Administrators',
    description: 'Portal administrators',
    isPublic: false,
    autoAssignment: false,
    serviceFee: 0,
    billingFrequency: 'N',
    billingPeriod: 1,
    trialFee: 0,
    trialPeriod: 1,
    trialFrequency: 'N',
    rsvpCode: '',
    iconFile: '',
  };

  /**
   * The authenticated admin whose portal context the create flow MUST adopt (C4).
   * `RolesController.Create` calls `RequirePortalAccess(dto.PortalID)`, so a portal
   * admin scoped to portal 7 must submit `portalID: 7` (submitting 0 => HTTP 403).
   * The component reads ONLY `currentUser().portalID`, so a portalID-bearing stub cast
   * suffices (the `as unknown as` cast mirrors the accepted pattern already used at the
   * bottom of this file for index-signature access).
   */
  const ADMIN_PORTAL_ID = 7;
  const mockCurrentUser = { portalID: ADMIN_PORTAL_ID } as unknown as CurrentUser;

  /**
   * DRY TestBed factory parameterized by the route `:id` param. Passing `null` yields
   * create mode (`convertToParamMap({})` -> `get('id')` returns `null`); passing an id
   * string yields edit mode. Returns a change-detected fixture (ngOnInit has run).
   */
  function setup(
    idParam: string | null,
    currentUser: CurrentUser | null = mockCurrentUser,
  ): ComponentFixture<RoleFormComponent> {
    roleServiceSpy = jasmine.createSpyObj<RoleService>('RoleService', [
      'getRole',
      'createRole',
      'updateRole',
      'deleteRole',
    ]);
    roleServiceSpy.getRole.and.returnValue(of(mockRole));
    roleServiceSpy.createRole.and.returnValue(of(mockRole));
    roleServiceSpy.updateRole.and.returnValue(of(mockRole));
    // `of(void 0)` is the correct emit for the `deleteRole(): Observable<void>` spy.
    roleServiceSpy.deleteRole.and.returnValue(of(void 0));

    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    // `convertToParamMap` builds a real `ParamMap`; the empty map (create mode) makes
    // `get('id')` return `null`, exactly what the component's `/^\d+$/` guard expects.
    const paramMap = convertToParamMap(idParam === null ? {} : { id: idParam });

    TestBed.configureTestingModule({
      // A standalone component goes in `imports`, NEVER `declarations`.
      imports: [RoleFormComponent],
      providers: [
        { provide: RoleService, useValue: roleServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap } } },
        // AuthService: the component reads only `currentUser()` (for the create
        // portal context, C4); a signal-backed stub suffices — no real HTTP/session.
        {
          provide: AuthService,
          useValue: { currentUser: signal<CurrentUser | null>(currentUser) },
        },
      ],
    });

    const fixture = TestBed.createComponent(RoleFormComponent);
    fixture.detectChanges(); // triggers ngOnInit (and edit-mode getRole prefill)
    return fixture;
  }

  describe('create mode (/roles/new)', () => {
    let fixture: ComponentFixture<RoleFormComponent>;
    let component: RoleFormComponent;

    beforeEach(() => {
      fixture = setup(null);
      component = fixture.componentInstance;
    });

    it('should create in create mode (isEdit=false, no prefill call)', () => {
      expect(component).toBeTruthy();
      expect(component.isEdit).toBeFalse();
      expect(component.roleId).toBeNull();
      expect(roleServiceSpy.getRole).not.toHaveBeenCalled();
    });

    it('should default roleGroupID=-1 and frequencies to N', () => {
      expect(component.form.controls.roleGroupID.value).toBe(-1);
      expect(component.form.controls.billingFrequency.value).toBe('N');
      expect(component.form.controls.trialFrequency.value).toBe('N');
    });

    it('should require roleName and NOT call createRole when invalid', () => {
      component.form.controls.roleName.setValue('');
      component.save();
      expect(component.form.controls.roleName.hasError('required')).toBeTrue();
      expect(roleServiceSpy.createRole).not.toHaveBeenCalled();
    });

    it('should call createRole with entered values and navigate to /roles', () => {
      component.form.controls.roleName.setValue('Editors');
      component.save();
      expect(roleServiceSpy.createRole).toHaveBeenCalledTimes(1);
      const body = roleServiceSpy.createRole.calls.mostRecent().args[0];
      expect(body.roleName).toBe('Editors');
      // C4: the create body MUST carry the authenticated admin's portal (7), NOT a
      // hardcoded 0 — otherwise the backend RequirePortalAccess check returns 403.
      expect(body.portalID).toBe(ADMIN_PORTAL_ID);
      expect(body.roleGroupID).toBe(-1);
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/roles']);
    });

    it('should source createRole portalID from the authenticated context (C4)', () => {
      // Regression guard for the finding this spec previously codified: the portalID
      // must come from AuthService.currentUser(), so it tracks the mocked admin's
      // portal and is never the old hardcoded 0.
      component.form.controls.roleName.setValue('Contributors');
      component.save();
      const body = roleServiceSpy.createRole.calls.mostRecent().args[0];
      expect(body.portalID).toBe(ADMIN_PORTAL_ID);
      expect(body.portalID).not.toBe(0);
    });

    it('should validate billingPeriod with greaterThan(0)', () => {
      component.form.controls.billingPeriod.setValue(0);
      expect(component.form.controls.billingPeriod.hasError('greaterThan')).toBeTrue();
    });
  });

  // Isolated describe (NO shared beforeEach): `setup(...)` runs exactly once inside the
  // test so it can override the authenticated user without re-configuring an already
  // instantiated TestBed.
  describe('create mode — host superuser portal fallback (C4)', () => {
    it('should fall back to portalID 0 when currentUser() has no portal context', () => {
      // The host superuser's currentUser() is null (no portal); the "?? 0" fallback
      // applies and RequirePortalAccess(0) still passes for a superuser.
      const fixture = setup(null, null);
      const component = fixture.componentInstance;
      component.form.controls.roleName.setValue('HostRole');
      component.save();
      expect(roleServiceSpy.createRole).toHaveBeenCalledTimes(1);
      const body = roleServiceSpy.createRole.calls.mostRecent().args[0];
      expect(body.portalID).toBe(0);
    });
  });

  describe('edit mode (/roles/:id)', () => {
    let fixture: ComponentFixture<RoleFormComponent>;
    let component: RoleFormComponent;

    beforeEach(() => {
      fixture = setup('5');
      component = fixture.componentInstance;
    });

    it('should be in edit mode and prefill from getRole', () => {
      expect(component.isEdit).toBeTrue();
      expect(component.roleId).toBe(5);
      expect(roleServiceSpy.getRole).toHaveBeenCalledWith(5);
      // getRawValue() includes the disabled roleName control set during prefill.
      expect(component.form.getRawValue().roleName).toBe('Administrators');
    });

    it('should disable roleName in edit mode (read-only parity)', () => {
      expect(component.form.controls.roleName.disabled).toBeTrue();
    });

    it('should call updateRole (without roleID/portalID) and navigate', () => {
      component.save();
      expect(roleServiceSpy.updateRole).toHaveBeenCalledTimes(1);
      const [id, body] = roleServiceSpy.updateRole.calls.mostRecent().args;
      expect(id).toBe(5);
      // UpdateRoleRequest is a CLOSED interface (no index signature); a direct
      // `as Record<string, unknown>` is rejected by strict TS (TS2352), so route
      // through `unknown` — the `any`-free remedy — to probe for the intentionally
      // absent roleID/portalID via bracket access (noPropertyAccessFromIndexSignature).
      expect((body as unknown as Record<string, unknown>)['roleID']).toBeUndefined();
      expect((body as unknown as Record<string, unknown>)['portalID']).toBeUndefined();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/roles']);
    });

    it('should delete via confirmDelete and navigate', () => {
      component.requestDelete();
      expect(component.showDeleteDialog()).toBeTrue();
      component.confirmDelete();
      expect(roleServiceSpy.deleteRole).toHaveBeenCalledWith(5);
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/roles']);
    });
  });

  it('cancel() navigates to /roles without saving', () => {
    const fixture = setup(null);
    fixture.componentInstance.cancel();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/roles']);
    expect(roleServiceSpy.createRole).not.toHaveBeenCalled();
    expect(roleServiceSpy.updateRole).not.toHaveBeenCalled();
  });
});
