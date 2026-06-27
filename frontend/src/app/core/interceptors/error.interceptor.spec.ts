// MIGRATION: Spec for the net-new functional errorInterceptor (replaces DNN Web Forms global error handling +
// the Forms-auth redirect). Verifies RFC 7807 ProblemDetails parsing for BOTH `errors` shapes, and the
// 401 -> refresh -> retry-once -> logout/redirect flow including the auth-endpoint recursion guard. Functional
// interceptor exercised through a real HttpClient with withInterceptors([errorInterceptor]) (Gate 4).
import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { errorInterceptor, parseProblemDetails } from './error.interceptor';
import { AuthService } from '../auth/auth.service';
import { environment } from '../../../environments/environment';
import type { AuthResponse } from '../models/auth.model';

describe('errorInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let refreshSpy: jasmine.Spy;
  let logoutSpy: jasmine.Spy;
  let accessTokenValue: string | null;

  const authResponse: AuthResponse = {
    accessToken: 'refreshed-access-token',
    refreshToken: 'rotated-refresh-token',
    tokenType: 'Bearer',
    expiresIn: 3600,
    expiresAt: '2026-01-01T00:00:00.000Z',
    user: null,
  };

  const buildAuthServiceStub = (): {
    accessToken: () => string | null;
    refresh: jasmine.Spy;
    logout: jasmine.Spy;
  } => {
    refreshSpy = jasmine.createSpy('refresh').and.returnValue(of(authResponse));
    logoutSpy = jasmine.createSpy('logout').and.returnValue(of(undefined));
    return {
      accessToken: (): string | null => accessTokenValue,
      refresh: refreshSpy,
      logout: logoutSpy,
    };
  };

  beforeEach(() => {
    accessTokenValue = 'refreshed-access-token';
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: buildAuthServiceStub() },
      ],
    });
    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  // ---- Pure parser tests: RFC 7807 `errors` duality ----

  it('parseProblemDetails handles the Record<string, string[]> (model-validation) shape', () => {
    const result = parseProblemDetails({
      type: 'https://httpstatuses.io/400',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        portalName: ['The PortalName field is required.'],
        email: ['Invalid email.'],
      },
    });

    expect(result.fieldErrors['portalName']).toEqual(['The PortalName field is required.']);
    expect(result.messages).toContain('The PortalName field is required.');
    expect(result.messages).toContain('Invalid email.');
    expect(result.messages).toContain('One or more validation errors occurred.');
  });

  it('parseProblemDetails handles the flat string[] (Result.Errors) shape', () => {
    const result = parseProblemDetails({
      title: 'Bad Request',
      status: 400,
      detail: 'The portal could not be created.',
      errors: ['Portal name already exists.', 'Quota exceeded.'],
    });

    expect(result.messages).toContain('Portal name already exists.');
    expect(result.messages).toContain('Quota exceeded.');
    expect(result.messages).toContain('The portal could not be created.');
    expect(Object.keys(result.fieldErrors).length).toBe(0);
  });

  it('parseProblemDetails never throws on a missing/blank/non-object body', () => {
    expect(parseProblemDetails(null).messages).toEqual([]);
    expect(parseProblemDetails(undefined).fieldErrors).toEqual({});
    expect(parseProblemDetails('not-an-object').messages).toEqual([]);
  });

  // ---- Interceptor behavior ----

  it('passes a successful response through unchanged (no refresh)', () => {
    let body: unknown;
    httpClient.get(`${environment.apiUrl}/portals`).subscribe((res) => (body = res));

    const req = httpMock.expectOne(`${environment.apiUrl}/portals`);
    req.flush({ data: [], meta: {} });

    expect(body).toEqual({ data: [], meta: {} });
    expect(refreshSpy).not.toHaveBeenCalled();
  });

  it('logs and rethrows a non-401 error (parsing surfaced; no refresh)', () => {
    const consoleSpy = spyOn(console, 'error');
    let received: HttpErrorResponse | undefined;
    httpClient.get(`${environment.apiUrl}/portals`).subscribe({
      next: () => fail('expected an error'),
      error: (err: HttpErrorResponse) => (received = err),
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/portals`);
    req.flush(
      { title: 'Bad Request', status: 400, errors: ['Name is required.'] },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(received?.status).toBe(400);
    expect(refreshSpy).not.toHaveBeenCalled();
    expect(consoleSpy).toHaveBeenCalled();
  });

  it('redacts field-level payloads from the production error log (no PII leakage)', () => {
    // MIGRATION: the test build uses environments/environment.ts (production: true), so logError takes
    // the redaction branch (AAP Section 0.7.6). A validation message that echoes user-entered input must
    // NOT reach the log; only non-sensitive metadata (status, url, failed field NAMES, message count) may.
    const consoleSpy = spyOn(console, 'error');
    httpClient.get(`${environment.apiUrl}/users`).subscribe({
      next: () => fail('expected an error'),
      error: () => undefined,
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/users`);
    req.flush(
      {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { email: ["The email 'jane@example.com' is already registered."] },
      },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(consoleSpy).toHaveBeenCalledTimes(1);
    const logged = consoleSpy.calls.mostRecent().args[1] as Record<string, unknown>;
    // Non-sensitive metadata is present...
    expect(logged['status']).toBe(400);
    expect(logged['failedFields']).toEqual(['email']);
    // parseProblemDetails counts the title PLUS each field error, so 'title' + 1 email error = 2.
    expect(logged['messageCount']).toBe(2);
    // ...but the PII-bearing payloads are NOT logged.
    expect(logged['messages']).toBeUndefined();
    expect(logged['fieldErrors']).toBeUndefined();
    // Defense in depth: the echoed value appears nowhere in the logged arguments.
    expect(JSON.stringify(consoleSpy.calls.mostRecent().args)).not.toContain('jane@example.com');
  });

  it('on 401, refreshes the token and retries the original request once (re-tokenized)', () => {
    let body: unknown;
    httpClient.get(`${environment.apiUrl}/portals`).subscribe((res) => (body = res));

    // First attempt -> 401.
    const first = httpMock.expectOne(`${environment.apiUrl}/portals`);
    first.flush({ title: 'Unauthorized', status: 401 }, { status: 401, statusText: 'Unauthorized' });

    // refresh() invoked once, then the original request is retried carrying the rotated token.
    expect(refreshSpy).toHaveBeenCalledTimes(1);
    const retried = httpMock.expectOne(`${environment.apiUrl}/portals`);
    expect(retried.request.headers.get('Authorization')).toBe('Bearer refreshed-access-token');
    retried.flush({ data: [{ portalId: 0 }], meta: {} });

    expect(body).toEqual({ data: [{ portalId: 0 }], meta: {} });
  });

  it('on 401 with a failed refresh, logs out and redirects to /auth/login', () => {
    refreshSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400 })));
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    let errored = false;
    httpClient.get(`${environment.apiUrl}/portals`).subscribe({
      next: () => fail('expected an error'),
      error: () => (errored = true),
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/portals`);
    req.flush({ title: 'Unauthorized', status: 401 }, { status: 401, statusText: 'Unauthorized' });

    expect(refreshSpy).toHaveBeenCalledTimes(1);
    expect(logoutSpy).toHaveBeenCalledTimes(1);
    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login']);
    expect(errored).toBe(true);
  });

  it('does NOT trigger the refresh flow for auth-endpoint 401s (recursion guard)', () => {
    let errored = false;
    httpClient.post(`${environment.apiUrl}/auth/login`, {}).subscribe({
      next: () => fail('expected an error'),
      error: () => (errored = true),
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    req.flush({ title: 'Unauthorized', status: 401 }, { status: 401, statusText: 'Unauthorized' });

    expect(refreshSpy).not.toHaveBeenCalled();
    expect(errored).toBe(true);
  });
});
