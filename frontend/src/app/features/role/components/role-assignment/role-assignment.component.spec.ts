/**
 * Unit Tests for RoleAssignmentComponent
 *
 * MIGRATION: Comprehensive test suite for the Angular 19 standalone component
 * that manages user-role assignments. Tests derived from legacy VB.NET patterns
 * in Website/admin/Security/SecurityRoles.ascx.vb.
 *
 * Test Coverage Requirements (Section 0.7.5):
 * - Components: 80% minimum coverage
 * - User interactions and form validation
 * - Angular Unit Tests: 100% pass rate
 *
 * @fileoverview Jasmine unit tests for RoleAssignmentComponent
 */

import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';

// Internal imports from depends_on_files
import {
  RoleAssignmentComponent,
  UserRoleDisplay
} from './role-assignment.component';
import {
  RoleService,
  UserRole,
  AddUserRoleRequest
} from '../../services/role.service';
import { Role } from '../../models/role.model';
import { UserService } from '../../../user/services/user.service';
import { UserDto } from '../../../user/models/user.model';

// ============================================================================
// TEST DATA FACTORIES
// ============================================================================

/**
 * Creates a mock Role object for testing.
 * MIGRATION: Properties derived from RoleInfo.vb entity.
 */
function createMockRole(overrides: Partial<Role> = {}): Role {
  return {
    roleId: 1,
    portalId: 0,
    roleGroupId: null,
    roleName: 'Registered Users',
    description: 'Test role description',
    serviceFee: 0,
    billingFrequency: 'M',
    billingPeriod: 1,
    trialFee: 0,
    trialFrequency: null,
    trialPeriod: 0,
    isPublic: true,
    autoAssignment: false,
    rsvpCode: null,
    iconFile: null,
    ...overrides
  };
}

/**
 * Creates a mock UserRoleDisplay for testing grid display.
 * MIGRATION: Derived from grdUserRoles DataSource binding (lines 246, 253).
 */
function createMockUserRoleDisplay(overrides: Partial<UserRoleDisplay> = {}): UserRoleDisplay {
  return {
    userId: 1,
    username: 'testuser',
    displayName: 'Test User',
    effectiveDate: '2024-01-01',
    expiryDate: '2025-01-01',
    ...overrides
  };
}

/**
 * Creates a mock UserRole for testing service responses.
 * MIGRATION: Derived from UserRoleInfo properties in RoleController.vb.
 */
function createMockUserRole(overrides: Partial<UserRole> = {}): UserRole {
  return {
    userRoleId: 1,
    userId: 1,
    roleId: 1,
    effectiveDate: new Date('2024-01-01'),
    expiryDate: new Date('2025-01-01'),
    isTrialUsed: false,
    ...overrides
  };
}

/**
 * Creates a mock UserDto for testing user service responses.
 * MIGRATION: Derived from UserInfo.vb entity.
 */
function createMockUserDto(overrides: Partial<UserDto> = {}): UserDto {
  return {
    userId: 1,
    username: 'testuser',
    displayName: 'Test User',
    firstName: 'Test',
    lastName: 'User',
    email: 'testuser@example.com',
    portalId: 0,
    isSuperUser: false,
    affiliateId: null,
    isDeleted: false,
    approved: true,
    lockedOut: false,
    isOnline: false,
    createdDate: '2024-01-01T00:00:00Z',
    lastLoginDate: null,
    lastActivityDate: null,
    lastModifiedDate: null,
    roles: ['Registered Users'],
    ...overrides
  };
}

// ============================================================================
// TEST SUITE
// ============================================================================

