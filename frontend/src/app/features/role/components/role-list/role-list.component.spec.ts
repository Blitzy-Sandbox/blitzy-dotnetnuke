import { signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { RoleListComponent } from './role-list.component';
import { RoleService } from '../../services';
import type { Role } from '../../models';
import { AuthService } from '../../../../core/auth/auth.service';
import type { User } from '../../../../core/models/user.model';
import type { ProblemDetails } from '../../../../core/services/api.service';

// Typed Role factory mirroring the on-disk wire contract in `../../models` (role.model.ts). All 15
// fields are supplied so the literal satisfies the `Role` interface with no excess/missing members;
// the 14th field is `rsvpCode` (System.Text.Json camelCase lowercases the whole "RSVP" acronym run,
// then "Code" keeps its capital C) — any other casing would not satisfy the interface.
function makeRole(overrides: Partial<Role> = {}): Role {
  return {
    roleID: 1,
    portalID: 0,
    roleGroupID: null,
    roleName: 'Subscribers',
    description: 'Subscriber role',
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
  };
}

// Fully-populated `User` matching the on-disk core/models/user.model.ts contract: the acronym-prefixed
// ids serialize as `userID`/`portalID`/`affiliateID`, and every (always-present) member of the
// flattened UserDto is supplied so the literal satisfies the interface with no excess. The component
// reads `currentUser()?.portalID` when loading roles, so `portalID` (7) is the value asserted against
// `getRoles`.
const mockUser: User = {
  userID: 1,
  portalID: 7,
  affiliateID: null,
  username: 'admin',
  displayName: 'Administrator',
  email: 'admin@example.com',
  firstName: 'Super',
  lastName: 'User',
  fullName: 'Super User',
  isSuperUser: true,
  approved: true,
  updatePassword: false,
  roles: ['Administrators'],
  createdDate: null,
  lastLoginDate: null,
  lastPasswordChangeDate: null,
  lastActivityDate: null,
};

describe('RoleListComponent', () => {
  let component: RoleListComponent;
  let fixture: ComponentFixture<RoleListComponent>;
  let roleService: jasmine.SpyObj<RoleService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    roleService = jasmine.createSpyObj<RoleService>('RoleService', ['getRoles', 'deleteRole']);
    roleService.getRoles.and.returnValue(of([makeRole()]));
    roleService.deleteRole.and.returnValue(of(void 0));

    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.returnValue(Promise.resolve(true));

    const authStub = {
      currentUser: signal<User | null>(mockUser),
      hasRole: (_role: string): boolean => true,
    };

    await TestBed.configureTestingModule({
      imports: [RoleListComponent],
      providers: [
        { provide: RoleService, useValue: roleService },
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: authStub as unknown as AuthService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RoleListComponent);
    component = fixture.componentInstance;
  });

  it('should create and render the data table', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    const table = fixture.nativeElement.querySelector('app-data-table');
    expect(table).toBeTruthy();
  });

  it('loads roles for the current portal on init', () => {
    component.ngOnInit();
    expect(roleService.getRoles).toHaveBeenCalledWith(mockUser.portalID);
    expect(component.roles().length).toBe(1);
  });

  it('builds role-group filters from the loaded roles', () => {
    roleService.getRoles.and.returnValue(
      of([
        makeRole({ roleID: 1, roleGroupID: null }),
        makeRole({ roleID: 2, roleGroupID: 5 }),
        makeRole({ roleID: 3, roleGroupID: 5 }),
      ]),
    );
    component.loadRoles();
    expect(component.filters()).toEqual(['All Roles', 'Global Roles', 'Group 5']);
  });

  it('filters rows by role group (all, global, specific group)', () => {
    const roles = [
      makeRole({ roleID: 1, roleGroupID: null }),
      makeRole({ roleID: 2, roleGroupID: -1 }),
      makeRole({ roleID: 3, roleGroupID: 5 }),
    ];
    roleService.getRoles.and.returnValue(of(roles));
    component.loadRoles();

    component.onFilterChange('All Roles');
    expect(component.displayedRows().length).toBe(3);

    component.onFilterChange('Global Roles');
    expect(component.displayedRows().map((role) => role.roleID)).toEqual([1, 2]);

    component.onFilterChange('Group 5');
    expect(component.displayedRows().map((role) => role.roleID)).toEqual([3]);
  });

  it('navigates for edit, manage-users and add actions', () => {
    const row = makeRole({ roleID: 9 });

    component.onActionClick({ action: { id: 'edit', label: 'Edit', permission: 'EDIT' }, row });
    expect(router.navigate).toHaveBeenCalledWith(['/roles', 9, 'edit']);

    component.onActionClick({ action: { id: 'assignments', label: 'Manage Users', permission: 'EDIT' }, row });
    expect(router.navigate).toHaveBeenCalledWith(['/roles', 9, 'assignments']);

    component.onAddRole();
    expect(router.navigate).toHaveBeenCalledWith(['/roles/new']);
  });

  it('opens the confirmation dialog then deletes and refreshes', () => {
    const row = makeRole({ roleID: 4 });

    component.onActionClick({ action: { id: 'delete', label: 'Delete', permission: 'DELETE' }, row });
    expect(component.deleteDialogOpen()).toBe(true);
    expect(component.roleToDelete()?.roleID).toBe(4);

    roleService.getRoles.calls.reset();
    component.onConfirmDelete();

    expect(roleService.deleteRole).toHaveBeenCalledWith(4);
    expect(roleService.getRoles).toHaveBeenCalledWith(mockUser.portalID);
    expect(component.deleteDialogOpen()).toBe(false);
  });

  it('surfaces RFC 7807 errors via the error signal', () => {
    const problem: ProblemDetails = { title: 'Server Error', status: 500, detail: 'Boom' };
    roleService.getRoles.and.returnValue(throwError(() => problem));

    component.loadRoles();

    expect(component.error()).toBe('Boom');
    expect(component.roles().length).toBe(0);
  });
});
