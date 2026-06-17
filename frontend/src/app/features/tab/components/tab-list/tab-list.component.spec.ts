import { signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { TabListComponent } from './tab-list.component';
import { TabService } from '../../services';
import { type Tab, TabType } from '../../models';
import { AuthService } from '../../../../core/auth/auth.service';
import type { User } from '../../../../core/models/user.model';
import type { ProblemDetails } from '../../../../core/services/api.service';

// Typed Tab factory mirroring the on-disk wire contract in `../../models` (tab.model.ts). All 21
// fields are supplied so the literal satisfies the `Tab` interface with no excess/missing members;
// the acronym-prefixed ids are `tabID`/`portalID`, `parentId` mirrors the C# `ParentId`, and `tabType`
// is the NUMERIC enum ordinal the API emits.
function makeTab(overrides: Partial<Tab> = {}): Tab {
  return {
    tabID: 1,
    tabOrder: 1,
    portalID: 0,
    tabName: 'Home',
    isVisible: true,
    parentId: null,
    level: 0,
    iconFile: null,
    title: 'Home',
    description: null,
    keyWords: null,
    url: null,
    skinSrc: null,
    containerSrc: null,
    tabPath: '//Home',
    startDate: null,
    endDate: null,
    hasChildren: false,
    refreshInterval: null,
    isSecure: false,
    tabType: TabType.Normal,
    ...overrides,
  };
}

// Fully-populated `User` matching core/models/user.model.ts. The component reads
// `currentUser()?.portalID` when loading tabs, so `portalID` (7) is the value asserted against getTabs.
const mockUser: User = {
  userID: 1,
  portalID: 7,
  affiliateID: null,
  username: 'admin',
  displayName: 'Administrator',
  email: 'admin@example.com',
  firstName: 'Super',
  lastName: 'User',
  fullName: 'Super User',
  isSuperUser: true,
  approved: true,
  updatePassword: false,
  roles: ['Administrators'],
  createdDate: null,
  lastLoginDate: null,
  lastPasswordChangeDate: null,
  lastActivityDate: null,
};

describe('TabListComponent', () => {
  let component: TabListComponent;
  let fixture: ComponentFixture<TabListComponent>;
  let tabService: jasmine.SpyObj<TabService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    tabService = jasmine.createSpyObj<TabService>('TabService', ['getTabs', 'deleteTab']);
    tabService.getTabs.and.returnValue(of([makeTab()]));
    tabService.deleteTab.and.returnValue(of(void 0));

    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.returnValue(Promise.resolve(true));

    const authStub = {
      currentUser: signal<User | null>(mockUser),
      hasRole: (_role: string): boolean => true,
    };

    await TestBed.configureTestingModule({
      imports: [TabListComponent],
      providers: [
        { provide: TabService, useValue: tabService },
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: authStub as unknown as AuthService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TabListComponent);
    component = fixture.componentInstance;
  });

  it('should create and render the data table', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    const table = fixture.nativeElement.querySelector('app-data-table');
    expect(table).toBeTruthy();
  });

  it('loads tabs for the current portal on init', () => {
    component.ngOnInit();
    expect(tabService.getTabs).toHaveBeenCalledWith(mockUser.portalID);
    expect(component.tabs().length).toBe(1);
  });

  it('filters rows by visibility (all, visible, hidden)', () => {
    const tabs = [
      makeTab({ tabID: 1, isVisible: true }),
      makeTab({ tabID: 2, isVisible: false }),
      makeTab({ tabID: 3, isVisible: true }),
    ];
    tabService.getTabs.and.returnValue(of(tabs));
    component.loadTabs();

    component.onFilterChange('All Tabs');
    expect(component.displayedRows().length).toBe(3);

    component.onFilterChange('Visible');
    expect(component.displayedRows().map((tab) => tab.tabID)).toEqual([1, 3]);

    component.onFilterChange('Hidden');
    expect(component.displayedRows().map((tab) => tab.tabID)).toEqual([2]);
  });

  it('maps the numeric TabType ordinal to a display label', () => {
    expect(component.tabTypeLabel(TabType.Normal)).toBe('Normal');
    expect(component.tabTypeLabel(TabType.Url)).toBe('URL');
    expect(component.tabTypeLabel(TabType.Member)).toBe('Member');
  });

  it('navigates for edit, settings and add actions', () => {
    const row = makeTab({ tabID: 9 });

    component.onActionClick({ action: { id: 'edit', label: 'Edit', permission: 'EDIT' }, row });
    expect(router.navigate).toHaveBeenCalledWith(['/tabs', 9, 'edit']);

    component.onActionClick({ action: { id: 'settings', label: 'Settings', permission: 'EDIT' }, row });
    expect(router.navigate).toHaveBeenCalledWith(['/tabs', 9, 'settings']);

    component.onAddTab();
    expect(router.navigate).toHaveBeenCalledWith(['/tabs/new']);
  });

  it('opens the confirmation dialog then deletes (portal-scoped) and refreshes', () => {
    const row = makeTab({ tabID: 4 });

    component.onActionClick({ action: { id: 'delete', label: 'Delete', permission: 'DELETE' }, row });
    expect(component.deleteDialogOpen()).toBe(true);
    expect(component.tabToDelete()?.tabID).toBe(4);

    tabService.getTabs.calls.reset();
    component.onConfirmDelete();

    expect(tabService.deleteTab).toHaveBeenCalledWith(4, mockUser.portalID);
    expect(tabService.getTabs).toHaveBeenCalledWith(mockUser.portalID);
    expect(component.deleteDialogOpen()).toBe(false);
  });

  it('surfaces RFC 7807 errors via the error signal', () => {
    const problem: ProblemDetails = { title: 'Server Error', status: 500, detail: 'Boom' };
    tabService.getTabs.and.returnValue(throwError(() => problem));

    component.loadTabs();

    expect(component.error()).toBe('Boom');
    expect(component.tabs().length).toBe(0);
  });
});
