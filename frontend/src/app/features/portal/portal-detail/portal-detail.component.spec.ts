// MIGRATION: Gate-4 unit spec for the read-only Portal detail view derived from
// Website/admin/Portal/SiteSettings.ascx.vb (read-only projection). Verifies the fetch-on-activation
// (objPortalController.GetPortal L266 -> portalService.getById), read-only field rendering, and the Edit link.
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal, type WritableSignal } from '@angular/core';
import { of } from 'rxjs';
import { By } from '@angular/platform-browser';

import type { Portal } from '../../../core/models';
import { PortalService } from '../portal.service';
import { PortalDetailComponent } from './portal-detail.component';

function makePortal(overrides: Partial<Portal> = {}): Portal {
  return {
    portalId: 5,
    portalName: 'Contoso Intranet',
    userRegistration: 1,
    bannerAdvertising: 0,
    administratorId: 2,
    hostFee: 49.95,
    hostSpace: 100,
    pageQuota: 0,
    userQuota: 0,
    administratorRoleId: 0,
    registeredRoleId: 1,
    guid: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
    siteLogHistory: 30,
    adminTabId: 10,
    superTabId: 20,
    splashTabId: 30,
    homeTabId: 40,
    loginTabId: 50,
    userTabId: 60,
    timeZoneOffset: -480,
    description: 'Primary corporate intranet portal.',
    keyWords: 'intranet,corporate,contoso',
    footerText: 'Copyright Contoso',
    expiryDate: '2030-01-01T00:00:00Z',
    currency: 'USD',
    homeDirectory: 'Portals/5',
    ...overrides,
  };
}

describe('PortalDetailComponent', () => {
  let fixture: ComponentFixture<PortalDetailComponent>;
  let getByIdSpy: jasmine.Spy<(id: number | string) => ReturnType<PortalService['getById']>>;
  let selected: WritableSignal<Portal | null>;
  let loading: WritableSignal<boolean>;

  function configure(portal: Portal | null): void {
    selected = signal<Portal | null>(portal);
    loading = signal<boolean>(false);
    getByIdSpy = jasmine.createSpy('getById').and.callFake((_id: number | string) => of(makePortal()));

    const portalServiceMock = {
      getById: getByIdSpy,
      selected: selected.asReadonly(),
      loading: loading.asReadonly(),
    } as unknown as PortalService;

    TestBed.configureTestingModule({
      imports: [PortalDetailComponent],
      providers: [
        provideRouter([]),
        { provide: PortalService, useValue: portalServiceMock },
      ],
    });

    fixture = TestBed.createComponent(PortalDetailComponent);
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();
  }

  it('should create', () => {
    configure(makePortal());
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('fetches the requested portal by route id on activation', () => {
    configure(makePortal());
    expect(getByIdSpy).toHaveBeenCalledTimes(1);
    expect(getByIdSpy).toHaveBeenCalledWith('5');
  });

  it('renders the selected portal fields read-only', () => {
    configure(makePortal());
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Contoso Intranet');
    expect(text).toContain('Primary corporate intranet portal.');
    expect(text).toContain('USD');
    // MIGRATION: SiteSettings.ascx.vb L273 lblGUID.Text = objPortal.GUID.ToString.ToUpper (uppercase parity).
    expect(text).toContain('A1B2C3D4-E5F6-7890-ABCD-EF1234567890');
  });

  it('renders no editable form controls (read-only view)', () => {
    configure(makePortal());
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('input').length).toBe(0);
    expect(host.querySelectorAll('textarea').length).toBe(0);
    expect(host.querySelectorAll('select').length).toBe(0);
  });

  it('exposes an Edit link to the edit route for this portal', () => {
    configure(makePortal());
    // A `[routerLink]` property binding leaves NO `routerlink` DOM attribute, so an `a[routerLink]`
    // attribute selector matches nothing in Angular 19. The read-only detail view renders exactly two
    // anchors (Back + Edit) and RouterLink serializes their `href`s; query all anchors and assert the hrefs.
    const links = fixture.debugElement.queryAll(By.css('a'));
    const hrefs = links.map((link) => (link.nativeElement as HTMLAnchorElement).getAttribute('href'));
    expect(hrefs).toContain('/portals/5/edit');
    expect(hrefs).toContain('/portals');
  });

  it('shows an empty-state message when no portal is loaded', () => {
    configure(null);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Portal not found.');
  });
});
