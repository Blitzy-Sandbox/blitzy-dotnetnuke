// MIGRATION: Replaces ManageUsers.ascx.vb tab navigation (User/Profile panels) + Users.ascx.vb list — now standalone lazy routes.
//
// The legacy DotNetNuke admin "user management" experience was two Web Forms user controls:
//   • Website/admin/Users/Users.ascx.vb        (class UserAccounts : PortalModuleBase) — the registered-user grid/list.
//   • Website/admin/Users/ManageUsers.ascx.vb  (class ManageUsers  : UserModuleBase)  — the tabbed editor that switched
//     between the "User" (account) panel and the "Profile" panel via ViewState/postback.
// Both are re-expressed here as Angular 19 per-route, lazily loaded standalone components. All ViewState/postback/tab
// machinery is discarded; navigation alone is modeled. This route table is mounted by app.routes.ts at the `users` path
// with `canActivate: [authGuard]` applied UPSTREAM — the guard is intentionally NOT re-declared here.
import { Routes } from '@angular/router';

/**
 * Lazy-loaded child routes for the `user` feature.
 *
 * Mounted by the root router via
 * `loadChildren: () => import('./features/user/user.routes').then((m) => m.USER_ROUTES)`
 * at the `users` path, behind `authGuard` (declared on the parent route).
 *
 * `withComponentInputBinding()` (enabled in `app.config.ts`) binds the `:id` path parameter directly to the target
 * component's `id` input (`UserFormComponent`/`ProfileComponent` each declare `readonly id = input<string>()`), so no
 * `resolve`/manual parameter wiring is required.
 *
 * Resolved absolute URLs:
 *   /users              → UserListComponent  (registered-user grid)
 *   /users/new          → UserFormComponent  (create mode)
 *   /users/:id/edit     → UserFormComponent  (edit mode, `id` input bound)
 *   /users/:id/profile  → ProfileComponent   (profile editor, `id` input bound)
 *   /users/:id          → UserFormComponent  (edit/detail mode, `id` input bound)
 *
 * ORDERING (critical — Angular matches order-sensitively at the same path depth): the static single-segment `new`
 * route MUST precede the dynamic single-segment `:id` route, otherwise `/users/new` would match `:id` with
 * `id === 'new'`. The two-segment routes (`:id/edit`, `:id/profile`) are unambiguous against the single-segment
 * `:id` but are kept ahead of it for readability. There is no separate detail component in scope — the bare `:id`
 * route reuses `UserFormComponent`, which derives create-vs-edit-vs-detail from the presence of its `id` input.
 */
export const USER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./user-list/user-list.component').then((m) => m.UserListComponent),
    title: 'Users',
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./user-form/user-form.component').then((m) => m.UserFormComponent),
    title: 'New User',
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./user-form/user-form.component').then((m) => m.UserFormComponent),
    title: 'Edit User',
  },
  {
    path: ':id/profile',
    loadComponent: () =>
      import('./profile/profile.component').then((m) => m.ProfileComponent),
    title: 'User Profile',
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./user-form/user-form.component').then((m) => m.UserFormComponent),
    title: 'User',
  },
];