describe('RoleAssignmentComponent', () => {
  let component: RoleAssignmentComponent;
  let fixture: ComponentFixture<RoleAssignmentComponent>;
  let roleServiceSpy: jasmine.SpyObj<RoleService>;
  let userServiceSpy: jasmine.SpyObj<UserService>;
  let mockActivatedRoute: { snapshot: { paramMap: { get: jasmine.Spy } } };

  // Default mock data
  const mockRole = createMockRole();
  const mockUserRoleDisplay = createMockUserRoleDisplay();
  const mockUserRole = createMockUserRole();
  const mockUserDto = createMockUserDto();
  const mockUsersInRole = [mockUserDto];
  const mockAvailableUsers = [
    createMockUserDto({ userId: 2, username: 'user2', displayName: 'User Two' }),
    createMockUserDto({ userId: 3, username: 'user3', displayName: 'User Three' })
  ];

  beforeEach(async () => {
    // Create spy objects for services
    roleServiceSpy = jasmine.createSpyObj('RoleService', [
      'getRole',
      'getUsersInRole',
      'getUserRole',
      'addUserToRole',
      'removeUserFromRole'
    ]);

    userServiceSpy = jasmine.createSpyObj('UserService', [
      'getUsers',
      'getUserByUsername'
    ]);

    // Mock ActivatedRoute
    mockActivatedRoute = {
      snapshot: {
        paramMap: {
          get: jasmine.createSpy('get').and.returnValue('1')
        }
      }
    };

    // Set up default spy return values
    roleServiceSpy.getRole.and.returnValue(of(mockRole));
    roleServiceSpy.getUsersInRole.and.returnValue(of(mockUsersInRole));
    roleServiceSpy.getUserRole.and.returnValue(of(mockUserRole));
    roleServiceSpy.addUserToRole.and.returnValue(of(void 0));
    roleServiceSpy.removeUserFromRole.and.returnValue(of(void 0));

    userServiceSpy.getUsers.and.returnValue(of({
      items: mockAvailableUsers,
      totalCount: mockAvailableUsers.length,
      pageIndex: 0,
      pageSize: 10,
      totalPages: 1
    }));
    userServiceSpy.getUserByUsername.and.returnValue(of(mockUserDto));

    await TestBed.configureTestingModule({
      imports: [RoleAssignmentComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: RoleService, useValue: roleServiceSpy },
        { provide: UserService, useValue: userServiceSpy },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RoleAssignmentComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    // Reset all mocks
    roleServiceSpy.getRole.calls.reset();
    roleServiceSpy.getUsersInRole.calls.reset();
    roleServiceSpy.getUserRole.calls.reset();
    roleServiceSpy.addUserToRole.calls.reset();
    roleServiceSpy.removeUserFromRole.calls.reset();
    userServiceSpy.getUsers.calls.reset();
    userServiceSpy.getUserByUsername.calls.reset();
    mockActivatedRoute.snapshot.paramMap.get.calls.reset();
  });

  // ==========================================================================
  // 1. COMPONENT INITIALIZATION TESTS
  // MIGRATION: Based on Page_Init (lines 411-420), Page_Load (lines 435-445)
  // ==========================================================================

  describe('Component Initialization', () => {
    it('should create the component', () => {
      expect(component).toBeTruthy();
    });

    it('should load role from route parameter on init', fakeAsync(() => {
      fixture.detectChanges();
      tick();

      expect(mockActivatedRoute.snapshot.paramMap.get).toHaveBeenCalledWith('id');
      expect(roleServiceSpy.getRole).toHaveBeenCalledWith(1);
    }));

    it('should set loading signal to true during data fetch', fakeAsync(() => {
      // Initially loading should be true before detectChanges
      expect(component.loading()).toBeTrue();

      fixture.detectChanges();
      tick();

      // After async operations complete, loading should be false
      expect(component.loading()).toBeFalse();
    }));

    it('should load users in role on initialization', fakeAsync(() => {
      fixture.detectChanges();
      tick();

      expect(roleServiceSpy.getUsersInRole).toHaveBeenCalledWith(1);
    }));

    it('should display role title with role name and ID', fakeAsync(() => {
      // MIGRATION: From lblTitle.Text pattern (line 193)
      fixture.detectChanges();
      tick();

      const role = component.selectedRole();
      expect(role).toBeTruthy();
      expect(role?.roleName).toBe('Registered Users');
      expect(role?.roleId).toBe(1);
    }));

    it('should handle invalid role ID in route parameter', fakeAsync(() => {
      mockActivatedRoute.snapshot.paramMap.get.and.returnValue('invalid');

      fixture.detectChanges();
      tick();

      expect(component.errorMessage()).toBe('Invalid role ID');
      expect(component.loading()).toBeFalse();
    }));

    it('should handle missing role ID in route parameter', fakeAsync(() => {
      mockActivatedRoute.snapshot.paramMap.get.and.returnValue(null);

      fixture.detectChanges();
      tick();

      expect(component.errorMessage()).toBe('Role ID is required');
      expect(component.loading()).toBeFalse();
    }));

    it('should handle role service error on initialization', fakeAsync(() => {
      roleServiceSpy.getRole.and.returnValue(throwError(() => new Error('API Error')));

      fixture.detectChanges();
      tick();

      expect(component.errorMessage()).toBe('Failed to load role data. Please try again.');
      expect(component.loading()).toBeFalse();
    }));
  });

  // ==========================================================================
  // 2. USER LIST DISPLAY TESTS
  // MIGRATION: Based on BindGrid (lines 239-257)
  // ==========================================================================

  describe('User List Display', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('should display users in the role grid', () => {
      const usersInRole = component.usersInRole();
      expect(usersInRole.length).toBeGreaterThan(0);
    });

    it('should show user display name as link', () => {
      // MIGRATION: From FormatUser function (lines 394-396)
      const usersInRole = component.usersInRole();
      expect(usersInRole[0].displayName).toBeTruthy();
      expect(usersInRole[0].username).toBeTruthy();
    });

    it('should show effective date for each user-role', () => {
      const usersInRole = component.usersInRole();
      expect(usersInRole[0].effectiveDate).toBeTruthy();
    });

    it('should show expiry date for each user-role', () => {
      const usersInRole = component.usersInRole();
      expect(usersInRole[0].expiryDate).toBeTruthy();
    });

    it('should show delete button when user can be removed', () => {
      // MIGRATION: From DeleteButtonVisible (lines 360-363)
      const canDelete = component.canDeleteUserRole(1);
      expect(canDelete).toBeTrue();
    });

    it('should format date correctly', () => {
      // MIGRATION: From FormatDate function (lines 377-383)
      const formattedDate = component.formatDate('2024-01-15');
      expect(formattedDate).toBeTruthy();
    });

    it('should return empty string for null dates', () => {
      const formattedDate = component.formatDate(null);
      expect(formattedDate).toBe('');
    });

    it('should update filteredUsersInRole computed signal', () => {
      const filtered = component.filteredUsersInRole();
      expect(filtered).toBeTruthy();
      expect(Array.isArray(filtered)).toBeTrue();
    });
  });

  // ==========================================================================
  // 3. ADD USER TO ROLE TESTS
  // MIGRATION: Based on cmdAdd_Click (lines 518-551)
  // ==========================================================================

  describe('Add User to Role', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('should add user to role with form data', fakeAsync(() => {
      // Set up user selection
      component.selectedUserId.set(2);
      component.userRoleForm.patchValue({
        effectiveDate: '2024-06-01',
        expiryDate: '2025-06-01',
        notify: false
      });

      component.onAddUserToRole();
      tick();

      expect(roleServiceSpy.addUserToRole).toHaveBeenCalledWith(
        2,
        1,
        jasmine.objectContaining({
          effectiveDate: '2024-06-01',
          expiryDate: '2025-06-01'
        })
      );
    }));

    it('should parse effective date from form input', fakeAsync(() => {
      component.selectedUserId.set(2);
      component.userRoleForm.patchValue({
        effectiveDate: '2024-03-15',
        expiryDate: null
      });

      component.onAddUserToRole();
      tick();

      const callArgs = roleServiceSpy.addUserToRole.calls.mostRecent().args;
      expect(callArgs[2]?.effectiveDate).toBe('2024-03-15');
    }));

    it('should parse expiry date from form input', fakeAsync(() => {
      component.selectedUserId.set(2);
      component.userRoleForm.patchValue({
        effectiveDate: null,
        expiryDate: '2025-12-31'
      });

      component.onAddUserToRole();
      tick();

      const callArgs = roleServiceSpy.addUserToRole.calls.mostRecent().args;
      expect(callArgs[2]?.expiryDate).toBe('2025-12-31');
    }));

    it('should call addUserToRole service method', fakeAsync(() => {
      component.selectedUserId.set(2);

      component.onAddUserToRole();
      tick();

      expect(roleServiceSpy.addUserToRole).toHaveBeenCalled();
    }));

    it('should refresh user list after adding', fakeAsync(() => {
      component.selectedUserId.set(2);
      roleServiceSpy.getUsersInRole.calls.reset();

      component.onAddUserToRole();
      tick();

      expect(roleServiceSpy.getUsersInRole).toHaveBeenCalledWith(1);
    }));

    it('should handle add user errors', fakeAsync(() => {
      component.selectedUserId.set(2);
      roleServiceSpy.addUserToRole.and.returnValue(
        throwError(() => new Error('Add failed'))
      );

      component.onAddUserToRole();
      tick();

      expect(component.errorMessage()).toBe('Failed to add user to role. Please try again.');
    }));

    it('should display error when no user is selected', () => {
      component.selectedUserId.set(null);

      component.onAddUserToRole();

      expect(component.errorMessage()).toBe('Please select a user first');
      expect(roleServiceSpy.addUserToRole).not.toHaveBeenCalled();
    });

    it('should display success message after adding user', fakeAsync(() => {
      component.selectedUserId.set(2);

      component.onAddUserToRole();
      tick();

      expect(component.successMessage()).toContain('successfully');
    }));

    it('should set submitting signal during add operation', fakeAsync(() => {
      component.selectedUserId.set(2);

      // Capture submitting state during the operation
      let wasSubmitting = false;
      const originalAddUserToRole = roleServiceSpy.addUserToRole;
      roleServiceSpy.addUserToRole.and.callFake(() => {
        wasSubmitting = component.submitting();
        return of(void 0);
      });

      component.onAddUserToRole();
      tick();

      expect(wasSubmitting).toBeTrue();
      expect(component.submitting()).toBeFalse();
    }));
  });

  // ==========================================================================
  // 4. REMOVE USER FROM ROLE TESTS
  // MIGRATION: Based on grdUserRoles_Delete (lines 565-589)
  // ==========================================================================

  describe('Remove User from Role', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('should remove user from role on delete', fakeAsync(() => {
      const userRole = createMockUserRoleDisplay({ userId: 2, username: 'user2' });
      
      // Initiate delete
      component.onDeleteUserRole(userRole);
      
      // Confirm delete
      component.onConfirmDelete();
      tick();

      expect(roleServiceSpy.removeUserFromRole).toHaveBeenCalledWith(2, 1);
    }));

    it('should set userRoleToDelete signal before delete', () => {
      // MIGRATION: From AddButtonConfirm (line 608)
      const userRole = createMockUserRoleDisplay({ userId: 2 });
      
      component.onDeleteUserRole(userRole);

      expect(component.userRoleToDelete()).toEqual(userRole);
    });

    it('should display error message on removal failure', fakeAsync(() => {
      roleServiceSpy.removeUserFromRole.and.returnValue(
        throwError(() => new Error('Remove failed'))
      );
      
      const userRole = createMockUserRoleDisplay({ userId: 2 });
      component.onDeleteUserRole(userRole);
      component.onConfirmDelete();
      tick();

      expect(component.errorMessage()).toContain('Unable to remove user');
    }));

    it('should refresh grid after deletion', fakeAsync(() => {
      const userRole = createMockUserRoleDisplay({ userId: 2 });
      roleServiceSpy.getUsersInRole.calls.reset();
      
      component.onDeleteUserRole(userRole);
      component.onConfirmDelete();
      tick();

      expect(roleServiceSpy.getUsersInRole).toHaveBeenCalledWith(1);
    }));

    it('should clear userRoleToDelete after confirmation', fakeAsync(() => {
      const userRole = createMockUserRoleDisplay({ userId: 2 });
      
      component.onDeleteUserRole(userRole);
      component.onConfirmDelete();
      tick();

      expect(component.userRoleToDelete()).toBeNull();
    }));

    it('should clear userRoleToDelete on cancel', () => {
      const userRole = createMockUserRoleDisplay({ userId: 2 });
      
      component.onDeleteUserRole(userRole);
      component.onCancelDelete();

      expect(component.userRoleToDelete()).toBeNull();
    });

    it('should generate correct delete confirmation message', () => {
      const userRole = createMockUserRoleDisplay({
        userId: 2,
        username: 'johndoe',
        displayName: 'John Doe'
      });
      
      component.onDeleteUserRole(userRole);

      const message = component.deleteConfirmationMessage();
      expect(message).toContain('John Doe');
      expect(message).toContain('johndoe');
    });

    it('should handle delete when role is not selected', fakeAsync(() => {
      const userRole = createMockUserRoleDisplay({ userId: 2 });
      component.selectedRole.set(null);
      
      component.onDeleteUserRole(userRole);
      component.onConfirmDelete();
      tick();

      expect(roleServiceSpy.removeUserFromRole).not.toHaveBeenCalled();
    }));
  });

  // ==========================================================================
  // 5. DATE HANDLING TESTS
  // MIGRATION: Based on GetDates (lines 273-303)
  // ==========================================================================

  describe('Date Handling', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('should load existing effective/expiry dates for user-role', fakeAsync(() => {
      const existingUserRole = createMockUserRole({
        effectiveDate: new Date('2024-03-01'),
        expiryDate: new Date('2025-03-01')
      });
      roleServiceSpy.getUserRole.and.returnValue(of(existingUserRole));
      
      // Trigger user selection which loads dates
      const mockEvent = { target: { value: '2' } } as unknown as Event;
      component.onUserSelected(mockEvent);
      tick();

      expect(roleServiceSpy.getUserRole).toHaveBeenCalledWith(2, 1);
    }));

    it('should calculate default expiry from billing settings with Daily frequency', fakeAsync(() => {
      // MIGRATION: Lines 290-297 - billing frequency calculations
      const roleWithDailyBilling = createMockRole({
        billingFrequency: 'D',
        billingPeriod: 30
      });
      component.selectedRole.set(roleWithDailyBilling);
      roleServiceSpy.getUserRole.and.returnValue(of(null));

      const mockEvent = { target: { value: '2' } } as unknown as Event;
      component.onUserSelected(mockEvent);
      tick();

      // The form should have an expiry date set
      const expiryDate = component.userRoleForm.get('expiryDate')?.value;
      expect(expiryDate).toBeTruthy();
    }));

    it('should calculate default expiry from billing settings with Weekly frequency', fakeAsync(() => {
      const roleWithWeeklyBilling = createMockRole({
        billingFrequency: 'W',
        billingPeriod: 4
      });
      component.selectedRole.set(roleWithWeeklyBilling);
      roleServiceSpy.getUserRole.and.returnValue(of(null));

      const mockEvent = { target: { value: '2' } } as unknown as Event;
      component.onUserSelected(mockEvent);
      tick();

      const expiryDate = component.userRoleForm.get('expiryDate')?.value;
      expect(expiryDate).toBeTruthy();
    }));

    it('should calculate default expiry from billing settings with Monthly frequency', fakeAsync(() => {
      const roleWithMonthlyBilling = createMockRole({
        billingFrequency: 'M',
        billingPeriod: 12
      });
      component.selectedRole.set(roleWithMonthlyBilling);
      roleServiceSpy.getUserRole.and.returnValue(of(null));

      const mockEvent = { target: { value: '2' } } as unknown as Event;
      component.onUserSelected(mockEvent);
      tick();

      const expiryDate = component.userRoleForm.get('expiryDate')?.value;
      expect(expiryDate).toBeTruthy();
    }));

    it('should calculate default expiry from billing settings with Yearly frequency', fakeAsync(() => {
      const roleWithYearlyBilling = createMockRole({
        billingFrequency: 'Y',
        billingPeriod: 1
      });
      component.selectedRole.set(roleWithYearlyBilling);
      roleServiceSpy.getUserRole.and.returnValue(of(null));

      const mockEvent = { target: { value: '2' } } as unknown as Event;
      component.onUserSelected(mockEvent);
      tick();

      const expiryDate = component.userRoleForm.get('expiryDate')?.value;
      expect(expiryDate).toBeTruthy();
    }));

    it('should handle null dates gracefully', () => {
      const userRoleWithNullDates = createMockUserRoleDisplay({
        effectiveDate: null,
        expiryDate: null
      });

      const formattedEffective = component.formatDate(userRoleWithNullDates.effectiveDate);
      const formattedExpiry = component.formatDate(userRoleWithNullDates.expiryDate);

      expect(formattedEffective).toBe('');
      expect(formattedExpiry).toBe('');
    });

    it('should handle invalid date strings', () => {
      const formattedDate = component.formatDate('invalid-date');
      expect(formattedDate).toBe('');
    });
  });

  // ==========================================================================
  // 6. USER SELECTOR TESTS
  // MIGRATION: Based on BindData (lines 178-226)
  // ==========================================================================

  describe('User Selector', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('should display user dropdown in combo mode', fakeAsync(() => {
      // By default, with few users, combo mode should be used
      expect(component.usersControlMode()).toBe('combo');
    }));

    it('should display user text input in textbox mode when many users', fakeAsync(() => {
      // Create more than 25 users to trigger textbox mode
      const manyUsers = Array.from({ length: 30 }, (_, i) => 
        createMockUserDto({ userId: i + 1, username: `user${i + 1}` })
      );
      userServiceSpy.getUsers.and.returnValue(of({
        items: manyUsers,
        totalCount: 30,
        pageIndex: 0,
        pageSize: 30,
        totalPages: 1
      }));

      // Re-initialize component
      component.ngOnInit();
      tick();

      expect(component.usersControlMode()).toBe('textbox');
    }));

    it('should validate username on validate button click', fakeAsync(() => {
      // MIGRATION: cmdValidate_Click (lines 476-488)
      component.usernameInput = 'testuser';

      component.onValidateUsername();
      tick();

      expect(userServiceSpy.getUserByUsername).toHaveBeenCalledWith('testuser');
    }));

    it('should set selectedUserId after successful validation', fakeAsync(() => {
      component.usernameInput = 'testuser';

      component.onValidateUsername();
      tick();

      expect(component.selectedUserId()).toBe(mockUserDto.userId);
    }));

    it('should set validatedUser signal after successful validation', fakeAsync(() => {
      component.usernameInput = 'testuser';

      component.onValidateUsername();
      tick();

      const validated = component.validatedUser();
      expect(validated).toBeTruthy();
      expect(validated?.username).toBe('testuser');
    }));

    it('should display error when username is empty', () => {
      component.usernameInput = '';

      component.onValidateUsername();

      expect(component.usernameValidationError()).toBe('Please enter a username');
    });

    it('should display error when username is not found', fakeAsync(() => {
      userServiceSpy.getUserByUsername.and.returnValue(of(null));
      component.usernameInput = 'nonexistent';

      component.onValidateUsername();
      tick();

      expect(component.usernameValidationError()).toContain('User not found');
    }));

    it('should handle username validation errors', fakeAsync(() => {
      userServiceSpy.getUserByUsername.and.returnValue(
        throwError(() => new Error('Validation failed'))
      );
      component.usernameInput = 'testuser';

      component.onValidateUsername();
      tick();

      expect(component.usernameValidationError()).toContain('Failed to validate');
    }));

    it('should set validatingUsername signal during validation', fakeAsync(() => {
      let wasValidating = false;
      userServiceSpy.getUserByUsername.and.callFake(() => {
        wasValidating = component.validatingUsername();
        return of(mockUserDto);
      });
      component.usernameInput = 'testuser';

      component.onValidateUsername();
      tick();

      expect(wasValidating).toBeTrue();
      expect(component.validatingUsername()).toBeFalse();
    }));

    it('should load dates after successful username validation', fakeAsync(() => {
      component.usernameInput = 'testuser';
      roleServiceSpy.getUserRole.calls.reset();

      component.onValidateUsername();
      tick();

      expect(roleServiceSpy.getUserRole).toHaveBeenCalledWith(mockUserDto.userId, 1);
    }));

    it('should handle user selection from dropdown', fakeAsync(() => {
      const mockEvent = { target: { value: '2' } } as unknown as Event;

      component.onUserSelected(mockEvent);
      tick();

      expect(component.selectedUserId()).toBe(2);
    }));

    it('should clear validation state on dropdown selection', fakeAsync(() => {
      // First validate a user
      component.validatedUser.set({ userId: 1, username: 'old', displayName: 'Old', email: 'old@test.com' });

      const mockEvent = { target: { value: '2' } } as unknown as Event;
      component.onUserSelected(mockEvent);
      tick();

      expect(component.validatedUser()).toBeNull();
    }));

    it('should handle invalid selection value', () => {
      const mockEvent = { target: { value: 'invalid' } } as unknown as Event;

      component.onUserSelected(mockEvent);

      expect(component.selectedUserId()).toBeNull();
    });
  });

  // ==========================================================================
  // 7. SIGNAL STATE TESTS
  // Angular 19 specific signal management
  // ==========================================================================

  describe('Signal State Management', () => {
    it('should update users signal when data loads', fakeAsync(() => {
      fixture.detectChanges();
      tick();

      expect(component.usersInRole()).toBeTruthy();
      expect(component.usersInRole().length).toBeGreaterThan(0);
    }));

    it('should update loading signal during operations', fakeAsync(() => {
      expect(component.loading()).toBeTrue();

      fixture.detectChanges();
      tick();

      expect(component.loading()).toBeFalse();
    }));

    it('should update selectedRole signal', fakeAsync(() => {
      fixture.detectChanges();
      tick();

      expect(component.selectedRole()).toBeTruthy();
      expect(component.selectedRole()?.roleId).toBe(1);
    }));

    it('should update errorMessage signal', () => {
      component.errorMessage.set('Test error');
      expect(component.errorMessage()).toBe('Test error');
    });

    it('should update successMessage signal', () => {
      component.successMessage.set('Test success');
      expect(component.successMessage()).toBe('Test success');
    });

    it('should update availableUsers signal', fakeAsync(() => {
      fixture.detectChanges();
      tick();

      expect(component.availableUsers()).toBeTruthy();
      expect(Array.isArray(component.availableUsers())).toBeTrue();
    }));

    it('should compute addButtonText based on selection state', fakeAsync(() => {
      fixture.detectChanges();
      tick();

      // No user selected
      component.selectedUserId.set(null);
      expect(component.addButtonText()).toBe('Add User');
    }));

    it('should compute canAddUser correctly', fakeAsync(() => {
      fixture.detectChanges();
      tick();

      // No user selected
      component.selectedUserId.set(null);
      expect(component.canAddUser()).toBeFalse();

      // User selected and role loaded
      component.selectedUserId.set(2);
      expect(component.canAddUser()).toBeTrue();
    }));

    it('should update currentPage signal for pagination', () => {
      component.currentPage.set(1);
      expect(component.currentPage()).toBe(1);

      component.onNextPage();
      expect(component.currentPage()).toBe(2);
    });

    it('should compute totalPages correctly', fakeAsync(() => {
      fixture.detectChanges();
      tick();

      const totalPages = component.totalPages();
      expect(totalPages).toBeGreaterThanOrEqual(1);
    }));
  });

  // ==========================================================================
  // 8. ERROR HANDLING TESTS
  // ==========================================================================

  describe('Error Handling', () => {
    it('should handle getUsersInRole error gracefully', fakeAsync(() => {
      roleServiceSpy.getUsersInRole.and.returnValue(
        throwError(() => new Error('Users fetch failed'))
      );

      fixture.detectChanges();
      tick();

      // Should set empty array on error
      expect(component.usersInRole()).toEqual([]);
    }));

    it('should handle getUsers (available users) error gracefully', fakeAsync(() => {
      userServiceSpy.getUsers.and.returnValue(
        throwError(() => new Error('Available users fetch failed'))
      );

      fixture.detectChanges();
      tick();

      // Should switch to textbox mode as fallback
      expect(component.usersControlMode()).toBe('textbox');
      expect(component.availableUsers()).toEqual([]);
    }));

    it('should clear error message on clearErrorMessage call', () => {
      component.errorMessage.set('Some error');
      
      component.clearErrorMessage();

      expect(component.errorMessage()).toBeNull();
    });

    it('should clear success message on clearSuccessMessage call', () => {
      component.successMessage.set('Some success');
      
      component.clearSuccessMessage();

      expect(component.successMessage()).toBeNull();
    });

    it('should display error when role is null during add user', fakeAsync(() => {
      fixture.detectChanges();
      tick();
      
      component.selectedRole.set(null);
      component.selectedUserId.set(2);

      component.onAddUserToRole();

      expect(component.errorMessage()).toBe('Please select a user first');
    }));

    it('should handle getUserRole error and calculate defaults', fakeAsync(() => {
      fixture.detectChanges();
      tick();

      roleServiceSpy.getUserRole.and.returnValue(
        throwError(() => new Error('Get user role failed'))
      );

      const mockEvent = { target: { value: '2' } } as unknown as Event;
      component.onUserSelected(mockEvent);
      tick();

      // Should still work - defaults are calculated on error
      expect(component.selectedUserId()).toBe(2);
    }));
  });

  // ==========================================================================
  // 9. FORM VALIDATION TESTS
  // ==========================================================================

  describe('Form Validation', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('should initialize userRoleForm with default values', () => {
      expect(component.userRoleForm).toBeTruthy();
      expect(component.userRoleForm.get('effectiveDate')).toBeTruthy();
      expect(component.userRoleForm.get('expiryDate')).toBeTruthy();
      expect(component.userRoleForm.get('notify')).toBeTruthy();
    });

    it('should allow null effective date', () => {
      component.userRoleForm.patchValue({ effectiveDate: null });
      expect(component.userRoleForm.valid).toBeTrue();
    });

    it('should allow null expiry date', () => {
      component.userRoleForm.patchValue({ expiryDate: null });
      expect(component.userRoleForm.valid).toBeTrue();
    });

    it('should accept valid date strings', () => {
      component.userRoleForm.patchValue({
        effectiveDate: '2024-01-01',
        expiryDate: '2025-01-01'
      });
      expect(component.userRoleForm.valid).toBeTrue();
    });

    it('should reset form after successful add', fakeAsync(() => {
      component.selectedUserId.set(2);
      component.userRoleForm.patchValue({
        effectiveDate: '2024-01-01',
        expiryDate: '2025-01-01',
        notify: true
      });
      component.usernameInput = 'testuser';

      component.onAddUserToRole();
      tick();

      expect(component.userRoleForm.get('effectiveDate')?.value).toBeNull();
      expect(component.userRoleForm.get('expiryDate')?.value).toBeNull();
      expect(component.userRoleForm.get('notify')?.value).toBeFalse();
      expect(component.selectedUserId()).toBeNull();
      expect(component.usernameInput).toBe('');
    }));
  });

  // ==========================================================================
  // 10. UTILITY AND HELPER METHOD TESTS
  // ==========================================================================

  describe('Utility Methods', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('should correctly identify expired user roles', () => {
      const expiredRole = createMockUserRoleDisplay({
        expiryDate: '2020-01-01' // Past date
      });
      const activeRole = createMockUserRoleDisplay({
        expiryDate: '2030-01-01' // Future date
      });

      expect(component.isExpired(expiredRole)).toBeTrue();
      expect(component.isExpired(activeRole)).toBeFalse();
    });

    it('should correctly identify pending user roles', () => {
      const pendingRole = createMockUserRoleDisplay({
        effectiveDate: '2030-01-01' // Future date
      });
      const activeRole = createMockUserRoleDisplay({
        effectiveDate: '2020-01-01' // Past date
      });

      expect(component.isPending(pendingRole)).toBeTrue();
      expect(component.isPending(activeRole)).toBeFalse();
    });

    it('should handle null expiry in isExpired check', () => {
      const roleWithNoExpiry = createMockUserRoleDisplay({
        expiryDate: null
      });

      expect(component.isExpired(roleWithNoExpiry)).toBeFalse();
    });

    it('should handle null effective in isPending check', () => {
      const roleWithNoEffective = createMockUserRoleDisplay({
        effectiveDate: null
      });

      expect(component.isPending(roleWithNoEffective)).toBeFalse();
    });

    it('should call openDatePicker without error', () => {
      // This method exists for template compatibility
      expect(() => component.openDatePicker('effective')).not.toThrow();
      expect(() => component.openDatePicker('expiry')).not.toThrow();
    });
  });

  // ==========================================================================
  // 11. SORTING TESTS
  // ==========================================================================

  describe('Sorting', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('should handle column sort click', () => {
      component.onSortColumn('username');
      expect(component.getSortDirection('username')).toBe('ascending');

      component.onSortColumn('username');
      expect(component.getSortDirection('username')).toBe('descending');

      component.onSortColumn('username');
      expect(component.getSortDirection('username')).toBe('none');
    });

    it('should reset sort when clicking different column', () => {
      component.onSortColumn('username');
      expect(component.getSortDirection('username')).toBe('ascending');

      component.onSortColumn('displayName');
      expect(component.getSortDirection('displayName')).toBe('ascending');
      expect(component.getSortDirection('username')).toBe('none');
    });

    it('should return correct sort indicator', () => {
      expect(component.getSortIndicator('username')).toBe('↕');

      component.onSortColumn('username');
      expect(component.getSortIndicator('username')).toBe('↑');

      component.onSortColumn('username');
      expect(component.getSortIndicator('username')).toBe('↓');
    });
  });

  // ==========================================================================
  // 12. PAGINATION TESTS
  // ==========================================================================

  describe('Pagination', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('should navigate to next page', () => {
      component.currentPage.set(1);
      
      component.onNextPage();

      expect(component.currentPage()).toBe(2);
    });

    it('should not navigate past last page', () => {
      // Set current page to total pages
      const totalPages = component.totalPages();
      component.currentPage.set(totalPages);
      
      component.onNextPage();

      expect(component.currentPage()).toBe(totalPages);
    });

    it('should navigate to previous page', () => {
      component.currentPage.set(3);
      
      component.onPreviousPage();

      expect(component.currentPage()).toBe(2);
    });

    it('should not navigate before first page', () => {
      component.currentPage.set(1);
      
      component.onPreviousPage();

      expect(component.currentPage()).toBe(1);
    });
  });

  // ==========================================================================
  // 13. FILTERING TESTS
  // ==========================================================================

  describe('Filtering', () => {
    beforeEach(fakeAsync(() => {
      // Set up multiple users in role for filtering tests
      const multipleUsers = [
        createMockUserDto({ userId: 1, username: 'admin', displayName: 'Administrator' }),
        createMockUserDto({ userId: 2, username: 'editor', displayName: 'Content Editor' }),
        createMockUserDto({ userId: 3, username: 'viewer', displayName: 'Read Only Viewer' })
      ];
      roleServiceSpy.getUsersInRole.and.returnValue(of(multipleUsers));
      
      fixture.detectChanges();
      tick();
    }));

    it('should filter users by username', () => {
      component.filterText = 'admin';
      
      const filtered = component.filteredUsersInRole();
      
      expect(filtered.length).toBe(1);
      expect(filtered[0].username).toBe('admin');
    });

    it('should filter users by display name', () => {
      component.filterText = 'Editor';
      
      const filtered = component.filteredUsersInRole();
      
      expect(filtered.length).toBe(1);
      expect(filtered[0].displayName).toContain('Editor');
    });

    it('should return all users when filter is empty', () => {
      component.filterText = '';
      
      const filtered = component.filteredUsersInRole();
      
      expect(filtered.length).toBe(3);
    });

    it('should be case-insensitive when filtering', () => {
      component.filterText = 'ADMIN';
      
      const filtered = component.filteredUsersInRole();
      
      expect(filtered.length).toBe(1);
    });
  });
});
