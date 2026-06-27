// MIGRATION: Gate-4 unit tests for RoleAssignmentComponent, asserting parity with the verified
// behaviors of Website/admin/Security/SecurityRoles.ascx.vb: default-expiry by billing frequency
// (GetDates D/W/M/Y, L273-303), admin-account-date guard (cmdAdd_Click L523), add-vs-update labeling
// (grdUserRoles_ItemDataBound L641-664), and permission-guarded + confirmation-gated delete
// (CanRemoveUserFromRole L360-363 / grdUserRoles_Delete L565-589). User-role assignment WRITES are
// now wired to the backend assignment sub-resource (POST /api/roles/assignments,
// DELETE /api/roles/{roleId}/users/{userId}) via RoleService.assignUserRole / removeUserRole; these
// tests assert the write calls, the request shape, and the success / error feedback signals.
import { signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';

import { RoleAssignmentComponent } from './role-assignment.component';
import { RoleService } from '../role.service';
import { AuthService } from '../../../core/auth/auth.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import type { Role, UserRole } from '../../../core/models';

// MIGRATION: minimal shape of the authenticated principal the component reads for the tenant portalId
// (the REQUIRED query/body param on the protected role read + assignment endpoints, AAP Section 0.7.1).
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

// MIGRATION: an RFC 7807 ProblemDetails body (the backend ExceptionHandlingMiddleware / ApiControllerBase
// failure envelope, AAP Section 0.7.5). parseProblemDetails flattens title + detail + errors[] into the
// messages list the component surfaces; the first message becomes actionError.
function problemError(title: string, errors: string[] = []): HttpErrorResponse {
  return new HttpErrorResponse({
    status: 400,
    error: { type: 'about:blank', title, status: 400, errors },
  });
}

describe('RoleAssignmentComponent', () => {
  let fixture: ComponentFixture<RoleAssignmentComponent>;
  let component: RoleAssignmentComponent;

  let getByIdSpy: jasmine.Spy;
  let getUserRolesSpy: jasmine.Spy;
  let assignUserRoleSpy: jasmine.Spy;
  let updateUserRoleSpy: jasmine.Spy;
  let removeUserRoleSpy: jasmine.Spy;
  let currentUser: WritableSignal<StubUser | null>;

  beforeEach(async () => {
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(makeRole()));
    getUserRolesSpy = jasmine.createSpy('getUserRoles').and.returnValue(of([]));
    // MIGRATION: assignUserRole()/removeUserRole()/updateUserRole() are the IMPLEMENTED write contract
    // wired to the backend assignment sub-resource (Phase-5 RolesController endpoints). assignUserRole is
    // an UPSERT (POST /api/roles/assignments), so both the "Add User" and "Update Role" affordances map
    // to it; removeUserRole calls DELETE /api/roles/{roleId}/users/{userId}.
    assignUserRoleSpy = jasmine.createSpy('assignUserRole').and.returnValue(of(makeUserRole()));
    updateUserRoleSpy = jasmine.createSpy('updateUserRole').and.returnValue(of(makeUserRole()));
    removeUserRoleSpy = jasmine.createSpy('removeUserRole').and.returnValue(of(void 0));
    currentUser = signal<StubUser | null>({ portalId: 3 });

    const roleServiceMock = {
      getById: getByIdSpy,
      getUserRoles: getUserRolesSpy,
      assignUserRole: assignUserRoleSpy,
      updateUserRole: updateUserRoleSpy,
      removeUserRole: removeUserRoleSpy,
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

  it('ADMIN GUARD: clears effective/expiry dates for the Administrator account on the Administrator role and posts cleared dates', () => {
    fixture.componentRef.setInput('id', '2');
    fixture.componentRef.setInput('administratorId', 1);
    fixture.componentRef.setInput('administratorRoleId', 2);
    fixture.detectChanges();

    component.onUserChange(1);
    component.form.patchValue({ effectiveDate: '2025-01-01', expiryDate: '2025-12-31' });

    component.onAdd();

    // MIGRATION: the admin-account-date guard (cmdAdd_Click L523) clears both dates on the write path.
    expect(component.form.controls.effectiveDate.value).toBe('');
    expect(component.form.controls.expiryDate.value).toBe('');
    // MIGRATION: the cleared dates map to null (Null.NullDate) in the POSTed assignment request; the
    // portal scope travels in the body and is validated against the JWT claim server-side.
    expect(assignUserRoleSpy).toHaveBeenCalledWith({
      portalId: 3,
      userId: 1,
      roleId: 2,
      effectiveDate: null,
      expiryDate: null,
    });
    expect(component.actionSuccess()).toBe('The role assignment was saved.');
    expect(component.actionError()).toBeNull();
  });

  it('posts a valid (non-admin) assignment with the form dates, surfaces success, and reloads the grid', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onUserChange(99);
    component.form.patchValue({ effectiveDate: '2025-01-01', expiryDate: '2025-12-31' });

    component.onAdd();

    expect(assignUserRoleSpy).toHaveBeenCalledWith({
      portalId: 3,
      userId: 99,
      roleId: 5,
      effectiveDate: '2025-01-01',
      expiryDate: '2025-12-31',
    });
    expect(component.actionSuccess()).toBe('The role assignment was saved.');
    // MIGRATION: a successful write reloads the user's assignments so the grid reflects the persisted
    // state (getUserRoles is called once on user selection and again on the post-write reload).
    expect(getUserRolesSpy).toHaveBeenCalledTimes(2);
    expect(component.loading()).toBe(false);
  });

  it('maps empty form dates to null in the assignment request (Null.NullDate parity)', () => {
    getByIdSpy.and.returnValue(of(makeRole({ roleId: 5, billingPeriod: 0 })));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onUserChange(99);
    component.onAdd();

    expect(assignUserRoleSpy).toHaveBeenCalledWith({
      portalId: 3,
      userId: 99,
      roleId: 5,
      effectiveDate: null,
      expiryDate: null,
    });
  });

  it('does NOT post (and raises no feedback) when the form is invalid (no user selected)', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onAdd();

    // MIGRATION: an invalid form short-circuits before the write path (legacy Page.IsValid gate).
    expect(assignUserRoleSpy).not.toHaveBeenCalled();
    expect(component.actionSuccess()).toBeNull();
    expect(component.actionError()).toBeNull();
  });

  it('surfaces the backend failure message via actionError when the assignment write fails', () => {
    assignUserRoleSpy.and.returnValue(throwError(() => problemError('Assignment rejected', ['User is not active.'])));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.onUserChange(99);
    component.onAdd();

    // MIGRATION: the first parsed RFC 7807 message (the title) surfaces as the action error.
    expect(component.actionError()).toBe('Assignment rejected');
    expect(component.actionSuccess()).toBeNull();
    expect(component.loading()).toBe(false);
  });

  it('DELETE is permission-guarded: no dialog and no remove call for the Administrator in the Administrator role', () => {
    fixture.componentRef.setInput('id', '2');
    fixture.componentRef.setInput('administratorId', 1);
    fixture.componentRef.setInput('administratorRoleId', 2);
    fixture.detectChanges();

    getDataTable().delete.emit(makeUserRole({ userRoleId: 10, userId: 1, roleId: 2 }));
    fixture.detectChanges();

    expect(getDialog()).toBeNull();
    // MIGRATION: removing the Administrator from the Administrator role is blocked before any dialog or
    // write (CanRemoveUserFromRole L360-363, DNN-4285).
    expect(removeUserRoleSpy).not.toHaveBeenCalled();
  });

  it('DELETE is confirmation-gated: confirm removes the assignment and surfaces success', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    getDataTable().delete.emit(makeUserRole({ userRoleId: 42, userId: 99, roleId: 5 }));
    fixture.detectChanges();

    const dialog = getDialog();
    expect(dialog).not.toBeNull();
    expect(removeUserRoleSpy).not.toHaveBeenCalled();
    dialog!.confirm.emit();
    fixture.detectChanges();

    // MIGRATION: confirming closes the dialog and calls DELETE /api/roles/{roleId}/users/{userId}
    // (portalId, roleId, userId) -- the backend CanRemoveUserFromRole guard remains authoritative.
    expect(component.showDeleteConfirm()).toBe(false);
    expect(removeUserRoleSpy).toHaveBeenCalledWith(3, 5, 99);
    expect(component.actionSuccess()).toBe('The role assignment was removed.');
    expect(component.actionError()).toBeNull();
  });

  it('DELETE can be cancelled: dialog closes without calling removeUserRole', () => {
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    getDataTable().delete.emit(makeUserRole({ userRoleId: 42, userId: 99, roleId: 5 }));
    fixture.detectChanges();

    getDialog()!.cancel.emit();
    fixture.detectChanges();

    expect(getDialog()).toBeNull();
    // MIGRATION: cancelling closes the dialog and performs no write.
    expect(removeUserRoleSpy).not.toHaveBeenCalled();
  });

  it('surfaces the backend failure via actionError when the removal write fails (admin-lockout guard 400)', () => {
    removeUserRoleSpy.and.returnValue(
      throwError(() => problemError('Cannot remove the administrator from the Administrator role.')),
    );
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    getDataTable().delete.emit(makeUserRole({ userRoleId: 42, userId: 99, roleId: 5 }));
    fixture.detectChanges();
    getDialog()!.confirm.emit();
    fixture.detectChanges();

    expect(component.actionError()).toBe('Cannot remove the administrator from the Administrator role.');
    expect(component.actionSuccess()).toBeNull();
    expect(component.loading()).toBe(false);
  });
});
