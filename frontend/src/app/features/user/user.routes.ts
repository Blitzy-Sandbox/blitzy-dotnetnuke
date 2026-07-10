import { Routes } from '@angular/router';

// MIGRATION: The legacy DNN "Manage Users" navigation
// (Website/admin/Users/ManageUsers.ascx.vb) switched between the Users list,
// User add/edit, Membership, Profile and Password screens via Web Forms postback
// tabs and ?userid= querystrings. That stateful postback navigation is eliminated
// (AAP §0.6.3) in favor of stateless, lazy-loaded standalone routes:
//   Users.ascx (list)     -> ''             (UserListComponent)
//   User.ascx (add)       -> 'new'          (UserFormComponent, create mode)
//   User.ascx (edit)      -> ':id'          (UserFormComponent, edit mode)
//   Password.ascx         -> ':id/password' (ChangePasswordComponent)
// This area is protected by the parent route's canActivate:[authGuard]
// (frontend/src/app/app.routes.ts) — the guard is NOT re-declared here.
export const USER_ROUTES: Routes = [
  {
    path: '',
    title: 'Users',
    loadComponent: () =>
      import('./user-list/user-list.component').then((m) => m.UserListComponent),
  },
  {
    // Static segment MUST precede the ':id' param route so '/users/new' is not
    // captured as an edit with id === 'new'.
    path: 'new',
    title: 'New User',
    loadComponent: () =>
      import('./user-form/user-form.component').then((m) => m.UserFormComponent),
  },
  {
    path: ':id/password',
    title: 'Change Password',
    loadComponent: () =>
      import('./change-password/change-password.component').then(
        (m) => m.ChangePasswordComponent,
      ),
  },
  {
    path: ':id',
    title: 'Edit User',
    loadComponent: () =>
      import('./user-form/user-form.component').then((m) => m.UserFormComponent),
  },
];
