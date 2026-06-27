// MIGRATION: Unit spec (Gate 4) for the net-new signal-based RoleService, which replaces the data-access
// of the legacy DNN Admin > Security controls (Roles.ascx.vb / EditRoles.ascx.vb / SecurityRoles.ascx.vb).
// Uses the REAL ApiService (root-injected) with HttpClient testing so URL building, the { data, meta }
// envelope contract, and signal state are exercised end to end.
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';

import { RoleService } from './role.service';
import type {
  AssignUserRoleRequest,
  CreateRoleRequest,
  UpdateRoleRequest,
  UpdateUserRoleRequest,
} from './role.service';
import { environment } from '../../../environments/environment';
import type { Role, UserRole } from '../../core/models';

describe('RoleService', () => {
  let service: RoleService;
  let httpMock: HttpTestingController;
  const apiUrl = environment.apiUrl;

  function makeRole(overrides: Partial<Role> = {}): Role {
    return {
      roleId: 1,
      portalId: 0,
      roleGroupId: null,
      roleName: 'Administrators',
      description: 'Portal Administrators',
      serviceFee: 0,
      billingFrequency: 'N',
      trialPeriod: 0,
      trialFrequency: 'N',
      billingPeriod: 1,
      trialFee: 0,
      isPublic: false,
      autoAssignment: false,
      rsvpCode: null,
      iconFile: null,
      ...overrides,
    };
  }

  function makeRolePayload(overrides: Partial<CreateRoleRequest> = {}): CreateRoleRequest {
    return {
      portalId: 0,
      roleGroupId: null,
      roleName: 'Editors',
      description: null,
      serviceFee: 0,
      billingFrequency: 'N',
      trialPeriod: 0,
      trialFrequency: 'N',
      billingPeriod: 1,
      trialFee: 0,
      isPublic: false,
      autoAssignment: false,
      rsvpCode: null,
      iconFile: null,
      ...overrides,
    };
  }

  function makeUserRole(overrides: Partial<UserRole> = {}): UserRole {
    return {
      userRoleId: 10,
      userId: 3,
      roleId: 1,
      effectiveDate: null,
      expiryDate: null,
      isTrialUsed: false,
      subscribed: true,
      ...overrides,
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RoleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('list() should GET roles for a portal, forward the paging params, and populate the signals', () => {
    const items = [
      makeRole({ roleId: 1 }),
      makeRole({ roleId: 2, roleName: 'Registered Users' }),
    ];

    service.list(7, 0, 20).subscribe();
    expect(service.loading()).toBeTrue();

    const req = httpMock.expectOne((r) => r.url === `${apiUrl}/roles`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('portalId')).toBe('7');
    expect(req.request.params.get('pageIndex')).toBe('0');
    expect(req.request.params.get('pageSize')).toBe('20');
    req.flush({
      data: items,
      meta: {
        totalCount: 2,
        pageIndex: 0,
        pageSize: 20,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      },
    });

    expect(service.roles()).toEqual(items);
    expect(service.totalCount()).toBe(2);
    expect(service.loading()).toBeFalse();
  });

  it('list() should forward the optional filter and roleGroupId params (incl. the -2 sentinel)', () => {
    service.list(7, 1, 20, 'admin', -2).subscribe();

    const req = httpMock.expectOne((r) => r.url === `${apiUrl}/roles`);
    expect(req.request.params.get('filter')).toBe('admin');
    expect(req.request.params.get('roleGroupId')).toBe('-2');
    req.flush({
      data: [],
      meta: {
        totalCount: 0,
        pageIndex: 1,
        pageSize: 20,
        totalPages: 0,
        hasPreviousPage: true,
        hasNextPage: false,
      },
    });

    expect(service.roles()).toEqual([]);
  });

  it('list() should reset the loading signal when the request errors', () => {
    service.list(7, 0, 20).subscribe({ error: () => undefined });
    expect(service.loading()).toBeTrue();

    const req = httpMock.expectOne((r) => r.url === `${apiUrl}/roles`);
    req.flush(
      { type: 'about:blank', title: 'Server Error', status: 500, detail: 'boom' },
      { status: 500, statusText: 'Server Error' },
    );

    expect(service.loading()).toBeFalse();
  });

  it('getById() should GET a single role with the required portalId query and set the selected signal', () => {
    const role = makeRole({ roleId: 5, roleName: 'Subscribers' });
    let actual: Role | undefined;

    service.getById(5, 3).subscribe((r) => (actual = r));

    // The portalId travels as a query param, so match on the base url (predicate form)
    // and assert the param separately -- a string matcher compares urlWithParams.
    const req = httpMock.expectOne((r) => r.url === `${apiUrl}/roles/5`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('portalId')).toBe('3');
    req.flush({ data: role, meta: {} });

    expect(actual).toEqual(role);
    expect(service.selected()).toEqual(role);
  });

  it('create() should POST the payload and return the created role (201)', () => {
    const payload = makeRolePayload({ roleName: 'Editors' });
    const created = makeRole({ roleId: 99, roleName: 'Editors' });
    let actual: Role | undefined;

    service.create(payload).subscribe((r) => (actual = r));

    const req = httpMock.expectOne(`${apiUrl}/roles`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ data: created, meta: {} }, { status: 201, statusText: 'Created' });

    expect(actual).toEqual(created);
  });

  it('update() should PUT to roles/{id} with the required portalId query and return the updated role (200)', () => {
    const payload: UpdateRoleRequest = makeRolePayload({ roleName: 'Editors-Renamed' });
    const updated = makeRole({ roleId: 5, roleName: 'Editors-Renamed' });
    let actual: Role | undefined;

    service.update(5, 3, payload).subscribe((r) => (actual = r));

    const req = httpMock.expectOne((r) => r.url === `${apiUrl}/roles/5`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.params.get('portalId')).toBe('3');
    expect(req.request.body).toEqual(payload);
    req.flush({ data: updated, meta: {} });

    expect(actual).toEqual(updated);
  });

  it('delete() should DELETE roles/{id} (204), remove it from the signal and decrement totalCount', () => {
    service.list(7, 0, 20).subscribe();
    const listReq = httpMock.expectOne((r) => r.url === `${apiUrl}/roles`);
    listReq.flush({
      data: [makeRole({ roleId: 1 }), makeRole({ roleId: 2 })],
      meta: {
        totalCount: 2,
        pageIndex: 0,
        pageSize: 20,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      },
    });
    expect(service.roles().length).toBe(2);
    expect(service.totalCount()).toBe(2);

    let completed = false;
    service.delete(1, 7).subscribe({ complete: () => (completed = true) });

    const req = httpMock.expectOne((r) => r.url === `${apiUrl}/roles/1`);
    expect(req.request.method).toBe('DELETE');
    expect(req.request.params.get('portalId')).toBe('7');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBeTrue();
    expect(service.roles().map((r) => r.roleId)).toEqual([2]);
    expect(service.totalCount()).toBe(1);
  });

  it('getUserRoles() should GET roles/user/{userId} with the required portalId query and return the user-role list', () => {
    const userRoles = [makeUserRole({ userRoleId: 10, userId: 3, roleId: 1 })];
    let actual: UserRole[] | undefined;

    service.getUserRoles(3, 7).subscribe((r) => (actual = r));

    const req = httpMock.expectOne((r) => r.url === `${apiUrl}/roles/user/3`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('portalId')).toBe('7');
    req.flush({ data: userRoles, meta: { count: 1 } });

    expect(actual).toEqual(userRoles);
  });

  // MIGRATION: user-role assignment WRITE contract (RolesController assignments endpoints). The backend now
  // exposes the assignment write surface (RoleController.AddUserRole/UpdateUserRole/DeleteUserRole), so these
  // service methods are implemented and covered here. Recorded in MIGRATION_NOTES.md.
  it('assignUserRole() should POST roles/assignments with the request body and return the saved user-role', () => {
    const request: AssignUserRoleRequest = {
      portalId: 7,
      userId: 3,
      roleId: 1,
      effectiveDate: '2026-01-01',
      expiryDate: null,
    };
    const saved = makeUserRole({ userRoleId: 42, userId: 3, roleId: 1, effectiveDate: '2026-01-01' });
    let actual: UserRole | undefined;

    service.assignUserRole(request).subscribe((r) => (actual = r));

    const req = httpMock.expectOne((r) => r.url === `${apiUrl}/roles/assignments`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ data: saved, meta: {} });

    expect(actual).toEqual(saved);
  });

  it('updateUserRole() should PUT roles/assignments with the request body and return the updated user-role', () => {
    const request: UpdateUserRoleRequest = {
      portalId: 7,
      userId: 3,
      roleId: 1,
      cancel: true,
    };
    const updated = makeUserRole({ userRoleId: 42, userId: 3, roleId: 1, expiryDate: '2026-02-01' });
    let actual: UserRole | undefined;

    service.updateUserRole(request).subscribe((r) => (actual = r));

    const req = httpMock.expectOne((r) => r.url === `${apiUrl}/roles/assignments`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush({ data: updated, meta: {} });

    expect(actual).toEqual(updated);
  });

  it('removeUserRole() should DELETE roles/{roleId}/users/{userId} with the required portalId query', () => {
    let completed = false;

    service.removeUserRole(7, 1, 3).subscribe(() => (completed = true));

    const req = httpMock.expectOne((r) => r.url === `${apiUrl}/roles/1/users/3`);
    expect(req.request.method).toBe('DELETE');
    expect(req.request.params.get('portalId')).toBe('7');
    req.flush({ data: null, meta: {} });

    expect(completed).toBeTrue();
  });

  it('clearSelected() should reset the selected signal to null', () => {
    const role = makeRole({ roleId: 5 });
    service.getById(5, 0).subscribe();
    httpMock
      .expectOne((r) => r.url === `${apiUrl}/roles/5`)
      .flush({ data: role, meta: {} });
    expect(service.selected()).toEqual(role);

    service.clearSelected();
    expect(service.selected()).toBeNull();
  });
});
