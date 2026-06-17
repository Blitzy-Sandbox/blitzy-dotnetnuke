import { signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { TabSettingsComponent } from './tab-settings.component';
import { TabService } from '../../services';
import { type Tab, TabType } from '../../models';
import { AuthService } from '../../../../core/auth/auth.service';
import type { User } from '../../../../core/models/user.model';
import type { ProblemDetails } from '../../../../core/services/api.service';

function makeTab(overrides: Partial<Tab> = {}): Tab {
  return {
    tabID: 7,
    tabOrder: 2,
    portalID: 7,
    tabName: 'Home',
    isVisible: true,
    parentId: null,
    level: 0,
    iconFile: 'home.png',
    title: 'Home',
    description: 'Landing',
    keyWords: 'home',
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

describe('TabSettingsComponent', () => {
  let component: TabSettingsComponent;
  let fixture: ComponentFixture<TabSettingsComponent>;
  let tabService: jasmine.SpyObj<TabService>;
  let router: jasmine.SpyObj<Router>;

  function setup(idParam: string | null): void {
    tabService = jasmine.createSpyObj<TabService>('TabService', ['getTab', 'updateTab']);
    tabService.getTab.and.returnValue(of(makeTab()));
    tabService.updateTab.and.returnValue(of(makeTab({ isVisible: false })));

    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.returnValue(Promise.resolve(true));

    const authStub = {
      currentUser: signal<User | null>(mockUser),
      hasRole: (_role: string): boolean => true,
    };

    const routeStub = {
      snapshot: { paramMap: { get: (_key: string): string | null => idParam } },
    };

    TestBed.configureTestingModule({
      imports: [TabSettingsComponent],
      providers: [
        { provide: TabService, useValue: tabService },
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: authStub as unknown as AuthService },
        { provide: ActivatedRoute, useValue: routeStub },
      ],
    });

    fixture = TestBed.createComponent(TabSettingsComponent);
    component = fixture.componentInstance;
  }

  it('loads the tab on init and patches the settings subset', () => {
    setup('7');
    fixture.detectChanges();
    expect(tabService.getTab).toHaveBeenCalledWith(7, 7);
    expect(component.controls.iconFile.value).toBe('home.png');
    expect(component.pageName()).toBe('Home');
    expect(component.loading()).toBe(false);
  });

  it('reports an invalid id without calling the service', () => {
    setup(null);
    fixture.detectChanges();
    expect(component.loadError()).toBe('Invalid page id.');
    expect(tabService.getTab).not.toHaveBeenCalled();
  });

  it('PUTs an UpdateTab carrying content fields plus the edited settings', () => {
    setup('7');
    fixture.detectChanges();
    component.controls.isVisible.setValue(false);
    component.controls.skinSrc.setValue('custom.ascx');
    component.onSubmit();

    expect(tabService.updateTab).toHaveBeenCalled();
    const [id, dto] = tabService.updateTab.calls.mostRecent().args;
    expect(id).toBe(7);
    expect(dto.tabID).toBe(7);
    expect(dto.portalID).toBe(7);
    // content carried over unchanged
    expect(dto.tabName).toBe('Home');
    expect(dto.title).toBe('Home');
    // settings overrides applied
    expect(dto.isVisible).toBe(false);
    expect(dto.skinSrc).toBe('custom.ascx');
    expect(component.saved()).toBe(true);
  });

  it('surfaces RFC 7807 field errors on save failure', () => {
    setup('7');
    fixture.detectChanges();
    const problem: ProblemDetails = {
      title: 'Validation failed',
      status: 400,
      errors: { refreshInterval: ['Refresh Interval must be greater than or equal to zero.'] },
    };
    tabService.updateTab.and.returnValue(throwError(() => problem));
    component.onSubmit();
    expect(component.serverErrors()).toEqual({
      refreshInterval: ['Refresh Interval must be greater than or equal to zero.'],
    });
  });
});
