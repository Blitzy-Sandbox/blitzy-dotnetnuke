import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiService } from '../../../core/services/api.service';
import type { User } from '../../../core/models/user.model';
import type { CreateRole, Role, UpdateRole } from '../models';
import { RoleService } from './role.service';

describe('RoleService', () => {
  let service: RoleService;
  let api: jasmine.SpyObj<ApiService>;

  // Fixture conforms EXACTLY to the on-disk `Role` contract (features/role/models/role.model.ts).
  // NOTE: the wire/field name is `rsvpCode` (camelCase) per the documented System.Text.Json casing
  // rule — the whole "RSVP" acronym run is lowercased, then "Code" keeps its capital C.
  const mockRole: Role = {
    roleID: 5,
    portalID: 0,
    roleGroupID: null,
    roleName: 'Administrators',
    description: 'Portal Administrators',
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
  };

  // Fixture conforms EXACTLY to the on-disk `User` contract (core/models/user.model.ts): the
  // acronym-prefixed ids serialize as `userID`/`portalID`/`affiliateID`, and every (always-present)
  // member of the flattened UserDto is supplied so the literal satisfies the interface with no excess.
  const mockUser: User = {
    userID: 1,
    portalID: 0,
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

  beforeEach(() => {
    const spy = jasmine.createSpyObj<ApiService>('ApiService', [
      'resourceUrl',
      'get',
      'getList',
      'post',
      'put',
      'delete',
    ]);
    spy.resourceUrl.and.callFake((entity: string, id?: string | number) =>
      id === undefined ? `/api/v1/${entity}` : `/api/v1/${entity}/${id}`,
    );

    TestBed.configureTestingModule({
      providers: [RoleService, { provide: ApiService, useValue: spy }],
    });

    service = TestBed.inject(RoleService);
    api = TestBed.inject(ApiService) as jasmine.SpyObj<ApiService>;
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getRoles', () => {
    it('calls getList with the portalId query param and unwraps .data', () => {
      api.getList.and.returnValue(of({ data: [mockRole], meta: {} }));

      let result: Role[] | undefined;
      service.getRoles(42).subscribe((roles) => (result = roles));

      expect(api.getList).toHaveBeenCalledWith('/api/v1/roles', { portalId: 42 });
      expect(result).toEqual([mockRole]);
    });
  });

  describe('getRole', () => {
    it('GETs /api/v1/roles/{id} and returns the role', () => {
      api.get.and.returnValue(of(mockRole));

      let result: Role | undefined;
      service.getRole(5).subscribe((role) => (result = role));

      expect(api.get).toHaveBeenCalledWith('/api/v1/roles/5');
      expect(result).toEqual(mockRole);
    });
  });

  describe('createRole', () => {
    it('POSTs the dto to /api/v1/roles and returns the created role', () => {
      const dto: CreateRole = { ...mockRole };
      api.post.and.returnValue(of(mockRole));

      let result: Role | undefined;
      service.createRole(dto).subscribe((role) => (result = role));

      expect(api.post).toHaveBeenCalledWith('/api/v1/roles', dto);
      expect(result).toEqual(mockRole);
    });
  });

  describe('updateRole', () => {
    it('PUTs the dto to /api/v1/roles/{id} (caller sets roleID === id) and returns the role', () => {
      const dto: UpdateRole = { ...mockRole, roleID: 5 };
      api.put.and.returnValue(of(mockRole));

      let result: Role | undefined;
      service.updateRole(5, dto).subscribe((role) => (result = role));

      expect(api.put).toHaveBeenCalledWith('/api/v1/roles/5', dto);
      expect(dto.roleID).toBe(5);
      expect(result).toEqual(mockRole);
    });
  });

  describe('deleteRole', () => {
    it('DELETEs /api/v1/roles/{id} and completes (void)', () => {
      api.delete.and.returnValue(of(undefined));

      let completed = false;
      service.deleteRole(5).subscribe({ complete: () => (completed = true) });

      expect(api.delete).toHaveBeenCalledWith('/api/v1/roles/5');
      expect(completed).toBe(true);
    });
  });

  describe('getUsersInRole', () => {
    it('GETs /api/v1/roles/{roleId}/users and unwraps .data', () => {
      api.getList.and.returnValue(of({ data: [mockUser], meta: {} }));

      let result: User[] | undefined;
      service.getUsersInRole(5).subscribe((users) => (result = users));

      expect(api.getList).toHaveBeenCalledWith('/api/v1/roles/5/users');
      expect(result).toEqual([mockUser]);
    });
  });

  describe('getUserRoles', () => {
    it('GETs /api/v1/roles/user/{userId} and unwraps .data', () => {
      api.getList.and.returnValue(of({ data: [mockRole], meta: {} }));

      let result: Role[] | undefined;
      service.getUserRoles(1).subscribe((roles) => (result = roles));

      expect(api.getList).toHaveBeenCalledWith('/api/v1/roles/user/1');
      expect(result).toEqual([mockRole]);
    });
  });

  describe('assignUserToRole', () => {
    it('POSTs to /api/v1/roles/{roleId}/users/{userId} with the assignment-window body (roleId first, userId last)', () => {
      api.post.and.returnValue(of(undefined));

      let completed = false;
      service.assignUserToRole(5, 1).subscribe({ complete: () => (completed = true) });

      // INTEGRATION: CP2's role-contract fix sends the admin effective/expiry window in the request body
      // (both dates default to null when no assignment is supplied); the route IDs remain authoritative for
      // identity. The earlier "no body" expectation reflected the pre-CP2 contract.
      expect(api.post).toHaveBeenCalledWith('/api/v1/roles/5/users/1', {
        roleID: 5,
        userID: 1,
        effectiveDate: null,
        expiryDate: null,
      });
      expect(api.post.calls.mostRecent().args.length).toBe(2);
      expect(completed).toBe(true);
    });
  });

  describe('removeUserFromRole', () => {
    it('DELETEs /api/v1/roles/{roleId}/users/{userId} (roleId first, userId last)', () => {
      api.delete.and.returnValue(of(undefined));

      let completed = false;
      service.removeUserFromRole(5, 1).subscribe({ complete: () => (completed = true) });

      expect(api.delete).toHaveBeenCalledWith('/api/v1/roles/5/users/1');
      expect(completed).toBe(true);
    });
  });
});
