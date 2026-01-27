/**
 * MIGRATION: RoleFormComponent
 *
 * Angular 19 standalone component for role create/edit form functionality.
 * Provides a comprehensive form for creating new roles or editing existing ones.
 *
 * MIGRATION SOURCE: Website/admin/Security/EditRoles.ascx.vb
 *
 * Key transformations:
 * - VB.NET Page_Load → ngOnInit()
 * - VB.NET cmdUpdate_Click → onSubmit()
 * - VB.NET cmdDelete_Click → onDelete()
 * - VB.NET cmdCancel_Click → onCancel()
 * - VB.NET cmdManage_Click → onManageUsers()
 * - VB.NET form controls → Angular ReactiveFormsModule
 * - VB.NET ViewState → Angular signals
 * - VB.NET Server.Transfer → Angular Router navigation
 */

import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  inject,
  signal,
  computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';

import { RoleService } from '../../services/role.service';
import { Role, RoleGroup, CreateRoleRequest, UpdateRoleRequest } from '../../models/role.model';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * Interface for frequency options used in billing/trial dropdowns.
 * MIGRATION: Derived from DNN ListController "Frequency" list (lines 117-125).
 */
interface FrequencyOption {
  value: string;
  label: string;
}

/**
 * RoleFormComponent
 *
 * Angular 19 standalone component implementing:
 * - Role creation form for new roles
 * - Role editing form for existing roles
 * - System role protection (Administrator/Registered roles)
 * - Billing and trial period configuration
 * - RSVP code management with link generation
 *
 * Uses Angular 19 patterns:
 * - Standalone component with OnPush change detection
 * - Signals for reactive state management
 * - inject() function for dependency injection
 * - Typed reactive FormGroup
 */
