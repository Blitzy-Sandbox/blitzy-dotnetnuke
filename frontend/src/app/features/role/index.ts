// Role feature public API.
// MIGRATION: Components are intentionally NOT re-exported here — they are reached only via
// lazy `loadComponent` in `role.routes.ts`, so exporting them would defeat route-level code-splitting.
export * from './models';
export * from './services';
