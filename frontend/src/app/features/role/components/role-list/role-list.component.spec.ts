/**
 * MIGRATION: RoleListComponent Unit Tests
 *
 * Angular 19 unit test file for RoleListComponent using Jasmine and TestBed.
 * Tests component creation, role loading, role group filtering, navigation
 * to edit role and user assignments, and delete role group functionality.
 *
 * MIGRATION SOURCE: Website/admin/Security/Roles.ascx.vb
 *
 * Test cases derived from:
 * - BindData() lines 66-93 → loadRoles() test
 * - BindGroups() lines 105-135 → role group dropdown test
 * - FormatPeriod() lines 152-162 → period formatting test
 * - FormatPrice() lines 175-185 → price formatting test
 * - cboRoleGroups_SelectedIndexChanged lines 273-278 → filter test
 * - cmdDelete_Click lines 290-299 → delete group test
 */

import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router, ActivatedRoute, provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';

// Internal imports from depends_on_files
import { RoleListComponent } from './role-list.component';
import { RoleService, PagedResult } from '../../services/role.service';
import { Role, RoleGroup } from '../../models/role.model';

/**
 * Mock test data for roles.
 * MIGRATION: Replaces VB.NET ArrayList arrRoles populated from RoleController.GetPortalRoles()
 */
const mockRoles: Role[] = [
  {
    roleId: 1,
    portalId: 0,
    roleGroupId: 1,
    roleName: 'Administrators',
    description: 'Portal administrators with full access',
    serviceFee: 0,
    billingFrequency: 'N',
    billingPeriod: 0,
    trialFee: 0,
    trialFrequency: 'N',
    trialPeriod: 0,
    isPublic: false,
    autoAssignment: false,
    rsvpCode: null,
    iconFile: null
  },
  {
    roleId: 2,
    portalId: 0,
    roleGroupId: 1,
    roleName: 'Registered Users',
    description: 'All registered users',
    serviceFee: 10.50,
    billingFrequency: 'M',
    billingPeriod: 12,
    trialFee: 0,
    trialFrequency: 'N',
    trialPeriod: 0,
    isPublic: true,
    autoAssignment: true,
    rsvpCode: null,
    iconFile: null
  },
  {
    roleId: 3,
    portalId: 0,
    roleGroupId: 2,
    roleName: 'Premium Members',
    description: 'Premium subscription members',
    serviceFee: 29.99,
    billingFrequency: 'M',
    billingPeriod: 1,
    trialFee: 9.99,
    trialFrequency: 'D',
    trialPeriod: 7,
    isPublic: true,
    autoAssignment: false,
    rsvpCode: 'PREMIUM2024',
    iconFile: '/images/premium.png'
  }
];

/**
 * Mock test data for role groups.
 * MIGRATION: Replaces VB.NET ArrayList arrGroups from RoleController.GetRoleGroups()
 */
const mockRoleGroups: RoleGroup[] = [
  {
    roleGroupId: 1,
    portalId: 0,
    roleGroupName: 'Security Roles',
    description: 'System security roles'
  },
  {
    roleGroupId: 2,
    portalId: 0,
    roleGroupName: 'Subscription Roles',
    description: 'Paid subscription roles'
  },
  {
    roleGroupId: 3,
    portalId: 0,
    roleGroupName: 'Empty Group',
    description: 'A group with no roles'
  }
];

/**
 * Mock paged result for getRoles response.
 * MIGRATION: Represents paginated API response replacing VB.NET ArrayList
 */
const mockPagedResult: PagedResult<Role> = {
  items: mockRoles,
  totalCount: mockRoles.length,
  pageIndex: 0,
  pageSize: 10,
  totalPages: 1
};

/**
 * Empty paged result for testing role group with no roles.
 */
const emptyPagedResult: PagedResult<Role> = {
  items: [],
  totalCount: 0,
  pageIndex: 0,
  pageSize: 10,
  totalPages: 0
};

