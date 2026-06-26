// MIGRATION: Gate-4 unit tests for RoleAssignmentComponent, asserting parity with the verified
// behaviors of Website/admin/Security/SecurityRoles.ascx.vb: default-expiry by billing frequency
// (GetDates D/W/M/Y, L273-303), admin-account-date guard (cmdAdd_Click L523), add-vs-update labeling
// (grdUserRoles_ItemDataBound L641-664), and permission-guarded + confirmation-gated delete
// (CanRemoveUserFromRole L360-363 / grdUserRoles_Delete L565-589). Assignment writes are PROVISIONAL.
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';

import { RoleAssignmentComponent } from './role-assignment.component';
import { RoleService } from '../role.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import type { Role, UserRole } from '../../../core/models';

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
  let assignSpy: jasmine.Spy;
  let removeSpy: jasmine.Spy;

  beforeEach(async () => {
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(makeRole()));
    getUserRolesSpy = jasmine.createSpy('getUserRoles').and.returnValue(of([]));
    assignSpy = jasmine.createSpy('assignUserRole').and.returnValue(of(makeUserRole()));
    removeSpy = jasmine.createSpy('removeUserRole').and.returnValue(of(void 0));

    const roleServiceMock = {
      getById: getByIdSpy,
      getUserRoles: getUserRolesSpy,
      assignUserRole: assignSpy,
      removeUserRole: removeSpy,
    };

    await TestBed.configureTestingModule({
      imports: [RoleAssignmentComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: RoleService, useValue: roleServiceMock },
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
    expect(getByIdSpy).toHaveBeenCalledWith(5);
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

    component.onAdd();

    expect(component.form.controls.effectiveDate.value).toBe('');
    expect(component.form.controls.expiryDate.value).toBe('');
    expect(assignSpy).toHaveBeenCalledWith(
      jasmine.objectContaining({ userId: 1, roleId: 2, effectiveDate: null, expiryDate: null }),
    );
  });

  it('submits a provisional assignment for a valid (non-admin) add', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onUserChange(99);
    component.onAdd();

    expect(assignSpy).toHaveBeenCalledWith(jasmine.objectContaining({ userId: 99, roleId: 5 }));
  });

  it('does NOT submit when the form is invalid (no user selected)', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onAdd();

    expect(assignSpy).not.toHaveBeenCalled();
  });

  it('DELETE is permission-guarded: no dialog/removal for the Administrator in the Administrator role', () => {
    fixture.componentRef.setInput('id', '2');
    fixture.componentRef.setInput('administratorId', 1);
    fixture.componentRef.setInput('administratorRoleId', 2);
    fixture.detectChanges();

    getDataTable().delete.emit(makeUserRole({ userRoleId: 10, userId: 1, roleId: 2 }));
    fixture.detectChanges();

    expect(getDialog()).toBeNull();
    expect(removeSpy).not.toHaveBeenCalled();
  });

  it('DELETE is confirmation-gated: opens the dialog and removes on confirm', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    getDataTable().delete.emit(makeUserRole({ userRoleId: 42, userId: 99, roleId: 5 }));
    fixture.detectChanges();

    const dialog = getDialog();
    expect(dialog).not.toBeNull();
    dialog!.confirm.emit();

    expect(removeSpy).toHaveBeenCalledWith(42);
  });

  it('DELETE can be cancelled: dialog closes without removing', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    getDataTable().delete.emit(makeUserRole({ userRoleId: 42, userId: 99, roleId: 5 }));
    fixture.detectChanges();

    getDialog()!.cancel.emit();
    fixture.detectChanges();

    expect(getDialog()).toBeNull();
    expect(removeSpy).not.toHaveBeenCalled();
  });
});
