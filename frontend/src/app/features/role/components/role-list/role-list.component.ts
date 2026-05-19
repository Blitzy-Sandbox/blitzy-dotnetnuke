/**
 * MIGRATION: RoleListComponent
 * 
 * Angular 19 standalone component for role list display and management.
 * Displays a data grid of portal roles with filtering by role group,
 * formatted columns for period and price, and navigation links to edit
 * roles and manage user assignments.
 * 
 * MIGRATION SOURCE: Website/admin/Security/Roles.ascx.vb
 * 
 * Key transformations:
 * - VB.NET Page_Load → ngOnInit lifecycle hook
 * - VB.NET grdRoles DataGrid → app-data-table component
 * - VB.NET cboRoleGroups DropDownList → native select with signals
 * - VB.NET cmdDelete ImageButton → button with confirmation dialog
 * - VB.NET postback model → SPA navigation with Router
 * - VB.NET private members → Angular signals for reactive state
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
import { RoleService, PagedResult } from '../../services/role.service';
import { Role, RoleGroup } from '../../models/role.model';
import {
  DataTableComponent,
  ColumnDefinition
} from '../../../../shared/components/data-table/data-table.component';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * RoleListComponent
 * 
 * Displays a list of security roles with filtering by role group,
 * action buttons for role management, and navigation to role editing
 * and user assignment screens.
 * 
 * Uses Angular 19 patterns:
 * - Standalone component with OnPush change detection
 * - Signals for reactive state management
 * - inject() function for dependency injection
 */
