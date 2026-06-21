import { Routes } from '@angular/router';

/**
 * USER_ROUTES — lazy child route tree for the User administration feature.
 *
 * Mounted by app.routes.ts at path 'users' (already guarded there by
 * canActivate: [authGuard]); these child routes therefore do NOT re-declare the guard.
 *
 * Second-tier lazy loading: each screen is loaded on demand via loadComponent, so the
 * user feature splits into per-screen chunks (AAP §0.3.4 lazy-loading mandate). The
 * top-level app.routes.ts owns the global '**' fallback, so no feature-level wildcard
 * is declared here.
 *
 * Screen lineage (legacy DNN Website/admin/Users — BEHAVIOR reference, UI parity):
 *   '' (list)        <- ManageUsers.ascx.vb + Users.ascx.vb   (grid: filter / search / page / delete)
 *   'new' / ':id'    <- User.ascx.vb                           (create + edit, one reused form component)
 *   ':id/profile'    <- Membership.ascx.vb                     (membership + profile management)
 *
 * Route ORDERING is correctness-critical: Angular matches top-to-bottom, so the static
 * segment 'new' MUST precede the parameterized ':id' (otherwise '/users/new' would match
 * ':id' with id === 'new'). The two-segment ':id/profile' does not collide with ':id'.
 */
export const USER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./components/user-list/user-list.component').then((m) => m.UserListComponent),
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./components/user-form/user-form.component').then((m) => m.UserFormComponent),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./components/user-form/user-form.component').then((m) => m.UserFormComponent),
  },
  {
    path: ':id/profile',
    loadComponent: () =>
      import('./components/user-profile/user-profile.component').then(
        (m) => m.UserProfileComponent,
      ),
  },
];
