/**
 * Unit spec for RoleAssignmentComponent (Karma/Jasmine, standalone TestBed) — Gate 4.
 *
 * Verifies the User<->Role membership screen ported from the legacy Web Forms control
 * `Website/admin/Security/SecurityRoles.ascx.vb` (AAP 0.4.1). The HEART of this spec is the
 * API-contract assertion that the component calls `assignUserToRole(roleId, userId)` and
 * `removeUserFromRole(roleId, userId)` with the roleId FIRST and EXACTLY two arguments — proving
 * the legacy per-membership EffectiveDate/ExpiryDate/notify values are never passed by the SPA.
 *
 * CONTRACT FIDELITY: the typed factories conform EXACTLY to the on-disk dependency contracts.
 * Per `core/models/user.model.ts` the User identity fields serialize as `userID`/`portalID`/
 * `affiliateID` (System.Text.Json camelCase lowercases only the leading acronym run) and the
 * interface carries all sixteen always-present members; per `features/role/models/role.model.ts`
 * the role RSVP field is `rsvpCode`. `RoleService.assignUserToRole` returns `Observable<UserRole>`,
 * so the spy returns a fully-typed `UserRole`. These conventions match the sibling specs
 * (`role.service.spec.ts`, `role-form.component.spec.ts`), both of which pass Gate 4.
 */
import { signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import type { Role, UserRole } from '../../models';
import { RoleService } from '../../services';
import { ApiService, type ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import type { User } from '../../../../core/models/user.model';
import type { DataTableActionEvent } from '../../../../shared/components/data-table';
import { RoleAssignmentComponent } from './role-assignment.component';

const ROLE_ID = 5;
const ASSIGNED_USER_ID = 100;
const CANDIDATE_USER_ID = 200;

// Typed Role factory matching the on-disk `Role` contract (features/role/models/role.model.ts).
// The RSVP field is `rsvpCode` (the whole "RSVP" acronym run is lowercased, then "Code" keeps its
// capital C). `roleName` is deliberately a NON-administrator name so the component's best-effort
// last-administrator `disabled` guard stays inert during the remove flow.
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
    rsvpCode: null,
    iconFile: null,
    ...overrides,
  };
}

// Fully-populated `User` matching core/models/user.model.ts: the acronym-prefixed ids serialize as
// `userID`/`portalID`/`affiliateID`, and every always-present member of the flattened UserDto is
// supplied so the literal satisfies the interface with no missing/excess properties.
function makeUser(overrides: Partial<User> = {}): User {
  return {
    userID: ASSIGNED_USER_ID,
    portalID: 0,
    affiliateID: null,
    username: 'jdoe',
    displayName: 'John Doe',
    email: 'jdoe@example.com',
    firstName: 'John',
    lastName: 'Doe',
    fullName: 'John Doe',
    isSuperUser: false,
    approved: true,
    updatePassword: false,
    roles: ['Subscribers'],
    createdDate: null,
    lastLoginDate: null,
    lastPasswordChangeDate: null,
    lastActivityDate: null,
    ...overrides,
  };
}

// Typed `UserRole` factory (features/role/models/role.model.ts). `assignUserToRole` resolves to the
// persisted assignment, so the spy returns one of these; the component ignores the emitted value
// (it only reacts to next/complete), so any valid `UserRole` suffices.
function makeUserRole(overrides: Partial<UserRole> = {}): UserRole {
  return {
    userRoleID: 1,
    userID: ASSIGNED_USER_ID,
    roleID: ROLE_ID,
    effectiveDate: null,
    expiryDate: null,
    isTrialUsed: false,
    subscribed: false,
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
    roleServiceSpy.assignUserToRole.and.returnValue(of(makeUserRole()));
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

    // MIGRATION (reconcile/CP5): the legacy AddUserRole captured a per-membership EffectiveDate/ExpiryDate
    // window; the new API persists it, so assignUserToRole sends (roleId, userId) PLUS the optional
    // { effectiveDate, expiryDate } body — both null here because no dates were entered (unbounded window).
    expect(roleServiceSpy.assignUserToRole).toHaveBeenCalledWith(ROLE_ID, CANDIDATE_USER_ID, {
      effectiveDate: null,
      expiryDate: null,
    });
    expect(roleServiceSpy.assignUserToRole.calls.mostRecent().args.length).toBe(3);
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

  it('forwards the EffectiveDate/ExpiryDate membership window on assign and sends NO body (no legacy notify) on remove', () => {
    component.ngOnInit();

    // MIGRATION (reconcile/CP5): legacy cmdAdd_Click passed EffectiveDate/ExpiryDate (+ a notify flag).
    // The SPA forwards the optional membership window on assign; the entered dates flow through verbatim
    // (blank inputs would map to null = unbounded — covered by the default-window assertion above).
    component.onSelectUser(String(CANDIDATE_USER_ID));
    component.onEffectiveDateChange('2025-01-01');
    component.onExpiryDateChange('2025-12-31');
    component.onAddUser();

    expect(roleServiceSpy.assignUserToRole.calls.mostRecent().args).toEqual([
      ROLE_ID,
      CANDIDATE_USER_ID,
      { effectiveDate: '2025-01-01', expiryDate: '2025-12-31' },
    ]);

    // Remove drops the legacy notify flag entirely: the new DELETE endpoint takes NO body, so
    // removeUserFromRole is invoked with EXACTLY (roleId, userId).
    component.onActionClick({ action: { id: 'remove', label: 'Remove' }, row: makeUser() });
    component.onConfirmRemove();

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
