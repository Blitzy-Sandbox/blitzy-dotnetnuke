/**
 * Public API barrel for the confirmation-dialog component folder.
 *
 * Re-exports `ConfirmationDialogComponent` so consumers (features/*, layout/*) can
 * import it from the folder path rather than the concrete file:
 *
 *   import { ConfirmationDialogComponent } from '.../shared/components/confirmation-dialog';
 *
 * This mirrors the sibling `shared/directives/index.ts` barrel convention for
 * consistent, ergonomic imports across the `shared/` tree.
 *
 * Pure aggregator: no imports, no logic, no side effects, no default export. The
 * `.spec` test file is intentionally NOT re-exported. `ConfirmationDialogComponent`
 * is a standalone (Angular v19) component -- consumers still add it to their own
 * component `imports: [...]` array; this barrel only simplifies the import path, it
 * registers nothing.
 */
export * from './confirmation-dialog.component';
