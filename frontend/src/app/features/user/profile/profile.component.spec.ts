// MIGRATION (CP-final review - profile workflow parity): Karma/Jasmine spec for ProfileComponent. The legacy DNN
// Profile.ascx.vb dynamic profile editor is now IMPLEMENTED as a typed reactive form bound to
// GET/PUT /api/users/{id}/profile (the EXISTING [UserProfile] EAV). These tests verify the tenant-scoped load
// (getById + getProfile), the form patch from the server profile, the save (updateProfile) request shape
// (empty string -> null, integer timeZone, fullName omitted), the success/error feedback, and back navigation.
// Gate 4: ng test --watch=false --browsers=ChromeHeadless.
import { signal, type WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';

import { ProfileComponent } from './profile.component';
import { UserService } from '../user.service';
import { AuthService } from '../../../core/auth/auth.service';
import type { User, UserProfile } from '../../../core/models';

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

function buildProfile(overrides: Partial<UserProfile> = {}): UserProfile {
  return {
    firstName: null,
    lastName: null,
    fullName: '',
    cell: null,
    telephone: null,
    fax: null,
    im: null,
    street: null,
    unit: null,
    city: null,
    region: null,
    country: null,
    postalCode: null,
    preferredLocale: null,
    timeZone: -1,
    website: null,
    ...overrides,
  };
}

// MIGRATION: an RFC 7807 ProblemDetails body (ApiControllerBase Result.Errors -> flat errors[]).
function problemError(title: string, errors: string[] = []): HttpErrorResponse {
  return new HttpErrorResponse({
    status: 400,
    error: { type: 'about:blank', title, status: 400, errors },
  });
}

describe('ProfileComponent', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let component: ProfileComponent;
  let getByIdSpy: jasmine.Spy;
  let getProfileSpy: jasmine.Spy;
  let updateProfileSpy: jasmine.Spy;
  let currentUser: WritableSignal<CurrentUserLike | null>;
  let navigateSpy: jasmine.Spy;

  beforeEach(() => {
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(buildUser()));
    getProfileSpy = jasmine.createSpy('getProfile').and.returnValue(of(buildProfile()));
    updateProfileSpy = jasmine.createSpy('updateProfile').and.returnValue(of(buildProfile()));
    // Authenticated principal supplies the tenant portalId threaded onto the profile reads/writes.
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
        {
          provide: UserService,
          useValue: { getById: getByIdSpy, getProfile: getProfileSpy, updateProfile: updateProfileSpy },
        },
        { provide: AuthService, useValue: { currentUser } },
      ],
    });

    fixture = TestBed.createComponent(ProfileComponent);
    component = fixture.componentInstance;
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  function init(idValue = '5'): void {
    fixture.componentRef.setInput('id', idValue);
    fixture.detectChanges();
  }

  it('creates the component', () => {
    init();
    expect(component).toBeTruthy();
  });

  it('loads the user context and the profile with the tenant portalId from the principal', () => {
    getByIdSpy.and.returnValue(of(buildUser({ userId: 5, displayName: 'John Doe' })));
    getProfileSpy.and.returnValue(of(buildProfile({ firstName: 'John', lastName: 'Doe', fullName: 'John Doe', timeZone: 5 })));
    init('5');

    expect(getByIdSpy).toHaveBeenCalledWith(5, 3);
    expect(getProfileSpy).toHaveBeenCalledWith(5, 3);
    expect(component.loadedUser()?.userId).toBe(5);
    expect(component.loading()).toBeFalse();
    // MIGRATION: the form is patched from the server profile (Null.NullString -> '', integer timeZone).
    expect(component.form.controls.firstName.value).toBe('John');
    expect(component.form.controls.lastName.value).toBe('Doe');
    expect(component.form.controls.timeZone.value).toBe(5);
    expect(component.fullName()).toBe('John Doe');
  });

  it('renders an editable profile form (not a deferred notice)', () => {
    init('5');
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('form')).not.toBeNull();
    expect(host.querySelector('#profile-firstName')).not.toBeNull();
    expect(host.querySelector('#profile-im')).not.toBeNull();
    expect(host.querySelector('#profile-timeZone')).not.toBeNull();
    // The legacy deferred-notice is gone.
    expect(host.querySelector('.profile__notice')).toBeNull();
  });

  it('does not attempt any load when no id is bound', () => {
    fixture.detectChanges();
    expect(getByIdSpy).not.toHaveBeenCalled();
    expect(getProfileSpy).not.toHaveBeenCalled();
  });

  it('surfaces the server message when the profile load fails', () => {
    getProfileSpy.and.returnValue(throwError(() => problemError('Profile unavailable')));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    expect(component.errorMessages()).toContain('Profile unavailable');
    expect(component.loading()).toBeFalse();
  });

  it('saves the profile, mapping empty strings to null and forwarding the integer timeZone (fullName omitted)', () => {
    init('5');
    component.form.patchValue({ firstName: 'Jane', lastName: '', im: 'jane@im', timeZone: 5 });

    component.onSave();

    const args = updateProfileSpy.calls.mostRecent().args;
    expect(args[0]).toBe(5);
    expect(args[1]).toBe(3);
    const dto = args[2] as UserProfile;
    expect(dto.firstName).toBe('Jane');
    // MIGRATION: empty string maps to null (Null.NullString) on save.
    expect(dto.lastName).toBeNull();
    expect(dto.im).toBe('jane@im');
    expect(dto.timeZone).toBe(5);
    // MIGRATION: the server-composed fullName is read-only and is NOT sent on update.
    expect(dto.fullName).toBeUndefined();
    expect(component.saved()).toBeTrue();
    expect(component.errorMessages().length).toBe(0);
  });

  it('surfaces the server validation messages when the save fails (data-driven definition rules)', () => {
    updateProfileSpy.and.returnValue(
      throwError(() => problemError('Validation failed', ['First Name is required.', 'City exceeds the maximum length of 50 characters.'])),
    );
    init('5');

    component.onSave();

    expect(component.errorMessages()).toEqual(
      jasmine.arrayContaining(['First Name is required.', 'City exceeds the maximum length of 50 characters.']),
    );
    expect(component.saved()).toBeFalse();
    expect(component.saving()).toBeFalse();
  });

  it('navigates back to the users list', () => {
    init();
    component.back();
    expect(navigateSpy).toHaveBeenCalledWith(['/users']);
  });
});
