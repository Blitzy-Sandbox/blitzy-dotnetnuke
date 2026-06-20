import { signal, WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';

import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

/**
 * Gate 4 unit tests for the functional `authGuard` (`CanActivateFn`).
 *
 * Verifies the two-branch contract:
 *  - authenticated   -> returns `true` (navigation proceeds)
 *  - unauthenticated -> returns a `UrlTree` for `/auth/login` (redirect)
 *
 * A real Router is provided via `provideRouter([])` so that `createUrlTree`
 * produces a genuine `UrlTree` whose serialized form can be asserted. The
 * `AuthService` is replaced by a minimal stub exposing a writable
 * `isAuthenticated` signal, letting each test toggle the authentication state.
 * The guard is executed inside `TestBed.runInInjectionContext` because
 * functional guards resolve their collaborators with `inject()`.
 */
describe('authGuard', () => {
  let isAuthenticated: WritableSignal<boolean>;

  // The route/state arguments are mandated by the `CanActivateFn` type signature
  // but ignored by the guard implementation, so trivial stubs suffice.
  const routeStub = {} as unknown as ActivatedRouteSnapshot;
  const stateStub = {} as unknown as RouterStateSnapshot;

  beforeEach(() => {
    isAuthenticated = signal(false);

    const authServiceStub: Pick<AuthService, 'isAuthenticated'> = {
      isAuthenticated,
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceStub },
      ],
    });
  });

  function runGuard(): boolean | UrlTree {
    return TestBed.runInInjectionContext(
      () => authGuard(routeStub, stateStub) as boolean | UrlTree,
    );
  }

  it('allows activation when the user is authenticated', () => {
    isAuthenticated.set(true);

    const result = runGuard();

    expect(result).toBeTrue();
  });

  it('redirects to /auth/login (returns a UrlTree) when unauthenticated', () => {
    isAuthenticated.set(false);

    const result = runGuard();

    expect(result instanceof UrlTree).toBeTrue();
    expect((result as UrlTree).toString()).toBe('/auth/login');
  });
});
