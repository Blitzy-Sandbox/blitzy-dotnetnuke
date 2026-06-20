import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';

import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

/**
 * Gate 4 unit tests for the functional `authGuard` (`CanActivateFn`).
 *
 * The guard enforces a two-branch contract (see `auth.guard.ts`):
 *  - authenticated   -> returns `true` so navigation proceeds.
 *  - unauthenticated -> returns the `UrlTree` produced by
 *    `Router.createUrlTree(['/auth/login'])`, which cancels the in-flight
 *    navigation AND redirects in a single, race-free step.
 *
 * Testing strategy (mandated):
 *  - BOTH collaborators are replaced with `jasmine.createSpyObj` doubles, so the
 *    suite exercises the guard's branching logic in isolation — no real
 *    `AuthService`, no real router navigation, no `RouterTestingModule`.
 *  - `AuthService.isAuthenticated` is a `computed` SIGNAL (a callable). A spy
 *    method returning a boolean is call-compatible with the guard's
 *    `authService.isAuthenticated()` invocation and is simpler (and fully
 *    type-compatible) than constructing a real `signal()` here.
 *  - The mocked `Router.createUrlTree` returns a single suite-scoped sentinel
 *    `UrlTree`, letting the redirect specs assert REFERENCE identity (`toBe`) —
 *    proof that the guard forwards exactly what the router produced.
 *  - Functional guards resolve their dependencies with `inject()`, which throws
 *    outside an injection context, so every invocation is wrapped in
 *    `TestBed.runInInjectionContext` via the {@link runGuard} helper.
 *
 * MIGRATION context: `authGuard` is the client-side successor to the legacy
 * per-request ASP.NET Forms Authentication gating in
 * `Library/Components/Security/PortalSecurity.vb`; these tests pin its redirect
 * contract. Authoritative authorization remains server-side on the JWT-secured
 * API — this guard (and therefore this suite) covers only client navigation.
 */
describe('authGuard', () => {
  let authSpy: jasmine.SpyObj<AuthService>;
  let routerSpy: jasmine.SpyObj<Router>;

  /**
   * Stable sentinel returned by the mocked `Router.createUrlTree`. Built once at
   * suite scope so the redirect specs can assert it is forwarded verbatim.
   * `new UrlTree()` is a valid no-arg construction in Angular 19 (every
   * constructor parameter — `root`, `queryParams`, `fragment` — is optional).
   */
  const redirectTree = new UrlTree();

  /**
   * The guard ignores both `CanActivateFn` parameters (its decision is purely
   * authentication-based), so empty objects cast to the precise snapshot types
   * are safe. They are cast to their REAL types (never `any`) to satisfy the
   * project's strict TypeScript configuration.
   */
  const route = {} as ActivatedRouteSnapshot;
  const state = {} as RouterStateSnapshot;

  /**
   * Run `authGuard` inside an Angular injection context and narrow the result to
   * `boolean | UrlTree`. This guard is synchronous and never returns an
   * Observable/Promise, so the narrowed type keeps the assertions precise while
   * remaining assignable from `CanActivateFn`'s `MaybeAsync<GuardResult>`.
   */
  const runGuard = (): boolean | UrlTree =>
    TestBed.runInInjectionContext(
      () => authGuard(route, state) as boolean | UrlTree,
    );

  beforeEach(() => {
    authSpy = jasmine.createSpyObj<AuthService>('AuthService', [
      'isAuthenticated',
    ]);
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
    // The happy path must never compute a redirect.
    expect(routerSpy.createUrlTree).not.toHaveBeenCalled();
  });

  it('requests a redirect to /auth/login when the user is NOT authenticated', () => {
    authSpy.isAuthenticated.and.returnValue(false);

    const result = runGuard();

    expect(routerSpy.createUrlTree).toHaveBeenCalledOnceWith(['/auth/login']);
    // A UrlTree (not a boolean) is returned so navigation is cancelled + redirected.
    expect(result).toBeInstanceOf(UrlTree);
  });

  it('returns exactly the UrlTree produced by Router.createUrlTree when unauthenticated', () => {
    authSpy.isAuthenticated.and.returnValue(false);

    const result = runGuard();

    // Reference identity: the guard forwards the router's tree verbatim rather
    // than fabricating its own.
    expect(result).toBe(redirectTree);
  });
});
