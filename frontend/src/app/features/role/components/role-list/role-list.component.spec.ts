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
    rSVPCode: null,
    iconFile: null,
    ...overrides,
  };
}

// MIGRATION: field casing matches the current `User` contract (core/models/user.model.ts),
// which uses `userID` / `portalID` — the first-char-only camelCase of the backend `UserID` /
// `PortalID` properties (System.Text.Json default policy; see the CP1 casing note in
// role.model.ts). The component reads `currentUser()?.portalID`, so `portalID: 7` is the
// value passed to getRoles.
const mockUser: User = {
  userID: 1,
  username: 'admin',
  displayName: 'Administrator',
  firstName: 'Super',
  lastName: 'User',
  email: 'admin@example.com',
  portalID: 7,
  isSuperUser: true,
  roles: ['Administrators'],
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
