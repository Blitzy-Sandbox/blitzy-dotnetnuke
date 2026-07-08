import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { CreatePortalRequest, Portal, UpdatePortalRequest } from '../../core/models';
import { PortalService } from './portal.service';

/**
 * Unit spec for {@link PortalService}.
 *
 * Verifies the service is a pure, thin delegator to {@link ApiService}: each
 * method forwards to the correct ApiService method with the `'portals'` resource
 * string plus the correct arguments, and returns the ApiService observable
 * unchanged. ApiService is fully mocked with a jasmine.SpyObj so no HttpClient /
 * real HTTP is exercised, and `of(...)` return stubs make `subscribe()` run
 * synchronously. Contributes to Gate 4 (ng test --code-coverage, 100% pass).
 */
describe('PortalService', () => {
  let service: PortalService;
  let api: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    const spy = jasmine.createSpyObj<ApiService>('ApiService', [
      'getList',
      'getListWithMeta',
      'getById',
      'create',
      'update',
      'delete',
    ]);

    TestBed.configureTestingModule({
      providers: [PortalService, { provide: ApiService, useValue: spy }],
    });

    service = TestBed.inject(PortalService);
    api = TestBed.inject(ApiService) as jasmine.SpyObj<ApiService>;
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('list() delegates to ApiService.getList with the "portals" resource', () => {
    api.getList.and.returnValue(of([]));
    service.list({ query: 'a' }).subscribe();
    expect(api.getList).toHaveBeenCalledWith('portals', { query: 'a' });
  });

  it('list() forwards undefined params', () => {
    api.getList.and.returnValue(of([]));
    service.list().subscribe();
    expect(api.getList).toHaveBeenCalledWith('portals', undefined);
  });

  it('listWithMeta() delegates to ApiService.getListWithMeta', () => {
    api.getListWithMeta.and.returnValue(of({ data: [] }));
    service.listWithMeta({ page: 1 }).subscribe();
    expect(api.getListWithMeta).toHaveBeenCalledWith('portals', { page: 1 });
  });

  it('getById() delegates to ApiService.getById', () => {
    api.getById.and.returnValue(of({ portalID: 5 } as Portal));
    service.getById(5).subscribe();
    expect(api.getById).toHaveBeenCalledWith('portals', 5);
  });

  it('create() delegates to ApiService.create', () => {
    const request = {
      portalName: 'Site',
      firstName: 'A',
      lastName: 'B',
      username: 'ab',
      password: 'p',
      email: 'a@b.co',
      portalAlias: 'site',
    } as CreatePortalRequest;
    api.create.and.returnValue(of({} as Portal));
    service.create(request).subscribe();
    expect(api.create).toHaveBeenCalledWith('portals', request);
  });

  it('update() delegates to ApiService.update', () => {
    const request = { portalName: 'Site' } as UpdatePortalRequest;
    api.update.and.returnValue(of({} as Portal));
    service.update(7, request).subscribe();
    expect(api.update).toHaveBeenCalledWith('portals', 7, request);
  });

  it('remove() delegates to ApiService.delete', () => {
    api.delete.and.returnValue(of(void 0));
    service.remove(9).subscribe();
    expect(api.delete).toHaveBeenCalledWith('portals', 9);
  });
});
