import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiService, PagedResponse } from '../../../core/services/api.service';
import { CreateModuleDto, Module, UpdateModuleDto, VisibilityState } from '../models';
import { ModuleService } from './module.service';

function makeModule(overrides: Partial<Module> = {}): Module {
  return {
    moduleID: 1,
    portalID: 0,
    tabID: 0,
    tabModuleID: 0,
    moduleDefID: 0,
    moduleOrder: 1,
    paneName: 'ContentPane',
    moduleTitle: 'Sample Module',
    cacheTime: 0,
    alignment: null,
    color: null,
    border: null,
    iconFile: null,
    allTabs: false,
    visibility: VisibilityState.Maximized,
    displayTitle: true,
    displayPrint: true,
    displaySyndicate: false,
    header: null,
    footer: null,
    startDate: null,
    endDate: null,
    containerSrc: null,
    inheritViewPermissions: false,
    desktopModuleID: 0,
    friendlyName: null,
    description: null,
    version: null,
    isDeleted: false,
    ...overrides,
  };
}

function makeCreateModuleDto(): CreateModuleDto {
  const { moduleID, tabModuleID, friendlyName, description, version, isDeleted, ...rest } =
    makeModule();
  return rest;
}

function makeUpdateModuleDto(id: number): UpdateModuleDto {
  const {
    portalID,
    tabID,
    tabModuleID,
    moduleDefID,
    desktopModuleID,
    friendlyName,
    description,
    version,
    isDeleted,
    ...rest
  } = makeModule({ moduleID: id });
  return rest;
}

describe('ModuleService', () => {
  let service: ModuleService;
  let api: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    const apiSpy = jasmine.createSpyObj<ApiService>('ApiService', [
      'resourceUrl',
      'get',
      'getList',
      'post',
      'put',
      'delete',
    ]);
    apiSpy.resourceUrl.and.callFake((entity: string, id?: string | number) =>
      id === undefined ? `/api/v1/${entity}` : `/api/v1/${entity}/${id}`,
    );

    TestBed.configureTestingModule({
      providers: [ModuleService, { provide: ApiService, useValue: apiSpy }],
    });

    service = TestBed.inject(ModuleService);
    api = TestBed.inject(ApiService) as jasmine.SpyObj<ApiService>;
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getModulesByPortal delegates to api.getList with the portalId discriminator + query params', () => {
    const page: PagedResponse<Module> = {
      data: [makeModule()],
      meta: { pageIndex: 0, pageSize: 10, totalCount: 1, totalPages: 1 },
    };
    api.getList.and.returnValue(of(page));

    let result: PagedResponse<Module> | undefined;
    // INTEGRATION: CP2 replaced the undiscriminated getModules(params) with getModulesByPortal/
    // getModulesByTab because GET /api/v1/modules requires exactly one list discriminator. The
    // discriminator (portalId) is spread AFTER params so it cannot be overridden.
    service.getModulesByPortal(0, { pageIndex: 0, pageSize: 10 }).subscribe((p) => (result = p));

    expect(api.resourceUrl).toHaveBeenCalledWith('modules');
    expect(api.getList).toHaveBeenCalledWith('/api/v1/modules', { pageIndex: 0, pageSize: 10, portalId: 0 });
    expect(result).toBe(page);
  });

  it('getModule delegates to api.get with the id URL', () => {
    const module = makeModule({ moduleID: 5 });
    api.get.and.returnValue(of(module));

    let result: Module | undefined;
    service.getModule(5).subscribe((m) => (result = m));

    expect(api.resourceUrl).toHaveBeenCalledWith('modules', 5);
    expect(api.get).toHaveBeenCalledWith('/api/v1/modules/5');
    expect(result).toBe(module);
  });

  it('createModule delegates to api.post with the request body', () => {
    const dto = makeCreateModuleDto();
    const created = makeModule({ moduleID: 99 });
    api.post.and.returnValue(of(created));

    let result: Module | undefined;
    service.createModule(dto).subscribe((m) => (result = m));

    expect(api.resourceUrl).toHaveBeenCalledWith('modules');
    expect(api.post).toHaveBeenCalledWith('/api/v1/modules', dto);
    expect(result).toBe(created);
  });

  it('updateModule delegates to api.put with the id URL and body', () => {
    const dto = makeUpdateModuleDto(7);
    const updated = makeModule({ moduleID: 7 });
    api.put.and.returnValue(of(updated));

    let result: Module | undefined;
    service.updateModule(7, dto).subscribe((m) => (result = m));

    expect(api.resourceUrl).toHaveBeenCalledWith('modules', 7);
    expect(api.put).toHaveBeenCalledWith('/api/v1/modules/7', dto);
    expect(result).toBe(updated);
  });

  it('deleteModule delegates to api.delete with the id URL', () => {
    api.delete.and.returnValue(of(undefined));

    let completed = false;
    service.deleteModule(3).subscribe({ complete: () => (completed = true) });

    expect(api.resourceUrl).toHaveBeenCalledWith('modules', 3);
    expect(api.delete).toHaveBeenCalledWith('/api/v1/modules/3');
    expect(completed).toBe(true);
  });
});
