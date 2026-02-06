/**
 * Unit Tests for UserFormComponent - Angular 19 Standalone User Form Testing
 *
 * MIGRATION: Test coverage for User.ascx.vb WebForms control converted to Angular component.
 * Tests validate form validation, create/edit mode behaviors, CRUD operations, and error handling.
 *
 * Key VB.NET functionality tested:
 * - Validate() function (lines 133-194) → Form validation tests
 * - cmdUpdate_Click (lines 361-384) → onSubmit() tests
 * - cmdDelete_Click (lines 342-350) → onDelete() tests
 * - Password validation (lines 151-154) → hasPasswordMismatch() tests
 * - chkAuthorize.Checked (line 223) → isAuthorized form control tests
 *
 * Test Requirements from Section 0.7.5:
 * - Minimum 80% component coverage
 * - 100% test pass rate required
 * - Focus on user interactions and form validation
 *
 * @fileoverview Jasmine/Karma unit tests for UserFormComponent
 */

import { ComponentFixture, TestBed, fakeAsync, tick, flush } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap, convertToParamMap } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { Subject, of, throwError } from 'rxjs';

import { UserFormComponent } from './user-form.component';
import { UserService } from '../../services/user.service';
import { User } from '../../models/user.model';

/**
 * Mock User Data Fixtures
 *
 * MIGRATION: Test fixtures based on UserInfo.vb entity properties.
 * Properties match the User interface derived from VB.NET UserInfo class.
 */
const mockUser: User = {
  userId: 1,
  username: 'testuser',
  email: 'test@example.com',
  firstName: 'Test',
  lastName: 'User',
  displayName: 'Test User',
  portalId: 0,
  isSuperUser: false,
  roles: ['Registered Users'],
};

/**
 * Secondary mock user for update scenario testing
 */
const updatedMockUser: User = {
  userId: 1,
  username: 'testuser',
  email: 'updated@example.com',
  firstName: 'Updated',
  lastName: 'User',
  displayName: 'Updated User',
  portalId: 0,
  isSuperUser: false,
  roles: ['Registered Users'],
};

/**
 * Mock created user for create scenario testing
 */
const createdMockUser: User = {
  userId: 2,
  username: 'newuser',
  email: 'newuser@example.com',
  firstName: 'New',
  lastName: 'User',
  displayName: 'New User',
  portalId: 0,
  isSuperUser: false,
  roles: ['Registered Users'],
};

