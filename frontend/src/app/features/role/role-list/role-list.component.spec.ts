import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';

import { RoleListComponent } from './role-list.component';
import { RoleService } from '../role.service';
import { Role } from '../../../core/models';
import { DEFAULT_LIST_PAGE_SIZE } from '../../../core/services/api.service';

/**
 * Karma/Jasmine unit spec for {@link RoleListComponent} (Gate 4 deliverable).
 *
 * MIGRATION: verifies the behavior migrated from the legacy DotNetNuke Web Forms
 * roles screen (Website/admin/Security/Roles.ascx.vb): `BindData()` ->
 * `RoleController.GetPortalRoles(PortalId)` -> DataGrid render becomes
 * `loadRoles()` -> `RoleService.getRolesWithMeta()` -> DataTable render; the
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
    // MIGRATION (QA Issues 3 & 13): the list consumes the getRolesWithMeta ({ data, meta })
    // variant so it can read meta.totalCount to drive the server-side pager (totalItems).
    roleServiceSpy = jasmine.createSpyObj<RoleService>('RoleService', ['getRolesWithMeta', 'deleteRole']);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);

    // Safe defaults: an empty server page load and a successful (void) delete. Individual
    // tests override getRolesWithMeta/deleteRole as needed. Both emit synchronously via
    // of(...), so no fakeAsync/tick is required anywhere in this spec.
    roleServiceSpy.getRolesWithMeta.and.returnValue(of({ data: [], meta: { totalCount: 0 } }));
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

  it('loads the first server page of roles on init and exposes them + renders rows', () => {
    const rows = [
      makeRole({ roleID: 1, roleName: 'Administrators' }),
      makeRole({ roleID: 2, roleName: 'Registered Users' }),
    ];
    roleServiceSpy.getRolesWithMeta.and.returnValue(of({ data: rows, meta: { totalCount: 2 } }));

    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges(); // ngOnInit -> loadRoles() (getRolesWithMeta emits synchronously)
    fixture.detectChanges(); // flush the DataTable's OnPush input propagation
    const component = fixture.componentInstance;

    expect(roleServiceSpy.getRolesWithMeta).toHaveBeenCalled();
    // MIGRATION (QA Issue 13): the load requests server page 1 with the default per-page size
    // and an empty query. RolesController now accepts ?query=, so roles are searched/paged
    // server-side rather than loaded as a single first window and filtered client-side.
    expect(roleServiceSpy.getRolesWithMeta).toHaveBeenCalledWith({ query: '', page: 1, pageSize: DEFAULT_LIST_PAGE_SIZE });
    // Authoritative assertion: the signal holds the loaded rows.
    expect(component.roles().length).toBe(2);
    expect(component.loading()).toBeFalse();

    // Supporting assertion: the DataTable rendered one body row per role.
    const bodyRows = fixture.nativeElement.querySelectorAll('tbody tr[role="row"]');
    expect(bodyRows.length).toBe(2);
  });

  it('surfaces an error message when loading fails', () => {
    // MIGRATION: ApiService rethrows RFC 7807 ProblemDetails; the component reads err.title.
    roleServiceSpy.getRolesWithMeta.and.returnValue(throwError(() => ({ title: 'Boom' })));

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
    roleServiceSpy.getRolesWithMeta.and.returnValue(of({ data: [makeRole({ roleID: 5 })], meta: { totalCount: 1 } }));
    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    roleServiceSpy.getRolesWithMeta.calls.reset(); // isolate the post-delete reload call

    component.roleToDelete.set(makeRole({ roleID: 5, roleName: 'Doomed' }));
    component.showDeleteDialog.set(true);
    component.confirmDelete();

    expect(roleServiceSpy.deleteRole).toHaveBeenCalledOnceWith(5);
    expect(component.roleToDelete()).toBeNull();
    expect(component.showDeleteDialog()).toBeFalse();
    expect(roleServiceSpy.getRolesWithMeta).toHaveBeenCalledTimes(1); // reload after delete
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

  // MIGRATION (QA Issue 13, ROOT CAUSE): the roles endpoint originally had NO server-side
  // search and RoleListComponent never wired (filterChange), so a role beyond the loaded
  // window could not be found. It now emits a server-side query and re-queries from page 1.
  it('applies a server-side search and resets to page 1 on filterChange', () => {
    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges(); // initial load
    const component = fixture.componentInstance;
    component.page.set(4); // simulate the operator having paged forward first
    roleServiceSpy.getRolesWithMeta.calls.reset();

    component.onFilter({ term: 'admin' });

    expect(component.page()).toBe(1);
    expect(component.query()).toBe('admin');
    expect(roleServiceSpy.getRolesWithMeta).toHaveBeenCalledWith({ query: 'admin', page: 1, pageSize: DEFAULT_LIST_PAGE_SIZE });
  });

  // MIGRATION (QA Issues 3 & 13): a pageChange updates the page signal and re-queries the
  // server for that page, so roles beyond the first page are reachable (server-side paging).
  it('reloads the requested server page when the table emits pageChange', () => {
    const fixture = TestBed.createComponent(RoleListComponent);
    fixture.detectChanges(); // initial load (page 1)
    const component = fixture.componentInstance;
    roleServiceSpy.getRolesWithMeta.calls.reset();

    component.onPageChange({ page: 2, pageSize: DEFAULT_LIST_PAGE_SIZE });

    expect(component.page()).toBe(2);
    expect(roleServiceSpy.getRolesWithMeta).toHaveBeenCalledWith({ query: '', page: 2, pageSize: DEFAULT_LIST_PAGE_SIZE });
  });
});