describe('RoleListComponent', () => {
  let component: RoleListComponent;
  let fixture: ComponentFixture<RoleListComponent>;
  let mockRoleService: jasmine.SpyObj<RoleService>;
  let mockRouter: jasmine.SpyObj<Router>;
  let queryParamsSubject: Subject<{ [key: string]: string }>;

  beforeEach(async () => {
    // Create spy objects for services
    mockRoleService = jasmine.createSpyObj<RoleService>('RoleService', [
      'getRoles',
      'getRoleGroups',
      'deleteRoleGroup'
    ]);

    mockRouter = jasmine.createSpyObj<Router>('Router', ['navigate']);

    // Create a subject for query params to allow testing query parameter changes
    queryParamsSubject = new Subject<{ [key: string]: string }>();

    // Configure default mock return values
    mockRoleService.getRoles.and.returnValue(of(mockPagedResult));
    mockRoleService.getRoleGroups.and.returnValue(of(mockRoleGroups));
    mockRoleService.deleteRoleGroup.and.returnValue(of(undefined));

    await TestBed.configureTestingModule({
      imports: [RoleListComponent],
      providers: [
        provideRouter([]),
        { provide: RoleService, useValue: mockRoleService },
        { provide: Router, useValue: mockRouter },
        {
          provide: ActivatedRoute,
          useValue: {
            queryParams: queryParamsSubject.asObservable()
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RoleListComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    queryParamsSubject.complete();
  });

  // ============================================================================
  // COMPONENT CREATION TEST
  // ============================================================================

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  // ============================================================================
  // INITIALIZATION TESTS
  // MIGRATION: Tests ngOnInit derived from VB.NET Page_Load (lines 249-260)
  // ============================================================================

  it('should load roles on init', fakeAsync(() => {
    // MIGRATION: Tests BindData() (lines 66-93) called via Page_Load
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    expect(mockRoleService.getRoleGroups).toHaveBeenCalled();
    expect(mockRoleService.getRoles).toHaveBeenCalled();
    expect(component.roles()).toEqual(mockRoles);
    expect(component.loading()).toBeFalse();
  }));

  it('should load role groups on init', fakeAsync(() => {
    // MIGRATION: Tests BindGroups() (lines 105-135) called via Page_Load
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    expect(mockRoleService.getRoleGroups).toHaveBeenCalled();
    expect(component.roleGroups()).toEqual(mockRoleGroups);
  }));

  it('should read roleGroupId from query params on init', fakeAsync(() => {
    // MIGRATION: Tests Request.QueryString("RoleGroupID") check (lines 252-254)
    fixture.detectChanges();
    queryParamsSubject.next({ roleGroupId: '2' });
    tick();

    expect(component.selectedRoleGroupId()).toBe(2);
  }));

  // ============================================================================
  // ROLE GROUP FILTERING TESTS
  // MIGRATION: Tests cboRoleGroups_SelectedIndexChanged (lines 273-278)
  // ============================================================================

  it('should filter roles by selected role group', fakeAsync(() => {
    // MIGRATION: Derived from cboRoleGroups_SelectedIndexChanged
    // RoleGroupId = Int32.Parse(cboRoleGroups.SelectedValue)
    // BindData()
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    // Reset spy call count
    mockRoleService.getRoles.calls.reset();

    // Create a mock change event
    const mockEvent = {
      target: { value: '2' } as HTMLSelectElement
    } as unknown as Event;

    component.onRoleGroupChange(mockEvent);
    tick();

    expect(component.selectedRoleGroupId()).toBe(2);
    expect(mockRoleService.getRoles).toHaveBeenCalledWith(undefined, 2);
  }));

  it('should get all roles when role group is -2 (All Roles)', fakeAsync(() => {
    // MIGRATION: If RoleGroupId < -1 Then arrRoles = objRoles.GetPortalRoles(PortalId) (line 73)
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    // Reset spy call count
    mockRoleService.getRoles.calls.reset();

    const mockEvent = {
      target: { value: '-2' } as HTMLSelectElement
    } as unknown as Event;

    component.onRoleGroupChange(mockEvent);
    tick();

    expect(component.selectedRoleGroupId()).toBe(-2);
    // When roleGroupId is -2, undefined should be passed to get all roles
    expect(mockRoleService.getRoles).toHaveBeenCalledWith(undefined, undefined);
  }));

  // ============================================================================
  // NAVIGATION TESTS
  // MIGRATION: Tests ImageCommandColumn handlers (lines 202-235)
  // ============================================================================

  it('should navigate to edit role on row click', fakeAsync(() => {
    // MIGRATION: Derived from ImageCommandColumn Edit (lines 216-218)
    // imageColumn.NavigateURLFormatString = EditUrl("RoleID", "KEYFIELD", "Edit")
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    const testRole = mockRoles[0];
    component.onRoleClick(testRole);

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles', testRole.roleId, 'edit']);
  }));

  it('should navigate to user roles management', fakeAsync(() => {
    // MIGRATION: Derived from ImageCommandColumn UserRoles (lines 221-227)
    // formatString = NavigateURL(TabId, "User Roles", "RoleId=KEYFIELD")
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    const testRole = mockRoles[1];
    component.onManageUsers(testRole);

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles', testRole.roleId, 'users']);
  }));

  it('should navigate to edit role group', fakeAsync(() => {
    // MIGRATION: Derived from lnkEditGroup.NavigateUrl (line 84)
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    component.selectedRoleGroupId.set(2);
    component.onEditRoleGroup();

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles/groups', 2, 'edit']);
  }));

  it('should navigate to add new role', fakeAsync(() => {
    // MIGRATION: Derived from ModuleActions AddContent (line 307)
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    component.onAddRole();

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles/new']);
  }));

  it('should navigate to add new role group', fakeAsync(() => {
    // MIGRATION: Derived from ModuleActions AddGroup.Action (line 308)
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    component.onAddRoleGroup();

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/roles/groups/new']);
  }));

  // ============================================================================
  // DELETE ROLE GROUP TESTS
  // MIGRATION: Tests cmdDelete_Click (lines 290-299)
  // ============================================================================

  it('should call deleteRoleGroup when confirmed', fakeAsync(() => {
    // MIGRATION: Derived from cmdDelete_Click (lines 290-299)
    // RoleController.DeleteRoleGroup(PortalId, RoleGroupId)
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    // Set a valid role group ID
    component.selectedRoleGroupId.set(3);

    // Mock window.confirm to return true
    spyOn(window, 'confirm').and.returnValue(true);

    component.onDeleteRoleGroup();
    tick();

    expect(window.confirm).toHaveBeenCalledWith('Are you sure you want to delete this role group?');
    expect(mockRoleService.deleteRoleGroup).toHaveBeenCalledWith(3);
  }));

  it('should not delete role group when confirmation is cancelled', fakeAsync(() => {
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    component.selectedRoleGroupId.set(3);

    // Mock window.confirm to return false
    spyOn(window, 'confirm').and.returnValue(false);

    component.onDeleteRoleGroup();
    tick();

    expect(window.confirm).toHaveBeenCalled();
    expect(mockRoleService.deleteRoleGroup).not.toHaveBeenCalled();
  }));

  it('should reset to Global Roles (-1) after successful delete', fakeAsync(() => {
    // MIGRATION: RoleGroupId = -1 after delete (line 295)
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    component.selectedRoleGroupId.set(3);
    spyOn(window, 'confirm').and.returnValue(true);

    component.onDeleteRoleGroup();
    tick();

    expect(component.selectedRoleGroupId()).toBe(-1);
    // Should reload role groups after delete
    expect(mockRoleService.getRoleGroups).toHaveBeenCalled();
  }));

  it('should not delete when role group ID is 0 or negative', fakeAsync(() => {
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    component.selectedRoleGroupId.set(-1);
    spyOn(window, 'confirm');

    component.onDeleteRoleGroup();
    tick();

    // Should not even show confirmation for system role groups
    expect(window.confirm).not.toHaveBeenCalled();
    expect(mockRoleService.deleteRoleGroup).not.toHaveBeenCalled();
  }));

  // ============================================================================
  // DELETE BUTTON VISIBILITY TESTS
  // MIGRATION: Tests cmdDelete.Visible = Not (arrRoles.Count > 0) (line 85)
  // ============================================================================

  it('should hide delete button when role group has roles', fakeAsync(() => {
    // MIGRATION: cmdDelete.Visible = Not (arrRoles.Count > 0) (line 85)
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    // Set a role group with roles
    component.selectedRoleGroupId.set(1);
    component.roles.set(mockRoles);

    // canDeleteGroup computed signal should be false when roles exist
    expect(component.canDeleteGroup()).toBeFalse();
  }));

  it('should show delete button when role group is empty', fakeAsync(() => {
    // MIGRATION: cmdDelete.Visible = Not (arrRoles.Count > 0) (line 85)
    // When no roles, delete should be visible
    mockRoleService.getRoles.and.returnValue(of(emptyPagedResult));

    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    // Set to a role group with no roles
    component.selectedRoleGroupId.set(3);
    component.roles.set([]);

    // canDeleteGroup should be true when role group is > 0 and no roles
    expect(component.canDeleteGroup()).toBeTrue();
  }));

  // ============================================================================
  // FORMAT PERIOD TESTS
  // MIGRATION: Tests FormatPeriod() (lines 152-162)
  // ============================================================================

  it('should format period as empty string when null', () => {
    // MIGRATION: If period <> Null.NullInteger Then _FormatPeriod = period.ToString (line 156)
    // Null.NullInteger check returns empty string
    expect(component.formatPeriod(null)).toBe('');
  });

  it('should format period as empty string when undefined', () => {
    // MIGRATION: Additional edge case for undefined values
    expect(component.formatPeriod(undefined)).toBe('');
  });

  it('should format period as string when valid number', () => {
    // MIGRATION: _FormatPeriod = period.ToString (line 156)
    expect(component.formatPeriod(12)).toBe('12');
    expect(component.formatPeriod(0)).toBe('0');
    expect(component.formatPeriod(365)).toBe('365');
  });

  // ============================================================================
  // FORMAT PRICE TESTS
  // MIGRATION: Tests FormatPrice() (lines 175-185)
  // ============================================================================

  it('should format price with two decimal places', () => {
    // MIGRATION: _FormatPrice = price.ToString("##0.00") (line 179)
    expect(component.formatPrice(10)).toBe('10.00');
    expect(component.formatPrice(10.5)).toBe('10.50');
    expect(component.formatPrice(29.99)).toBe('29.99');
    expect(component.formatPrice(0)).toBe('0.00');
    expect(component.formatPrice(1234.5678)).toBe('1234.57');
  });

  it('should format price as empty string when null', () => {
    // MIGRATION: If price <> Null.NullSingle Then (line 178)
    // Null.NullSingle check returns empty string
    expect(component.formatPrice(null)).toBe('');
  });

  it('should format price as empty string when undefined', () => {
    // MIGRATION: Additional edge case for undefined values
    expect(component.formatPrice(undefined)).toBe('');
  });

  // ============================================================================
  // ROLE GROUP DROPDOWN TESTS
  // MIGRATION: Tests BindGroups() dropdown population (lines 105-135)
  // ============================================================================

  it('should show All Roles (-2) and Global Roles (-1) in dropdown', fakeAsync(() => {
    // MIGRATION: cboRoleGroups.Items.Add(New ListItem(Localization.GetString("AllRoles"), "-2")) (line 112)
    // liItem = New ListItem(Localization.GetString("GlobalRoles"), "-1") (line 114)
    // Note: The special items are added in the template, not in roleGroups signal
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    // The roleGroups signal contains only the custom groups from API
    // -2 (All Roles) and -1 (Global Roles) are template constants
    expect(component.roleGroups()).toEqual(mockRoleGroups);

    // Verify default selection is -2 (All Roles)
    expect(component.selectedRoleGroupId()).toBe(-2);
  }));

  it('should show role groups section when groups exist', fakeAsync(() => {
    // MIGRATION: trGroups.Visible = arrGroups.Count > 0 (line 127)
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    // roleGroupsVisible should be true when groups exist
    expect(component.roleGroups().length).toBeGreaterThan(0);
  }));

  it('should hide role groups section when no groups exist', fakeAsync(() => {
    // MIGRATION: Else RoleGroupId = -2; trGroups.Visible = False (lines 129-130)
    mockRoleService.getRoleGroups.and.returnValue(of([]));

    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    expect(component.roleGroups()).toEqual([]);
  }));

  // ============================================================================
  // LOADING STATE TESTS
  // ============================================================================

  it('should set loading to true while fetching roles', fakeAsync(() => {
    // Don't detect changes yet to check initial state
    expect(component.loading()).toBeTrue();

    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    // After loading completes, should be false
    expect(component.loading()).toBeFalse();
  }));

  it('should set loading to false after roles are loaded', fakeAsync(() => {
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    expect(component.loading()).toBeFalse();
    expect(component.roles().length).toBe(mockRoles.length);
  }));

  // ============================================================================
  // ERROR HANDLING TESTS
  // ============================================================================

  it('should handle role loading error gracefully', fakeAsync(() => {
    // Configure getRoles to throw an error
    mockRoleService.getRoles.and.returnValue(
      new Subject<PagedResult<Role>>().asObservable()
    );

    fixture.detectChanges();
    queryParamsSubject.next({});

    // Simulate error by not emitting any value
    const rolesSubject = new Subject<PagedResult<Role>>();
    mockRoleService.getRoles.and.returnValue(rolesSubject.asObservable());

    component.loadRoles();
    rolesSubject.error(new Error('Network error'));
    tick();

    // Should set empty roles and loading false on error
    expect(component.roles()).toEqual([]);
    expect(component.loading()).toBeFalse();
  }));

  it('should handle role group loading error gracefully', fakeAsync(() => {
    const groupsSubject = new Subject<RoleGroup[]>();
    mockRoleService.getRoleGroups.and.returnValue(groupsSubject.asObservable());

    fixture.detectChanges();
    queryParamsSubject.next({});

    groupsSubject.error(new Error('Network error'));
    tick();

    // Should still attempt to load roles after group loading fails
    expect(mockRoleService.getRoles).toHaveBeenCalled();
  }));

  it('should handle delete role group error gracefully', fakeAsync(() => {
    const deleteSubject = new Subject<void>();
    mockRoleService.deleteRoleGroup.and.returnValue(deleteSubject.asObservable());

    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    component.selectedRoleGroupId.set(3);
    spyOn(window, 'confirm').and.returnValue(true);
    spyOn(window, 'alert');

    component.onDeleteRoleGroup();
    deleteSubject.error(new Error('Delete failed'));
    tick();

    expect(window.alert).toHaveBeenCalledWith('Failed to delete role group. Please try again.');
  }));

  // ============================================================================
  // USER SETTINGS NAVIGATION TEST
  // ============================================================================

  it('should navigate to user settings', fakeAsync(() => {
    // MIGRATION: Derived from ModuleActions UserSettings.Action (line 309)
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    component.onUserSettings();

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/settings/users']);
  }));

  // ============================================================================
  // EDIT ROLE GROUP EDGE CASES
  // ============================================================================

  it('should not navigate to edit role group when ID is negative', fakeAsync(() => {
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    component.selectedRoleGroupId.set(-1);
    component.onEditRoleGroup();

    expect(mockRouter.navigate).not.toHaveBeenCalled();
  }));

  it('should not navigate to edit role group when ID is 0', fakeAsync(() => {
    fixture.detectChanges();
    queryParamsSubject.next({});
    tick();

    component.selectedRoleGroupId.set(0);
    component.onEditRoleGroup();

    expect(mockRouter.navigate).not.toHaveBeenCalled();
  }));
});
