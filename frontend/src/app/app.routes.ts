import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'portals', pathMatch: 'full' },
  {
    path: 'roles',
    loadChildren: () => import('./features/role/role.routes').then(m => m.roleRoutes)
  }
];
