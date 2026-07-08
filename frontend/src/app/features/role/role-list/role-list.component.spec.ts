import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';

import { RoleListComponent } from './role-list.component';
import { RoleService } from '../role.service';
import { Role } from '../../../core/models';

/**
 * Karma/Jasmine unit spec for {@link RoleListComponent} (Gate 4 deliverable).
 *
 * MIGRATION: verifies the behavior migrated from the legacy DotNetNuke Web Forms
 * roles screen (Website/admin/Security/Roles.ascx.vb): `BindData()` ->
 * `RoleController.GetPortalRoles(PortalId)` -> DataGrid render becomes
 * `loadRoles()` -> `RoleService.getRoles()` -> DataTable render; the
 * `ClientAPI.AddButtonConfirm(cmdDelete, "DeleteItem")` confirm-then-delete
 * postback becomes the confirmation-dialog -> `deleteRole()` flow; and the Edit
 * `ImageCommandColumn` (`EditUrl("RoleID")`) becomes edit navigation.
 *
 * Isolation strategy (no live HTTP): `RoleService` is a `jasmine.SpyObj` whose
 * methods return synchronous `of(...)` streams; `Router` is a spy asserted on
 * `navigate`; `ActivatedRoute` is a minimal stub (the component never reads route
 * params). The standalone component is registered via `imports` (Angular v19).
 */

/**
 * Build a fully-typed {@link Role} fixture. All 15 preserved-acronym fields are
 * present so the object satisfies the strict `Role` contract without casts; pass
 * `overrides` to vary individual fields per test.
 */
function makeRole(overrides: Partial<Role> = {}): Role {
  return {
    roleID: 1,
    portalID: 0,
    roleGroupID: -1,
    roleName: 'Test Role',
    description: 'A test role',
    isPublic: false,
    autoAssignment: false,
    serviceFee: 0,
    billingFrequency: 'N',
    billingPeriod: 1,
    trialFee: 0,
    trialPeriod: 0,
    trialFrequency: 'N',
    rsvpCode: '',
    iconFile: '',
    ...overrides,
  };
}

describe('RoleListComponent', () => {
  let roleServiceSpy: jasmine.SpyObj<RoleService>;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    roleServiceSpy = jasmine.createSpyObj<RoleService>('RoleService', ['getRoles', 'deleteRole']);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);

    // Safe defaults: an empty list load and a successful (void) delete. Individual
    // tests override getRoles/deleteRole as needed. Both emit synchronously via
    // of(...), so no fakeAsync/tick is required anywhere in this spec.
    roleServiceSpy.getRoles.and.returnValue(of([]));
    roleServiceSpy.deleteRole.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [RoleListComponent],
      providers: [
        { provide: RoleService, useValue: roleServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: ActivatedRoute, useValue: {} },
      ],
    }).compileComponents();
  });

  it('creates', () => {
    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('loads roles on init and exposes them + renders rows', () => {
    const rows = [
      makeRole({ roleID: 1, roleName: 'Administrators' }),
      makeRole({ roleID: 2, roleName: 'Registered Users' }),
    ];
    roleServiceSpy.getRoles.and.returnValue(of(rows));

    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges(); // ngOnInit -> loadRoles() (getRoles emits synchronously)
    fixture.detectChanges(); // flush the DataTable's OnPush input propagation
    const component = fixture.componentInstance;

    expect(roleServiceSpy.getRoles).toHaveBeenCalled();
    // Authoritative assertion: the signal holds the loaded rows.
    expect(component.roles().length).toBe(2);
    expect(component.loading()).toBeFalse();

    // Supporting assertion: the DataTable rendered one body row per role.
    const bodyRows = fixture.nativeElement.querySelectorAll('tbody tr[role="row"]');
    expect(bodyRows.length).toBe(2);
  });

  it('surfaces an error message when loading fails', () => {
    // MIGRATION: ApiService rethrows RFC 7807 ProblemDetails; the component reads err.title.
    roleServiceSpy.getRoles.and.returnValue(throwError(() => ({ title: 'Boom' })));

    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    expect(component.error()).toBe('Boom');
    expect(component.loading()).toBeFalse();
  });

  it('navigates to the edit form on the edit row action', () => {
    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.onRowAction({ action: 'edit', row: makeRole({ roleID: 7 }) });

    expect(routerSpy.navigate).toHaveBeenCalledWith(['/roles', 7]);
  });

  it('opens the confirmation dialog on the delete row action (no delete yet)', () => {
    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    const target = makeRole({ roleID: 5, roleName: 'Doomed' });
    component.onRowAction({ action: 'delete', row: target });

    // MIGRATION: the delete is deferred until the user confirms (AddButtonConfirm).
    expect(component.showDeleteDialog()).toBeTrue();
    expect(component.roleToDelete()).toEqual(target);
    expect(component.deleteMessage()).toContain('Doomed');
    expect(roleServiceSpy.deleteRole).not.toHaveBeenCalled();
  });

  it('deletes the pending role on confirm, resets state, and reloads', () => {
    roleServiceSpy.getRoles.and.returnValue(of([makeRole({ roleID: 5 })]));
    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    roleServiceSpy.getRoles.calls.reset(); // isolate the post-delete reload call

    component.roleToDelete.set(makeRole({ roleID: 5, roleName: 'Doomed' }));
    component.showDeleteDialog.set(true);
    component.confirmDelete();

    expect(roleServiceSpy.deleteRole).toHaveBeenCalledOnceWith(5);
    expect(component.roleToDelete()).toBeNull();
    expect(component.showDeleteDialog()).toBeFalse();
    expect(roleServiceSpy.getRoles).toHaveBeenCalledTimes(1); // reload after delete
  });

  it('does nothing on confirm when no role is pending', () => {
    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    roleServiceSpy.deleteRole.calls.reset();

    component.roleToDelete.set(null);
    component.confirmDelete();

    expect(roleServiceSpy.deleteRole).not.toHaveBeenCalled();
  });

  it('clears the pending role on cancel', () => {
    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.roleToDelete.set(makeRole({ roleID: 9 }));
    component.cancelDelete();

    expect(component.roleToDelete()).toBeNull();
  });

  it('navigates to the create form on Add Role', () => {
    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.onAddRole();

    expect(routerSpy.navigate).toHaveBeenCalledWith(['/roles', 'new']);
  });
});
