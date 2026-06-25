// MIGRATION: Spec for the net-new functional authGuard (replaces the legacy Forms-auth redirect logic).
// Verifies authenticated -> true and unauthenticated -> redirect UrlTree to /auth/login.
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';

import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  let isAuthenticatedValue: boolean;

  // Minimal AuthService stub exposing only what the guard reads (isAuthenticated()).
  const authServiceStub = {
    isAuthenticated: (): boolean => isAuthenticatedValue,
  };

  const runGuard = (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) =>
    TestBed.runInInjectionContext(() => authGuard(route, state));

  beforeEach(() => {
    isAuthenticatedValue = false;
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: authServiceStub }],
    });
  });

  it('should allow activation (true) when the user is authenticated', () => {
    isAuthenticatedValue = true;

    const result = runGuard(
      {} as ActivatedRouteSnapshot,
      { url: '/portals' } as RouterStateSnapshot,
    );

    expect(result).toBe(true);
  });

  it('should redirect to /auth/login (UrlTree) when the user is NOT authenticated', () => {
    isAuthenticatedValue = false;

    const result = runGuard(
      {} as ActivatedRouteSnapshot,
      { url: '/portals' } as RouterStateSnapshot,
    );

    expect(result instanceof UrlTree).toBe(true);
    const router = TestBed.inject(Router);
    expect(router.serializeUrl(result as UrlTree)).toContain('/auth/login');
  });
});
