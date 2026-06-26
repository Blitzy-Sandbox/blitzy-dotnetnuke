// MIGRATION: Gate-4 unit tests for PortalListComponent, asserting parity with the legacy
// behaviors of Website/admin/Portal/Portals.ascx.vb (pageSize=20, zero-based paging, A/All filter,
// confirm-before-delete, active-portal delete guard, host-only access). The legacy "Expired" filter and bulk
// "Delete Expired" action are NOT migrated (no `portals/expired` endpoint in the frozen backend contract, AAP
// Section 0.3.4), so their specs are removed and the filter list is now All + A..Z.
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal, WritableSignal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';

import { PortalListComponent } from './portal-list.component';
import { PortalService } from '../portal.service';
import { AuthService } from '../../../core/auth/auth.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
// MIGRATION: `CurrentUser` is exported from core/models (auth.model, re-exported by the barrel),
// NOT from auth.service (which only consumes it). `Paged`/`Portal` likewise come from the barrel.
import type { CurrentUser, Paged, Portal } from '../../../core/models';

function makePortal(overrides: Partial<Portal> = {}): Portal {
  // Only fields exercised by the component are set explicitly; the assertion keeps the factory
  // resilient to the full Portal shape without using `any`.
  return {
    portalId: 1,
    portalName: 'Test Portal',
    description: 'A test portal',
    expiryDate: null,
    ...overrides,
  } as Portal;
}

function makeUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    userId: 1,
    username: 'host',
    email: 'host@example.com',
    displayName: 'Host User',
    firstName: 'Host',
    lastName: 'User',
    fullName: 'Host User',
    isSuperUser: true,
    portalId: 0,
    roles: ['Administrators'],
    ...overrides,
  };
}

