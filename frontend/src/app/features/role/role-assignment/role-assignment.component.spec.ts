// MIGRATION: Gate-4 unit tests for RoleAssignmentComponent, asserting parity with the verified
// behaviors of Website/admin/Security/SecurityRoles.ascx.vb: default-expiry by billing frequency
// (GetDates D/W/M/Y, L273-303), admin-account-date guard (cmdAdd_Click L523), add-vs-update labeling
// (grdUserRoles_ItemDataBound L641-664), and permission-guarded + confirmation-gated delete
// (CanRemoveUserFromRole L360-363 / grdUserRoles_Delete L565-589). Assignment writes are PROVISIONAL.
import { signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';

import { RoleAssignmentComponent } from './role-assignment.component';
import { RoleService } from '../role.service';
import { AuthService } from '../../../core/auth/auth.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import type { Role, UserRole } from '../../../core/models';

// MIGRATION: minimal shape of the authenticated principal the component reads for the tenant portalId
// (the REQUIRED query param on the protected role read endpoints, AAP Section 0.7.1).
interface StubUser {
  portalId: number;
}

function makeRole(overrides: Partial<Role> = {}): Role {
  return {
    roleId: 5,
    portalId: 0,
    roleGroupId: null,
    roleName: 'Subscribers',
    description: null,
    serviceFee: 0,
    billingFrequency: 'N',
    trialPeriod: 0,
    trialFrequency: 'N',
    billingPeriod: 0,
    trialFee: 0,
    isPublic: true,
    autoAssignment: false,
    rsvpCode: null,
    iconFile: null,
    ...overrides,
  } as Role;
}

function makeUserRole(overrides: Partial<UserRole> = {}): UserRole {
  return {
    userRoleId: 1,
    userId: 99,
    roleId: 5,
    effectiveDate: null,
    expiryDate: null,
    isTrialUsed: false,
    subscribed: true,
    ...overrides,
  } as UserRole;
}

function expectedExpiry(code: string, period: number): string {
  const d = new Date();
  if (code === 'D') {
    d.setDate(d.getDate() + period);
  } else if (code === 'W') {
    d.setDate(d.getDate() + period * 7);
  } else if (code === 'M') {
    d.setMonth(d.getMonth() + period);
  } else if (code === 'Y') {
    d.setFullYear(d.getFullYear() + period);
  }
  const y = d.getFullYear();
  const m = `${d.getMonth() + 1}`.padStart(2, '0');
  const day = `${d.getDate()}`.padStart(2, '0');
  return `${y}-${m}-${day}`;
}

describe('RoleAssignmentComponent', () => {
  let fixture: ComponentFixture<RoleAssignmentComponent>;
  let component: RoleAssignmentComponent;

  let getByIdSpy: jasmine.Spy;
  let getUserRolesSpy: jasmine.Spy;
  let currentUser: WritableSignal<StubUser | null>;

  beforeEach(async () => {
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(makeRole()));
    getUserRolesSpy = jasmine.createSpy('getUserRoles').and.returnValue(of([]));
    currentUser = signal<StubUser | null>({ portalId: 3 });

    // MIGRATION: assignUserRole()/removeUserRole() were REMOVED from RoleService -- user-role assignment
    // WRITES are DEFERRED this phase (the frozen backend RolesController exposes NO assignment write
    // endpoint/DTO, AAP Section 0.3.4). The mock therefore exposes ONLY the read-only contract
    // (getById + getUserRoles), both of which require the tenant portalId from the authenticated
    // principal; onAdd / onConfirmDelete now raise the writesDeferred notice instead of writing.
    const roleServiceMock = {
      getById: getByIdSpy,
      getUserRoles: getUserRolesSpy,
    };
    const authStub = { currentUser };

    await TestBed.configureTestingModule({
      imports: [RoleAssignmentComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: RoleService, useValue: roleServiceMock },
        { provide: AuthService, useValue: authStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RoleAssignmentComponent);
    component = fixture.componentInstance;
  });

  function getDataTable(): DataTableComponent<UserRole> {
    const de = fixture.debugElement.query(By.directive(DataTableComponent));
    if (de === null) {
      throw new Error('app-data-table was not rendered');
    }
    return de.componentInstance as DataTableComponent<UserRole>;
  }

  function getDialog(): ConfirmationDialogComponent | null {
    const de = fixture.debugElement.query(By.directive(ConfirmationDialogComponent));
    return de ? (de.componentInstance as ConfirmationDialogComponent) : null;
  }

  it('should create', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('loads the fixed role on init (role-focused) and seeds the form roleId', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();
    // MIGRATION: the role load carries the required tenant portalId (AAP 0.7.1), sourced from the
    // authenticated principal (currentUser.portalId === 3 here).
    expect(getByIdSpy).toHaveBeenCalledWith(5, 3);
    expect(component.role()?.roleId).toBe(5);
    expect(component.form.controls.roleId.value).toBe(5);
    expect(component.mode()).toBe('role');
  });

  (['D', 'W', 'M', 'Y'] as const).forEach((code) => {
    it(`computes the default expiry for billing frequency "${code}" on a new assignment (GetDates)`, () => {
      getByIdSpy.and.returnValue(of(makeRole({ roleId: 5, billingPeriod: 2, billingFrequency: code })));
      fixture.componentRef.setInput('id', '5');
      fixture.detectChanges();

      component.onUserChange(99);

      expect(component.form.controls.expiryDate.value).toBe(expectedExpiry(code, 2));
      // MIGRATION: effective date stays empty for a new assignment (L273-303).
      expect(component.form.controls.effectiveDate.value).toBe('');
    });
  });

  it('does NOT compute a default expiry when billingPeriod is 0', () => {
    getByIdSpy.and.returnValue(of(makeRole({ roleId: 5, billingPeriod: 0, billingFrequency: 'M' })));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onUserChange(99);

    expect(component.form.controls.expiryDate.value).toBe('');
    expect(component.form.controls.effectiveDate.value).toBe('');
  });

  it('shows the stored dates when an assignment already exists (GetDates existing branch)', () => {
    getUserRolesSpy.and.returnValue(
      of([makeUserRole({ userId: 99, roleId: 5, effectiveDate: '2024-01-01', expiryDate: '2024-12-31' })]),
    );
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onUserChange(99);

    // MIGRATION: the assignments read carries the required tenant portalId (AAP 0.7.1) from the principal.
    expect(getUserRolesSpy).toHaveBeenCalledWith(99, 3);
    expect(component.form.controls.effectiveDate.value).toBe('2024-01-01');
    expect(component.form.controls.expiryDate.value).toBe('2024-12-31');
  });

  it('labels the action "Update Role" when the selected user already holds the role', () => {
    getUserRolesSpy.and.returnValue(of([makeUserRole({ userId: 99, roleId: 5 })]));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onUserChange(99);
    expect(component.addLabel()).toBe('Update Role');
  });

  it('labels the action "Add User" (role-focused) when the selected user is not yet in the role', () => {
    getUserRolesSpy.and.returnValue(of([makeUserRole({ userId: 99, roleId: 5 })]));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onUserChange(77);
    expect(component.addLabel()).toBe('Add User');
  });

  it('ADMIN GUARD: clears effective/expiry dates for the Administrator account on the Administrator role', () => {
    fixture.componentRef.setInput('id', '2');
    fixture.componentRef.setInput('administratorId', 1);
    fixture.componentRef.setInput('administratorRoleId', 2);
    fixture.detectChanges();

    component.onUserChange(1);
    component.form.patchValue({ effectiveDate: '2025-01-01', expiryDate: '2025-12-31' });

    expect(component.writesDeferred()).toBe(false);
    component.onAdd();

    // MIGRATION: the admin-account-date guard (cmdAdd_Click L523) still clears both dates on the
    // write path -- this client-parity behavior is preserved.
    expect(component.form.controls.effectiveDate.value).toBe('');
    expect(component.form.controls.expiryDate.value).toBe('');
    // MIGRATION: the assignment WRITE is DEFERRED (frozen backend has NO assignment endpoint/DTO,
    // AAP 0.3.4) -- onAdd raises the deferral notice instead of POSTing. The validated userId/roleId
    // and the admin guard above are preserved for parity.
    expect(component.writesDeferred()).toBe(true);
  });

  it('defers the assignment WRITE for a valid (non-admin) add and raises the notice', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onUserChange(99);
    expect(component.writesDeferred()).toBe(false);
    component.onAdd();

    // MIGRATION: a valid add clears the form/identifier guards, then raises the deferral notice -- the
    // assignment write endpoint is DEFERRED this phase (AAP 0.3.4). No service write method is invoked.
    expect(component.writesDeferred()).toBe(true);
  });

  it('does NOT proceed (no deferral notice) when the form is invalid (no user selected)', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onAdd();

    // MIGRATION: an invalid form short-circuits before the write/deferral path (legacy Page.IsValid gate).
    expect(component.writesDeferred()).toBe(false);
  });

  it('DELETE is permission-guarded: no dialog/deferral for the Administrator in the Administrator role', () => {
    fixture.componentRef.setInput('id', '2');
    fixture.componentRef.setInput('administratorId', 1);
    fixture.componentRef.setInput('administratorRoleId', 2);
    fixture.detectChanges();

    getDataTable().delete.emit(makeUserRole({ userRoleId: 10, userId: 1, roleId: 2 }));
    fixture.detectChanges();

    expect(getDialog()).toBeNull();
    // MIGRATION: removing the Administrator from the Administrator role is blocked before any
    // dialog/deferral (CanRemoveUserFromRole L360-363, DNN-4285).
    expect(component.writesDeferred()).toBe(false);
  });

  it('DELETE is confirmation-gated: opens the dialog and defers the removal on confirm', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    getDataTable().delete.emit(makeUserRole({ userRoleId: 42, userId: 99, roleId: 5 }));
    fixture.detectChanges();

    const dialog = getDialog();
    expect(dialog).not.toBeNull();
    expect(component.writesDeferred()).toBe(false);
    dialog!.confirm.emit();
    fixture.detectChanges();

    // MIGRATION: confirming closes the dialog and raises the deferral notice; the removal WRITE is
    // DEFERRED (frozen backend has NO assignment endpoint this phase, AAP 0.3.4).
    expect(component.showDeleteConfirm()).toBe(false);
    expect(component.writesDeferred()).toBe(true);
  });

  it('DELETE can be cancelled: dialog closes without deferral', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    getDataTable().delete.emit(makeUserRole({ userRoleId: 42, userId: 99, roleId: 5 }));
    fixture.detectChanges();

    getDialog()!.cancel.emit();
    fixture.detectChanges();

    expect(getDialog()).toBeNull();
    // MIGRATION: cancelling closes the dialog and raises NO deferral notice (no write attempted).
    expect(component.writesDeferred()).toBe(false);
  });
});
