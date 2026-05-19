/**
 * RoleFormComponent Unit Tests
 *
 * MIGRATION: Test coverage for EditRoles.ascx.vb migrated functionality
 * Tests the role create/edit form including: form initialization for create vs edit modes,
 * form validation, role group dropdown population, billing/trial settings toggling,
 * system role protection (Administrator/Registered roles disable editing), form submission
 * for create and update operations, delete confirmation dialog, and navigation after
 * save/cancel/delete.
 *
 * @fileoverview Unit tests for RoleFormComponent
 * @module features/role/components/role-form.spec
 */

import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, ActivatedRoute, provideRouter } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

import { RoleFormComponent } from './role-form.component';
import { RoleService } from '../../services/role.service';
import { Role, RoleGroup, CreateRoleRequest } from '../../models/role.model';

// =============================================================================
// TEST DATA FIXTURES
// =============================================================================

/**
 * Mock role data for testing edit mode
 */
const mockRole: Role = {
  roleId: 10,
  roleName: 'Test Role',
  description: 'A test role description',
  roleGroupId: 5,
  isPublic: true,
  autoAssignment: false,
  serviceFee: 0,
  billingPeriod: 0,
  billingFrequency: 'N',
  trialFee: 0,
  trialPeriod: 0,
  trialFrequency: 'N',
  rsvpCode: '',
  iconFile: '',
  portalId: 1
};

/**
 * Mock Administrator role (system role - cannot be modified)
 * MIGRATION: PortalSettings.AdministratorRoleId (typically 0)
 */
const mockAdminRole: Role = {
  ...mockRole,
  roleId: 0,
  roleName: 'Administrators',
  description: 'Portal Administrators'
};

/**
 * Mock Registered Users role (system role - cannot be modified or have users assigned)
 * MIGRATION: PortalSettings.RegisteredRoleId (typically 1)
 */
const mockRegisteredRole: Role = {
  ...mockRole,
  roleId: 1,
  roleName: 'Registered Users',
  description: 'All registered users'
};

/**
 * Mock role with billing settings enabled
 * MIGRATION: Tests form population for ServiceFee > 0 (lines 146-152)
 */
const mockPaidRole: Role = {
  ...mockRole,
  roleId: 20,
  roleName: 'Premium Role',
  serviceFee: 9.99,
  billingPeriod: 1,
  billingFrequency: 'M'
};

/**
 * Mock role with trial settings enabled
 * MIGRATION: Tests form population for TrialFrequency != 'N' (lines 154-161)
 */
const mockTrialRole: Role = {
  ...mockRole,
  roleId: 30,
  roleName: 'Trial Role',
  serviceFee: 19.99,
  billingPeriod: 1,
  billingFrequency: 'M',
  trialFee: 0,
  trialPeriod: 14,
  trialFrequency: 'D'
};

/**
 * Mock role with RSVP code
 * MIGRATION: Tests RSVP link generation (lines 166-168)
 */
const mockRsvpRole: Role = {
  ...mockRole,
  roleId: 40,
  roleName: 'RSVP Role',
  rsvpCode: 'TESTRSVP123'
};

/**
 * Mock role groups for dropdown
 * MIGRATION: Tests BindGroups() functionality (lines 71-81)
 */
const mockRoleGroups: RoleGroup[] = [
  { roleGroupId: 1, roleGroupName: 'Group A', portalId: 1, description: 'Test group A' },
  { roleGroupId: 2, roleGroupName: 'Group B', portalId: 1, description: 'Test group B' },
  { roleGroupId: 3, roleGroupName: 'Group C', portalId: 1, description: 'Test group C' }
];

// =============================================================================
// TEST SUITE
// =============================================================================

