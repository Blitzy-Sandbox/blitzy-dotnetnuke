/**
 * RoleFormComponent - Angular 19 Standalone Component for Role Create/Edit Form
 *
 * MIGRATION: Converted from DotNetNuke 4.x VB.NET WebForms:
 * - Website/admin/Security/EditRoles.ascx.vb (lines 1-350)
 * - Library/Components/Security/Roles/RoleInfo.vb (entity properties)
 *
 * This component provides a comprehensive form for creating new roles or editing
 * existing ones with fields for: role name, description, role group selection,
 * public/auto-assignment flags, billing settings, trial settings, RSVP code, and icon file.
 *
 * Key transformations:
 * - VB.NET Page_Load → ngOnInit()
 * - VB.NET cmdUpdate_Click → onSubmit()
 * - VB.NET cmdDelete_Click → onDelete()
 * - VB.NET cmdCancel_Click → onCancel()
 * - VB.NET cmdManage_Click → onManageUsers()
 * - VB.NET BindGroups() → loadRoleGroups()
 * - VB.NET ActivateControls(False) → disableFormControls()
 * - VB.NET form controls → Angular ReactiveFormsModule
 * - VB.NET ViewState → Angular signals
 * - VB.NET Response.Redirect → Angular Router navigation
 * - VB.NET ClientAPI.AddButtonConfirm → ConfirmationDialogComponent
 *
 * Implements Angular 19 patterns:
 * - Standalone component with OnPush change detection
 * - Signals for reactive state management (signal(), computed())
 * - inject() function for dependency injection
 * - Typed reactive FormGroup for form handling
 * - @if, @for control flow syntax in templates
 *
 * System role protection:
 * - Administrator and Registered Users roles cannot be edited or deleted
 * - Registered Users role cannot have users manually assigned
 *
 * @fileoverview Role create/edit form component
 * @module features/role/components/role-form
 */

import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  inject,
  signal,
  computed,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import {
  ReactiveFormsModule,
  FormGroup,
  FormControl,
  Validators
} from '@angular/forms';

// Internal imports - from depends_on_files
import { RoleService } from '../../services/role.service';
import {
  Role,
  RoleGroup,
  CreateRoleRequest,
  UpdateRoleRequest
} from '../../models/role.model';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';

// ============================================================================
// INTERFACES
// ============================================================================

/**
 * Interface for frequency options used in billing/trial dropdowns.
 * MIGRATION: Derived from DNN ListController "Frequency" list (EditRoles.ascx.vb lines 117-125).
 *
 * Values documented in RoleInfo.vb lines 140-146 and 178-185:
 * - N = None
 * - O = One time fee
 * - D = Daily
 * - W = Weekly
 * - M = Monthly
 * - Y = Yearly
 */
interface FrequencyOption {
  /**
   * The frequency code value ('N', 'O', 'D', 'W', 'M', 'Y')
   */
  value: string;

  /**
   * Human-readable label for the frequency
   */
  label: string;
}

// ============================================================================
// COMPONENT DEFINITION
// ============================================================================

/**
 * RoleFormComponent
 *
 * Angular 19 standalone component implementing:
 * - Role creation form for new roles
 * - Role editing form for existing roles
 * - System role protection (Administrator/Registered roles cannot be modified)
 * - Billing and trial period configuration
 * - RSVP code management with link generation
 * - Integration with ConfirmationDialogComponent for delete operations
 *
 * Uses Angular 19 patterns:
 * - Standalone component with OnPush change detection
 * - Signals for reactive state management
 * - inject() function for dependency injection
 * - Typed reactive FormGroup
 *
 * @example
 * // Route configuration for create mode
 * { path: 'roles/new', component: RoleFormComponent }
 *
 * // Route configuration for edit mode
 * { path: 'roles/:id', component: RoleFormComponent }
 */
