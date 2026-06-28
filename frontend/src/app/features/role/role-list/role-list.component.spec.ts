// MIGRATION: Gate-4 unit tests for RoleListComponent, asserting parity with the legacy behaviors of
// Website/admin/Security/Roles.ascx.vb (pageSize=20, zero-based paging, portalId-scoped list,
// confirm-before-delete, and system-role (Administrators / Registered Users) delete protection).
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal, WritableSignal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { HttpErrorResponse } from '@angular/common/http';
import { NEVER, of, throwError } from 'rxjs';

import { RoleListComponent } from './role-list.component';
import { RoleService } from '../role.service';
import { AuthService } from '../../../core/auth/auth.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
// MIGRATION: `CurrentUser` is exported from core/models (auth.model, re-exported by the barrel), NOT from
// auth.service. `Paged`/`Role` likewise come from the barrel.
import type { CurrentUser, Paged, Role, ProblemDetails } from '../../../core/models';

function makeRole(overrides: Partial<Role> = {}): Role {
  // Only fields exercised by the component are set explicitly; the cast keeps the factory resilient to
  // the full Role shape without using `any`.
  return {
    roleId: 1,
    portalId: 7,
    roleName: 'Editors',
    description: 'Content editors',
    isPublic: true,
    autoAssignment: false,
    ...overrides,
  } as Role;
}

function makeUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    userId: 1,
    username: 'admin',
    email: 'admin@example.com',
    displayName: 'Admin User',
    firstName: 'Admin',
    lastName: 'User',
    fullName: 'Admin User',
    isSuperUser: false,
    portalId: 7,
    roles: ['Administrators'],
    ...overrides,
  };
}

