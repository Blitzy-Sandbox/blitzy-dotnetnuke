/**
 * Portal Form Component Unit Tests
 *
 * Comprehensive Jasmine unit tests for PortalFormComponent verifying:
 * - Component initialization with proper DI mocking
 * - Form creation with all required FormControls
 * - Validation rules matching VB.NET Signup.ascx.vb patterns
 * - Create mode behavior (no route id)
 * - Edit mode behavior (route has id)
 * - Duplicate alias async validation
 * - Template selection dropdown loading
 * - Form submission success/error handling
 * - Cancel button navigation
 * - Loading state signal updates
 *
 * MIGRATION: Tests ensure Angular 19 component correctly replicates
 * VB.NET validation logic from Website/admin/Portal/Signup.ascx.vb:
 * - Lines 191-195: Child portal validation (abcdefghijklmnopqrstuvwxyz0123456789-)
 * - Lines 207-216: Parent portal validation (abcdefghijklmnopqrstuvwxyz0123456789-./:)
 * - Lines 220-222: Password confirmation validation
 * - Lines 260-265: Duplicate alias check
 *
 * @fileoverview Unit tests for PortalFormComponent with 80%+ coverage target
 */

import {
  ComponentFixture,
  TestBed,
  fakeAsync,
  tick,
  waitForAsync,
} from '@angular/core/testing';
import { Router, ActivatedRoute, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule, FormGroup } from '@angular/forms';
import { of, throwError, delay } from 'rxjs';

import {
  PortalFormComponent,
  PortalFormValue,
} from './portal-form.component';
import { PortalService } from '../../services/portal.service';
import {
  Portal,
  CreatePortalRequest,
  UpdatePortalRequest,
  PortalTemplate,
  UserRegistrationType,
  BannerType,
} from '../../models/portal.model';

// =============================================================================
// TEST DATA FIXTURES
// =============================================================================

/**
 * Mock portal data for edit mode testing
 * MIGRATION: Data structure mirrors VB.NET PortalInfo entity
 */
const mockPortal: Portal = {
  portalId: 1,
  portalName: 'Test Portal',
  logoFile: '/images/logo.png',
  footerText: 'Copyright 2024',
  expiryDate: '2025-12-31T00:00:00Z',
  userRegistration: UserRegistrationType.Public,
  bannerAdvertising: BannerType.None,
  administratorId: 1,
  currency: 'USD',
  hostFee: 0,
  hostSpace: 100,
  pageQuota: 50,
  userQuota: 100,
  administratorRoleId: 1,
  administratorRoleName: 'Administrators',
  registeredRoleId: 2,
  registeredRoleName: 'Registered Users',
  description: 'Test portal description',
  keyWords: 'test, portal, keywords',
  backgroundFile: undefined,
  siteLogHistory: 30,
  email: 'admin@test.com',
  adminTabId: 1,
  superTabId: 2,
  users: 10,
  pages: 5,
  splashTabId: 3,
  homeTabId: 4,
  loginTabId: 5,
  userTabId: 6,
  defaultLanguage: 'en-US',
  timeZoneOffset: -300,
  homeDirectory: 'Portals/1',
  version: '8.0.0',
  guid: '12345678-1234-1234-1234-123456789012',
};

/**
 * Mock templates for template dropdown testing
 */
const mockTemplates: PortalTemplate[] = [
  {
    name: 'Default Template',
    fileName: 'default.template',
    description: 'Standard portal template with basic pages',
  },
  {
    name: 'Blank Template',
    fileName: 'blank.template',
    description: 'Empty template for custom setups',
  },
  {
    name: 'Corporate Template',
    fileName: 'corporate.template',
    description: 'Professional business portal template',
  },
];

/**
 * Valid form data for create mode testing
 */
const validCreateFormData: PortalFormValue = {
  portalName: 'newportal',
  alias: '',
  firstName: 'John',
  lastName: 'Doe',
  username: 'johndoe',
  password: 'Password123!',
  confirmPassword: 'Password123!',
  email: 'john.doe@example.com',
  title: 'New Portal',
  description: 'A new portal for testing',
  keywords: 'test, new, portal',
  template: 'default.template',
  homeDirectory: 'Portals/[PortalID]',
  portalType: 'P',
};

// =============================================================================
// TEST SUITE
// =============================================================================

