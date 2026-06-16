// Public API barrel for the Portal feature.
// Re-exports ONLY the lightweight, eagerly-importable surface: typed models and the data service.
// Intentionally EXCLUDES:
//   - ./components (screen components are lazy-loaded via portal.routes.ts loadComponent — keep them out of the eager graph)
//   - ./portal.routes (PORTAL_ROUTES is consumed directly by app.routes.ts via a dynamic import, not through this barrel)
export * from './models';
export * from './services';