@Component({
  selector: 'app-role-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    DataTableComponent,
    LoadingSpinnerComponent
  ],
  templateUrl: './role-list.component.html',
  styleUrl: './role-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RoleListComponent implements OnInit {
  // ============================================================================
  // DEPENDENCY INJECTION (using inject() function per Angular 19)
  // ============================================================================

  private readonly roleService = inject(RoleService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // ============================================================================
  // SIGNAL-BASED STATE (derived from Roles.ascx.vb private members line 48)
  // ============================================================================

  /**
   * Role list data.
   * MIGRATION: Replaces VB.NET ArrayList arrRoles (line 48).
   */
  readonly roles = signal<Role[]>([]);

  /**
   * Role groups for filter dropdown.
   * MIGRATION: Replaces VB.NET ArrayList arrGroups (line 49).
   */
  readonly roleGroups = signal<RoleGroup[]>([]);

  /**
   * Currently selected role group ID.
   * MIGRATION: Replaces VB.NET RoleGroupId property (line 252).
   * Default -2 for "All Roles" filter.
   */
  readonly selectedRoleGroupId = signal<number>(-2);

  /**
   * Loading state indicator.
   */
  readonly loading = signal<boolean>(true);

  /**
   * Whether role groups filter section is visible.
   * MIGRATION: Replaces VB.NET trGroups.Visible (line 127).
   */
  readonly roleGroupsVisible = signal<boolean>(false);

  // ============================================================================
  // COMPUTED SIGNALS
  // ============================================================================

  /**
   * Whether the selected role group can be deleted.
   * MIGRATION: Based on cmdDelete.Visible = Not (arrRoles.Count > 0) (line 85).
   * Only allow deletion when role group has no roles.
   */
  readonly canDeleteGroup = computed(() => {
    return this.selectedRoleGroupId() > 0 && this.roles().length === 0;
  });

  // ============================================================================
  // TABLE COLUMNS DEFINITION
  // ============================================================================

  /**
   * Column definitions for the data table.
   * MIGRATION: Derived from grdRoles DataGrid columns in Roles.ascx.
   */
  readonly columns: ColumnDefinition[] = [
    {
      field: 'roleName',
      header: 'Role Name',
      sortable: true,
      width: '150px'
    },
    {
      field: 'description',
      header: 'Description',
      sortable: true,
      width: '200px'
    },
    {
      field: 'serviceFee',
      header: 'Fee',
      sortable: true,
      width: '100px',
      formatter: (value: unknown) => this.formatPrice(value as number | null)
    },
    {
      field: 'billingPeriod',
      header: 'Period',
      sortable: true,
      width: '80px',
      formatter: (value: unknown) => this.formatPeriod(value as number | null)
    },
    {
      field: 'isPublic',
      header: 'Public',
      sortable: true,
      width: '80px',
      formatter: (value: unknown) => (value ? 'Yes' : 'No')
    },
    {
      field: 'autoAssignment',
      header: 'Auto Assign',
      sortable: true,
      width: '100px',
      formatter: (value: unknown) => (value ? 'Yes' : 'No')
    }
  ];

  // ============================================================================
  // LIFECYCLE
  // ============================================================================

  /**
   * Initialize component.
   * MIGRATION: Replaces VB.NET Page_Load event handler.
   */
  ngOnInit(): void {
    // MIGRATION: Read RoleGroupID from query params (lines 252-253)
    this.route.queryParams.subscribe(params => {
      const roleGroupId = params['roleGroupId'];
      if (roleGroupId !== undefined) {
        this.selectedRoleGroupId.set(parseInt(roleGroupId, 10));
      }
    });

    // Load role groups first, then roles
    this.loadRoleGroups();
  }

  // ============================================================================
  // DATA LOADING METHODS
  // ============================================================================

  /**
   * Load role groups for the filter dropdown.
   * MIGRATION: Derived from BindGroups() (lines 105-135).
   */
  loadRoleGroups(): void {
    this.roleService.getRoleGroups().subscribe({
      next: (groups) => {
        // Store groups (does not include "All Roles" and "Global Roles" which are
        // added in the template with values -2 and -1 respectively)
        this.roleGroups.set(groups);

        // MIGRATION: trGroups.Visible = arrGroups.Count > 0 (lines 127-131)
        this.roleGroupsVisible.set(groups.length > 0);

        // Load roles after groups are loaded
        this.loadRoles();
      },
      error: (error) => {
        console.error('Failed to load role groups:', error);
        this.roleGroupsVisible.set(false);
        this.loadRoles();
      }
    });
  }

  /**
   * Load roles based on selected role group filter.
   * MIGRATION: Derived from BindData() (lines 66-93).
   */
  loadRoles(): void {
    this.loading.set(true);

    const roleGroupId = this.selectedRoleGroupId();

    // MIGRATION: If roleGroupId < -1, get all portal roles (line 73)
    // Else get roles by group (line 75)
    this.roleService.getRoles(undefined, roleGroupId < -1 ? undefined : roleGroupId).subscribe({
      next: (result: PagedResult<Role>) => {
        this.roles.set(result.items);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Failed to load roles:', error);
        this.roles.set([]);
        this.loading.set(false);
      }
    });
  }

  // ============================================================================
  // EVENT HANDLERS
  // ============================================================================

  /**
   * Handle role group selection change.
   * MIGRATION: Replaces cboRoleGroups_SelectedIndexChanged (lines 273-278).
   */
  onRoleGroupChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const value = parseInt(select.value, 10);
    this.selectedRoleGroupId.set(value);
    this.loadRoles();
  }

  /**
   * Handle role row click - navigate to edit.
   * MIGRATION: Replaces ImageCommandColumn Edit (lines 216-218).
   */
  onRoleClick(role: Role): void {
    this.router.navigate(['/roles', role.roleId, 'edit']);
  }

  /**
   * Navigate to manage users in role.
   * MIGRATION: Replaces ImageCommandColumn UserRoles (lines 221-227).
   */
  onManageUsers(role: Role): void {
    this.router.navigate(['/roles', role.roleId, 'users']);
  }

  /**
   * Delete the selected role group.
   * MIGRATION: Replaces cmdDelete_Click (lines 290-299).
   */
  onDeleteRoleGroup(): void {
    const roleGroupId = this.selectedRoleGroupId();

    if (roleGroupId <= 0) {
      return;
    }

    // Confirm deletion
    if (!window.confirm('Are you sure you want to delete this role group?')) {
      return;
    }

    this.roleService.deleteRoleGroup(roleGroupId).subscribe({
      next: () => {
        // MIGRATION: Reset to Global Roles after deletion (line 295)
        this.selectedRoleGroupId.set(-1);
        this.loadRoleGroups();
      },
      error: (error) => {
        console.error('Failed to delete role group:', error);
        window.alert('Failed to delete role group. Please try again.');
      }
    });
  }

  /**
   * Navigate to edit role group form.
   */
  onEditRoleGroup(): void {
    const roleGroupId = this.selectedRoleGroupId();
    if (roleGroupId > 0) {
      this.router.navigate(['/roles/groups', roleGroupId, 'edit']);
    }
  }

  // ============================================================================
  // NAVIGATION METHODS
  // ============================================================================

  /**
   * Navigate to create new role.
   * MIGRATION: Replaces Actions.Add AddContent (line 307).
   */
  onAddRole(): void {
    this.router.navigate(['/roles/new']);
  }

  /**
   * Navigate to create new role group.
   * MIGRATION: Replaces Actions.Add AddGroup.Action (line 308).
   */
  onAddRoleGroup(): void {
    this.router.navigate(['/roles/groups/new']);
  }

  /**
   * Navigate to user settings.
   * MIGRATION: Replaces Actions.Add UserSettings.Action (line 309).
   */
  onUserSettings(): void {
    this.router.navigate(['/settings/users']);
  }

  // ============================================================================
  // HELPER METHODS
  // ============================================================================

  /**
   * Format period value for display.
   * MIGRATION: Replaces FormatPeriod() (lines 152-162).
   * Returns empty string if null/undefined (Null.NullInteger check).
   * 
   * @param period Period value
   * @returns Formatted string
   */
  formatPeriod(period: number | null | undefined): string {
    if (period === null || period === undefined) {
      return '';
    }
    return period.toString();
  }

  /**
   * Format price value for display.
   * MIGRATION: Replaces FormatPrice() (lines 175-185).
   * Returns empty string if null/undefined (Null.NullSingle check).
   * Uses '##0.00' format for currency display.
   * 
   * @param price Price value
   * @returns Formatted string with 2 decimal places
   */
  formatPrice(price: number | null | undefined): string {
    if (price === null || price === undefined) {
      return '';
    }
    return price.toFixed(2);
  }
}