@Component({
  selector: 'app-role-form',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    LoadingSpinnerComponent
  ],
  templateUrl: './role-form.component.html',
  styleUrls: ['./role-form.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RoleFormComponent implements OnInit {
  // ============================================================================
  // DEPENDENCY INJECTION (Angular 19 inject() function)
  // MIGRATION: Replaces VB.NET Module-level imports and DI pattern
  // ============================================================================

  private readonly roleService = inject(RoleService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // ============================================================================
  // CONSTANTS
  // MIGRATION: Derived from EditRoles.ascx.vb system role checks (lines 174-182)
  // These would typically come from a configuration service in production
  // ============================================================================

  /**
   * The role ID of the Administrator role (typically 0 in DNN).
   * MIGRATION: From PortalSettings.AdministratorRoleId
   */
  private readonly administratorRoleId = 0;

  /**
   * The role ID of the Registered Users role (typically 1 in DNN).
   * MIGRATION: From PortalSettings.RegisteredRoleId
   */
  private readonly registeredRoleId = 1;

  // ============================================================================
  // SIGNAL-BASED STATE (Angular 19 signals)
  // MIGRATION: Replaces VB.NET class-level variables and ViewState
  // ============================================================================

  /**
   * The currently loaded role (null for create mode).
   * MIGRATION: From VB.NET _RoleID field and RoleController.GetRole()
   */
  readonly role = signal<Role | null>(null);

  /**
   * Loading state indicator.
   * MIGRATION: Implicit in VB.NET postback model, now explicit for SPA UX
   */
  readonly loading = signal<boolean>(true);

  /**
   * List of available role groups for dropdown.
   * MIGRATION: From BindGroups() method (lines 71-81)
   */
  readonly roleGroups = signal<RoleGroup[]>([]);

  /**
   * Error message to display to the user.
   * MIGRATION: From VB.NET DuplicateRole error handling (line 256)
   */
  readonly errorMessage = signal<string | null>(null);

  /**
   * Generated RSVP link for the role.
   * MIGRATION: From txtRSVPLink display (lines 166-168)
   */
  readonly rsvpLink = signal<string | null>(null);

  /**
   * Warning flag for missing payment processor settings.
   * MIGRATION: From processor check (lines 106-109)
   */
  readonly showProcessorWarning = signal<boolean>(false);

  // ============================================================================
  // COMPUTED SIGNALS (Angular 19 derived state)
  // MIGRATION: Replaces VB.NET calculated properties and conditional checks
  // ============================================================================

  /**
   * Indicates whether the component is in edit mode (vs create mode).
   * MIGRATION: Derived from RoleID = -1 check (line 42, 131)
   */
  readonly isEditMode = computed(() => {
    const currentRole = this.role();
    return currentRole !== null && currentRole.roleId !== undefined && currentRole.roleId > 0;
  });

  /**
   * Indicates whether the current role is a system role (Administrator or Registered).
   * MIGRATION: Derived from system role checks (lines 174-182)
   * System roles cannot be modified or deleted.
   */
  readonly isSystemRole = computed(() => {
    const currentRole = this.role();
    if (!currentRole) return false;
    return currentRole.roleId === this.administratorRoleId ||
           currentRole.roleId === this.registeredRoleId;
  });

  /**
   * Indicates whether the current role is the Registered Users role.
   * MIGRATION: Used to disable "Manage Users" button (lines 180-182)
   * Registered Users role cannot have users manually assigned.
   */
  readonly isRegisteredRole = computed(() => {
    const currentRole = this.role();
    return currentRole?.roleId === this.registeredRoleId;
  });

  // ============================================================================
  // FREQUENCY OPTIONS
  // MIGRATION: Derived from DNN ListController "Frequency" list (lines 117-125)
  // ============================================================================

  /**
   * Billing/Trial frequency options for dropdowns.
   * MIGRATION: Populated from ListController.GetListEntryInfoCollection("Frequency")
   */
  readonly frequencies: FrequencyOption[] = [
    { value: 'N', label: 'None' },
    { value: 'O', label: 'One Time' },
    { value: 'D', label: 'Daily' },
    { value: 'W', label: 'Weekly' },
    { value: 'M', label: 'Monthly' },
    { value: 'Y', label: 'Yearly' }
  ];

  // ============================================================================
  // REACTIVE FORM DEFINITION
  // MIGRATION: Form fields from EditRoles.ascx.vb control names
  // ============================================================================

  /**
   * Reactive form group for role data.
   * MIGRATION: Replaces individual VB.NET form controls
   */
  readonly roleForm = new FormGroup({
    /** txtRoleName (lines 132-134, 186-188) */
    roleName: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50)]
    }),

    /** txtDescription (line 140) */
    description: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.maxLength(1000)]
    }),

    /** cboRoleGroups (lines 71-81, 141-144) - -1 represents "Global Roles" */
    roleGroupId: new FormControl<number>(-1, { nonNullable: true }),

    /** chkIsPublic (line 163) */
    isPublic: new FormControl<boolean>(false, { nonNullable: true }),

    /** chkAutoAssignment (line 164) */
    autoAssignment: new FormControl<boolean>(false, { nonNullable: true }),

    /** txtServiceFee (lines 146-147) */
    serviceFee: new FormControl<number | null>(null),

    /** txtBillingPeriod (line 148) */
    billingPeriod: new FormControl<number | null>(null),

    /** cboBillingFrequency (lines 119-121, 149-151) */
    billingFrequency: new FormControl<string>('N', { nonNullable: true }),

    /** txtTrialFee (line 155) */
    trialFee: new FormControl<number | null>(null),

    /** txtTrialPeriod (line 156) */
    trialPeriod: new FormControl<number | null>(null),

    /** cboTrialFrequency (lines 123-125, 157-159) */
    trialFrequency: new FormControl<string>('N', { nonNullable: true }),

    /** txtRSVPCode (line 165) */
    rsvpCode: new FormControl<string>('', { nonNullable: true }),

    /** ctlIcon.Url (line 169) */
    iconFile: new FormControl<string>('', { nonNullable: true })
  });

  // ============================================================================
  // CONSTRUCTOR
  // MIGRATION: Setup RSVP code change listener
  // ============================================================================

  constructor() {
    // Watch for rsvpCode changes to update the RSVP link
    this.roleForm.controls.rsvpCode.valueChanges.subscribe(code => {
      if (code && code.trim()) {
        this.rsvpLink.set(`${window.location.origin}/?rsvp=${code}`);
      } else {
        this.rsvpLink.set(null);
      }
    });
  }

  // ============================================================================
  // LIFECYCLE HOOKS
  // MIGRATION: Derived from Page_Load (lines 98-195)
  // ============================================================================

  /**
   * Initialize the component.
   * MIGRATION: Replaces VB.NET Page_Load event handler
   */
  ngOnInit(): void {
    // MIGRATION: Check RoleID from route param (replaces QueryString check, lines 100-102)
    const roleIdParam = this.route.snapshot.paramMap.get('id');

    // MIGRATION: Check processor settings (lines 104-109)
    this.checkProcessorSettings();

    // MIGRATION: Bind role groups (line 127)
    this.loadRoleGroups();

    // MIGRATION: Load role if editing (lines 131-172)
    if (roleIdParam) {
      const roleId = parseInt(roleIdParam, 10);
      if (!isNaN(roleId)) {
        this.loadRole(roleId);
      } else {
        // Invalid role ID, redirect to list
        this.router.navigate(['/roles']);
      }
    } else {
      // Create mode - no role to load
      this.loading.set(false);
    }
  }

  // ============================================================================
  // DATA LOADING METHODS
  // MIGRATION: Derived from EditRoles.ascx.vb data binding methods
  // ============================================================================

  /**
   * Load a role by ID for editing.
   * MIGRATION: Derived from lines 136-169 in EditRoles.ascx.vb
   */
  private loadRole(roleId: number): void {
    this.roleService.getRole(roleId).subscribe({
      next: (role) => {
        this.role.set(role);
        this.populateForm(role);
        this.updateRsvpLink();

        // MIGRATION: Disable controls for system roles (lines 174-182)
        if (this.isSystemRole()) {
          this.disableFormControls();
        }

        this.loading.set(false);
      },
      error: () => {
        // Role not found or error loading, redirect to list
        this.router.navigate(['/roles']);
      }
    });
  }

  /**
   * Load role groups for the dropdown.
   * MIGRATION: Derived from BindGroups() method (lines 71-81)
   */
  private loadRoleGroups(): void {
    this.roleService.getRoleGroups().subscribe({
      next: (groups) => {
        this.roleGroups.set(groups);
      },
      error: () => {
        // Failed to load groups, continue with empty list
        this.roleGroups.set([]);
      }
    });
  }

  /**
   * Check if payment processor settings are configured.
   * MIGRATION: Derived from lines 104-109 in EditRoles.ascx.vb
   */
  private checkProcessorSettings(): void {
    // In the original VB.NET code, this checked Host.PaymentProcessor and PortalSettings.ProcessorPassword
    // For the migration, we'll assume processor is configured unless we add a check endpoint
    // This would be enhanced in production to make an API call to verify processor settings
    this.showProcessorWarning.set(false);
  }

  // ============================================================================
  // FORM POPULATION METHODS
  // MIGRATION: Derived from form population logic in Page_Load
  // ============================================================================

  /**
   * Populate the form with role data.
   * MIGRATION: Derived from lines 139-169 in EditRoles.ascx.vb
   */
  private populateForm(role: Role): void {
    this.roleForm.patchValue({
      roleName: role.roleName,
      description: role.description || '',
      roleGroupId: role.roleGroupId ?? -1,
      isPublic: role.isPublic,
      autoAssignment: role.autoAssignment,

      // MIGRATION: Only populate billing if ServiceFee > 0 (lines 146-152)
      serviceFee: role.serviceFee > 0 ? role.serviceFee : null,
      billingPeriod: role.billingPeriod > 0 ? role.billingPeriod : null,
      billingFrequency: role.billingFrequency || 'N',

      // MIGRATION: Only populate trial if TrialFrequency != 'N' (lines 154-161)
      trialFee: role.trialFrequency !== 'N' ? role.trialFee : null,
      trialPeriod: role.trialFrequency !== 'N' ? role.trialPeriod : null,
      trialFrequency: role.trialFrequency || 'N',

      rsvpCode: role.rsvpCode || '',
      iconFile: role.iconFile || ''
    });
  }

  /**
   * Disable all form controls for system roles.
   * MIGRATION: Derived from ActivateControls(False) method (lines 47-59)
   */
  private disableFormControls(): void {
    Object.keys(this.roleForm.controls).forEach(key => {
      this.roleForm.get(key)?.disable();
    });
  }

  /**
   * Update the RSVP link based on current role data.
   * MIGRATION: Derived from lines 166-168 in EditRoles.ascx.vb
   */
  private updateRsvpLink(): void {
    const currentRole = this.role();
    if (currentRole?.rsvpCode) {
      this.rsvpLink.set(`${window.location.origin}/?rsvp=${currentRole.rsvpCode}`);
    } else {
      this.rsvpLink.set(null);
    }
  }

  // ============================================================================
  // FORM ACTION HANDLERS
  // MIGRATION: Derived from button click handlers in EditRoles.ascx.vb
  // ============================================================================

  /**
   * Handle form submission (create or update role).
   * MIGRATION: Derived from cmdUpdate_Click (lines 208-274)
   */
  onSubmit(): void {
    // Validate form
    if (this.roleForm.invalid) {
      // Mark all controls as touched to show validation errors
      Object.keys(this.roleForm.controls).forEach(key => {
        this.roleForm.get(key)?.markAsTouched();
      });
      return;
    }

    // Prevent submission for system roles
    if (this.isSystemRole()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const formValue = this.roleForm.getRawValue();

    // MIGRATION: Build role request (lines 232-248)
    const request: CreateRoleRequest = {
      roleName: formValue.roleName,
      description: formValue.description || undefined,
      roleGroupId: formValue.roleGroupId !== -1 ? formValue.roleGroupId : undefined,
      isPublic: formValue.isPublic,
      autoAssignment: formValue.autoAssignment,

      // MIGRATION: Billing settings (lines 216-220)
      serviceFee: formValue.serviceFee ?? undefined,
      billingPeriod: formValue.billingPeriod ?? undefined,
      billingFrequency: formValue.billingFrequency !== 'N' ? formValue.billingFrequency as any : undefined,

      // MIGRATION: Trial settings (lines 222-230)
      trialFee: formValue.trialFee ?? undefined,
      trialPeriod: formValue.trialPeriod ?? undefined,
      trialFrequency: formValue.trialFrequency !== 'N' ? formValue.trialFrequency as any : undefined,

      rsvpCode: formValue.rsvpCode || undefined,
      iconFile: formValue.iconFile || undefined
    };

    // MIGRATION: Create or update based on mode (lines 251-262)
    const operation = this.isEditMode()
      ? this.roleService.updateRole(this.role()!.roleId, request as UpdateRoleRequest)
      : this.roleService.createRole(request);

    operation.subscribe({
      next: () => {
        this.router.navigate(['/roles']);
      },
      error: (err) => {
        // MIGRATION: Handle duplicate role error (line 256)
        if (err.status === 409) {
          this.errorMessage.set('A role with this name already exists.');
        } else if (err.status === 400) {
          this.errorMessage.set('Please check the form for errors.');
        } else {
          this.errorMessage.set('An error occurred while saving the role.');
        }
        this.loading.set(false);
      }
    });
  }

  /**
   * Handle role deletion.
   * MIGRATION: Derived from cmdDelete_Click (lines 287-303)
   */
  onDelete(): void {
    // Validate state
    if (!this.isEditMode() || this.isSystemRole()) {
      return;
    }

    const currentRole = this.role();
    if (!currentRole) {
      return;
    }

    // MIGRATION: Show confirmation (replaces ClientAPI.AddButtonConfirm, line 112)
    if (confirm(`Are you sure you want to delete the role "${currentRole.roleName}"?`)) {
      this.loading.set(true);
      this.errorMessage.set(null);

      this.roleService.deleteRole(currentRole.roleId).subscribe({
        next: () => {
          this.router.navigate(['/roles']);
        },
        error: () => {
          this.errorMessage.set('An error occurred while deleting the role.');
          this.loading.set(false);
        }
      });
    }
  }

  /**
   * Handle cancel action.
   * MIGRATION: Derived from cmdCancel_Click (lines 316-323)
   */
  onCancel(): void {
    this.router.navigate(['/roles']);
  }

  /**
   * Navigate to user management for this role.
   * MIGRATION: Derived from cmdManage_Click (lines 336-342)
   */
  onManageUsers(): void {
    if (this.isEditMode() && !this.isRegisteredRole()) {
      const currentRole = this.role();
      if (currentRole) {
        // MIGRATION: Navigate to user roles (replaces NavigateURL, line 338)
        this.router.navigate(['/roles', currentRole.roleId, 'users']);
      }
    }
  }

  /**
   * Copy the RSVP link to clipboard.
   * Enhancement for better UX - not in original VB.NET code.
   */
  copyRsvpLink(): void {
    const link = this.rsvpLink();
    if (link) {
      navigator.clipboard.writeText(link).then(() => {
        // Could show a toast notification here
        console.log('RSVP link copied to clipboard');
      }).catch(err => {
        console.error('Failed to copy RSVP link:', err);
      });
    }
  }

  /**
   * Handle icon image load error.
   * Enhancement for better UX - shows fallback when icon path is invalid.
   */
  onIconError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (img) {
      img.style.display = 'none';
    }
  }
}
