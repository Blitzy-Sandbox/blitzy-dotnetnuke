import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, type Params, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { RoleFormComponent } from './role-form.component';
import { RoleService } from '../../services';
import type { Role } from '../../models';
import type { ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import type { User } from '../../../../core/models/user.model';

// Typed Role factory mirroring the wire contract in `../../models` (role.model.ts). The 14th field is
// `rsvpCode` (System.Text.Json camelCase lowercases the whole "RSVP" acronym run); using any other
// casing would not satisfy the `Role` interface.
function makeRole(overrides: Partial<Role> = {}): Role {
  return {
    roleID: 5,
    portalID: 0,
    roleGroupID: -1,
    roleName: 'Editors',
    description: 'Content editors',
    serviceFee: 0,
    billingFrequency: 'N',
    trialPeriod: 1,
    trialFrequency: 'N',
    billingPeriod: 1,
    trialFee: 0,
    isPublic: false,
    autoAssignment: false,
    rsvpCode: '',
    iconFile: '',
    ...overrides,
  };
}

// Fully-populated `User` matching core/models/user.model.ts (acronym-prefixed ids serialize as
// `userID`/`portalID`/`affiliateID`). The component reads `currentUser()?.portalID` when building a
// CreateRole, so `portalID` is 0 to keep the create-mode assertion (`dto.portalID === 0`) honest.
const mockUser: User = {
  userID: 1,
  portalID: 0,
  affiliateID: null,
  username: 'admin',
  displayName: 'Administrator',
  email: 'admin@example.com',
  firstName: 'Ad',
  lastName: 'Min',
  fullName: 'Ad Min',
  isSuperUser: true,
  approved: true,
  updatePassword: false,
  roles: ['Administrators'],
  createdDate: null,
  lastLoginDate: null,
  lastPasswordChangeDate: null,
  lastActivityDate: null,
};

describe('RoleFormComponent', () => {
  let roleServiceSpy: jasmine.SpyObj<RoleService>;
  let routerSpy: jasmine.SpyObj<Router>;
  let routeParams: Params;

  const authStub = {
    currentUser: signal<User | null>(mockUser),
    hasRole: () => true,
  } as unknown as AuthService;

  function createComponent(): RoleFormComponent {
    return TestBed.createComponent(RoleFormComponent).componentInstance;
  }

  beforeEach(() => {
    routeParams = {};
    roleServiceSpy = jasmine.createSpyObj<RoleService>('RoleService', [
      'getRole',
      'createRole',
      'updateRole',
      'deleteRole',
    ]);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      imports: [RoleFormComponent],
      providers: [
        { provide: RoleService, useValue: roleServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: AuthService, useValue: authStub },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              get paramMap() {
                return convertToParamMap(routeParams);
              },
            },
          },
        },
      ],
    });
  });

  it('create mode: starts with an empty form and submits a CreateRole, then navigates', () => {
    routeParams = {};
    roleServiceSpy.createRole.and.returnValue(of(makeRole()));
    const component = createComponent();
    component.ngOnInit();

    expect(component.isEditMode()).toBeFalse();
    expect(roleServiceSpy.getRole).not.toHaveBeenCalled();
    expect(component.controls.roleName.value).toBe('');
    expect(component.controls.roleGroupID.value).toBe(-1);

    component.controls.roleName.setValue('Editors');
    component.onSubmit();

    expect(roleServiceSpy.createRole).toHaveBeenCalledTimes(1);
    const dto = roleServiceSpy.createRole.calls.mostRecent().args[0];
    expect(Object.keys(dto).length).toBe(14);
    expect(dto.roleName).toBe('Editors');
    expect(dto.portalID).toBe(0);
    expect(dto.billingFrequency).toBe('N');
    expect(dto.serviceFee).toBe(0);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/roles']);
    expect(component.submitting()).toBeFalse();
  });

  it('edit mode: loads the role, patches the form, and submits an UpdateRole with roleID === id', () => {
    routeParams = { id: '5' };
    roleServiceSpy.getRole.and.returnValue(of(makeRole({ roleID: 5, roleName: 'Editors' })));
    roleServiceSpy.updateRole.and.returnValue(of(makeRole({ roleID: 5 })));
    const component = createComponent();
    component.ngOnInit();

    expect(roleServiceSpy.getRole).toHaveBeenCalledWith(5);
    expect(component.isEditMode()).toBeTrue();
    expect(component.controls.roleName.value).toBe('Editors');

    component.controls.description.setValue('Updated description');
    component.onSubmit();

    expect(roleServiceSpy.updateRole).toHaveBeenCalledTimes(1);
    const [idArg, dto] = roleServiceSpy.updateRole.calls.mostRecent().args;
    expect(idArg).toBe(5);
    expect(dto.roleID).toBe(5);
    expect(dto.description).toBe('Updated description');
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/roles']);
  });

  it('defaults the frequency dropdowns to "N" and toggles the billing/trial groups', () => {
    routeParams = {};
    const component = createComponent();
    component.ngOnInit();

    expect(component.controls.billingFrequency.value).toBe('N');
    expect(component.controls.trialFrequency.value).toBe('N');
    expect(component.billingEnabled()).toBeFalse();
    expect(component.trialEnabled()).toBeFalse();
    expect(component.controls.serviceFee.disabled).toBeTrue();
    expect(component.controls.billingPeriod.disabled).toBeTrue();
    expect(component.controls.trialFee.disabled).toBeTrue();
    expect(component.controls.trialPeriod.disabled).toBeTrue();

    component.controls.billingFrequency.setValue('M');
    expect(component.billingEnabled()).toBeTrue();
    expect(component.controls.serviceFee.enabled).toBeTrue();
    expect(component.controls.billingPeriod.enabled).toBeTrue();

    component.controls.trialFrequency.setValue('M');
    expect(component.trialEnabled()).toBeTrue();
    expect(component.controls.trialFee.enabled).toBeTrue();
    expect(component.controls.trialPeriod.enabled).toBeTrue();
  });

  it('disables the form and flags a system role when editing Administrators', () => {
    routeParams = { id: '1' };
    roleServiceSpy.getRole.and.returnValue(of(makeRole({ roleID: 1, roleName: 'Administrators' })));
    const component = createComponent();
    component.ngOnInit();

    expect(component.isSystemRole()).toBeTrue();
    expect(component.form.disabled).toBeTrue();
  });

  it('maps RFC 7807 errors to serverErrors (duplicate-name parity)', () => {
    routeParams = {};
    const problem: ProblemDetails = {
      errors: { roleName: ['A role with this name already exists.'] },
    };
    roleServiceSpy.createRole.and.returnValue(throwError(() => problem));
    const component = createComponent();
    component.ngOnInit();

    component.controls.roleName.setValue('Editors');
    component.onSubmit();

    expect(component.serverErrors()).toEqual({
      roleName: ['A role with this name already exists.'],
    });
    expect(component.submitting()).toBeFalse();
  });

  it('confirms deletion by calling deleteRole(id) and navigating to the list', () => {
    routeParams = { id: '5' };
    roleServiceSpy.getRole.and.returnValue(of(makeRole({ roleID: 5, roleName: 'Editors' })));
    roleServiceSpy.deleteRole.and.returnValue(of(void 0));
    const component = createComponent();
    component.ngOnInit();

    component.requestDelete();
    expect(component.showDeleteDialog()).toBeTrue();

    component.confirmDelete();
    expect(roleServiceSpy.deleteRole).toHaveBeenCalledWith(5);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/roles']);
  });
});
