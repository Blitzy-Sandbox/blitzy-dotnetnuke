import { signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import type { Role } from '../../models';
import { RoleService } from '../../services';
import { ApiService, type ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import type { User } from '../../../../core/models/user.model';
import type { DataTableActionEvent } from '../../../../shared/components/data-table';
import { RoleAssignmentComponent } from './role-assignment.component';

const ROLE_ID = 5;
const ASSIGNED_USER_ID = 100;
const CANDIDATE_USER_ID = 200;

function makeRole(overrides: Partial<Role> = {}): Role {
  return {
    roleID: ROLE_ID,
    portalID: 0,
    roleGroupID: null,
    roleName: 'Subscribers',
    description: 'Portal subscribers',
    serviceFee: 0,
    billingFrequency: 'N',
    trialPeriod: 0,
    trialFrequency: 'N',
    billingPeriod: 0,
    trialFee: 0,
    isPublic: false,
    autoAssignment: false,
    rSVPCode: null,
    iconFile: null,
    ...overrides,
  };
}

// MIGRATION: field casing matches the current `User` contract (core/models/user.model.ts),
// which uses `userID` / `portalID` (first-char-only camelCase of the backend `UserID` /
// `PortalID` properties; see the CP1 remediation note). The component under test reads
// `user.userID` / `currentUser()?.portalID`, and the sibling `role.service.spec` uses the same
// casing. `affiliateID` is optional and intentionally omitted.
function makeUser(overrides: Partial<User> = {}): User {
  return {
    userID: ASSIGNED_USER_ID,
    username: 'jdoe',
    displayName: 'John Doe',
    firstName: 'John',
    lastName: 'Doe',
    email: 'jdoe@example.com',
    portalID: 0,
    isSuperUser: false,
    roles: ['Subscribers'],
    ...overrides,
  };
}

const mockUser: User = makeUser({
  userID: 1,
  username: 'host',
  displayName: 'Host Account',
  isSuperUser: true,
});

describe('RoleAssignmentComponent', () => {
  let component: RoleAssignmentComponent;
  let fixture: ComponentFixture<RoleAssignmentComponent>;
  let roleServiceSpy: jasmine.SpyObj<RoleService>;
  let apiServiceSpy: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    roleServiceSpy = jasmine.createSpyObj<RoleService>('RoleService', [
      'getRole',
      'getUsersInRole',
      'assignUserToRole',
      'removeUserFromRole',
    ]);
    roleServiceSpy.getRole.and.returnValue(of(makeRole()));
    roleServiceSpy.getUsersInRole.and.returnValue(of([makeUser()]));
    roleServiceSpy.assignUserToRole.and.returnValue(of(void 0));
    roleServiceSpy.removeUserFromRole.and.returnValue(of(void 0));

    apiServiceSpy = jasmine.createSpyObj<ApiService>('ApiService', ['resourceUrl', 'getList']);
    apiServiceSpy.resourceUrl.and.callFake((entity: string, id?: string | number) =>
      id === undefined ? `/api/v1/${entity}` : `/api/v1/${entity}/${id}`,
    );
    apiServiceSpy.getList.and.returnValue(
      of({
        data: [
          makeUser({
            userID: CANDIDATE_USER_ID,
            username: 'candidate',
            displayName: 'Candidate User',
          }),
        ],
        meta: {},
      }),
    );

    const authStub = {
      currentUser: signal<User | null>(mockUser),
      hasRole: (_role: string): boolean => true,
    };

    const activatedRouteStub = {
      snapshot: { paramMap: convertToParamMap({ id: String(ROLE_ID) }) },
    };

    await TestBed.configureTestingModule({
      imports: [RoleAssignmentComponent],
      providers: [
        { provide: RoleService, useValue: roleServiceSpy },
        { provide: ApiService, useValue: apiServiceSpy },
        { provide: AuthService, useValue: authStub as unknown as AuthService },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RoleAssignmentComponent);
    component = fixture.componentInstance;
  });

  it('loads the role and assigned users for the roleId and renders the data table', () => {
    fixture.detectChanges();

    expect(roleServiceSpy.getRole).toHaveBeenCalledWith(ROLE_ID);
    expect(roleServiceSpy.getUsersInRole).toHaveBeenCalledWith(ROLE_ID);
    expect(component.role()?.roleID).toBe(ROLE_ID);
    expect(component.users().length).toBe(1);
    expect(component.loading()).toBeFalse();

    const dataTable = (fixture.nativeElement as HTMLElement).querySelector('app-data-table');
    expect(dataTable).toBeTruthy();
  });

  it('adds the selected user via assignUserToRole(roleId, userId) and refreshes the grid', () => {
    component.ngOnInit();
    roleServiceSpy.getUsersInRole.calls.reset();

    component.onSelectUser(String(CANDIDATE_USER_ID));
    component.onAddUser();

    expect(roleServiceSpy.assignUserToRole).toHaveBeenCalledWith(ROLE_ID, CANDIDATE_USER_ID);
    expect(roleServiceSpy.assignUserToRole.calls.mostRecent().args.length).toBe(2);
    expect(roleServiceSpy.getUsersInRole).toHaveBeenCalledWith(ROLE_ID);
    expect(component.selectedUserId()).toBeNull();
  });

  it('removes a user via removeUserFromRole(roleId, userId) after confirmation and refreshes', () => {
    component.ngOnInit();
    roleServiceSpy.getUsersInRole.calls.reset();
    const event: DataTableActionEvent<User> = {
      action: { id: 'remove', label: 'Remove' },
      row: makeUser(),
    };

    component.onActionClick(event);
    expect(component.removeDialogOpen()).toBeTrue();
    expect(component.userToRemove()?.userID).toBe(ASSIGNED_USER_ID);

    component.onConfirmRemove();

    expect(roleServiceSpy.removeUserFromRole).toHaveBeenCalledWith(ROLE_ID, ASSIGNED_USER_ID);
    expect(roleServiceSpy.removeUserFromRole.calls.mostRecent().args.length).toBe(2);
    expect(component.removeDialogOpen()).toBeFalse();
    expect(roleServiceSpy.getUsersInRole).toHaveBeenCalledWith(ROLE_ID);
  });

  it('never sends EffectiveDate/ExpiryDate/notify (assign and remove take exactly two args)', () => {
    component.ngOnInit();

    component.onSelectUser(String(CANDIDATE_USER_ID));
    component.onAddUser();
    component.onActionClick({ action: { id: 'remove', label: 'Remove' }, row: makeUser() });
    component.onConfirmRemove();

    expect(roleServiceSpy.assignUserToRole.calls.mostRecent().args).toEqual([
      ROLE_ID,
      CANDIDATE_USER_ID,
    ]);
    expect(roleServiceSpy.removeUserFromRole.calls.mostRecent().args).toEqual([
      ROLE_ID,
      ASSIGNED_USER_ID,
    ]);
  });

  it('surfaces an RFC 7807 error when loading fails', () => {
    const problem: ProblemDetails = { title: 'Server Error', status: 500, detail: 'Boom' };
    roleServiceSpy.getUsersInRole.and.returnValue(throwError(() => problem));

    component.ngOnInit();

    expect(component.error()).toBe('Boom');
    expect(component.loading()).toBeFalse();
  });
});
