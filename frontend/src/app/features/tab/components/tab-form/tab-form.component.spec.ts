import { signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { TabFormComponent } from './tab-form.component';
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

describe('TabFormComponent', () => {
  let component: TabFormComponent;
  let fixture: ComponentFixture<TabFormComponent>;
  let tabService: jasmine.SpyObj<TabService>;
  let router: jasmine.SpyObj<Router>;

  function setup(idParam: string | null): void {
    tabService = jasmine.createSpyObj<TabService>('TabService', [
      'getTab',
      'createTab',
      'updateTab',
      'deleteTab',
    ]);
    tabService.getTab.and.returnValue(of(makeTab()));
    tabService.createTab.and.returnValue(of(makeTab()));
    tabService.updateTab.and.returnValue(of(makeTab()));
    tabService.deleteTab.and.returnValue(of(void 0));

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
      imports: [TabFormComponent],
      providers: [
        { provide: TabService, useValue: tabService },
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: authStub as unknown as AuthService },
        { provide: ActivatedRoute, useValue: routeStub },
      ],
    });

    fixture = TestBed.createComponent(TabFormComponent);
    component = fixture.componentInstance;
  }

  describe('create mode', () => {
    beforeEach(() => setup(null));

    it('creates in add mode (no id)', () => {
      fixture.detectChanges();
      expect(component).toBeTruthy();
      expect(component.isEditMode()).toBe(false);
      expect(component.heading()).toBe('Add Page');
    });

    it('does not submit when tabName is empty (required)', () => {
      fixture.detectChanges();
      component.onSubmit();
      expect(tabService.createTab).not.toHaveBeenCalled();
      expect(component.form.controls.tabName.touched).toBe(true);
    });

    it('POSTs a CreateTab built from the form (portalID from the current user)', () => {
      fixture.detectChanges();
      component.form.controls.tabName.setValue('About');
      component.onSubmit();

      expect(tabService.createTab).toHaveBeenCalledWith({
        portalID: 7,
        tabName: 'About',
        parentId: null,
        title: null,
        description: null,
        keyWords: null,
        isVisible: true,
        iconFile: null,
        url: null,
        skinSrc: null,
        containerSrc: null,
        startDate: null,
        endDate: null,
        refreshInterval: null,
        isSecure: false,
        tabOrder: 0,
      });
      expect(router.navigate).toHaveBeenCalledWith(['/tabs']);
    });
  });

  describe('edit mode', () => {
    beforeEach(() => setup('7'));

    it('loads the tab on init and patches the form', () => {
      fixture.detectChanges();
      expect(component.isEditMode()).toBe(true);
      expect(tabService.getTab).toHaveBeenCalledWith(7, 7);
      expect(component.form.controls.tabName.value).toBe('Home');
    });

    it('PUTs an UpdateTab carrying tabID === route id and portalID', () => {
      fixture.detectChanges();
      component.onSubmit();

      expect(tabService.updateTab).toHaveBeenCalled();
      const [id, dto] = tabService.updateTab.calls.mostRecent().args;
      expect(id).toBe(7);
      expect(dto.tabID).toBe(7);
      expect(dto.portalID).toBe(7);
      expect(dto.tabName).toBe('Home');
      expect(router.navigate).toHaveBeenCalledWith(['/tabs']);
    });

    it('deletes the tab (portal-scoped) after confirmation', () => {
      fixture.detectChanges();
      component.requestDelete();
      expect(component.showDeleteDialog()).toBe(true);
      component.confirmDelete();
      expect(tabService.deleteTab).toHaveBeenCalledWith(7, 7);
      expect(router.navigate).toHaveBeenCalledWith(['/tabs']);
    });

    it('surfaces RFC 7807 field errors on save failure', () => {
      fixture.detectChanges();
      const problem: ProblemDetails = {
        title: 'Validation failed',
        status: 400,
        errors: { tabName: ['Tab Name Is Required'] },
      };
      tabService.updateTab.and.returnValue(throwError(() => problem));
      component.onSubmit();
      expect(component.serverErrors()).toEqual({ tabName: ['Tab Name Is Required'] });
    });
  });
});
