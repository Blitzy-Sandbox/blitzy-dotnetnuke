import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';

import { AuthService } from './auth.service';
import { authGuard } from './auth.guard';

/**
 * Unit tests for the functional {@link authGuard} (`core/auth/auth.guard.ts`).
 * Satisfies Gate 4 (`ng test --watch=false --browsers=ChromeHeadless` -> 100% pass).
 *
 * Testing strategy (CRITICAL for functional `CanActivateFn` guards):
 *  - A functional guard calls `inject()`, so it MUST execute inside an Angular
 *    injection context. The {@link runGuard} helper wraps every invocation in
 *    `TestBed.runInInjectionContext(...)`; calling `authGuard(...)` directly would
 *    throw NG0203 ("inject() must be called from an injection context").
 *  - Both collaborators are replaced with Jasmine spies (NO `RouterTestingModule`,
 *    NO real router configuration, NO exercising of `AuthService` internals):
 *      * `AuthService.isAuthenticated` is a computed `Signal<boolean>` (a callable);
 *        spying it as a method that returns a boolean is call-compatible with the
 *        guard's `authService.isAuthenticated()` invocation and keeps the test
 *        simple and strictly typed.
 *      * `Router.createUrlTree` returns a sentinel {@link UrlTree} so the redirect
 *        result can be asserted by identity (`toBe`).
 *  - The `route` argument is unused by the guard, but `state.url` IS read to
 *    capture the attempted destination as the `returnUrl` query parameter, so the
 *    `state` stub carries a representative guarded deep-link URL. Both are cast to
 *    their REAL snapshot types — not `any` — to keep the suite strict-TS clean.
 *
 * MIGRATION: `authGuard` is the client-side replacement for the legacy server-side
 * request gating in `Library/Components/Security/PortalSecurity.vb`
 * (`FormsAuthentication.SignOut` + portal cookies, L77-95, and the `IsInRole` /
 * `IsInRoles` role checks, L103-136). Authoritative authorization now lives
 * server-side via JWT validation and ASP.NET Core policies; this guard only gates
 * client navigation. See the root `MIGRATION_NOTES.md`
 * (Deviation D-001: Forms Authentication -> stateless JWT Bearer).
 */
describe('authGuard', () => {
  let authSpy: jasmine.SpyObj<AuthService>;
  let routerSpy: jasmine.SpyObj<Router>;

  /**
   * Unique sentinel returned by the mocked `Router.createUrlTree`, enabling an
   * identity (`toBe`) assertion that the guard returns exactly this redirect tree.
   * `new UrlTree()` is a valid no-arg construction in Angular 19 (every constructor
   * parameter is optional).
   */
  const redirectTree = new UrlTree();

  /**
   * Representative guarded deep-link the user attempted to reach while
   * unauthenticated; the guard must echo it back as `?returnUrl=<this>`.
   */
  const attemptedUrl = '/modules';

  /**
   * The guard ignores `route`; it reads `state.url` to build the `returnUrl`
   * query param. Both are cast to their real snapshot types (NOT `any`) to satisfy
   * the `CanActivateFn` signature under strict TypeScript.
   */
  const route = {} as ActivatedRouteSnapshot;
  const state = { url: attemptedUrl } as RouterStateSnapshot;

  /**
   * Invoke the guard inside an injection context and narrow the `CanActivateFn`
   * union return type to `boolean | UrlTree` (this guard never returns an
   * Observable/Promise).
   */
  const runGuard = (): boolean | UrlTree =>
    TestBed.runInInjectionContext(
      () => authGuard(route, state) as boolean | UrlTree,
    );

  beforeEach(() => {
    authSpy = jasmine.createSpyObj<AuthService>('AuthService', ['isAuthenticated']);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);
    routerSpy.createUrlTree.and.returnValue(redirectTree);

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });
  });

  it('allows activation (returns true) when the user is authenticated', () => {
    authSpy.isAuthenticated.and.returnValue(true);

    const result = runGuard();

    expect(result).toBe(true);
    expect(routerSpy.createUrlTree).not.toHaveBeenCalled();
  });

  it('redirects to /auth/login with a returnUrl query param echoing the attempted URL when the user is not authenticated', () => {
    authSpy.isAuthenticated.and.returnValue(false);

    runGuard();

    // PRODUCER (F4-AUTH-01): the guard must append the attempted destination as
    // `returnUrl` so `login.component.ts` `resolveReturnUrl()` can restore it.
    expect(routerSpy.createUrlTree).toHaveBeenCalledOnceWith(['/auth/login'], {
      queryParams: { returnUrl: attemptedUrl },
    });
  });

  it('returns the redirect UrlTree sentinel when the user is not authenticated', () => {
    authSpy.isAuthenticated.and.returnValue(false);

    const result = runGuard();

    expect(result).toBe(redirectTree);
  });
});
