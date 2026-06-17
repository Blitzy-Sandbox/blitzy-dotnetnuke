import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';

import { type CanComponentDeactivate, unsavedChangesGuard } from './unsaved-changes.guard';

/**
 * Unit tests for the generic {@link unsavedChangesGuard}. The guard owns no
 * form/dirty logic — it simply delegates to the deactivating component's
 * `canDeactivate()` — so these tests verify that delegation and its fail-open
 * fallback. The guard uses no `inject()`, so it is invoked directly. The route /
 * state arguments are unused by the guard and are supplied as minimal typed
 * stubs to satisfy the `CanDeactivateFn` signature.
 */
describe('unsavedChangesGuard', () => {
  const route = {} as unknown as ActivatedRouteSnapshot;
  const state = {} as unknown as RouterStateSnapshot;

  function run(component: CanComponentDeactivate): boolean | Promise<boolean> | unknown {
    return unsavedChangesGuard(component, route, state, state);
  }

  it("returns the component's allow decision (true)", () => {
    const component: CanComponentDeactivate = { canDeactivate: () => true };
    expect(run(component)).toBeTrue();
  });

  it("returns the component's block decision (false)", () => {
    const component: CanComponentDeactivate = { canDeactivate: () => false };
    expect(run(component)).toBeFalse();
  });

  it('calls the component canDeactivate exactly once and passes its result through', () => {
    const component: CanComponentDeactivate = { canDeactivate: () => true };
    const spy = spyOn(component, 'canDeactivate').and.returnValue(true);

    const result = run(component);

    expect(spy).toHaveBeenCalledTimes(1);
    expect(result).toBeTrue();
  });

  it('fails open (allows navigation) when the component does not implement canDeactivate', () => {
    const component = {} as unknown as CanComponentDeactivate;
    expect(run(component)).toBeTrue();
  });
});
