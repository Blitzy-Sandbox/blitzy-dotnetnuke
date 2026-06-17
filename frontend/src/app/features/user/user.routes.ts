import { Routes } from '@angular/router';

/**
 * USER_ROUTES — lazy child route tree for the User administration feature.
 *
 * Mounted by app.routes.ts at path 'users' (already guarded there by
 * canActivate: [authGuard]); these child routes therefore do NOT re-declare the guard.
 *
 * Second-tier lazy loading: each screen is loaded on demand via loadComponent, so the
 * user feature splits into per-screen chunks (AAP §0.3.4 lazy-loading mandate). The
 * parent app.routes.ts reaches this table via
 * `loadChildren: () => import('./features/user/user.routes').then((m) => m.USER_ROUTES)`,
 * so the export name USER_ROUTES is a hard, non-negotiable contract — do not rename.
 *
 * Screen lineage (legacy DNN Website/admin/Users — BEHAVIOR reference, UI parity):
 *   '' (list)        <- ManageUsers.ascx.vb + Users.ascx.vb
 *   'new'/':id'      <- User.ascx.vb
 *   ':id/profile'    <- Membership.ascx.vb
 *
 * Route ordering is correctness-critical: the static 'new' segment MUST precede the
 * parameterized ':id', otherwise /users/new would match ':id' with id === 'new'.
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