@Component({
  selector: 'app-role-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    LoadingSpinnerComponent,
    ConfirmationDialogComponent
  ],
  templateUrl: './role-form.component.html',
  styleUrls: ['./role-form.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RoleFormComponent implements OnInit {
  // ==========================================================================
  // DEPENDENCY INJECTION (Angular 19 inject() function)
  // MIGRATION: Replaces VB.NET Module-level imports and DI pattern
  // ==========================================================================

  /**
   * RoleService injected using Angular 19 inject() function.
   * MIGRATION: Replaces VB.NET RoleController instantiation patterns.
   * Used for: getRole(), getRoleGroups(), createRole(), updateRole(), deleteRole()
   */
  private readonly roleService = inject(RoleService);

  /**
   * Angular Router injected for programmatic navigation.
   * MIGRATION: Replaces VB.NET Response.Redirect() and NavigateURL() calls.
   */
  private readonly router = inject(Router);

  /**
   * ActivatedRoute injected for accessing route parameters.
   * MIGRATION: Replaces VB.NET Request.QueryString("RoleID") access.
   */
  private readonly route = inject(ActivatedRoute);

  // ==========================================================================
  // VIEW CHILD REFERENCES
  // ==========================================================================

  /**
   * Reference to the delete confirmation dialog component.
   * MIGRATION: Replaces DNN's ClientAPI.AddButtonConfirm(cmdDelete, ...) pattern
   * from EditRoles.ascx.vb line 112.
   */
  @ViewChild('deleteConfirmDialog') deleteConfirmDialog?: ConfirmationDialogComponent;

  // ==========================================================================
  // CONSTANTS
  // MIGRATION: Derived from EditRoles.ascx.vb system role checks (lines 174-182)
  // ==========================================================================

  /**
   * The role ID of the Administrator role.
   * MIGRATION: From PortalSettings.AdministratorRoleId (typically 0 in DNN).
   * System administrators cannot modify this role.
   */
  private readonly administratorRoleId = 0;

  /**
   * The role ID of the Registered Users role.
   * MIGRATION: From PortalSettings.RegisteredRoleId (typically 1 in DNN).
   * This role is automatically assigned to all registered users and cannot be modified.
   */
  private readonly registeredRoleId = 1;

  // ==========================================================================
  // SIGNAL-BASED STATE (Angular 19 signals)
  // MIGRATION: Replaces VB.NET class-level variables and ViewState
  // ==========================================================================

  /**
   * The currently loaded role (null for create mode).
   * MIGRATION: From VB.NET RoleID field and RoleController.GetRole() (line 136).
   */
  readonly role = signal<Role | null>(null);

  /**
   * Loading state indicator for async operations.
   * MIGRATION: Implicit in VB.NET postback model, now explicit for SPA UX.
   * Controls visibility of LoadingSpinnerComponent.
   */
  readonly loading = signal<boolean>(true);

  /**
   * List of available role groups for dropdown selection.
   * MIGRATION: From BindGroups() method (lines 71-81).
   * Populated from RoleService.getRoleGroups().
   */
  readonly roleGroups = signal<RoleGroup[]>([]);

  /**
   * Error message to display to the user.
   * MIGRATION: From VB.NET DuplicateRole error handling (line 256) and
   * Skin.AddModuleMessage error display pattern.
   */
  readonly errorMessage = signal<string | null>(null);

  /**
   * Generated RSVP link for the role based on rsvpCode.
   * MIGRATION: From txtRSVPLink display logic (lines 166-168).
   * Format: {origin}/?rsvp={rsvpCode}
   */
  readonly rsvpLink = signal<string | null>(null);

  /**
   * Warning flag for missing payment processor settings.
   * MIGRATION: From processor check (lines 104-109).
   * Warns users about fee-based roles if no processor is configured.
   */
  readonly showProcessorWarning = signal<boolean>(false);

  // ==========================================================================
  // COMPUTED SIGNALS (Angular 19 derived state)
  // MIGRATION: Replaces VB.NET calculated properties and conditional checks
  // ==========================================================================

  /**
   * Indicates whether the component is in edit mode (vs create mode).
   * MIGRATION: Derived from RoleID = -1 check (lines 42, 131).
   * In VB.NET, RoleID = -1 indicated create mode.
   */
  readonly isEditMode = computed(() => {
    const currentRole = this.role();
    return currentRole !== null &&
           currentRole.roleId !== undefined &&
           currentRole.roleId > 0;
  });

  /**
   * Indicates whether the current role is a system role (Administrator or Registered).
   * MIGRATION: Derived from system role checks (lines 174-182):
   * ```vb
   * If RoleID = PortalSettings.AdministratorRoleId Or RoleID = PortalSettings.RegisteredRoleId Then
   *     cmdDelete.Visible = False
   *     cmdUpdate.Visible = False
   *     ActivateControls(False)
   * End If
   * ```
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
   * MIGRATION: Used to disable "Manage Users" button (lines 180-182):
   * ```vb
   * If RoleID = PortalSettings.RegisteredRoleId Then
   *     cmdManage.Visible = False
   * End If
   * ```
   * Registered Users role cannot have users manually assigned.
   */
  readonly isRegisteredRole = computed(() => {
    const currentRole = this.role();
    return currentRole?.roleId === this.registeredRoleId;
  });

  // ==========================================================================
  // FREQUENCY OPTIONS
  // MIGRATION: Derived from DNN ListController "Frequency" list (lines 117-125)
  // ==========================================================================

  /**
   * Billing/Trial frequency options for dropdowns.
   * MIGRATION: Populated from ListController.GetListEntryInfoCollection("Frequency").
   * Values documented in RoleInfo.vb lines 140-146 and 178-185.
   */
  readonly frequencies: FrequencyOption[] = [
    { value: 'N', label: 'None' },
    { value: 'O', label: 'One Time' },
    { value: 'D', label: 'Daily' },
    { value: 'W', label: 'Weekly' },
    { value: 'M', label: 'Monthly' },
    { value: 'Y', label: 'Yearly' }
  ];

  // ==========================================================================
  // REACTIVE FORM DEFINITION
  // MIGRATION: Form fields from EditRoles.ascx.vb control names
  // ==========================================================================

  /**
   * Reactive form group for role data.
   * MIGRATION: Replaces individual VB.NET form controls (txtRoleName, txtDescription,
   * cboRoleGroups, chkIsPublic, chkAutoAssignment, txtServiceFee, txtBillingPeriod,
   * cboBillingFrequency, txtTrialFee, txtTrialPeriod, cboTrialFrequency, txtRSVPCode, ctlIcon).
   */
  readonly roleForm = new FormGroup({
    /**
     * Role name field.
     * MIGRATION: txtRoleName (lines 132-134, 186-188, 237).
     * Required, max 50 characters per DNN validation.
     */
    roleName: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50)]
    }),

    /**
     * Role description field.
     * MIGRATION: txtDescription (lines 48, 140, 238).
     * Optional, max 1000 characters.
     */
    description: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.maxLength(1000)]
    }),

    /**
     * Role group selection.
     * MIGRATION: cboRoleGroups (lines 49, 71-81, 141-144, 236).
     * Value -1 represents "Global Roles" (no group assigned).
     */
    roleGroupId: new FormControl<number>(-1, { nonNullable: true }),

    /**
     * Public role flag.
     * MIGRATION: chkIsPublic (lines 50, 163, 245).
     * When true, users can see and request membership to this role.
     */
    isPublic: new FormControl<boolean>(false, { nonNullable: true }),

    /**
     * Auto-assignment flag.
     * MIGRATION: chkAutoAssignment (lines 51, 164, 246).
     * When true, new users are automatically assigned to this role.
     */
    autoAssignment: new FormControl<boolean>(false, { nonNullable: true }),

    /**
     * Service fee for role membership.
     * MIGRATION: txtServiceFee (lines 52, 146-147, 217, 239).
     * Used for paid role memberships.
     */
    serviceFee: new FormControl<number | null>(null),

    /**
     * Billing period (number of billing frequency units).
     * MIGRATION: txtBillingPeriod (lines 53, 148, 218, 240).
     */
    billingPeriod: new FormControl<number | null>(null),

    /**
     * Billing frequency code.
     * MIGRATION: cboBillingFrequency (lines 54, 119-121, 149-151, 219, 241).
     * Default 'N' (None).
     */
    billingFrequency: new FormControl<string>('N', { nonNullable: true }),

    /**
     * Trial fee for trial membership.
     * MIGRATION: txtTrialFee (lines 55, 155, 227, 242).
     */
    trialFee: new FormControl<number | null>(null),

    /**
     * Trial period (number of trial frequency units).
     * MIGRATION: txtTrialPeriod (lines 56, 156, 228, 243).
     */
    trialPeriod: new FormControl<number | null>(null),

    /**
     * Trial frequency code.
     * MIGRATION: cboTrialFrequency (lines 57, 123-125, 157-159, 229, 244).
     * Default 'N' (None).
     */
    trialFrequency: new FormControl<string>('N', { nonNullable: true }),

    /**
     * RSVP code for role membership requests.
     * MIGRATION: txtRSVPCode (lines 58, 165, 247).
     * Users can use this code to request membership via URL.
     */
    rsvpCode: new FormControl<string>('', { nonNullable: true }),

    /**
     * Icon file path/URL.
     * MIGRATION: ctlIcon.Url (lines 59, 129, 169, 248).
     */
    iconFile: new FormControl<string>('', { nonNullable: true })
  });

  // ==========================================================================
  // CONSTRUCTOR
  // ==========================================================================

  /**
   * Initialize component and set up reactive subscriptions.
   * MIGRATION: Setup RSVP code change listener for dynamic link generation.
   */
  constructor() {
    // Watch for rsvpCode changes to update the RSVP link dynamically
    // MIGRATION: In VB.NET, this was done on postback (lines 166-168)
    this.roleForm.controls.rsvpCode.valueChanges.subscribe(code => {
      if (code && code.trim()) {
        // MIGRATION: Build RSVP link format from line 167:
        // txtRSVPLink.Text = AddHTTP(GetDomainName(Request)) & "/" & glbDefaultPage & "?rsvp=" & txtRSVPCode.Text
        this.rsvpLink.set(`${window.location.origin}/?rsvp=${code.trim()}`);
      } else {
        this.rsvpLink.set(null);
      }
    });
  }

  // ==========================================================================
  // LIFECYCLE HOOKS
  // MIGRATION: Derived from Page_Load (lines 98-195)
  // ==========================================================================

  /**
   * Initialize the component on load.
   *
   * MIGRATION: Replaces VB.NET Page_Load event handler (lines 98-195).
   * Performs:
   * 1. Route parameter extraction (replaces Request.QueryString check, lines 100-102)
   * 2. Processor settings check (lines 104-109)
   * 3. Role groups loading (line 127)
   * 4. Role data loading for edit mode (lines 131-172)
   * 5. System role protection (lines 174-182)
   */
  ngOnInit(): void {
    // MIGRATION: Check RoleID from route param
    // VB.NET: If Not (Request.QueryString("RoleID") Is Nothing) Then
    //            RoleID = Int32.Parse(Request.QueryString("RoleID"))
    const roleIdParam = this.route.snapshot.paramMap.get('id');

    // MIGRATION: Check processor settings (lines 104-109)
    // VB.NET: If (objPortalInfo Is Nothing OrElse String.IsNullOrEmpty(objPortalInfo.ProcessorUserId)) Then
    //            lblProcessorWarning.Visible = True
    this.checkProcessorSettings();

    // MIGRATION: Bind role groups (line 127)
    // VB.NET: BindGroups()
    this.loadRoleGroups();

    // MIGRATION: Load role if editing (lines 131-172)
    // VB.NET: If RoleID <> -1 Then ... Dim objRoleInfo As RoleInfo = objUser.GetRole(RoleID, PortalSettings.PortalId)
    if (roleIdParam) {
      const roleId = parseInt(roleIdParam, 10);
      if (!isNaN(roleId) && roleId > 0) {
        this.loadRole(roleId);
      } else {
        // Invalid role ID format, redirect to list
        // MIGRATION: Response.Redirect(NavigateURL("Security Roles"))
        this.router.navigate(['/roles']);
      }
    } else {
      // Create mode - no role to load
      // MIGRATION: In VB.NET, RoleID = -1 indicated create mode (line 42)
      this.loading.set(false);
    }
  }

  // ==========================================================================
  // DATA LOADING METHODS
  // MIGRATION: Derived from EditRoles.ascx.vb data binding methods
  // ==========================================================================

  /**
   * Load a role by ID for editing.
   *
   * MIGRATION: Derived from lines 136-169 in EditRoles.ascx.vb:
   * ```vb
   * Dim objRoleInfo As RoleInfo = objUser.GetRole(RoleID, PortalSettings.PortalId)
   * If Not objRoleInfo Is Nothing Then
   *     lblRoleName.Text = objRoleInfo.RoleName
   *     txtDescription.Text = objRoleInfo.Description
   *     ...
   * Else
   *     Response.Redirect(NavigateURL("Security Roles"))
   * End If
   * ```
   *
   * @param roleId - The ID of the role to load
   */
  private loadRole(roleId: number): void {
    this.roleService.getRole(roleId).subscribe({
      next: (role) => {
        this.role.set(role);
        this.populateForm(role);
        this.updateRsvpLink();

        // MIGRATION: Disable controls for system roles (lines 174-182)
        // VB.NET: If RoleID = PortalSettings.AdministratorRoleId Or RoleID = PortalSettings.RegisteredRoleId Then
        //            ActivateControls(False)
        if (this.isSystemRole()) {
          this.disableFormControls();
        }

        this.loading.set(false);
      },
      error: () => {
        // Role not found or error loading, redirect to list
        // MIGRATION: Response.Redirect(NavigateURL("Security Roles"))
        this.router.navigate(['/roles']);
      }
    });
  }

  /**
   * Load role groups for the dropdown selection.
   *
   * MIGRATION: Derived from BindGroups() method (lines 71-81):
   * ```vb
   * Private Sub BindGroups()
   *     Dim arrGroups As ArrayList = RoleController.GetRoleGroups(PortalId)
   *     cboRoleGroups.Items.Add(New ListItem(Localization.GetString("GlobalRoles"), "-1"))
   *     For Each roleGroup As RoleGroupInfo In arrGroups
   *         cboRoleGroups.Items.Add(New ListItem(roleGroup.RoleGroupName, roleGroup.RoleGroupID.ToString))
   *     Next
   * End Sub
   * ```
   */
  private loadRoleGroups(): void {
    this.roleService.getRoleGroups().subscribe({
      next: (groups) => {
        this.roleGroups.set(groups);
      },
      error: () => {
        // Failed to load groups, continue with empty list
        // User can still create/edit roles without a group
        this.roleGroups.set([]);
      }
    });
  }

  /**
   * Check if payment processor settings are configured.
   *
   * MIGRATION: Derived from lines 104-109 in EditRoles.ascx.vb:
   * ```vb
   * Dim objPortalController As New PortalController
   * Dim objPortalInfo As PortalInfo = objPortalController.GetPortal(PortalSettings.PortalId)
   * If (objPortalInfo Is Nothing OrElse String.IsNullOrEmpty(objPortalInfo.ProcessorUserId)) Then
   *     lblProcessorWarning.Visible = True
   * End If
   * ```
   *
   * In the modernized architecture, this could be enhanced to make an API call
   * to verify processor settings. For now, we assume processor is configured.
   */
  private checkProcessorSettings(): void {
    // In production, this would call an API endpoint to check processor configuration
    // For initial migration, assume processor is configured to avoid false warnings
    this.showProcessorWarning.set(false);
  }

  // ==========================================================================
  // FORM POPULATION METHODS
  // MIGRATION: Derived from form population logic in Page_Load
  // ==========================================================================

  /**
   * Populate the form with role data for editing.
   *
   * MIGRATION: Derived from lines 139-169 in EditRoles.ascx.vb.
   * Maps RoleInfo properties to form controls with conditional logic for
   * billing and trial fields.
   *
   * @param role - The role data to populate the form with
   */
  private populateForm(role: Role): void {
    this.roleForm.patchValue({
      // Basic role info
      roleName: role.roleName,
      description: role.description || '',
      // MIGRATION: roleGroupId -1 means "Global Roles" (no group)
      roleGroupId: role.roleGroupId ?? -1,
      isPublic: role.isPublic,
      autoAssignment: role.autoAssignment,

      // MIGRATION: Only populate billing if ServiceFee > 0 (lines 146-152)
      // VB.NET: If Format(objRoleInfo.ServiceFee, "#,##0.00") <> "0.00" Then
      serviceFee: role.serviceFee > 0 ? role.serviceFee : null,
      billingPeriod: role.billingPeriod > 0 ? role.billingPeriod : null,
      billingFrequency: role.billingFrequency || 'N',

      // MIGRATION: Only populate trial if TrialFrequency != 'N' (lines 154-161)
      // VB.NET: If objRoleInfo.TrialFrequency <> "N" Then
      trialFee: role.trialFrequency !== 'N' ? role.trialFee : null,
      trialPeriod: role.trialFrequency !== 'N' ? role.trialPeriod : null,
      trialFrequency: role.trialFrequency || 'N',

      // RSVP and icon
      rsvpCode: role.rsvpCode || '',
      iconFile: role.iconFile || ''
    });
  }

  /**
   * Disable all form controls for system roles.
   *
   * MIGRATION: Derived from ActivateControls(False) method (lines 47-59):
   * ```vb
   * Private Sub ActivateControls(ByVal enabled As Boolean)
   *     txtDescription.Enabled = enabled
   *     cboRoleGroups.Enabled = enabled
   *     chkIsPublic.Enabled = enabled
   *     chkAutoAssignment.Enabled = enabled
   *     ...
   * End Sub
   * ```
   *
   * Called when viewing Administrator or Registered Users roles.
   */
  private disableFormControls(): void {
    // Disable all form controls
    Object.keys(this.roleForm.controls).forEach(key => {
      const control = this.roleForm.get(key);
      if (control) {
        control.disable();
      }
    });
  }

  /**
   * Update the RSVP link based on current role data.
   *
   * MIGRATION: Derived from lines 166-168 in EditRoles.ascx.vb:
   * ```vb
   * If txtRSVPCode.Text <> "" Then
   *     txtRSVPLink.Text = AddHTTP(GetDomainName(Request)) & "/" & glbDefaultPage & "?rsvp=" & txtRSVPCode.Text
   * End If
   * ```
   */
  private updateRsvpLink(): void {
    const currentRole = this.role();
    if (currentRole?.rsvpCode) {
      this.rsvpLink.set(`${window.location.origin}/?rsvp=${currentRole.rsvpCode}`);
    } else {
      this.rsvpLink.set(null);
    }
  }

  // ==========================================================================
  // FORM ACTION HANDLERS
  // MIGRATION: Derived from button click handlers in EditRoles.ascx.vb
  // ==========================================================================

  /**
   * Handle form submission (create or update role).
   *
   * MIGRATION: Derived from cmdUpdate_Click (lines 208-274):
   * ```vb
   * Private Sub cmdUpdate_Click(ByVal sender As Object, ByVal e As EventArgs)
   *     If Page.IsValid Then
   *         ...
   *         If RoleID = -1 Then
   *             If objRoleController.GetRoleByName(PortalId, objRoleInfo.RoleName) Is Nothing Then
   *                 objRoleController.AddRole(objRoleInfo)
   *             Else
   *                 ' DuplicateRole error
   *             End If
   *         Else
   *             objRoleController.UpdateRole(objRoleInfo)
   *         End If
   *         Response.Redirect(NavigateURL())
   *     End If
   * End Sub
   * ```
   */
  onSubmit(): void {
    // Validate form before submission
    if (this.roleForm.invalid) {
      // Mark all controls as touched to show validation errors
      Object.keys(this.roleForm.controls).forEach(key => {
        const control = this.roleForm.get(key);
        if (control) {
          control.markAsTouched();
        }
      });
      return;
    }

    // Prevent submission for system roles
    // MIGRATION: These roles have buttons hidden in VB.NET (lines 175-176)
    if (this.isSystemRole()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const formValue = this.roleForm.getRawValue();

    // MIGRATION: Build role request (lines 232-248)
    // VB.NET built RoleInfo object with all properties
    const request: CreateRoleRequest = {
      roleName: formValue.roleName,
      description: formValue.description || undefined,
      // MIGRATION: -1 means "Global Roles" (no group), map to undefined
      roleGroupId: formValue.roleGroupId !== -1 ? formValue.roleGroupId : undefined,
      isPublic: formValue.isPublic,
      autoAssignment: formValue.autoAssignment,

      // MIGRATION: Billing settings (lines 216-220)
      // VB.NET: If txtServiceFee.Text <> "" And txtBillingPeriod.Text <> "" And cboBillingFrequency.SelectedItem.Value <> "N"
      serviceFee: formValue.serviceFee ?? undefined,
      billingPeriod: formValue.billingPeriod ?? undefined,
      billingFrequency: formValue.billingFrequency !== 'N'
        ? (formValue.billingFrequency as CreateRoleRequest['billingFrequency'])
        : undefined,

      // MIGRATION: Trial settings (lines 222-230)
      // VB.NET: If sglServiceFee <> 0 And txtTrialFee.Text <> "" And txtTrialPeriod.Text <> "" And cboTrialFrequency.SelectedItem.Value <> "N"
      trialFee: formValue.trialFee ?? undefined,
      trialPeriod: formValue.trialPeriod ?? undefined,
      trialFrequency: formValue.trialFrequency !== 'N'
        ? (formValue.trialFrequency as CreateRoleRequest['trialFrequency'])
        : undefined,

      rsvpCode: formValue.rsvpCode || undefined,
      iconFile: formValue.iconFile || undefined
    };

    // MIGRATION: Create or update based on mode (lines 251-262)
    // VB.NET: If RoleID = -1 Then objRoleController.AddRole(objRoleInfo) Else objRoleController.UpdateRole(objRoleInfo)
    const currentRole = this.role();
    const operation = this.isEditMode() && currentRole
      ? this.roleService.updateRole(currentRole.roleId, {
          ...request,
          roleId: currentRole.roleId
        } as UpdateRoleRequest)
      : this.roleService.createRole(request);

    operation.subscribe({
      next: () => {
        // MIGRATION: Response.Redirect(NavigateURL())
        this.router.navigate(['/roles']);
      },
      error: (err) => {
        // MIGRATION: Handle duplicate role error (line 256)
        // VB.NET: DotNetNuke.UI.Skins.Skin.AddModuleMessage(Me, Localization.GetString("DuplicateRole", Me.LocalResourceFile), ModuleMessageType.RedError)
        if (err.status === 409) {
          this.errorMessage.set('A role with this name already exists.');
        } else if (err.status === 400) {
          this.errorMessage.set('Please check the form for errors.');
        } else if (err.status === 403) {
          this.errorMessage.set('You do not have permission to perform this action.');
        } else {
          this.errorMessage.set('An error occurred while saving the role. Please try again.');
        }
        this.loading.set(false);
      }
    });
  }

  /**
   * Show delete confirmation dialog.
   *
   * MIGRATION: Replaces ClientAPI.AddButtonConfirm(cmdDelete, ...) pattern from line 112:
   * VB.NET: ClientAPI.AddButtonConfirm(cmdDelete, Services.Localization.Localization.GetString("DeleteItem"))
   *
   * Opens the ConfirmationDialogComponent for user confirmation before deletion.
   */
  showDeleteConfirmation(): void {
    if (!this.isEditMode() || this.isSystemRole()) {
      return;
    }

    if (this.deleteConfirmDialog) {
      this.deleteConfirmDialog.open();
    }
  }

  /**
   * Handle role deletion after confirmation.
   *
   * MIGRATION: Derived from cmdDelete_Click (lines 287-303):
   * ```vb
   * Private Sub cmdDelete_Click(ByVal sender As Object, ByVal e As System.EventArgs)
   *     Dim objUser As New RoleController
   *     objUser.DeleteRole(RoleID, PortalSettings.PortalId)
   *     ...
   *     Response.Redirect(NavigateURL())
   * End Sub
   * ```
   *
   * Called from the ConfirmationDialogComponent's confirmed event.
   */
  onDelete(): void {
    // Validate state before deletion
    if (!this.isEditMode() || this.isSystemRole()) {
      return;
    }

    const currentRole = this.role();
    if (!currentRole) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    // MIGRATION: objUser.DeleteRole(RoleID, PortalSettings.PortalId)
    this.roleService.deleteRole(currentRole.roleId).subscribe({
      next: () => {
        // MIGRATION: Response.Redirect(NavigateURL())
        this.router.navigate(['/roles']);
      },
      error: (err) => {
        // Handle deletion errors
        if (err.status === 400) {
          this.errorMessage.set('Cannot delete this role. It may have users assigned.');
        } else if (err.status === 403) {
          this.errorMessage.set('You do not have permission to delete this role.');
        } else if (err.status === 404) {
          this.errorMessage.set('Role not found. It may have already been deleted.');
          // Redirect to list since role doesn't exist
          setTimeout(() => this.router.navigate(['/roles']), 2000);
        } else {
          this.errorMessage.set('An error occurred while deleting the role. Please try again.');
        }
        this.loading.set(false);
      }
    });
  }

  /**
   * Handle cancel action - navigate back to role list.
   *
   * MIGRATION: Derived from cmdCancel_Click (lines 316-323):
   * ```vb
   * Private Sub cmdCancel_Click(ByVal sender As Object, ByVal e As System.EventArgs)
   *     Response.Redirect(NavigateURL())
   * End Sub
   * ```
   */
  onCancel(): void {
    // MIGRATION: Response.Redirect(NavigateURL())
    this.router.navigate(['/roles']);
  }

  /**
   * Navigate to user management for this role.
   *
   * MIGRATION: Derived from cmdManage_Click (lines 336-342):
   * ```vb
   * Private Sub cmdManage_Click(ByVal sender As System.Object, ByVal e As System.EventArgs)
   *     Response.Redirect(NavigateURL(Me.TabId, "User Roles", "RoleId=" & RoleID))
   * End Sub
   * ```
   *
   * Note: Disabled for Registered Users role (lines 180-182).
   */
  onManageUsers(): void {
    // Only available in edit mode and not for Registered Users role
    if (this.isEditMode() && !this.isRegisteredRole()) {
      const currentRole = this.role();
      if (currentRole) {
        // MIGRATION: NavigateURL(Me.TabId, "User Roles", "RoleId=" & RoleID)
        this.router.navigate(['/roles', currentRole.roleId, 'users']);
      }
    }
  }

  /**
   * Copy the RSVP link to clipboard.
   *
   * Enhancement for better UX - not in original VB.NET code.
   * Provides easy way to share the RSVP link with users.
   */
  copyRsvpLink(): void {
    const link = this.rsvpLink();
    if (link) {
      navigator.clipboard.writeText(link).then(
        () => {
          // Success - could show a toast notification here
        },
        (err) => {
          // Fallback for browsers that don't support clipboard API
          console.error('Failed to copy RSVP link:', err);
        }
      );
    }
  }

  /**
   * Handle icon image load error.
   *
   * Enhancement for better UX - shows fallback when icon path is invalid.
   * Hides broken image indicator.
   *
   * @param event - The error event from the img element
   */
  onIconError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (img) {
      img.style.display = 'none';
    }
  }
}