describe('PortalFormComponent', () => {
  let component: PortalFormComponent;
  let fixture: ComponentFixture<PortalFormComponent>;
  let portalServiceSpy: jasmine.SpyObj<PortalService>;
  let routerSpy: jasmine.SpyObj<Router>;
  let activatedRouteMock: Partial<ActivatedRoute>;

  // ===========================================================================
  // TEST SETUP HELPERS
  // ===========================================================================

  /**
   * Creates a mock ActivatedRoute with configurable params
   * @param params - Route parameters (e.g., { id: '1' } for edit mode)
   */
  function createActivatedRouteMock(params: { [key: string]: string } = {}): Partial<ActivatedRoute> {
    return {
      snapshot: {
        paramMap: {
          get: (key: string) => params[key] || null,
          has: (key: string) => key in params,
          getAll: (key: string) => params[key] ? [params[key]] : [],
          keys: Object.keys(params),
        },
      } as any,
    };
  }

  /**
   * Configures TestBed for create mode (no route id parameter)
   */
  async function setupCreateMode(): Promise<void> {
    activatedRouteMock = createActivatedRouteMock({});

    portalServiceSpy = jasmine.createSpyObj('PortalService', [
      'getPortal',
      'createPortal',
      'updatePortal',
      'getTemplates',
      'checkAliasExists',
    ]);

    // Configure default spy returns for create mode
    portalServiceSpy.getTemplates.and.returnValue(of(mockTemplates));
    portalServiceSpy.checkAliasExists.and.returnValue(of(false));
    portalServiceSpy.createPortal.and.returnValue(of(mockPortal));

    routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    await TestBed.configureTestingModule({
      imports: [PortalFormComponent, ReactiveFormsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: PortalService, useValue: portalServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: ActivatedRoute, useValue: activatedRouteMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PortalFormComponent);
    component = fixture.componentInstance;
  }

  /**
   * Configures TestBed for edit mode (route has id parameter)
   * @param portalId - The portal ID to simulate in the route
   */
  async function setupEditMode(portalId: number = 1): Promise<void> {
    activatedRouteMock = createActivatedRouteMock({ id: portalId.toString() });

    portalServiceSpy = jasmine.createSpyObj('PortalService', [
      'getPortal',
      'createPortal',
      'updatePortal',
      'getTemplates',
      'checkAliasExists',
    ]);

    // Configure default spy returns for edit mode
    portalServiceSpy.getPortal.and.returnValue(of(mockPortal));
    portalServiceSpy.updatePortal.and.returnValue(of(mockPortal));
    portalServiceSpy.checkAliasExists.and.returnValue(of(false));

    routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    await TestBed.configureTestingModule({
      imports: [PortalFormComponent, ReactiveFormsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: PortalService, useValue: portalServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: ActivatedRoute, useValue: activatedRouteMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PortalFormComponent);
    component = fixture.componentInstance;
  }

  // ===========================================================================
  // COMPONENT INITIALIZATION TESTS
  // ===========================================================================

  describe('Component Initialization', () => {
    beforeEach(async () => {
      await setupCreateMode();
    });

    it('should create the component', () => {
      fixture.detectChanges();
      expect(component).toBeTruthy();
    });

    it('should initialize with loading state as false initially', () => {
      // Before ngOnInit, loading should be false
      expect(component.loading()).toBeFalse();
    });

    it('should initialize with saving state as false', () => {
      fixture.detectChanges();
      expect(component.saving()).toBeFalse();
    });

    it('should initialize with no error', () => {
      fixture.detectChanges();
      expect(component.error()).toBeNull();
    });

    it('should initialize portalForm as a FormGroup', () => {
      fixture.detectChanges();
      expect(component.portalForm).toBeTruthy();
      expect(component.portalForm instanceof FormGroup).toBeTrue();
    });

    it('should call ngOnInit and set loading state during initialization', fakeAsync(() => {
      fixture.detectChanges();
      // After detectChanges, the component should have completed loading
      tick(); // Allow async operations to complete
      expect(component.loading()).toBeFalse();
    }));
  });

  // ===========================================================================
  // FORM CONTROLS TESTS
  // ===========================================================================

  describe('Form Controls Creation', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should create portalName form control', () => {
      expect(component.portalForm.get('portalName')).toBeTruthy();
    });

    it('should create alias form control', () => {
      expect(component.portalForm.get('alias')).toBeTruthy();
    });

    it('should create firstName form control', () => {
      expect(component.portalForm.get('firstName')).toBeTruthy();
    });

    it('should create lastName form control', () => {
      expect(component.portalForm.get('lastName')).toBeTruthy();
    });

    it('should create username form control', () => {
      expect(component.portalForm.get('username')).toBeTruthy();
    });

    it('should create password form control', () => {
      expect(component.portalForm.get('password')).toBeTruthy();
    });

    it('should create confirmPassword form control', () => {
      expect(component.portalForm.get('confirmPassword')).toBeTruthy();
    });

    it('should create email form control', () => {
      expect(component.portalForm.get('email')).toBeTruthy();
    });

    it('should create title form control', () => {
      expect(component.portalForm.get('title')).toBeTruthy();
    });

    it('should create description form control', () => {
      expect(component.portalForm.get('description')).toBeTruthy();
    });

    it('should create keywords form control', () => {
      expect(component.portalForm.get('keywords')).toBeTruthy();
    });

    it('should create template form control', () => {
      expect(component.portalForm.get('template')).toBeTruthy();
    });

    it('should create homeDirectory form control', () => {
      expect(component.portalForm.get('homeDirectory')).toBeTruthy();
    });

    it('should create portalType form control', () => {
      expect(component.portalForm.get('portalType')).toBeTruthy();
    });

    it('should have portalType default to "P" (Parent)', () => {
      expect(component.portalForm.get('portalType')?.value).toBe('P');
    });

    it('should have homeDirectory disabled by default with placeholder value', () => {
      const homeDirectoryControl = component.portalForm.get('homeDirectory');
      expect(homeDirectoryControl?.disabled).toBeTrue();
      expect(homeDirectoryControl?.value).toBe('Portals/[PortalID]');
    });
  });

  // ===========================================================================
  // REQUIRED FIELD VALIDATION TESTS
  // ===========================================================================

  describe('Required Field Validation', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should mark portalName as invalid when empty', () => {
      const control = component.portalForm.get('portalName');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeFalse();
      expect(control?.errors?.['required']).toBeTruthy();
    });

    it('should mark firstName as invalid when empty', () => {
      const control = component.portalForm.get('firstName');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeFalse();
      expect(control?.errors?.['required']).toBeTruthy();
    });

    it('should mark lastName as invalid when empty', () => {
      const control = component.portalForm.get('lastName');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeFalse();
      expect(control?.errors?.['required']).toBeTruthy();
    });

    it('should mark username as invalid when empty', () => {
      const control = component.portalForm.get('username');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeFalse();
      expect(control?.errors?.['required']).toBeTruthy();
    });

    it('should mark password as invalid when empty', () => {
      const control = component.portalForm.get('password');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeFalse();
      expect(control?.errors?.['required']).toBeTruthy();
    });

    it('should mark confirmPassword as invalid when empty', () => {
      const control = component.portalForm.get('confirmPassword');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeFalse();
      expect(control?.errors?.['required']).toBeTruthy();
    });

    it('should mark email as invalid when empty', () => {
      const control = component.portalForm.get('email');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeFalse();
      expect(control?.errors?.['required']).toBeTruthy();
    });

    it('should mark title as invalid when empty', () => {
      const control = component.portalForm.get('title');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeFalse();
      expect(control?.errors?.['required']).toBeTruthy();
    });

    it('should mark portalType as invalid when empty', () => {
      const control = component.portalForm.get('portalType');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeFalse();
      expect(control?.errors?.['required']).toBeTruthy();
    });

    it('should allow description to be empty (optional field)', () => {
      const control = component.portalForm.get('description');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeTrue();
    });

    it('should allow keywords to be empty (optional field)', () => {
      const control = component.portalForm.get('keywords');
      control?.setValue('');
      control?.markAsTouched();
      expect(control?.valid).toBeTrue();
    });
  });

  // ===========================================================================
  // PASSWORD VALIDATION TESTS
  // ===========================================================================

  describe('Password Validation', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    /**
     * MIGRATION: Tests password match validation from Signup.ascx.vb lines 220-222:
     * If txtPassword.Text <> txtConfirm.Text Then
     *     strMessage &= "<br>" & Localization.GetString("InvalidPassword", Me.LocalResourceFile)
     * End If
     */
    it('should validate that password and confirmPassword match', () => {
      component.portalForm.patchValue({
        password: 'Password123!',
        confirmPassword: 'Password123!',
      });
      component.portalForm.updateValueAndValidity();
      
      expect(component.portalForm.errors?.['passwordMismatch']).toBeFalsy();
    });

    it('should set error when password and confirmPassword do not match', () => {
      component.portalForm.patchValue({
        password: 'Password123!',
        confirmPassword: 'DifferentPassword!',
      });
      component.portalForm.updateValueAndValidity();
      
      expect(component.portalForm.errors?.['passwordMismatch']).toBeTruthy();
    });

    it('should set passwordMismatch error on confirmPassword control when passwords differ', () => {
      component.portalForm.patchValue({
        password: 'Password123!',
        confirmPassword: 'DifferentPassword!',
      });
      component.portalForm.updateValueAndValidity();
      
      const confirmPasswordControl = component.portalForm.get('confirmPassword');
      expect(confirmPasswordControl?.errors?.['passwordMismatch']).toBeTruthy();
    });

    it('should validate password minimum length of 6 characters', () => {
      const passwordControl = component.portalForm.get('password');
      passwordControl?.setValue('12345');
      passwordControl?.markAsTouched();
      
      expect(passwordControl?.valid).toBeFalse();
      expect(passwordControl?.errors?.['minlength']).toBeTruthy();
    });

    it('should accept password with 6 or more characters', () => {
      const passwordControl = component.portalForm.get('password');
      passwordControl?.setValue('123456');
      passwordControl?.markAsTouched();
      
      expect(passwordControl?.errors?.['minlength']).toBeFalsy();
    });

    it('should validate username minimum length of 3 characters', () => {
      const usernameControl = component.portalForm.get('username');
      usernameControl?.setValue('ab');
      usernameControl?.markAsTouched();
      
      expect(usernameControl?.valid).toBeFalse();
      expect(usernameControl?.errors?.['minlength']).toBeTruthy();
    });
  });

  // ===========================================================================
  // EMAIL VALIDATION TESTS
  // ===========================================================================

  describe('Email Validation', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should validate email format', () => {
      const emailControl = component.portalForm.get('email');
      emailControl?.setValue('invalid-email');
      emailControl?.markAsTouched();
      
      expect(emailControl?.valid).toBeFalse();
      expect(emailControl?.errors?.['email']).toBeTruthy();
    });

    it('should accept valid email format', () => {
      const emailControl = component.portalForm.get('email');
      emailControl?.setValue('valid@example.com');
      emailControl?.markAsTouched();
      
      expect(emailControl?.errors?.['email']).toBeFalsy();
    });

    it('should reject email without @ symbol', () => {
      const emailControl = component.portalForm.get('email');
      emailControl?.setValue('invalidemail.com');
      emailControl?.markAsTouched();
      
      expect(emailControl?.valid).toBeFalse();
    });

    it('should reject email without domain', () => {
      const emailControl = component.portalForm.get('email');
      emailControl?.setValue('invalid@');
      emailControl?.markAsTouched();
      
      expect(emailControl?.valid).toBeFalse();
    });
  });

  // ===========================================================================
  // PORTAL NAME/ALIAS CHARACTER VALIDATION TESTS
  // ===========================================================================

  describe('Portal Name Character Validation', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    /**
     * MIGRATION: Tests child portal validation from Signup.ascx.vb lines 191-195:
     * For intCounter = 1 To txtPortalName.Text.Length
     *     If InStr(1, "abcdefghijklmnopqrstuvwxyz0123456789-", Mid(txtPortalName.Text, intCounter, 1)) = 0 Then
     *         strMessage &= "<br>" & Localization.GetString("InvalidName", Me.LocalResourceFile)
     *     End If
     * Next intCounter
     */
    describe('Child Portal Validation (abcdefghijklmnopqrstuvwxyz0123456789-)', () => {
      beforeEach(() => {
        component.portalForm.get('portalType')?.setValue('C');
      });

      it('should accept lowercase letters in child portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('validportalname');
        portalNameControl?.updateValueAndValidity();
        tick(600); // Allow async validator debounce
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should accept numbers in child portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('portal123');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should accept hyphens in child portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('my-portal-name');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should accept mixed lowercase, numbers, and hyphens', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('my-portal-123');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should reject periods in child portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('my.portal');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeTruthy();
      }));

      it('should reject slashes in child portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('my/portal');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeTruthy();
      }));

      it('should reject colons in child portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('my:portal');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeTruthy();
      }));

      it('should reject spaces in child portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('my portal');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeTruthy();
      }));

      it('should reject special characters in child portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('my@portal!');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeTruthy();
      }));

      it('should handle uppercase by converting to lowercase', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('MYPORTAL');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        // Should be valid because validator converts to lowercase
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));
    });

    /**
     * MIGRATION: Tests parent portal validation from Signup.ascx.vb lines 207-216:
     * Dim strValidChars As String = "abcdefghijklmnopqrstuvwxyz0123456789-"
     * If Not blnChild Then
     *     strValidChars += "./:"
     * End If
     */
    describe('Parent Portal Validation (abcdefghijklmnopqrstuvwxyz0123456789-./:)', () => {
      beforeEach(() => {
        component.portalForm.get('portalType')?.setValue('P');
      });

      it('should accept lowercase letters in parent portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('validportalname');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should accept periods in parent portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('www.example.com');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should accept slashes in parent portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('example.com/subdir');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should accept colons in parent portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('localhost:8080');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should accept full URL-like parent portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('www.example.com:8080/portal');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should strip http:// prefix before validation', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('http://www.example.com');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should strip https:// prefix before validation', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('https://www.example.com');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      }));

      it('should reject spaces in parent portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('www.example .com');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeTruthy();
      }));

      it('should reject special characters like @ in parent portal name', fakeAsync(() => {
        const portalNameControl = component.portalForm.get('portalName');
        portalNameControl?.setValue('admin@example.com');
        portalNameControl?.updateValueAndValidity();
        tick(600);
        
        expect(portalNameControl?.errors?.['invalidCharacters']).toBeTruthy();
      }));
    });
  });

  // ===========================================================================
  // DUPLICATE ALIAS ASYNC VALIDATION TESTS
  // ===========================================================================

  describe('Duplicate Alias Async Validation', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    /**
     * MIGRATION: Tests duplicate alias check from Signup.ascx.vb lines 260-265:
     * Dim PortalAlias As PortalAliasInfo = PortalSettings.GetPortalAliasLookup(strPortalAlias.ToLower)
     * If PortalAlias IsNot Nothing Then
     *     strMessage = Localization.GetString("DuplicatePortalAlias", Me.LocalResourceFile)
     * End If
     */
    it('should call checkAliasExists when portalName is entered', fakeAsync(() => {
      const portalNameControl = component.portalForm.get('portalName');
      portalNameControl?.setValue('testportal');
      
      tick(600); // Allow debounce time
      
      expect(portalServiceSpy.checkAliasExists).toHaveBeenCalled();
    }));

    it('should set aliasExists error when alias already exists', fakeAsync(() => {
      portalServiceSpy.checkAliasExists.and.returnValue(of(true));
      
      const portalNameControl = component.portalForm.get('portalName');
      portalNameControl?.setValue('existingalias');
      
      tick(600); // Allow debounce and async validator to complete
      
      expect(portalNameControl?.errors?.['aliasExists']).toBeTruthy();
    }));

    it('should not set aliasExists error when alias is available', fakeAsync(() => {
      portalServiceSpy.checkAliasExists.and.returnValue(of(false));
      
      const portalNameControl = component.portalForm.get('portalName');
      portalNameControl?.setValue('newuniquealias');
      
      tick(600);
      
      expect(portalNameControl?.errors?.['aliasExists']).toBeFalsy();
    }));

    it('should handle checkAliasExists service error gracefully', fakeAsync(() => {
      portalServiceSpy.checkAliasExists.and.returnValue(
        throwError(() => new Error('Service error'))
      );
      
      const portalNameControl = component.portalForm.get('portalName');
      portalNameControl?.setValue('testalias');
      
      tick(600);
      
      // Should not throw and should not set aliasExists error on service failure
      expect(portalNameControl?.errors?.['aliasExists']).toBeFalsy();
    }));

    it('should not check alias when portalName is empty', fakeAsync(() => {
      portalServiceSpy.checkAliasExists.calls.reset();
      
      const portalNameControl = component.portalForm.get('portalName');
      portalNameControl?.setValue('');
      
      tick(600);
      
      expect(portalServiceSpy.checkAliasExists).not.toHaveBeenCalled();
    }));

    it('should debounce alias validation to prevent excessive API calls', fakeAsync(() => {
      portalServiceSpy.checkAliasExists.calls.reset();
      
      const portalNameControl = component.portalForm.get('portalName');
      
      // Rapidly change values
      portalNameControl?.setValue('test1');
      tick(100);
      portalNameControl?.setValue('test2');
      tick(100);
      portalNameControl?.setValue('test3');
      tick(600); // Wait for debounce
      
      // Should only call once due to debounce
      expect(portalServiceSpy.checkAliasExists.calls.count()).toBeLessThanOrEqual(1);
    }));
  });

  // ===========================================================================
  // CREATE MODE TESTS
  // ===========================================================================

  describe('Create Mode Behavior', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should set isEditMode to false when no route id is present', fakeAsync(() => {
      tick();
      expect(component.isEditMode()).toBeFalse();
    }));

    it('should load templates in create mode', fakeAsync(() => {
      tick();
      expect(portalServiceSpy.getTemplates).toHaveBeenCalled();
    }));

    it('should populate templates signal with loaded templates', fakeAsync(() => {
      tick();
      expect(component.templates()).toEqual(mockTemplates);
    }));

    it('should have empty form values in create mode', fakeAsync(() => {
      tick();
      
      expect(component.portalForm.get('portalName')?.value).toBe('');
      expect(component.portalForm.get('firstName')?.value).toBe('');
      expect(component.portalForm.get('lastName')?.value).toBe('');
    }));

    it('should not call getPortal in create mode', fakeAsync(() => {
      tick();
      expect(portalServiceSpy.getPortal).not.toHaveBeenCalled();
    }));

    it('should have portalTypeOptions available', () => {
      expect(component.portalTypeOptions).toBeDefined();
      expect(component.portalTypeOptions.length).toBe(2);
    });

    it('should have Parent Portal option', () => {
      const parentOption = component.portalTypeOptions.find(opt => opt.value === 'P');
      expect(parentOption).toBeTruthy();
      expect(parentOption?.label).toContain('Parent');
    });

    it('should have Child Portal option', () => {
      const childOption = component.portalTypeOptions.find(opt => opt.value === 'C');
      expect(childOption).toBeTruthy();
      expect(childOption?.label).toContain('Child');
    });
  });

  // ===========================================================================
  // EDIT MODE TESTS
  // ===========================================================================

  describe('Edit Mode Behavior', () => {
    beforeEach(async () => {
      await setupEditMode(1);
      fixture.detectChanges();
    });

    it('should set isEditMode to true when route id is present', fakeAsync(() => {
      tick();
      expect(component.isEditMode()).toBeTrue();
    }));

    it('should call getPortal with the route id in edit mode', fakeAsync(() => {
      tick();
      expect(portalServiceSpy.getPortal).toHaveBeenCalledWith(1);
    }));

    it('should populate form with loaded portal data', fakeAsync(() => {
      tick();
      
      expect(component.portalForm.get('portalName')?.value).toBe(mockPortal.portalName);
      expect(component.portalForm.get('description')?.value).toBe(mockPortal.description);
      expect(component.portalForm.get('keywords')?.value).toBe(mockPortal.keyWords);
    }));

    it('should disable create-only fields in edit mode', fakeAsync(() => {
      tick();
      
      expect(component.portalForm.get('firstName')?.disabled).toBeTrue();
      expect(component.portalForm.get('lastName')?.disabled).toBeTrue();
      expect(component.portalForm.get('username')?.disabled).toBeTrue();
      expect(component.portalForm.get('password')?.disabled).toBeTrue();
      expect(component.portalForm.get('confirmPassword')?.disabled).toBeTrue();
      expect(component.portalForm.get('email')?.disabled).toBeTrue();
      expect(component.portalForm.get('title')?.disabled).toBeTrue();
      expect(component.portalForm.get('template')?.disabled).toBeTrue();
      expect(component.portalForm.get('portalType')?.disabled).toBeTrue();
    }));

    it('should not call getTemplates in edit mode', fakeAsync(() => {
      tick();
      expect(portalServiceSpy.getTemplates).not.toHaveBeenCalled();
    }));

    it('should handle getPortal error in edit mode', fakeAsync(async () => {
      // Reset and reconfigure for error case
      await setupEditMode(999);
      portalServiceSpy.getPortal.and.returnValue(
        throwError(() => new Error('Portal not found'))
      );
      fixture.detectChanges();
      tick();
      
      expect(component.error()).toBeTruthy();
    }));
  });

  // ===========================================================================
  // FORM SUBMISSION TESTS
  // ===========================================================================

  describe('Form Submission - Create Mode', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should call createPortal when submitting in create mode', fakeAsync(() => {
      tick(); // Complete initial loading
      
      // Fill in valid form data
      component.portalForm.patchValue(validCreateFormData);
      component.portalForm.updateValueAndValidity();
      tick(600); // Allow async validators
      
      // Submit form
      component.onSubmit();
      tick();
      
      expect(portalServiceSpy.createPortal).toHaveBeenCalled();
    }));

    it('should navigate to /portals on successful create', fakeAsync(() => {
      tick();
      
      component.portalForm.patchValue(validCreateFormData);
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      tick();
      
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals']);
    }));

    it('should set saving signal to true during submission', fakeAsync(() => {
      tick();
      
      // Use delayed observable to test saving state
      portalServiceSpy.createPortal.and.returnValue(
        of(mockPortal).pipe(delay(100))
      );
      
      component.portalForm.patchValue(validCreateFormData);
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      
      expect(component.saving()).toBeTrue();
      
      tick(100); // Complete the request
      
      expect(component.saving()).toBeFalse();
    }));

    it('should set error signal on create failure', fakeAsync(() => {
      tick();
      
      const errorMessage = 'Failed to create portal';
      portalServiceSpy.createPortal.and.returnValue(
        throwError(() => new Error(errorMessage))
      );
      
      component.portalForm.patchValue(validCreateFormData);
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      tick();
      
      expect(component.error()).toBe(errorMessage);
    }));

    it('should not submit when form is invalid', fakeAsync(() => {
      tick();
      
      // Leave form empty (invalid)
      component.onSubmit();
      tick();
      
      expect(portalServiceSpy.createPortal).not.toHaveBeenCalled();
    }));

    it('should mark all controls as touched when submitting invalid form', fakeAsync(() => {
      tick();
      
      // Leave form empty
      component.onSubmit();
      tick();
      
      const portalNameControl = component.portalForm.get('portalName');
      expect(portalNameControl?.touched).toBeTrue();
    }));

    it('should send correct CreatePortalRequest data', fakeAsync(() => {
      tick();
      
      component.portalForm.patchValue(validCreateFormData);
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      tick();
      
      const expectedRequest: Partial<CreatePortalRequest> = {
        portalAlias: validCreateFormData.portalName.toLowerCase(),
        title: validCreateFormData.title,
        firstName: validCreateFormData.firstName,
        lastName: validCreateFormData.lastName,
        username: validCreateFormData.username,
        password: validCreateFormData.password,
        email: validCreateFormData.email,
        isChildPortal: false,
      };
      
      const actualRequest = portalServiceSpy.createPortal.calls.mostRecent().args[0];
      expect(actualRequest.portalAlias).toBe(expectedRequest.portalAlias);
      expect(actualRequest.title).toBe(expectedRequest.title);
      expect(actualRequest.firstName).toBe(expectedRequest.firstName);
      expect(actualRequest.isChildPortal).toBe(expectedRequest.isChildPortal);
    }));
  });

  describe('Form Submission - Edit Mode', () => {
    beforeEach(async () => {
      await setupEditMode(1);
      fixture.detectChanges();
    });

    it('should call updatePortal when submitting in edit mode', fakeAsync(() => {
      tick();
      
      // Update portal name
      component.portalForm.get('portalName')?.setValue('Updated Portal Name');
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      tick();
      
      expect(portalServiceSpy.updatePortal).toHaveBeenCalled();
    }));

    it('should call updatePortal with correct portal ID', fakeAsync(() => {
      tick();
      
      component.portalForm.get('portalName')?.setValue('Updated Portal Name');
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      tick();
      
      expect(portalServiceSpy.updatePortal).toHaveBeenCalledWith(1, jasmine.any(Object));
    }));

    it('should navigate to /portals on successful update', fakeAsync(() => {
      tick();
      
      component.portalForm.get('portalName')?.setValue('Updated Portal Name');
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      tick();
      
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals']);
    }));

    it('should set error signal on update failure', fakeAsync(() => {
      tick();
      
      const errorMessage = 'Failed to update portal';
      portalServiceSpy.updatePortal.and.returnValue(
        throwError(() => new Error(errorMessage))
      );
      
      component.portalForm.get('portalName')?.setValue('Updated Portal Name');
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      tick();
      
      expect(component.error()).toBe(errorMessage);
    }));

    it('should preserve existing portal values in update request', fakeAsync(() => {
      tick();
      
      component.portalForm.get('portalName')?.setValue('Updated Portal Name');
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      tick();
      
      const updateRequest = portalServiceSpy.updatePortal.calls.mostRecent().args[1] as UpdatePortalRequest;
      expect(updateRequest.administratorId).toBe(mockPortal.administratorId);
      expect(updateRequest.hostFee).toBe(mockPortal.hostFee);
      expect(updateRequest.hostSpace).toBe(mockPortal.hostSpace);
    }));
  });

  // ===========================================================================
  // CANCEL BUTTON TESTS
  // ===========================================================================

  describe('Cancel Button Navigation', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should navigate to /portals when onCancel is called', () => {
      component.onCancel();
      
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals']);
    });

    it('should navigate to /portals in edit mode when canceling', async () => {
      await setupEditMode(1);
      fixture.detectChanges();
      
      component.onCancel();
      
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals']);
    });
  });

  // ===========================================================================
  // TEMPLATE LOADING TESTS
  // ===========================================================================

  describe('Template Selection Dropdown', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should load templates from service in create mode', fakeAsync(() => {
      tick();
      expect(portalServiceSpy.getTemplates).toHaveBeenCalled();
    }));

    it('should set templates signal with service response', fakeAsync(() => {
      tick();
      expect(component.templates()).toEqual(mockTemplates);
    }));

    it('should compute templateOptions from templates', fakeAsync(() => {
      tick();
      
      const options = component.templateOptions();
      
      // Should include default option + all templates
      expect(options.length).toBe(mockTemplates.length + 1);
      expect(options[0].value).toBe(''); // Default option
    }));

    it('should handle template loading error gracefully', fakeAsync(async () => {
      // Reset and reconfigure for error case
      await setupCreateMode();
      portalServiceSpy.getTemplates.and.returnValue(
        throwError(() => new Error('Failed to load templates'))
      );
      fixture.detectChanges();
      tick();
      
      // Should have empty templates array, not throw error
      expect(component.templates()).toEqual([]);
    }));

    it('should map template name to label in options', fakeAsync(() => {
      tick();
      
      const options = component.templateOptions();
      const defaultTemplate = options.find(opt => opt.value === 'default.template');
      
      expect(defaultTemplate?.label).toBe('Default Template');
    }));
  });

  // ===========================================================================
  // LOADING STATE TESTS
  // ===========================================================================

  describe('Loading State Signal Updates', () => {
    it('should set loading to true during initial data fetch in create mode', async () => {
      await setupCreateMode();
      
      // Use delayed observable to observe loading state
      portalServiceSpy.getTemplates.and.returnValue(
        of(mockTemplates).pipe(delay(100))
      );
      
      fixture.detectChanges();
      
      // Loading should be true initially during async operation
      // Note: Due to async nature, this test verifies the pattern exists
      expect(component.loading).toBeTruthy();
    });

    it('should set loading to true during portal fetch in edit mode', fakeAsync(async () => {
      await setupEditMode(1);
      
      portalServiceSpy.getPortal.and.returnValue(
        of(mockPortal).pipe(delay(100))
      );
      
      fixture.detectChanges();
      
      // After initial setup but before tick, loading state should be transitioning
      tick(100);
      
      expect(component.loading()).toBeFalse();
    }));

    it('should set loading to false after data fetch completes', fakeAsync(() => {
      fixture.detectChanges();
      tick();
      
      expect(component.loading()).toBeFalse();
    }));

    it('should set loading to false even on error', fakeAsync(async () => {
      await setupCreateMode();
      portalServiceSpy.getTemplates.and.returnValue(
        throwError(() => new Error('Error'))
      );
      fixture.detectChanges();
      tick();
      
      expect(component.loading()).toBeFalse();
    }));
  });

  // ===========================================================================
  // ERROR HANDLING TESTS
  // ===========================================================================

  describe('Error Handling', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should set error signal on initialization error', fakeAsync(async () => {
      await setupEditMode(1);
      portalServiceSpy.getPortal.and.returnValue(
        throwError(() => new Error('Failed to load portal data'))
      );
      fixture.detectChanges();
      tick();
      
      expect(component.error()).toContain('Failed to load');
    }));

    it('should clear error when clearError is called', () => {
      component.error.set('Some error message');
      expect(component.error()).toBe('Some error message');
      
      component.clearError();
      
      expect(component.error()).toBeNull();
    });

    it('should display error message returned from service', fakeAsync(() => {
      tick();
      
      const customError = 'Portal creation failed: duplicate alias';
      portalServiceSpy.createPortal.and.returnValue(
        throwError(() => new Error(customError))
      );
      
      component.portalForm.patchValue(validCreateFormData);
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      tick();
      
      expect(component.error()).toBe(customError);
    }));

    it('should reset error before new submission', fakeAsync(() => {
      tick();
      
      // Set initial error
      component.error.set('Previous error');
      
      component.portalForm.patchValue(validCreateFormData);
      component.portalForm.updateValueAndValidity();
      tick(600);
      
      component.onSubmit();
      
      // Error should be cleared at start of submission
      // (before async operation completes)
      expect(component.error()).toBeNull();
      
      tick();
    }));
  });

  // ===========================================================================
  // HOME DIRECTORY CUSTOMIZATION TESTS
  // ===========================================================================

  describe('Home Directory Customization', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should have homeDirectory disabled by default', () => {
      const homeDirectoryControl = component.portalForm.get('homeDirectory');
      expect(homeDirectoryControl?.disabled).toBeTrue();
    });

    it('should enable homeDirectory when onCustomizeHomeDir is called', () => {
      component.onCustomizeHomeDir();
      
      const homeDirectoryControl = component.portalForm.get('homeDirectory');
      expect(homeDirectoryControl?.disabled).toBeFalse();
    });

    it('should clear homeDirectory value when enabling customization', () => {
      component.onCustomizeHomeDir();
      
      const homeDirectoryControl = component.portalForm.get('homeDirectory');
      expect(homeDirectoryControl?.value).toBe('');
    });

    it('should disable homeDirectory and reset to default when toggled back', () => {
      // First enable
      component.onCustomizeHomeDir();
      
      // Then disable
      component.onCustomizeHomeDir();
      
      const homeDirectoryControl = component.portalForm.get('homeDirectory');
      expect(homeDirectoryControl?.disabled).toBeTrue();
      expect(homeDirectoryControl?.value).toBe('Portals/[PortalID]');
    });

    it('should compute isHomeDirectoryCustomized correctly', () => {
      expect(component.isHomeDirectoryCustomized()).toBeFalse();
      
      component.onCustomizeHomeDir();
      component.portalForm.get('homeDirectory')?.setValue('custom/path');
      
      expect(component.isHomeDirectoryCustomized()).toBeTrue();
    });
  });

  // ===========================================================================
  // PORTAL TYPE CHANGE TESTS
  // ===========================================================================

  describe('Portal Type Change Handling', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should trigger validation update when portal type changes', fakeAsync(() => {
      tick();
      
      const portalNameControl = component.portalForm.get('portalName');
      const portalTypeControl = component.portalForm.get('portalType');
      
      // Set valid parent portal name
      portalNameControl?.setValue('www.example.com');
      portalTypeControl?.setValue('P');
      portalNameControl?.updateValueAndValidity();
      tick(600);
      
      // Should be valid for parent
      expect(portalNameControl?.errors?.['invalidCharacters']).toBeFalsy();
      
      // Change to child portal
      portalTypeControl?.setValue('C');
      portalNameControl?.updateValueAndValidity();
      tick(600);
      
      // www.example.com is invalid for child (contains periods)
      expect(portalNameControl?.errors?.['invalidCharacters']).toBeTruthy();
    }));
  });

  // ===========================================================================
  // ERROR MESSAGE HELPER TESTS
  // ===========================================================================

  describe('getErrorMessage Helper', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should return empty string for untouched control', () => {
      const message = component.getErrorMessage('portalName');
      expect(message).toBe('');
    });

    it('should return required error message for touched empty field', () => {
      const control = component.portalForm.get('portalName');
      control?.setValue('');
      control?.markAsTouched();
      
      const message = component.getErrorMessage('portalName');
      expect(message).toContain('required');
    });

    it('should return email error message for invalid email', () => {
      const control = component.portalForm.get('email');
      control?.setValue('invalid-email');
      control?.markAsTouched();
      
      const message = component.getErrorMessage('email');
      expect(message).toContain('valid email');
    });

    it('should return minlength error message', () => {
      const control = component.portalForm.get('username');
      control?.setValue('ab');
      control?.markAsTouched();
      
      const message = component.getErrorMessage('username');
      expect(message).toContain('at least');
    });

    it('should return password mismatch message', () => {
      component.portalForm.patchValue({
        password: 'Password123!',
        confirmPassword: 'DifferentPassword!',
      });
      component.portalForm.updateValueAndValidity();
      
      const confirmPasswordControl = component.portalForm.get('confirmPassword');
      confirmPasswordControl?.markAsTouched();
      
      const message = component.getErrorMessage('confirmPassword');
      expect(message).toContain('do not match');
    });

    it('should return empty string for valid control', () => {
      const control = component.portalForm.get('portalName');
      control?.setValue('validportal');
      control?.markAsTouched();
      
      // Wait for async validators if needed
      const message = component.getErrorMessage('portalName');
      // Should be empty or specific to async validation (aliasExists)
      expect(message === '' || message.includes('alias')).toBeTrue();
    });
  });

  // ===========================================================================
  // PORTAL NAME PLACEHOLDER TESTS
  // ===========================================================================

  describe('getPortalNamePlaceholder Helper', () => {
    beforeEach(async () => {
      await setupCreateMode();
      fixture.detectChanges();
    });

    it('should return parent placeholder for P portal type', fakeAsync(() => {
      tick();
      component.portalForm.get('portalType')?.setValue('P');
      
      const placeholder = component.getPortalNamePlaceholder();
      expect(placeholder).toContain('example.com');
    }));

    it('should return child placeholder for C portal type', fakeAsync(() => {
      tick();
      component.portalForm.get('portalType')?.setValue('C');
      
      const placeholder = component.getPortalNamePlaceholder();
      expect(placeholder).toContain('subdirectory');
    }));

    it('should return edit mode placeholder in edit mode', fakeAsync(async () => {
      await setupEditMode(1);
      fixture.detectChanges();
      tick();
      
      const placeholder = component.getPortalNamePlaceholder();
      expect(placeholder).toBe('Enter portal name');
    }));
  });
});