describe('RoleListComponent', () => {
  let fixture: ComponentFixture<RoleListComponent>;
  let component: RoleListComponent;

  let rolesSig: WritableSignal<Role[]>;
  let loadingSig: WritableSignal<boolean>;
  let totalCountSig: WritableSignal<number>;
  let currentUserSig: WritableSignal<CurrentUser | null>;

  let listSpy: jasmine.Spy;
  let deleteSpy: jasmine.Spy;

  beforeEach(async () => {
    rolesSig = signal<Role[]>([makeRole({ roleId: 1 }), makeRole({ roleId: 2 })]);
    loadingSig = signal<boolean>(false);
    totalCountSig = signal<number>(2);
    currentUserSig = signal<CurrentUser | null>(makeUser({ portalId: 7 }));

    // MIGRATION: align the spy return to the REAL Paged<Role> shape from core/models/api-envelope.model
    // ({ items, totalCount, pageIndex, pageSize, totalPages, hasPreviousPage, hasNextPage }).
    const emptyPage: Paged<Role> = {
      items: [],
      totalCount: 0,
      pageIndex: 0,
      pageSize: 20,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
    };

    listSpy = jasmine.createSpy('list').and.returnValue(of(emptyPage));
    deleteSpy = jasmine.createSpy('delete').and.returnValue(of(void 0));

    const roleServiceMock = {
      roles: rolesSig,
      loading: loadingSig,
      totalCount: totalCountSig,
      selected: signal<Role | null>(null),
      list: listSpy,
      delete: deleteSpy,
      getById: jasmine.createSpy('getById').and.returnValue(of(makeRole())),
      create: jasmine.createSpy('create').and.returnValue(of(makeRole())),
      update: jasmine.createSpy('update').and.returnValue(of(makeRole())),
      // MIGRATION: [QA F3 #4] expose the error() signal the list template now binds via [error]
      // so the shared data-table can render the RFC 7807 banner. Defaults to null (no error).
      error: signal<ProblemDetails | null>(null),
    };

    const authMock = {
      currentUser: currentUserSig,
      isAuthenticated: signal<boolean>(true),
      accessToken: signal<string | null>('test-token'),
    };

    await TestBed.configureTestingModule({
      imports: [RoleListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: RoleService, useValue: roleServiceMock },
        { provide: AuthService, useValue: authMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RoleListComponent);
    component = fixture.componentInstance;
  });

  function getDataTable(): DataTableComponent<Role> {
    const de = fixture.debugElement.query(By.directive(DataTableComponent));
    if (de === null) {
      throw new Error('app-data-table was not rendered');
    }
    return de.componentInstance as DataTableComponent<Role>;
  }

  function getDialog(): ConfirmationDialogComponent | null {
    const de = fixture.debugElement.query(By.directive(ConfirmationDialogComponent));
    return de ? (de.componentInstance as ConfirmationDialogComponent) : null;
  }

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('loads roles on init scoped by the current user portalId, zero-based page 0, pageSize 20', () => {
    fixture.detectChanges();
    // MIGRATION: PortalId scoping (AAP Section 0.7.1) -- list(portalId, pageIndex, pageSize).
    expect(listSpy).toHaveBeenCalledWith(7, 0, 20);
  });

  it('passes pageSize=20, zero-based currentPage, and totalRecords to the data-table', () => {
    fixture.detectChanges();
    const dt = getDataTable();
    expect(dt.pageSize()).toBe(20);
    expect(dt.currentPage()).toBe(0);
    expect(dt.totalRecords()).toBe(2);
  });

  it('does not surface a view/detail action (roles have no detail route)', () => {
    fixture.detectChanges();
    expect(getDataTable().showView()).toBeFalse();
  });

  it('pageChange keeps the zero-based index (no +/-1) and reloads scoped by portalId', () => {
    fixture.detectChanges();
    listSpy.calls.reset();
    getDataTable().pageChange.emit(2);
    expect(listSpy).toHaveBeenCalledWith(7, 2, 20);
  });

  it('navigates to the edit route on the edit output', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigate');
    getDataTable().edit.emit(makeRole({ roleId: 9 }));
    expect(navSpy).toHaveBeenCalledWith(['/roles', 9, 'edit']);
  });

  it('navigates to the new-role route from the New button', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigate');
    fixture.debugElement.query(By.css('.btn--primary')).nativeElement.click();
    expect(navSpy).toHaveBeenCalledWith(['/roles', 'new']);
  });

  it('delete opens the confirmation dialog and confirm deletes by roleId with the tenant portalId', () => {
    fixture.detectChanges();
    getDataTable().delete.emit(makeRole({ roleId: 9, roleName: 'Editors' }));
    fixture.detectChanges();
    const dialog = getDialog();
    expect(dialog).not.toBeNull();
    dialog!.confirm.emit();
    // MIGRATION: DELETE /roles/{id} carries the required tenant portalId (AAP 0.7.1) as the 2nd arg,
    // sourced from the authenticated principal (currentUser.portalId === 7 here); roleId 9 is distinct
    // from portalId 7 to make the argument order unambiguous.
    expect(deleteSpy).toHaveBeenCalledWith(9, 7);
  });

  it('cancel closes the confirmation dialog without deleting', () => {
    fixture.detectChanges();
    getDataTable().delete.emit(makeRole({ roleId: 7, roleName: 'Editors' }));
    fixture.detectChanges();
    getDialog()!.cancel.emit();
    fixture.detectChanges();
    expect(getDialog()).toBeNull();
    expect(deleteSpy).not.toHaveBeenCalled();
  });

  it('does NOT open the dialog or delete a system role (Administrators), and surfaces a reason', () => {
    fixture.detectChanges();
    getDataTable().delete.emit(makeRole({ roleId: 3, roleName: 'Administrators' }));
    fixture.detectChanges();
    expect(getDialog()).toBeNull();
    expect(deleteSpy).not.toHaveBeenCalled();
    // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #20] the protected-role delete is no longer a SILENT
    // no-op; the operator is told why through the existing actionError banner (role="alert").
    expect(component.actionError()).toBe(
      'System roles (Administrators and Registered Users) cannot be deleted.',
    );
  });

  it('does NOT open the dialog or delete a system role (Registered Users), and surfaces a reason', () => {
    fixture.detectChanges();
    getDataTable().delete.emit(makeRole({ roleId: 4, roleName: 'Registered Users' }));
    fixture.detectChanges();
    expect(getDialog()).toBeNull();
    expect(deleteSpy).not.toHaveBeenCalled();
    // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #20] user-facing reason for the Registered Users role too.
    expect(component.actionError()).toBe(
      'System roles (Administrators and Registered Users) cannot be deleted.',
    );
  });

  // MIGRATION: [QA F7 — Issue #3] cover the previously-untested delete failure / re-entrancy / no-pending /
  // unauthenticated branches that drove role-list's branch coverage down to 33%.

  it('surfaces a friendly RFC 7807 message and closes the dialog when DELETE fails', () => {
    deleteSpy.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            error: { title: 'Conflict', detail: 'Role is in use.' },
            status: 409,
            statusText: 'Conflict',
          }),
      ),
    );
    fixture.detectChanges();
    getDataTable().delete.emit(makeRole({ roleId: 9, roleName: 'Editors' }));
    fixture.detectChanges();
    getDialog()!.confirm.emit();
    fixture.detectChanges();

    // firstMessage -> parseProblemDetails -> messages[0] is the ProblemDetails title.
    expect(component.actionError()).toBe('Conflict');
    expect(component.pendingDelete()).toBeNull();
    expect(component.deleting()).toBeFalse();
    expect(getDialog()).toBeNull();
  });

  it('falls back to the default message when a transport failure carries no RFC 7807 body', () => {
    deleteSpy.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            error: new ProgressEvent('error'), // status-0 transport failure -> no parseable messages
            status: 0,
            statusText: 'Unknown Error',
          }),
      ),
    );
    fixture.detectChanges();
    getDataTable().delete.emit(makeRole({ roleId: 9, roleName: 'Editors' }));
    fixture.detectChanges();
    getDialog()!.confirm.emit();
    fixture.detectChanges();

    expect(component.actionError()).toBe('The role could not be deleted. Please try again.');
    expect(component.pendingDelete()).toBeNull();
  });

  it('fires exactly ONE delete when Confirm is clicked repeatedly while a delete is in flight', () => {
    deleteSpy.and.returnValue(NEVER); // never completes -> the request stays in flight, deleting() stays true
    fixture.detectChanges();
    getDataTable().delete.emit(makeRole({ roleId: 9, roleName: 'Editors' }));
    fixture.detectChanges();
    const dialog = getDialog()!;

    dialog.confirm.emit();
    dialog.confirm.emit();
    dialog.confirm.emit();

    expect(deleteSpy).toHaveBeenCalledTimes(1);
    expect(component.deleting()).toBeTrue();
  });

  it('onConfirmDelete is a no-op when there is no pending delete', () => {
    fixture.detectChanges();
    // No delete was requested, so pendingDelete() is null and onConfirmDelete must short-circuit.
    (component as unknown as { onConfirmDelete(): void }).onConfirmDelete();
    expect(deleteSpy).not.toHaveBeenCalled();
  });

  it('falls back to portalId 0 on list and delete when there is no authenticated user', () => {
    currentUserSig.set(null);
    fixture.detectChanges(); // ngOnInit -> load() with portalId fallback 0
    expect(listSpy).toHaveBeenCalledWith(0, 0, 20);

    getDataTable().delete.emit(makeRole({ roleId: 9, roleName: 'Editors' }));
    fixture.detectChanges();
    getDialog()!.confirm.emit();
    expect(deleteSpy).toHaveBeenCalledWith(9, 0);
  });
});
