import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiService } from '../../../core/services/api.service';
import type { CreateTab, Tab, UpdateTab } from '../models';
import { TabType } from '../models';
import { TabService } from './tab.service';

describe('TabService', () => {
  let service: TabService;
  let api: jasmine.SpyObj<ApiService>;

  // Fixture conforms EXACTLY to the on-disk `Tab` contract (features/tab/models/tab.model.ts). All 21
  // fields are supplied so the literal satisfies the interface with no excess/missing members. The
  // acronym-prefixed ids are `tabID` / `portalID` (System.Text.Json camelCase lowercases only the
  // leading run), `parentId` mirrors the C# `ParentId`, and `tabType` is the NUMERIC enum ordinal the
  // API emits (no string-enum converter is registered backend-side).
  const mockTab: Tab = {
    tabID: 7,
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
  };

  beforeEach(() => {
    const spy = jasmine.createSpyObj<ApiService>('ApiService', [
      'resourceUrl',
      'get',
      'getList',
      'post',
      'put',
      'delete',
    ]);
    spy.resourceUrl.and.callFake((entity: string, id?: string | number) =>
      id === undefined ? `/api/v1/${entity}` : `/api/v1/${entity}/${id}`,
    );

    TestBed.configureTestingModule({
      providers: [TabService, { provide: ApiService, useValue: spy }],
    });

    service = TestBed.inject(TabService);
    api = TestBed.inject(ApiService) as jasmine.SpyObj<ApiService>;
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getTabs', () => {
    it('calls getList with the portalId query param and unwraps .data', () => {
      api.getList.and.returnValue(of({ data: [mockTab], meta: {} }));

      let result: Tab[] | undefined;
      service.getTabs(42).subscribe((tabs) => (result = tabs));

      expect(api.getList).toHaveBeenCalledWith('/api/v1/tabs', { portalId: 42 });
      expect(result).toEqual([mockTab]);
    });
  });

  describe('getTabsByParent', () => {
    it('calls getList with both portalId and parentId and unwraps .data', () => {
      api.getList.and.returnValue(of({ data: [mockTab], meta: {} }));

      let result: Tab[] | undefined;
      service.getTabsByParent(3, 42).subscribe((tabs) => (result = tabs));

      expect(api.getList).toHaveBeenCalledWith('/api/v1/tabs', { portalId: 42, parentId: 3 });
      expect(result).toEqual([mockTab]);
    });
  });

  describe('getTabCount', () => {
    it('GETs /api/v1/tabs/count with the portalId query param', () => {
      api.get.and.returnValue(of(5));

      let result: number | undefined;
      service.getTabCount(42).subscribe((count) => (result = count));

      expect(api.get).toHaveBeenCalledWith('/api/v1/tabs/count', { portalId: 42 });
      expect(result).toBe(5);
    });
  });

  describe('getTab', () => {
    it('GETs /api/v1/tabs/{id} with the portalId query param', () => {
      api.get.and.returnValue(of(mockTab));

      let result: Tab | undefined;
      service.getTab(7, 0).subscribe((tab) => (result = tab));

      expect(api.get).toHaveBeenCalledWith('/api/v1/tabs/7', { portalId: 0 });
      expect(result).toEqual(mockTab);
    });
  });

  describe('createTab', () => {
    it('POSTs the dto to /api/v1/tabs (portalID travels in the body)', () => {
      const dto: CreateTab = {
        portalID: 0,
        tabName: 'New Page',
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
      };
      api.post.and.returnValue(of(mockTab));

      let result: Tab | undefined;
      service.createTab(dto).subscribe((tab) => (result = tab));

      expect(api.post).toHaveBeenCalledWith('/api/v1/tabs', dto);
      expect(result).toEqual(mockTab);
    });
  });

  describe('updateTab', () => {
    it('PUTs the dto to /api/v1/tabs/{id} (caller sets tabID === id)', () => {
      const dto: UpdateTab = {
        tabID: 7,
        portalID: 0,
        tabName: 'Home',
        parentId: null,
        title: 'Home',
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
        tabOrder: 1,
      };
      api.put.and.returnValue(of(mockTab));

      let result: Tab | undefined;
      service.updateTab(7, dto).subscribe((tab) => (result = tab));

      expect(api.put).toHaveBeenCalledWith('/api/v1/tabs/7', dto);
      expect(dto.tabID).toBe(7);
      expect(result).toEqual(mockTab);
    });
  });

  describe('deleteTab', () => {
    it('DELETEs /api/v1/tabs/{id} with the portalId query param and completes (void)', () => {
      api.delete.and.returnValue(of(undefined));

      let completed = false;
      service.deleteTab(7, 0).subscribe({ complete: () => (completed = true) });

      expect(api.delete).toHaveBeenCalledWith('/api/v1/tabs/7', { portalId: 0 });
      expect(completed).toBe(true);
    });
  });
});