describe('RoleFormComponent', () => {
  let component: RoleFormComponent;
  let fixture: ComponentFixture<RoleFormComponent>;
  let mockRoleService: jasmine.SpyObj<RoleService>;
  let mockRouter: jasmine.SpyObj<Router>;
  
  /**
   * Mutable route parameter holder - allows changing route params without TestBed reconfiguration.
   * This object is shared across all tests and its properties are modified before component creation.
   */
  let currentRouteRoleId: string | null = null;

  /**
   * Create a mutable mock ActivatedRoute that reads from currentRouteRoleId.
   * This allows us to change the route parameter before creating the component
   * without needing to call TestBed.overrideProvider.
   */
  const createMutableMockRoute = (): Partial<ActivatedRoute> => ({
    snapshot: {
      paramMap: {
        get: (key: string) => key === 'id' ? currentRouteRoleId : null,
        has: (key: string) => key === 'id' && currentRouteRoleId !== null,
        getAll: () => [],
        keys: []
      }
    } as any
  });

  /**
   * Configure component for create mode (no roleId param).
   * Sets currentRouteRoleId to null, configures service mocks, and creates component.
   */
  const setupCreateMode = () => {
    currentRouteRoleId = null;
    mockRoleService.getRoleGroups.and.returnValue(of(mockRoleGroups));
    
    fixture = TestBed.createComponent(RoleFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  /**
   * Configure component for edit mode with specified role.
   * Sets currentRouteRoleId, configures service mocks to return the role, and creates component.
   */
  const setupEditMode = (role: Role) => {
    currentRouteRoleId = role.roleId.toString();
    mockRoleService.getRole.and.returnValue(of(role));
    mockRoleService.getRoleGroups.and.returnValue(of(mockRoleGroups));
    
    fixture = TestBed.createComponent(RoleFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  // ===========================================================================
  // BEFORE EACH SETUP
  // ===========================================================================

  beforeEach(async () => {
    // Reset route parameter for each test
    currentRouteRoleId = null;
    
    // Create spies for services
    mockRoleService = jasmine.createSpyObj('RoleService', [
      'getRole',
      'getRoleGroups',
      'createRole',
      'updateRole',
      'deleteRole'
    ]);
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);

    // Default spy return values
    mockRoleService.getRoleGroups.and.returnValue(of(mockRoleGroups));
    mockRouter.navigate.and.returnValue(Promise.resolve(true));

    await TestBed.configureTestingModule({
      imports: [
        RoleFormComponent,
        ReactiveFormsModule,
        NoopAnimationsModule
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: RoleService, useValue: mockRoleService },
        { provide: Router, useValue: mockRouter },
        // Use the mutable mock that reads from currentRouteRoleId
        { provide: ActivatedRoute, useFactory: createMutableMockRoute }
      ]
    }).compileComponents();
  });

  // ===========================================================================
  // CREATE MODE TESTS
  // MIGRATION: Tests derived from EditRoles.ascx.vb lines 183-188
  // ===========================================================================

  describe('Create Mode', () => {
    beforeEach(() => {
      setupCreateMode();
    });

    it('should create component in create mode when no roleId param', () => {
      expect(component).toBeTruthy();
      expect(component.isEditMode()).toBeFalse();
    });

    it('should initialize loading as false in create mode', () => {
      expect(component.loading()).toBeFalse();
    });

    it('should initialize empty form with default values', () => {
      expect(component.roleForm.value).toEqual({
        roleName: '',
        description: '',
        roleGroupId: -1,
        isPublic: false,
        autoAssignment: false,
        serviceFee: null,
        billingPeriod: null,
        billingFrequency: 'N',
        trialFee: null,
        trialPeriod: null,
        trialFrequency: 'N',
        rsvpCode: '',
        iconFile: ''
      });
    });

    it('should have role name field as required', () => {
      const roleNameControl = component.roleForm.controls.roleName;
      expect(roleNameControl.valid).toBeFalse();
      roleNameControl.setValue('Test Role');
      expect(roleNameControl.valid).toBeTrue();
    });

    it('should have role() signal as null', () => {
      expect(component.role()).toBeNull();
    });

    it('should have isSystemRole() as false', () => {
      expect(component.isSystemRole()).toBeFalse();
    });

    it('should have isRegisteredRole() as false', () => {
      expect(component.isRegisteredRole()).toBeFalse();
    });

    it('should load role groups on init', () => {
      expect(mockRoleService.getRoleGroups).toHaveBeenCalled();
      expect(component.roleGroups()).toEqual(mockRoleGroups);
    });

    it('should not call getRole in create mode', () => {
      expect(mockRoleService.getRole).not.toHaveBeenCalled();
    });
  });

  // ===========================================================================
  // EDIT MODE TESTS
  // MIGRATION: Tests derived from EditRoles.ascx.vb lines 131-178
  // ===========================================================================

  describe('Edit Mode', () => {
    describe('with standard role', () => {
      beforeEach(() => {
        setupEditMode(mockRole);
      });

      it('should load role data when roleId param exists', () => {
        expect(mockRoleService.getRole).toHaveBeenCalledWith(mockRole.roleId);
        expect(component.role()).toEqual(mockRole);
      });

      it('should be in edit mode', () => {
        expect(component.isEditMode()).toBeTrue();
      });

      it('should populate form with loaded role data', () => {
        expect(component.roleForm.value).toEqual(jasmine.objectContaining({
          roleName: mockRole.roleName,
          description: mockRole.description,
          roleGroupId: mockRole.roleGroupId,
          isPublic: mockRole.isPublic,
          autoAssignment: mockRole.autoAssignment
        }));
      });

      it('should set loading to false after role loads', () => {
        expect(component.loading()).toBeFalse();
      });

      it('should not populate billing settings when ServiceFee is 0', () => {
        expect(component.roleForm.controls.serviceFee.value).toBeNull();
      });
    });

    describe('with role not found', () => {
      it('should navigate to /roles when role not found', fakeAsync(() => {
        currentRouteRoleId = '999';
        mockRoleService.getRole.and.returnValue(throwError(() => ({ status: 404 })));
        mockRoleService.getRoleGroups.and.returnValue(of(mockRoleGroups));

        fixture = TestBed.createComponent(RoleFormComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
        tick();

        expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles']);
      }));
    });

    describe('with paid role', () => {
      beforeEach(() => {
        setupEditMode(mockPaidRole);
      });

      it('should populate billing settings when ServiceFee > 0', () => {
        expect(component.roleForm.controls.serviceFee.value).toBe(mockPaidRole.serviceFee);
        expect(component.roleForm.controls.billingPeriod.value).toBe(mockPaidRole.billingPeriod);
        expect(component.roleForm.controls.billingFrequency.value).toBe(mockPaidRole.billingFrequency as string);
      });
    });

    describe('with trial role', () => {
      beforeEach(() => {
        setupEditMode(mockTrialRole);
      });

      it('should populate trial settings when TrialFrequency != N', () => {
        expect(component.roleForm.controls.trialFee.value).toBe(mockTrialRole.trialFee);
        expect(component.roleForm.controls.trialPeriod.value).toBe(mockTrialRole.trialPeriod);
        expect(component.roleForm.controls.trialFrequency.value).toBe(mockTrialRole.trialFrequency as string);
      });
    });

    describe('with RSVP role', () => {
      beforeEach(() => {
        setupEditMode(mockRsvpRole);
      });

      it('should display RSVP link when RSVP code exists', () => {
        expect(component.rsvpLink()).toContain(mockRsvpRole.rsvpCode);
        expect(component.rsvpLink()).toContain(window.location.origin);
      });
    });
  });

  // ===========================================================================
  // SYSTEM ROLE PROTECTION TESTS
  // MIGRATION: Tests derived from EditRoles.ascx.vb lines 174-182
  // ===========================================================================

  describe('System Role Protection', () => {
    it('should identify Administrator role as system role', fakeAsync(() => {
      setupEditMode(mockAdminRole);
      tick();
      expect(component.isSystemRole()).toBeTrue();
    }));

    it('should identify Registered Users role as system role', fakeAsync(() => {
      setupEditMode(mockRegisteredRole);
      tick();
      expect(component.isSystemRole()).toBeTrue();
    }));

    it('should not identify regular role as system role', fakeAsync(() => {
      setupEditMode(mockRole);
      tick();
      expect(component.isSystemRole()).toBeFalse();
    }));

    it('should disable form controls for Administrator role', fakeAsync(() => {
      setupEditMode(mockAdminRole);
      tick();
      fixture.detectChanges();
      
      expect(component.roleForm.controls.roleName.disabled).toBeTrue();
      expect(component.roleForm.controls.description.disabled).toBeTrue();
      expect(component.roleForm.controls.roleGroupId.disabled).toBeTrue();
      expect(component.roleForm.controls.isPublic.disabled).toBeTrue();
    }));

    it('should disable form controls for Registered Users role', fakeAsync(() => {
      setupEditMode(mockRegisteredRole);
      tick();
      fixture.detectChanges();
      
      expect(component.roleForm.controls.roleName.disabled).toBeTrue();
      expect(component.roleForm.controls.description.disabled).toBeTrue();
    }));

    it('should identify Registered Users role with isRegisteredRole()', fakeAsync(() => {
      setupEditMode(mockRegisteredRole);
      tick();
      expect(component.isRegisteredRole()).toBeTrue();
    }));

    it('should not identify Administrator role as Registered role', fakeAsync(() => {
      setupEditMode(mockAdminRole);
      tick();
      expect(component.isRegisteredRole()).toBeFalse();
    }));

    it('should prevent onSubmit for system roles', fakeAsync(() => {
      setupEditMode(mockAdminRole);
      tick();
      component.onSubmit();
      
      expect(mockRoleService.createRole).not.toHaveBeenCalled();
      expect(mockRoleService.updateRole).not.toHaveBeenCalled();
    }));

    it('should prevent showDeleteConfirmation for system roles', fakeAsync(() => {
      setupEditMode(mockAdminRole);
      tick();
      component.showDeleteConfirmation();
      
      // Delete dialog should not be triggered for system roles
      expect(mockRoleService.deleteRole).not.toHaveBeenCalled();
    }));

    it('should prevent onDelete for system roles', fakeAsync(() => {
      setupEditMode(mockAdminRole);
      tick();
      component.onDelete();
      
      expect(mockRoleService.deleteRole).not.toHaveBeenCalled();
    }));
  });

  // ===========================================================================
  // ROLE GROUP DROPDOWN TESTS
  // MIGRATION: Tests derived from BindGroups() (lines 71-81)
  // ===========================================================================

  describe('Role Groups', () => {
    beforeEach(() => {
      setupCreateMode();
    });

    it('should load role groups on init', () => {
      expect(mockRoleService.getRoleGroups).toHaveBeenCalled();
    });

    it('should populate roleGroups signal with loaded data', () => {
      expect(component.roleGroups()).toEqual(mockRoleGroups);
    });

    it('should have roleGroupId default to -1 (Global Roles)', () => {
      expect(component.roleForm.controls.roleGroupId.value).toBe(-1);
    });

    it('should handle error when loading role groups', fakeAsync(() => {
      mockRoleService.getRoleGroups.and.returnValue(throwError(() => new Error('Failed')));
      
      fixture = TestBed.createComponent(RoleFormComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      tick();
      
      expect(component.roleGroups()).toEqual([]);
    }));
  });

  // ===========================================================================
  // FORM VALIDATION TESTS
  // ===========================================================================

  describe('Form Validation', () => {
    beforeEach(() => {
      setupCreateMode();
    });

    it('should require roleName field', () => {
      const roleNameControl = component.roleForm.controls.roleName;
      expect(roleNameControl.hasError('required')).toBeTrue();
      
      roleNameControl.setValue('New Role');
      expect(roleNameControl.hasError('required')).toBeFalse();
    });

    it('should enforce max length on roleName (50 characters)', () => {
      const roleNameControl = component.roleForm.controls.roleName;
      const longName = 'A'.repeat(51);
      roleNameControl.setValue(longName);
      
      expect(roleNameControl.hasError('maxlength')).toBeTrue();
    });

    it('should accept roleName at max length (50 characters)', () => {
      const roleNameControl = component.roleForm.controls.roleName;
      const maxName = 'A'.repeat(50);
      roleNameControl.setValue(maxName);
      
      expect(roleNameControl.hasError('maxlength')).toBeFalse();
    });

    it('should enforce max length on description (1000 characters)', () => {
      const descControl = component.roleForm.controls.description;
      const longDesc = 'A'.repeat(1001);
      descControl.setValue(longDesc);
      
      expect(descControl.hasError('maxlength')).toBeTrue();
    });

    it('should not submit when form is invalid', () => {
      // Form is invalid because roleName is required and empty
      component.onSubmit();
      
      expect(mockRoleService.createRole).not.toHaveBeenCalled();
    });

    it('should mark controls as touched on invalid submit', () => {
      expect(component.roleForm.controls.roleName.touched).toBeFalse();
      
      component.onSubmit();
      
      expect(component.roleForm.controls.roleName.touched).toBeTrue();
    });
  });

  // ===========================================================================
  // BILLING/TRIAL SETTINGS TESTS
  // MIGRATION: Tests derived from lines 216-230
  // ===========================================================================

  describe('Billing and Trial Settings', () => {
    beforeEach(() => {
      setupCreateMode();
    });

    it('should default billing frequency to N (None)', () => {
      expect(component.roleForm.controls.billingFrequency.value).toBe('N');
    });

    it('should default trial frequency to N (None)', () => {
      expect(component.roleForm.controls.trialFrequency.value).toBe('N');
    });

    it('should have all frequency options available', () => {
      expect(component.frequencies.length).toBe(6);
      expect(component.frequencies.map(f => f.value)).toEqual(['N', 'O', 'D', 'W', 'M', 'Y']);
    });

    it('should have correct labels for frequency options', () => {
      const labels = component.frequencies.map(f => f.label);
      expect(labels).toContain('None');
      expect(labels).toContain('One Time');
      expect(labels).toContain('Monthly');
      expect(labels).toContain('Yearly');
    });
  });

  // ===========================================================================
  // SAVE OPERATION TESTS
  // MIGRATION: Tests derived from cmdUpdate_Click (lines 208-274)
  // ===========================================================================

  describe('Save Operation', () => {
    it('should call createRole for new role', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(of({} as Role));
      
      component.roleForm.patchValue({ roleName: 'New Role' });
      component.onSubmit();
      tick();
      
      expect(mockRoleService.createRole).toHaveBeenCalled();
      expect(mockRoleService.updateRole).not.toHaveBeenCalled();
    }));

    it('should call updateRole for existing role', fakeAsync(() => {
      setupEditMode(mockRole);
      mockRoleService.updateRole.and.returnValue(of(mockRole));
      
      component.roleForm.patchValue({ roleName: 'Updated Role' });
      component.onSubmit();
      tick();
      
      expect(mockRoleService.updateRole).toHaveBeenCalledWith(
        mockRole.roleId,
        jasmine.objectContaining({ roleId: mockRole.roleId })
      );
      expect(mockRoleService.createRole).not.toHaveBeenCalled();
    }));

    it('should navigate to role list after successful save', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(of({} as Role));
      
      component.roleForm.patchValue({ roleName: 'New Role' });
      component.onSubmit();
      tick();
      
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles']);
    }));

    it('should show duplicate role error message on 409', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(throwError(() => ({ status: 409 })));
      
      component.roleForm.patchValue({ roleName: 'Duplicate Role' });
      component.onSubmit();
      tick();
      
      expect(component.errorMessage()).toBe('A role with this name already exists.');
    }));

    it('should show validation error message on 400', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(throwError(() => ({ status: 400 })));
      
      component.roleForm.patchValue({ roleName: 'Invalid Role' });
      component.onSubmit();
      tick();
      
      expect(component.errorMessage()).toBe('Please check the form for errors.');
    }));

    it('should show permission error message on 403', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(throwError(() => ({ status: 403 })));
      
      component.roleForm.patchValue({ roleName: 'New Role' });
      component.onSubmit();
      tick();
      
      expect(component.errorMessage()).toBe('You do not have permission to perform this action.');
    }));

    it('should show generic error message on other errors', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(throwError(() => ({ status: 500 })));
      
      component.roleForm.patchValue({ roleName: 'New Role' });
      component.onSubmit();
      tick();
      
      expect(component.errorMessage()).toBe('An error occurred while saving the role. Please try again.');
    }));

    it('should set loading to true during save', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(of({} as Role));
      
      component.roleForm.patchValue({ roleName: 'New Role' });
      
      // Check that loading is set to true during operation
      const loadingBeforeSubscription = component.loading();
      component.onSubmit();
      
      // Loading should be true immediately after calling onSubmit
      expect(component.loading()).toBeTrue();
      
      tick();
    }));

    it('should set loading to false on error', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(throwError(() => ({ status: 500 })));
      
      component.roleForm.patchValue({ roleName: 'New Role' });
      component.onSubmit();
      tick();
      
      expect(component.loading()).toBeFalse();
    }));

    it('should include billing settings when billingFrequency is not N', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(of({} as Role));
      
      component.roleForm.patchValue({
        roleName: 'Paid Role',
        serviceFee: 10.00,
        billingPeriod: 1,
        billingFrequency: 'M'
      });
      component.onSubmit();
      tick();
      
      expect(mockRoleService.createRole).toHaveBeenCalledWith(
        jasmine.objectContaining({
          serviceFee: 10.00,
          billingPeriod: 1,
          billingFrequency: 'M'
        })
      );
    }));

    it('should exclude billing frequency when set to N', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(of({} as Role));
      
      component.roleForm.patchValue({
        roleName: 'Free Role',
        billingFrequency: 'N'
      });
      component.onSubmit();
      tick();
      
      const callArgs = mockRoleService.createRole.calls.mostRecent().args[0];
      expect(callArgs.billingFrequency).toBeUndefined();
    }));

    it('should map roleGroupId -1 to undefined (Global Roles)', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(of({} as Role));
      
      component.roleForm.patchValue({
        roleName: 'Global Role',
        roleGroupId: -1
      });
      component.onSubmit();
      tick();
      
      const callArgs = mockRoleService.createRole.calls.mostRecent().args[0];
      expect(callArgs.roleGroupId).toBeUndefined();
    }));
  });

  // ===========================================================================
  // DELETE OPERATION TESTS
  // MIGRATION: Tests derived from cmdDelete_Click (lines 287-303)
  // ===========================================================================

  describe('Delete Operation', () => {
    it('should call deleteRole service method', fakeAsync(() => {
      setupEditMode(mockRole);
      mockRoleService.deleteRole.and.returnValue(of(void 0));
      
      component.onDelete();
      tick();
      
      expect(mockRoleService.deleteRole).toHaveBeenCalledWith(mockRole.roleId);
    }));

    it('should navigate to role list after delete', fakeAsync(() => {
      setupEditMode(mockRole);
      mockRoleService.deleteRole.and.returnValue(of(void 0));
      
      component.onDelete();
      tick();
      
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles']);
    }));

    it('should not delete in create mode', () => {
      setupCreateMode();
      component.onDelete();
      
      expect(mockRoleService.deleteRole).not.toHaveBeenCalled();
    });

    it('should not delete system roles', () => {
      setupEditMode(mockAdminRole);
      component.onDelete();
      
      expect(mockRoleService.deleteRole).not.toHaveBeenCalled();
    });

    it('should show error when role has users assigned (400)', fakeAsync(() => {
      setupEditMode(mockRole);
      mockRoleService.deleteRole.and.returnValue(throwError(() => ({ status: 400 })));
      
      component.onDelete();
      tick();
      
      expect(component.errorMessage()).toBe('Cannot delete this role. It may have users assigned.');
    }));

    it('should show permission error on 403', fakeAsync(() => {
      setupEditMode(mockRole);
      mockRoleService.deleteRole.and.returnValue(throwError(() => ({ status: 403 })));
      
      component.onDelete();
      tick();
      
      expect(component.errorMessage()).toBe('You do not have permission to delete this role.');
    }));

    it('should show not found error and navigate on 404', fakeAsync(() => {
      setupEditMode(mockRole);
      mockRoleService.deleteRole.and.returnValue(throwError(() => ({ status: 404 })));
      
      component.onDelete();
      tick();
      
      expect(component.errorMessage()).toBe('Role not found. It may have already been deleted.');
      
      // Should navigate after delay
      tick(2000);
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles']);
    }));

    it('should set loading to true during delete', fakeAsync(() => {
      setupEditMode(mockRole);
      mockRoleService.deleteRole.and.returnValue(of(void 0));
      
      component.onDelete();
      expect(component.loading()).toBeTrue();
      
      tick();
    }));

    it('should set loading to false on delete error', fakeAsync(() => {
      setupEditMode(mockRole);
      mockRoleService.deleteRole.and.returnValue(throwError(() => ({ status: 500 })));
      
      component.onDelete();
      tick();
      
      expect(component.loading()).toBeFalse();
    }));
  });

  // ===========================================================================
  // CANCEL OPERATION TEST
  // MIGRATION: Tests derived from cmdCancel_Click (lines 316-323)
  // ===========================================================================

  describe('Cancel Operation', () => {
    it('should navigate to role list on cancel', () => {
      setupCreateMode();
      component.onCancel();
      
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles']);
    });

    it('should navigate to role list on cancel in edit mode', () => {
      setupEditMode(mockRole);
      component.onCancel();
      
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles']);
    });
  });

  // ===========================================================================
  // MANAGE USERS OPERATION TESTS
  // MIGRATION: Tests derived from cmdManage_Click (lines 336-342)
  // ===========================================================================

  describe('Manage Users', () => {
    it('should navigate to user roles in edit mode', () => {
      setupEditMode(mockRole);
      component.onManageUsers();
      
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles', mockRole.roleId, 'users']);
    });

    it('should not navigate in create mode', () => {
      setupCreateMode();
      mockRouter.navigate.calls.reset();
      component.onManageUsers();
      
      expect(mockRouter.navigate).not.toHaveBeenCalled();
    });

    it('should not navigate for Registered Users role', () => {
      setupEditMode(mockRegisteredRole);
      mockRouter.navigate.calls.reset();
      component.onManageUsers();
      
      // Should not navigate because this is the Registered Users role
      expect(mockRouter.navigate).not.toHaveBeenCalledWith(['/roles', mockRegisteredRole.roleId, 'users']);
    });
  });

  // ===========================================================================
  // RSVP LINK TESTS
  // MIGRATION: Tests derived from lines 166-168
  // ===========================================================================

  describe('RSVP Link', () => {
    it('should update RSVP link when rsvpCode changes', fakeAsync(() => {
      setupCreateMode();
      
      component.roleForm.controls.rsvpCode.setValue('NEWCODE123');
      tick();
      
      expect(component.rsvpLink()).toContain('NEWCODE123');
      expect(component.rsvpLink()).toContain(window.location.origin);
    }));

    it('should clear RSVP link when rsvpCode is empty', fakeAsync(() => {
      setupCreateMode();
      
      component.roleForm.controls.rsvpCode.setValue('SOMECODE');
      tick();
      expect(component.rsvpLink()).not.toBeNull();
      
      component.roleForm.controls.rsvpCode.setValue('');
      tick();
      expect(component.rsvpLink()).toBeNull();
    }));

    it('should display existing RSVP link when loading role', () => {
      setupEditMode(mockRsvpRole);
      
      expect(component.rsvpLink()).toBe(`${window.location.origin}/?rsvp=${mockRsvpRole.rsvpCode}`);
    });

    it('should copy RSVP link to clipboard', fakeAsync(() => {
      setupEditMode(mockRsvpRole);
      
      const clipboardSpy = spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
      
      component.copyRsvpLink();
      tick();
      
      expect(clipboardSpy).toHaveBeenCalledWith(component.rsvpLink()!);
    }));
  });

  // ===========================================================================
  // SIGNAL STATE TESTS
  // ===========================================================================

  describe('Signal State Management', () => {
    it('should initialize loading signal as true', () => {
      // Before ngOnInit completes loading of edit mode
      currentRouteRoleId = '10';
      mockRoleService.getRole.and.returnValue(of(mockRole));
      mockRoleService.getRoleGroups.and.returnValue(of(mockRoleGroups));
      
      fixture = TestBed.createComponent(RoleFormComponent);
      component = fixture.componentInstance;
      
      // Initial state before detectChanges/ngOnInit
      expect(component.loading()).toBeTrue();
    });

    it('should update role signal when data loads', () => {
      setupEditMode(mockRole);
      
      expect(component.role()).toEqual(mockRole);
    });

    it('should update roleGroups signal when groups load', () => {
      setupCreateMode();
      
      expect(component.roleGroups()).toEqual(mockRoleGroups);
    });

    it('should update errorMessage signal on API error', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(throwError(() => ({ status: 500 })));
      
      expect(component.errorMessage()).toBeNull();
      
      component.roleForm.patchValue({ roleName: 'Test' });
      component.onSubmit();
      tick();
      
      expect(component.errorMessage()).not.toBeNull();
    }));

    it('should clear errorMessage on new submission', fakeAsync(() => {
      setupCreateMode();
      mockRoleService.createRole.and.returnValue(throwError(() => ({ status: 500 })));
      
      component.roleForm.patchValue({ roleName: 'Test' });
      component.onSubmit();
      tick();
      
      expect(component.errorMessage()).not.toBeNull();
      
      // Start a new submission - errorMessage should be cleared
      mockRoleService.createRole.and.returnValue(of({} as Role));
      component.onSubmit();
      
      // Before tick, error should be cleared
      expect(component.errorMessage()).toBeNull();
      
      tick();
    }));
  });

  // ===========================================================================
  // FORM CONTROL STATE TESTS
  // ===========================================================================

  describe('Form Control State', () => {
    it('should have all expected form controls', () => {
      setupCreateMode();
      
      const expectedControls = [
        'roleName', 'description', 'roleGroupId', 'isPublic', 'autoAssignment',
        'serviceFee', 'billingPeriod', 'billingFrequency',
        'trialFee', 'trialPeriod', 'trialFrequency',
        'rsvpCode', 'iconFile'
      ];
      
      expectedControls.forEach(controlName => {
        expect(component.roleForm.contains(controlName))
          .withContext(`Form should contain ${controlName}`)
          .toBeTrue();
      });
    });

    it('should have correct initial values for boolean controls', () => {
      setupCreateMode();
      
      expect(component.roleForm.controls.isPublic.value).toBeFalse();
      expect(component.roleForm.controls.autoAssignment.value).toBeFalse();
    });

    it('should have correct initial values for numeric controls', () => {
      setupCreateMode();
      
      expect(component.roleForm.controls.serviceFee.value).toBeNull();
      expect(component.roleForm.controls.billingPeriod.value).toBeNull();
      expect(component.roleForm.controls.trialFee.value).toBeNull();
      expect(component.roleForm.controls.trialPeriod.value).toBeNull();
    });
  });

  // ===========================================================================
  // NAVIGATION GUARD TESTS
  // ===========================================================================

  describe('Navigation Guards', () => {
    it('should navigate to /roles for invalid roleId format', fakeAsync(() => {
      currentRouteRoleId = 'invalid';
      mockRoleService.getRole.and.returnValue(throwError(() => ({ status: 400 })));
      mockRoleService.getRoleGroups.and.returnValue(of(mockRoleGroups));
      
      fixture = TestBed.createComponent(RoleFormComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      tick();
      
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles']);
    }));

    it('should navigate to /roles for negative roleId', fakeAsync(() => {
      // Note: roleId 0 is actually the Admin role, but negative numbers should redirect
      currentRouteRoleId = '-1';
      mockRoleService.getRole.and.returnValue(throwError(() => ({ status: 400 })));
      mockRoleService.getRoleGroups.and.returnValue(of(mockRoleGroups));
      
      fixture = TestBed.createComponent(RoleFormComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      tick();
      
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles']);
    }));
  });

  // ===========================================================================
  // ICON ERROR HANDLER TEST
  // ===========================================================================

  describe('Icon Error Handler', () => {
    it('should hide image on icon load error', () => {
      setupCreateMode();
      
      const mockImg = document.createElement('img');
      mockImg.style.display = 'block';
      
      component.onIconError({ target: mockImg } as unknown as Event);
      
      expect(mockImg.style.display).toBe('none');
    });
  });
});
