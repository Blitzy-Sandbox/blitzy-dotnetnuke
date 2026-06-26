// MIGRATION: Karma/Jasmine spec for ProfileComponent. The legacy DNN Profile.ascx.vb dynamic profile editor is
// DEFERRED (the frozen backend exposes no profile endpoint per AAP D1 / 0.3.4), so this component is now a
// deferred-notice screen mirroring the forgot-password / module import-export deferrals. These tests verify the
// context preload (tenant-scoped getById), the accessible notice rendering (no editable form), graceful handling
// of a failed context lookup, and back navigation. Gate 4: ng test --watch=false --browsers=ChromeHeadless.
import { signal, type WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { ProfileComponent } from './profile.component';
import { UserService } from '../user.service';
import { AuthService } from '../../../core/auth/auth.service';
import type { User } from '../../../core/models';

interface CurrentUserLike {
  userId: number;
  portalId: number;
  isSuperUser: boolean;
  roles: string[];
}

function buildUser(overrides: Partial<User> = {}): User {
  return {
    userId: 5,
    username: 'jdoe',
    displayName: 'John Doe',
    email: 'jdoe@example.com',
    firstName: 'John',
    lastName: 'Doe',
    fullName: 'John Doe',
    isSuperUser: false,
    affiliateId: null,
    portalId: 0,
    isApproved: true,
    createdDate: null,
    lastLoginDate: null,
    lastActivityDate: null,
    lastLockoutDate: null,
    lockedOut: false,
    roles: [],
    ...overrides,
  };
}

describe('ProfileComponent', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let component: ProfileComponent;
  let getByIdSpy: jasmine.Spy;
  let currentUser: WritableSignal<CurrentUserLike | null>;
  let navigateSpy: jasmine.Spy;

  beforeEach(() => {
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(buildUser()));
    // Authenticated principal supplies the tenant portalId threaded onto the context lookup.
    currentUser = signal<CurrentUserLike | null>({
      userId: 1,
      portalId: 3,
      isSuperUser: true,
      roles: ['Administrators'],
    });

    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: UserService, useValue: { getById: getByIdSpy } },
        { provide: AuthService, useValue: { currentUser } },
      ],
    });

    fixture = TestBed.createComponent(ProfileComponent);
    component = fixture.componentInstance;
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  function init(idValue = '5', user: User = buildUser()): void {
    getByIdSpy.and.returnValue(of(user));
    fixture.componentRef.setInput('id', idValue);
    fixture.detectChanges();
  }

  it('creates the component', () => {
    init();
    expect(component).toBeTruthy();
  });

  it('preloads the routed user for context with the tenant portalId from the principal', () => {
    init('5', buildUser({ userId: 5, displayName: 'John Doe' }));

    expect(getByIdSpy).toHaveBeenCalledWith(5, 3);
    expect(component.loadedUser()?.userId).toBe(5);
    expect(component.loading()).toBeFalse();
  });

  it('renders the accessible deferred notice (not an editable profile form) and names the user', () => {
    init('5', buildUser({ userId: 5, displayName: 'John Doe' }));

    const host = fixture.nativeElement as HTMLElement;
    const notice = host.querySelector('.profile__notice');
    expect(notice).not.toBeNull();
    expect(notice?.getAttribute('role')).toBe('status');
    expect(host.querySelector('form')).toBeNull();
    expect(host.textContent).toContain('not yet available');
    expect(host.textContent).toContain('John Doe');
  });

  it('does not attempt a context lookup when no id is bound', () => {
    fixture.detectChanges();
    expect(getByIdSpy).not.toHaveBeenCalled();
  });

  it('still renders the deferred notice when the context lookup fails', () => {
    getByIdSpy.and.returnValue(throwError(() => new Error('not found')));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.profile__notice')).not.toBeNull();
    expect(component.loadedUser()).toBeNull();
    expect(component.loading()).toBeFalse();
  });

  it('navigates back to the users list', () => {
    init();
    component.back();
    expect(navigateSpy).toHaveBeenCalledWith(['/users']);
  });
});
