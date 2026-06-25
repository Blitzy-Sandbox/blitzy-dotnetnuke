// Barrel for frontend/src/app/core/models.
// MIGRATION: net-new convenience barrel (no legacy equivalent). Wildcard re-exports keep this safe under isolatedModules.
// Enables clean imports, e.g. `import { Portal, User, ApiEnvelope, ProblemDetails } from '../core/models';`
export * from './role.model';
export * from './portal.model';
export * from './problem-details.model';
export * from './api-envelope.model';
export * from './permission.model';
export * from './user.model';
export * from './module.model';
export * from './tab.model';
export * from './auth.model';