describe('PortalListComponent', () => {
  let fixture: ComponentFixture<PortalListComponent>;
  let component: PortalListComponent;

  let portalsSig: WritableSignal<Portal[]>;
  let loadingSig: WritableSignal<boolean>;
  let totalCountSig: WritableSignal<number>;
  let currentUserSig: WritableSignal<CurrentUser | null>;

  let listSpy: jasmine.Spy;
  let deleteSpy: jasmine.Spy;

  beforeEach(async () => {
    portalsSig = signal<Portal[]>([makePortal({ portalId: 1 }), makePortal({ portalId: 2 })]);
    loadingSig = signal<boolean>(false);
    totalCountSig = signal<number>(2);
    currentUserSig = signal<CurrentUser | null>(makeUser({ portalId: 0 }));

    // MIGRATION: align the spy return to the REAL Paged<Portal> shape from
    // core/models/api-envelope.model ({ items, totalCount, pageIndex, pageSize, totalPages,
    // hasPreviousPage, hasNextPage }) so the mock is fully typed without `any`.
    const emptyPage: Paged<Portal> = {
      items: [],
      totalCount: 0,
      pageIndex: 0,
      pageSize: 20,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
    };

    listSpy = jasmine.createSpy('list').and.returnValue(of(emptyPage));
    deleteSpy = jasmine.createSpy('delete').and.returnValue(of(void 0));

    const portalServiceMock = {
      portals: portalsSig,
      loading: loadingSig,
      totalCount: totalCountSig,
      selected: signal<Portal | null>(null),
      list: listSpy,
      delete: deleteSpy,
      getById: jasmine.createSpy('getById').and.returnValue(of(makePortal())),
      create: jasmine.createSpy('create').and.returnValue(of(makePortal())),
      update: jasmine.createSpy('update').and.returnValue(of(makePortal())),
    };

    const authMock = {
      currentUser: currentUserSig,
      isAuthenticated: signal<boolean>(true),
      accessToken: signal<string | null>('test-token'),
    };

    await TestBed.configureTestingModule({
      imports: [PortalListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: PortalService, useValue: portalServiceMock },
        { provide: AuthService, useValue: authMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PortalListComponent);
    component = fixture.componentInstance;
  });

  function getDataTable(): DataTableComponent<Portal> {
    const de = fixture.debugElement.query(By.directive(DataTableComponent));
    if (de === null) {
      throw new Error('app-data-table was not rendered');
    }
    return de.componentInstance as DataTableComponent<Portal>;
  }

  function getDialog(): ConfirmationDialogComponent | null {
    const de = fixture.debugElement.query(By.directive(ConfirmationDialogComponent));
    return de ? (de.componentInstance as ConfirmationDialogComponent) : null;
  }

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('loads portals on init with pageSize 20, zero-based page 0, and empty "All" filter', () => {
    fixture.detectChanges();
    expect(listSpy).toHaveBeenCalledWith(0, 20, '');
  });

  it('passes pageSize=20 and zero-based currentPage to the data-table', () => {
    fixture.detectChanges();
    const dt = getDataTable();
    expect(dt.pageSize()).toBe(20);
    expect(dt.currentPage()).toBe(0);
    expect(dt.totalRecords()).toBe(2);
  });

  it('builds the filter list as All + A..Z (no Expired)', () => {
    fixture.detectChanges();
    const dt = getDataTable();
    const filters = dt.filters();
    expect(filters[0]).toBe('All');
    expect(filters).toContain('A');
    expect(filters).toContain('Z');
    // MIGRATION: the legacy "Expired" filter is removed (no backend endpoint in the frozen contract).
    expect(filters[filters.length - 1]).toBe('Z');
    expect(filters).not.toContain('Expired');
    expect(filters.length).toBe(27);
  });

  it('filterChange("All") calls list(0, 20, "")', () => {
    fixture.detectChanges();
    listSpy.calls.reset();
    getDataTable().filterChange.emit('All');
    expect(listSpy).toHaveBeenCalledWith(0, 20, '');
  });

  it('filterChange("A") calls list(0, 20, "A")', () => {
    fixture.detectChanges();
    listSpy.calls.reset();
    getDataTable().filterChange.emit('A');
    expect(listSpy).toHaveBeenCalledWith(0, 20, 'A');
  });

  it('pageChange keeps zero-based index (no ±1) and reloads', () => {
    fixture.detectChanges();
    listSpy.calls.reset();
    getDataTable().pageChange.emit(2);
    expect(listSpy).toHaveBeenCalledWith(2, 20, '');
  });

  it('delete opens the confirmation dialog and confirm deletes by portalId', () => {
    fixture.detectChanges();
    const portal = makePortal({ portalId: 7 });
    getDataTable().delete.emit(portal);
    fixture.detectChanges();
    const dialog = getDialog();
    expect(dialog).not.toBeNull();
    dialog!.confirm.emit();
    expect(deleteSpy).toHaveBeenCalledWith(7);
  });

  it('cancel closes the confirmation dialog without deleting', () => {
    fixture.detectChanges();
    getDataTable().delete.emit(makePortal({ portalId: 7 }));
    fixture.detectChanges();
    getDialog()!.cancel.emit();
    fixture.detectChanges();
    expect(getDialog()).toBeNull();
    expect(deleteSpy).not.toHaveBeenCalled();
  });

  it('does NOT open the dialog or delete when targeting the active/current portal (L423)', () => {
    currentUserSig.set(makeUser({ isSuperUser: true, portalId: 5 }));
    fixture.detectChanges();
    getDataTable().delete.emit(makePortal({ portalId: 5 }));
    fixture.detectChanges();
    expect(getDialog()).toBeNull();
    expect(deleteSpy).not.toHaveBeenCalled();
  });

  it('navigates to edit route on edit output (pid=KEYFIELD parity)', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigate');
    getDataTable().edit.emit(makePortal({ portalId: 9 }));
    expect(navSpy).toHaveBeenCalledWith(['/portals', 9, 'edit']);
  });

  it('navigates to detail route on view output', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigate');
    getDataTable().view.emit(makePortal({ portalId: 9 }));
    expect(navSpy).toHaveBeenCalledWith(['/portals', 9]);
  });

  it('navigates to the new-portal route from the host-only New button', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigate');
    fixture.debugElement.query(By.css('.btn--primary')).nativeElement.click();
    expect(navSpy).toHaveBeenCalledWith(['/portals', 'new']);
  });

  it('renders an access-denied state and no data-table for non-super users (L339-341)', () => {
    currentUserSig.set(makeUser({ isSuperUser: false }));
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.directive(DataTableComponent))).toBeNull();
    expect(fixture.debugElement.query(By.css('.portal-list__access-denied'))).not.toBeNull();
    expect(listSpy).not.toHaveBeenCalled();
  });

  it('hides the host-only New action for non-super users', () => {
    currentUserSig.set(makeUser({ isSuperUser: false }));
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('.btn--primary'))).toBeNull();
    // MIGRATION: the "Delete Expired" (.btn--danger) action was removed entirely (no backend endpoint).
    expect(fixture.debugElement.query(By.css('.btn--danger'))).toBeNull();
  });
});