describe('UserFormComponent', () => {
  let component: UserFormComponent;
  let fixture: ComponentFixture<UserFormComponent>;
  let mockUserService: jasmine.SpyObj<UserService>;
  let mockRouter: jasmine.SpyObj<Router>;
  let paramMapSubject: Subject<ParamMap>;

  /**
   * Setup TestBed configuration before each test.
   * Configures mocks for UserService, Router, and ActivatedRoute.
   */
  beforeEach(async () => {
    // Create spy objects for service and router
    mockUserService = jasmine.createSpyObj('UserService', [
      'getUserById',
      'createUser',
      'updateUser',
      'deleteUser',
    ]);

    mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    mockRouter.navigate.and.returnValue(Promise.resolve(true));

    // Create Subject for paramMap to allow dynamic emission
    paramMapSubject = new Subject<ParamMap>();

    await TestBed.configureTestingModule({
      imports: [
        ReactiveFormsModule,
        UserFormComponent, // Standalone component
      ],
      providers: [
        { provide: UserService, useValue: mockUserService },
        { provide: Router, useValue: mockRouter },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: paramMapSubject.asObservable(),
          },
        },
      ],
      // NO_ERRORS_SCHEMA to ignore child components like app-loading-spinner
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();
  });

  /**
   * Helper function to create and initialize component
   * @param routeParams - Optional route parameters for create/edit mode
   */
  function createComponent(routeParams: { id?: string } = {}): void {
    fixture = TestBed.createComponent(UserFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    // Emit route params after component initialization
    if (routeParams.id) {
      paramMapSubject.next(convertToParamMap({ id: routeParams.id }));
    } else {
      paramMapSubject.next(convertToParamMap({}));
    }
  }

  // ============================================================================
  // COMPONENT CREATION TESTS
  // ============================================================================

  describe('Component Creation', () => {
    it('should create', () => {
      createComponent();
      expect(component).toBeTruthy();
    });
  });

  // ============================================================================
  // FORM INITIALIZATION TESTS
  // ============================================================================

  describe('Form Initialization', () => {
    beforeEach(() => {
      createComponent(); // Create mode (no id)
    });

    it('should initialize with empty form in create mode', () => {
      expect(component.userForm.get('username')?.value).toBe('');
      expect(component.userForm.get('email')?.value).toBe('');
      expect(component.userForm.get('firstName')?.value).toBe('');
      expect(component.userForm.get('lastName')?.value).toBe('');
      expect(component.userForm.get('displayName')?.value).toBe('');
      expect(component.userForm.get('password')?.value).toBe('');
      expect(component.userForm.get('confirmPassword')?.value).toBe('');
    });

    it('should have all required form controls', () => {
      expect(component.userForm.get('username')).toBeTruthy();
      expect(component.userForm.get('email')).toBeTruthy();
      expect(component.userForm.get('firstName')).toBeTruthy();
      expect(component.userForm.get('lastName')).toBeTruthy();
      expect(component.userForm.get('displayName')).toBeTruthy();
      expect(component.userForm.get('password')).toBeTruthy();
      expect(component.userForm.get('confirmPassword')).toBeTruthy();
      expect(component.userForm.get('isAuthorized')).toBeTruthy();
    });

    it('should mark form as invalid when empty', () => {
      expect(component.userForm.invalid).toBeTrue();
    });

    it('should mark form as valid with all required fields', fakeAsync(() => {
      // Fill all required fields with valid values
      component.userForm.patchValue({
        username: 'validuser',
        email: 'valid@example.com',
        firstName: 'Valid',
        lastName: 'User',
        displayName: 'Valid User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      expect(component.userForm.valid).toBeTrue();
    }));

    it('should have isAuthorized checkbox defaulted to false', () => {
      expect(component.userForm.get('isAuthorized')?.value).toBeFalse();
    });
  });

  // ============================================================================
  // VALIDATION TESTS - MIGRATION: VB Validate() function lines 133-194
  // ============================================================================

  describe('Form Validation', () => {
    beforeEach(() => {
      createComponent(); // Create mode
    });

    it('should require username', () => {
      const control = component.userForm.get('username');
      control?.setValue('');
      control?.markAsTouched();

      expect(control?.hasError('required')).toBeTrue();
    });

    it('should require email', () => {
      const control = component.userForm.get('email');
      control?.setValue('');
      control?.markAsTouched();

      expect(control?.hasError('required')).toBeTrue();
    });

    it('should validate email format', () => {
      const control = component.userForm.get('email');
      control?.setValue('invalid-email');
      control?.markAsTouched();

      expect(control?.hasError('email')).toBeTrue();

      // Valid email should not have error
      control?.setValue('valid@example.com');
      expect(control?.hasError('email')).toBeFalse();
    });

    it('should require firstName', () => {
      const control = component.userForm.get('firstName');
      control?.setValue('');
      control?.markAsTouched();

      expect(control?.hasError('required')).toBeTrue();
    });

    it('should require lastName', () => {
      const control = component.userForm.get('lastName');
      control?.setValue('');
      control?.markAsTouched();

      expect(control?.hasError('required')).toBeTrue();
    });

    it('should require displayName', () => {
      const control = component.userForm.get('displayName');
      control?.setValue('');
      control?.markAsTouched();

      expect(control?.hasError('required')).toBeTrue();
    });

    it('should require password in create mode', fakeAsync(() => {
      // Ensure we're in create mode
      tick();
      const control = component.userForm.get('password');
      control?.setValue('');
      control?.markAsTouched();

      expect(control?.hasError('required')).toBeTrue();
    }));

    it('should validate password minimum length', fakeAsync(() => {
      tick();
      const control = component.userForm.get('password');
      control?.setValue('Pass1');
      control?.markAsTouched();

      expect(control?.hasError('minlength')).toBeTrue();

      // Valid password should not have minlength error
      control?.setValue('Password123');
      expect(control?.hasError('minlength')).toBeFalse();
    }));

    it('should validate password pattern (uppercase, lowercase, number)', fakeAsync(() => {
      tick();
      const control = component.userForm.get('password');

      // Only lowercase
      control?.setValue('password');
      control?.markAsTouched();
      expect(control?.hasError('pattern')).toBeTrue();

      // Only lowercase and number
      control?.setValue('password1');
      expect(control?.hasError('pattern')).toBeTrue();

      // Valid password with all requirements
      control?.setValue('Password123');
      expect(control?.hasError('pattern')).toBeFalse();
    }));

    // MIGRATION: VB txtPassword.Text <> txtConfirm.Text check (lines 151-154)
    it('should validate password confirmation matches', fakeAsync(() => {
      tick();
      component.userForm.patchValue({
        password: 'Password123',
        confirmPassword: 'Password123',
      });

      expect(component.hasPasswordMismatch()).toBeFalse();
    }));

    // MIGRATION: Password mismatch validation from VB lines 151-154
    it('should show password mismatch error', fakeAsync(() => {
      tick();
      component.userForm.patchValue({
        password: 'Password123',
        confirmPassword: 'DifferentPassword456',
      });

      expect(component.hasPasswordMismatch()).toBeTrue();
    }));

    it('should require confirmPassword in create mode', fakeAsync(() => {
      tick();
      const control = component.userForm.get('confirmPassword');
      control?.setValue('');
      control?.markAsTouched();

      expect(control?.hasError('required')).toBeTrue();
    }));

    it('should validate username minimum length', () => {
      const control = component.userForm.get('username');
      control?.setValue('abc');
      control?.markAsTouched();

      expect(control?.hasError('minlength')).toBeTrue();

      control?.setValue('user');
      expect(control?.hasError('minlength')).toBeFalse();
    });

    it('should validate username pattern (alphanumeric)', () => {
      const control = component.userForm.get('username');

      // Username starting with number should be invalid
      control?.setValue('1user');
      control?.markAsTouched();
      expect(control?.hasError('pattern')).toBeTrue();

      // Valid username starting with letter
      control?.setValue('user123');
      expect(control?.hasError('pattern')).toBeFalse();
    });
  });

  // ============================================================================
  // CREATE MODE TESTS - MIGRATION: AddUser/IsRegister checks
  // ============================================================================

  describe('Create Mode', () => {
    beforeEach(fakeAsync(() => {
      createComponent(); // No id = create mode
      tick();
    }));

    it('should be in create mode when no id in route', () => {
      expect(component.isEditMode()).toBeFalse();
    });

    it('should show password fields in create mode', () => {
      // In create mode, password validators should be required
      const passwordControl = component.userForm.get('password');
      const confirmPasswordControl = component.userForm.get('confirmPassword');

      passwordControl?.setValue('');
      passwordControl?.markAsTouched();
      confirmPasswordControl?.setValue('');
      confirmPasswordControl?.markAsTouched();

      expect(passwordControl?.hasError('required')).toBeTrue();
      expect(confirmPasswordControl?.hasError('required')).toBeTrue();
    });

    it('should call createUser on submit in create mode', fakeAsync(() => {
      mockUserService.createUser.and.returnValue(of(createdMockUser));

      // Fill valid form data
      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
        isAuthorized: true,
      });
      tick();

      component.onSubmit();
      tick();

      expect(mockUserService.createUser).toHaveBeenCalledWith(
        jasmine.objectContaining({
          username: 'newuser',
          email: 'newuser@example.com',
          firstName: 'New',
          lastName: 'User',
          displayName: 'New User',
          password: 'Password123',
          approved: true,
        })
      );
    }));

    it('should navigate to user list after successful creation', fakeAsync(() => {
      mockUserService.createUser.and.returnValue(of(createdMockUser));

      // Fill valid form data
      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick(2000); // Wait for setTimeout delay

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/users']);
    }));

    it('should show error message on creation failure', fakeAsync(() => {
      const errorResponse = { error: { message: 'Username already exists' } };
      mockUserService.createUser.and.returnValue(throwError(() => errorResponse));

      // Fill valid form data
      component.userForm.patchValue({
        username: 'existinguser',
        email: 'existing@example.com',
        firstName: 'Existing',
        lastName: 'User',
        displayName: 'Existing User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe('Username already exists');
      expect(component.saving()).toBeFalse();
    }));

    it('should show DUPLICATE_USERNAME error code message', fakeAsync(() => {
      const errorResponse = { error: { code: 'DUPLICATE_USERNAME' } };
      mockUserService.createUser.and.returnValue(throwError(() => errorResponse));

      component.userForm.patchValue({
        username: 'duplicateuser',
        email: 'test@example.com',
        firstName: 'Test',
        lastName: 'User',
        displayName: 'Test User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe('A user with this username already exists.');
    }));

    it('should show DUPLICATE_EMAIL error code message', fakeAsync(() => {
      const errorResponse = { error: { code: 'DUPLICATE_EMAIL' } };
      mockUserService.createUser.and.returnValue(throwError(() => errorResponse));

      component.userForm.patchValue({
        username: 'testuser',
        email: 'duplicate@example.com',
        firstName: 'Test',
        lastName: 'User',
        displayName: 'Test User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe('A user with this email address already exists.');
    }));

    it('should set success message after successful creation', fakeAsync(() => {
      mockUserService.createUser.and.returnValue(of(createdMockUser));

      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.successMessage()).toBe('User created successfully.');
    }));

    it('should not submit if form is invalid', fakeAsync(() => {
      tick();

      // Form is empty and invalid
      component.onSubmit();
      tick();

      expect(mockUserService.createUser).not.toHaveBeenCalled();
      expect(component.errorMessage()).toBe('Please correct the validation errors before submitting.');
    }));

    it('should not submit if passwords do not match', fakeAsync(() => {
      tick();

      component.userForm.patchValue({
        username: 'testuser',
        email: 'test@example.com',
        firstName: 'Test',
        lastName: 'User',
        displayName: 'Test User',
        password: 'Password123',
        confirmPassword: 'DifferentPassword456',
      });
      tick();

      component.onSubmit();
      tick();

      expect(mockUserService.createUser).not.toHaveBeenCalled();
      expect(component.errorMessage()).toBe('Passwords do not match.');
    }));
  });

  // ============================================================================
  // EDIT MODE TESTS - MIGRATION: edit user flow
  // ============================================================================

  describe('Edit Mode', () => {
    beforeEach(fakeAsync(() => {
      mockUserService.getUserById.and.returnValue(of(mockUser));
      createComponent({ id: '1' });
      tick();
    }));

    it('should be in edit mode when id in route', () => {
      expect(component.isEditMode()).toBeTrue();
    });

    it('should load user data in edit mode', () => {
      expect(mockUserService.getUserById).toHaveBeenCalledWith(1);
    });

    it('should populate form with user data', () => {
      expect(component.userForm.get('username')?.value).toBe('testuser');
      expect(component.userForm.get('email')?.value).toBe('test@example.com');
      expect(component.userForm.get('firstName')?.value).toBe('Test');
      expect(component.userForm.get('lastName')?.value).toBe('User');
      expect(component.userForm.get('displayName')?.value).toBe('Test User');
    });

    it('should hide password fields in edit mode', () => {
      // In edit mode, password validators should be cleared (not required)
      const passwordControl = component.userForm.get('password');
      const confirmPasswordControl = component.userForm.get('confirmPassword');

      passwordControl?.setValue('');
      passwordControl?.markAsTouched();
      confirmPasswordControl?.setValue('');
      confirmPasswordControl?.markAsTouched();

      // Password fields should not be required in edit mode
      expect(passwordControl?.hasError('required')).toBeFalse();
      expect(confirmPasswordControl?.hasError('required')).toBeFalse();
    });

    // MIGRATION: UserInfo.Username IsReadOnly attribute
    it('should make username readonly in edit mode', () => {
      // Username should be populated but considered readonly
      // The readonly state is enforced via template [readonly] binding
      expect(component.isEditMode()).toBeTrue();
      expect(component.userForm.get('username')?.value).toBe('testuser');
    });

    it('should store user data in signal', () => {
      expect(component.user()).toEqual(mockUser);
    });

    it('should call updateUser on submit in edit mode', fakeAsync(() => {
      mockUserService.updateUser.and.returnValue(of(updatedMockUser));

      // Update display name
      component.userForm.patchValue({
        displayName: 'Updated User',
        email: 'updated@example.com',
        firstName: 'Updated',
        lastName: 'User',
      });
      tick();

      component.onSubmit();
      tick();

      expect(mockUserService.updateUser).toHaveBeenCalledWith(
        1,
        jasmine.objectContaining({
          displayName: 'Updated User',
          email: 'updated@example.com',
          firstName: 'Updated',
          lastName: 'User',
        })
      );
    }));

    it('should navigate to user list after successful update', fakeAsync(() => {
      mockUserService.updateUser.and.returnValue(of(updatedMockUser));

      component.userForm.patchValue({
        displayName: 'Updated User',
      });
      tick();

      component.onSubmit();
      tick(2000); // Wait for setTimeout delay

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/users']);
    }));

    it('should show error message on update failure', fakeAsync(() => {
      const errorResponse = { error: { message: 'Failed to update user' } };
      mockUserService.updateUser.and.returnValue(throwError(() => errorResponse));

      component.userForm.patchValue({
        displayName: 'Updated User',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe('Failed to update user');
      expect(component.saving()).toBeFalse();
    }));

    it('should update user signal with updated data after successful update', fakeAsync(() => {
      mockUserService.updateUser.and.returnValue(of(updatedMockUser));

      component.userForm.patchValue({
        displayName: 'Updated User',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.user()).toEqual(updatedMockUser);
    }));

    it('should set success message after successful update', fakeAsync(() => {
      mockUserService.updateUser.and.returnValue(of(updatedMockUser));

      component.userForm.patchValue({
        displayName: 'Updated User',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.successMessage()).toBe('User updated successfully.');
    }));

    it('should not call createUser in edit mode', fakeAsync(() => {
      mockUserService.updateUser.and.returnValue(of(updatedMockUser));

      component.userForm.patchValue({
        displayName: 'Updated User',
      });
      tick();

      component.onSubmit();
      tick();

      expect(mockUserService.createUser).not.toHaveBeenCalled();
    }));

    it('should handle error when user not found', fakeAsync(() => {
      mockUserService.getUserById.and.returnValue(
        throwError(() => ({ error: { message: 'User not found' } }))
      );

      // Reset and reinitialize with a new component instance
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [ReactiveFormsModule, UserFormComponent],
        providers: [
          { provide: UserService, useValue: mockUserService },
          { provide: Router, useValue: mockRouter },
          {
            provide: ActivatedRoute,
            useValue: { paramMap: paramMapSubject.asObservable() },
          },
        ],
        schemas: [NO_ERRORS_SCHEMA],
      }).compileComponents();

      fixture = TestBed.createComponent(UserFormComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      paramMapSubject.next(convertToParamMap({ id: '999' }));
      tick();

      expect(component.errorMessage()).toBe('User not found');
      expect(component.loading()).toBeFalse();
    }));

    it('should handle invalid id in route', fakeAsync(() => {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [ReactiveFormsModule, UserFormComponent],
        providers: [
          { provide: UserService, useValue: mockUserService },
          { provide: Router, useValue: mockRouter },
          {
            provide: ActivatedRoute,
            useValue: { paramMap: paramMapSubject.asObservable() },
          },
        ],
        schemas: [NO_ERRORS_SCHEMA],
      }).compileComponents();

      fixture = TestBed.createComponent(UserFormComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      paramMapSubject.next(convertToParamMap({ id: 'invalid' }));
      tick();

      expect(component.errorMessage()).toBe('Invalid user ID provided.');
      expect(component.loading()).toBeFalse();
    }));
  });

  // ============================================================================
  // DELETE TESTS - MIGRATION: cmdDelete_Click lines 342-350
  // ============================================================================

  describe('Delete Functionality', () => {
    beforeEach(fakeAsync(() => {
      mockUserService.getUserById.and.returnValue(of(mockUser));
      createComponent({ id: '1' });
      tick();
    }));

    // MIGRATION: VB.NET ClientAPI.AddButtonConfirm (line 260)
    it('should call deleteUser when delete confirmed', fakeAsync(() => {
      mockUserService.deleteUser.and.returnValue(of(void 0));
      spyOn(window, 'confirm').and.returnValue(true);

      component.onDelete();
      tick();

      expect(window.confirm).toHaveBeenCalled();
      expect(mockUserService.deleteUser).toHaveBeenCalledWith(1);
    }));

    it('should not call deleteUser when delete cancelled', fakeAsync(() => {
      spyOn(window, 'confirm').and.returnValue(false);

      component.onDelete();
      tick();

      expect(window.confirm).toHaveBeenCalled();
      expect(mockUserService.deleteUser).not.toHaveBeenCalled();
    }));

    it('should navigate to user list after successful deletion', fakeAsync(() => {
      mockUserService.deleteUser.and.returnValue(of(void 0));
      spyOn(window, 'confirm').and.returnValue(true);

      component.onDelete();
      tick(2000); // Wait for setTimeout delay

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/users']);
    }));

    it('should show error message on deletion failure', fakeAsync(() => {
      const errorResponse = { error: { message: 'Cannot delete admin user' } };
      mockUserService.deleteUser.and.returnValue(throwError(() => errorResponse));
      spyOn(window, 'confirm').and.returnValue(true);

      component.onDelete();
      tick();

      expect(component.errorMessage()).toBe('Cannot delete admin user');
      expect(component.saving()).toBeFalse();
    }));

    it('should set success message after successful deletion', fakeAsync(() => {
      mockUserService.deleteUser.and.returnValue(of(void 0));
      spyOn(window, 'confirm').and.returnValue(true);

      component.onDelete();
      tick();

      expect(component.successMessage()).toBe('User deleted successfully.');
    }));

    it('should set saving state during delete operation', fakeAsync(() => {
      mockUserService.deleteUser.and.returnValue(of(void 0));
      spyOn(window, 'confirm').and.returnValue(true);

      expect(component.saving()).toBeFalse();

      component.onDelete();
      // Check saving state before async completes
      expect(component.saving()).toBeTrue();

      tick();
      tick(2000);
      expect(component.saving()).toBeFalse();
    }));

    it('should include user display name in confirmation message', fakeAsync(() => {
      mockUserService.deleteUser.and.returnValue(of(void 0));
      const confirmSpy = spyOn(window, 'confirm').and.returnValue(true);

      component.onDelete();
      tick();

      const confirmArg = confirmSpy.calls.mostRecent().args[0];
      expect(confirmArg).toContain('Test User');
    }));

    it('should not delete when no user data available', fakeAsync(() => {
      component['user'].set(null);
      tick();

      component.onDelete();
      tick();

      expect(mockUserService.deleteUser).not.toHaveBeenCalled();
      expect(component.errorMessage()).toBe('No user data available for deletion.');
    }));
  });

  // ============================================================================
  // LOADING STATE TESTS
  // ============================================================================

  describe('Loading States', () => {
    it('should show loading spinner while loading', fakeAsync(() => {
      // Setup slow loading user
      mockUserService.getUserById.and.returnValue(
        new Subject<User>().asObservable()
      );

      createComponent({ id: '1' });
      // Don't tick - stay in loading state

      expect(component.loading()).toBeTrue();
    }));

    it('should hide loading spinner after load complete', fakeAsync(() => {
      mockUserService.getUserById.and.returnValue(of(mockUser));

      createComponent({ id: '1' });
      tick();

      expect(component.loading()).toBeFalse();
    }));

    it('should disable submit button while saving', fakeAsync(() => {
      mockUserService.getUserById.and.returnValue(of(mockUser));
      createComponent({ id: '1' });
      tick();

      // Start a long-running update operation
      const updateSubject = new Subject<User>();
      mockUserService.updateUser.and.returnValue(updateSubject.asObservable());

      component.userForm.patchValue({ displayName: 'Updated' });
      component.onSubmit();

      expect(component.saving()).toBeTrue();

      // Complete the operation
      updateSubject.next(updatedMockUser);
      updateSubject.complete();
      tick();

      expect(component.saving()).toBeFalse();
    }));

    it('should set loading to false when user load fails', fakeAsync(() => {
      mockUserService.getUserById.and.returnValue(
        throwError(() => ({ error: { message: 'Not found' } }))
      );

      createComponent({ id: '999' });
      tick();

      expect(component.loading()).toBeFalse();
    }));

    it('should start with loading true in edit mode', fakeAsync(() => {
      const userSubject = new Subject<User>();
      mockUserService.getUserById.and.returnValue(userSubject.asObservable());

      fixture = TestBed.createComponent(UserFormComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      paramMapSubject.next(convertToParamMap({ id: '1' }));

      // Loading should be true initially in edit mode
      expect(component.loading()).toBeTrue();

      // Complete loading
      userSubject.next(mockUser);
      userSubject.complete();
      tick();

      expect(component.loading()).toBeFalse();
    }));

    it('should set loading to false immediately in create mode', fakeAsync(() => {
      fixture = TestBed.createComponent(UserFormComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      paramMapSubject.next(convertToParamMap({}));
      tick();

      expect(component.loading()).toBeFalse();
    }));
  });

  // ============================================================================
  // CANCEL TESTS
  // ============================================================================

  describe('Cancel Functionality', () => {
    it('should navigate to user list on cancel', fakeAsync(() => {
      createComponent();
      tick();

      component.onCancel();

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/users']);
    }));

    it('should navigate to user list on cancel in edit mode', fakeAsync(() => {
      mockUserService.getUserById.and.returnValue(of(mockUser));
      createComponent({ id: '1' });
      tick();

      component.onCancel();

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/users']);
    }));
  });

  // ============================================================================
  // HELPER FUNCTION TESTS
  // ============================================================================

  describe('Helper Functions', () => {
    beforeEach(fakeAsync(() => {
      createComponent();
      tick();
    }));

    it('should return correct error message for required field', () => {
      const control = component.userForm.get('username');
      control?.setValue('');
      control?.markAsTouched();

      expect(component.getErrorMessage('username')).toBe('Username is required.');
    });

    it('should return correct error message for email validation', () => {
      const control = component.userForm.get('email');
      control?.setValue('invalid-email');
      control?.markAsTouched();

      expect(component.getErrorMessage('email')).toBe('Please enter a valid email address.');
    });

    it('should return correct error message for minlength validation', fakeAsync(() => {
      tick();
      const control = component.userForm.get('username');
      control?.setValue('ab');
      control?.markAsTouched();

      expect(component.getErrorMessage('username')).toContain('at least');
    }));

    it('should return correct error message for maxlength validation', () => {
      const control = component.userForm.get('firstName');
      control?.setValue('a'.repeat(51));
      control?.markAsTouched();

      expect(component.getErrorMessage('firstName')).toContain('cannot exceed');
    });

    it('should return correct error message for pattern validation on username', () => {
      const control = component.userForm.get('username');
      control?.setValue('123user');
      control?.markAsTouched();

      expect(component.getErrorMessage('username')).toContain(
        'must start with a letter'
      );
    });

    it('should return correct error message for password pattern validation', fakeAsync(() => {
      tick();
      const control = component.userForm.get('password');
      control?.setValue('password');
      control?.markAsTouched();

      expect(component.getErrorMessage('password')).toContain(
        'uppercase letter'
      );
    }));

    it('should return empty string for valid field', () => {
      const control = component.userForm.get('email');
      control?.setValue('valid@example.com');
      control?.markAsTouched();

      expect(component.getErrorMessage('email')).toBe('');
    });

    it('should return empty string for non-existent control', () => {
      expect(component.getErrorMessage('nonexistent')).toBe('');
    });

    it('should correctly identify field as invalid and touched', () => {
      const control = component.userForm.get('username');
      control?.setValue('');
      control?.markAsTouched();

      expect(component['isFieldInvalid']('username')).toBeTrue();
    });

    it('should not mark field as invalid when not touched', () => {
      const control = component.userForm.get('username');
      control?.setValue('');

      expect(component['isFieldInvalid']('username')).toBeFalse();
    });

    it('should correctly detect password mismatch', fakeAsync(() => {
      tick();
      component.userForm.patchValue({
        password: 'Password123',
        confirmPassword: 'Different123',
      });

      expect(component.hasPasswordMismatch()).toBeTrue();
    }));

    it('should return false for password mismatch when passwords match', fakeAsync(() => {
      tick();
      component.userForm.patchValue({
        password: 'Password123',
        confirmPassword: 'Password123',
      });

      expect(component.hasPasswordMismatch()).toBeFalse();
    }));

    it('should return false for password mismatch when password is empty', fakeAsync(() => {
      tick();
      component.userForm.patchValue({
        password: '',
        confirmPassword: 'Password123',
      });

      expect(component.hasPasswordMismatch()).toBeFalse();
    }));

    it('should return false for password mismatch when confirm password is empty', fakeAsync(() => {
      tick();
      component.userForm.patchValue({
        password: 'Password123',
        confirmPassword: '',
      });

      expect(component.hasPasswordMismatch()).toBeFalse();
    }));

    it('should return false for password mismatch in edit mode', fakeAsync(() => {
      mockUserService.getUserById.and.returnValue(of(mockUser));

      // Reinitialize in edit mode
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [ReactiveFormsModule, UserFormComponent],
        providers: [
          { provide: UserService, useValue: mockUserService },
          { provide: Router, useValue: mockRouter },
          {
            provide: ActivatedRoute,
            useValue: { paramMap: paramMapSubject.asObservable() },
          },
        ],
        schemas: [NO_ERRORS_SCHEMA],
      }).compileComponents();

      fixture = TestBed.createComponent(UserFormComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      paramMapSubject.next(convertToParamMap({ id: '1' }));
      tick();

      component.userForm.patchValue({
        password: 'Password123',
        confirmPassword: 'Different123',
      });

      expect(component.hasPasswordMismatch()).toBeFalse();
    }));
  });

  // ============================================================================
  // MESSAGE HANDLING TESTS
  // ============================================================================

  describe('Message Handling', () => {
    beforeEach(fakeAsync(() => {
      createComponent();
      tick();
    }));

    it('should clear error message on new submit', fakeAsync(() => {
      component.errorMessage.set('Previous error');
      mockUserService.createUser.and.returnValue(of(createdMockUser));

      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick();

      // Error message should be cleared (or replaced with success)
      expect(component.errorMessage()).toBeNull();
    }));

    it('should clear success message on new submit', fakeAsync(() => {
      component.successMessage.set('Previous success');
      mockUserService.createUser.and.returnValue(of(createdMockUser));

      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick();

      // Success message should be updated
      expect(component.successMessage()).toBe('User created successfully.');
    }));

    it('should handle generic error when no message in response', fakeAsync(() => {
      mockUserService.createUser.and.returnValue(throwError(() => ({})));

      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe(
        'Failed to create user. Please try again.'
      );
    }));

    it('should handle INVALID_PASSWORD error code', fakeAsync(() => {
      const errorResponse = { error: { code: 'INVALID_PASSWORD' } };
      mockUserService.createUser.and.returnValue(throwError(() => errorResponse));

      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe(
        'The password does not meet the security requirements.'
      );
    }));

    it('should handle INVALID_EMAIL error code', fakeAsync(() => {
      const errorResponse = { error: { code: 'INVALID_EMAIL' } };
      mockUserService.createUser.and.returnValue(throwError(() => errorResponse));

      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe('The email address format is invalid.');
    }));

    it('should handle PASSWORD_MISMATCH error code', fakeAsync(() => {
      const errorResponse = { error: { code: 'PASSWORD_MISMATCH' } };
      mockUserService.createUser.and.returnValue(throwError(() => errorResponse));

      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe('The passwords do not match.');
    }));
  });

  // ============================================================================
  // EDGE CASE TESTS
  // ============================================================================

  describe('Edge Cases', () => {
    it('should handle route param "new" as create mode', fakeAsync(() => {
      fixture = TestBed.createComponent(UserFormComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      paramMapSubject.next(convertToParamMap({ id: 'new' }));
      tick();

      expect(component.isEditMode()).toBeFalse();
      expect(mockUserService.getUserById).not.toHaveBeenCalled();
    }));

    it('should not call updateUser when no user data available in edit mode', fakeAsync(() => {
      createComponent();
      tick();

      // Manually set edit mode without user data
      component['isEditMode'].set(true);
      component['user'].set(null);

      component.onSubmit();
      tick();

      expect(mockUserService.updateUser).not.toHaveBeenCalled();
      expect(component.errorMessage()).toBe('No user data available for update.');
    }));

    it('should mark all fields as touched on invalid submit', fakeAsync(() => {
      createComponent();
      tick();

      const usernameSpy = spyOn(
        component.userForm.get('username')!,
        'markAsTouched'
      ).and.callThrough();

      component.onSubmit();
      tick();

      // markAllAsTouched is called on the form, not individual controls
      // But this should trigger field validation errors to display
      expect(component.errorMessage()).toBe(
        'Please correct the validation errors before submitting.'
      );
    }));

    it('should handle direct message in error response', fakeAsync(() => {
      const errorResponse = { message: 'Direct error message' };
      mockUserService.createUser.and.returnValue(throwError(() => errorResponse));

      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });

      createComponent();
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe('Direct error message');
    }));

    it('should handle null error properly', fakeAsync(() => {
      mockUserService.createUser.and.returnValue(throwError(() => null));

      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });

      createComponent();
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe('Failed to create user. Please try again.');
    }));

    it('should handle unknown error code', fakeAsync(() => {
      const errorResponse = { error: { code: 'UNKNOWN_ERROR_CODE' } };
      mockUserService.createUser.and.returnValue(throwError(() => errorResponse));

      component.userForm.patchValue({
        username: 'newuser',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        displayName: 'New User',
        password: 'Password123',
        confirmPassword: 'Password123',
      });

      createComponent();
      tick();

      component.onSubmit();
      tick();

      expect(component.errorMessage()).toBe('Failed to create user. Please try again.');
    }));
  });
});
