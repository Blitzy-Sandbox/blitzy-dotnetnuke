import type { Routes } from '@angular/router';

// MIGRATION: DNN Web Forms had no client router. These routes reproduce the legacy Admin > Pages
// (Tabs) navigation flow: page list -> Add Page / Edit Page / Page Settings. Each screen is lazily
// loaded with `loadComponent` (per-screen code-split chunks, keeping the initial bundle minimal).
// The parent `tabs` route in `app.routes.ts` applies `authGuard`, so these child routes intentionally
// declare NO `canActivate` (the guard already protects the whole subtree). Route order: the concrete
// `new` path precedes the parameterized `:id/...` paths; there is no bare `:id` route.
export const TAB_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./components/tab-list/tab-list.component').then((m) => m.TabListComponent),
    title: 'Pages',
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./components/tab-form/tab-form.component').then((m) => m.TabFormComponent),
    title: 'Add Page',
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./components/tab-form/tab-form.component').then((m) => m.TabFormComponent),
    title: 'Edit Page',
  },
  {
    path: ':id/settings',
    loadComponent: () =>
      import('./components/tab-settings/tab-settings.component').then((m) => m.TabSettingsComponent),
    title: 'Page Settings',
  },
];
