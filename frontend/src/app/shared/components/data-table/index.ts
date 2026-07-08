/**
 * Public API barrel for the `data-table` component folder.
 *
 * Re-exports the standalone `DataTableComponent` (a value) together with its
 * strongly-typed contract symbols (`ColumnDef`, `RowAction`, `RowActionEvent`,
 * `SortState`, `SortDirection`, `ColumnType`, `ColumnAlign`, `PageChangeEvent`,
 * `FilterChangeEvent`) so consumers (features/portal, features/user, features/role,
 * features/module and any layout/* screen) can import from the folder path:
 *
 *   import { DataTableComponent, ColumnDef, RowAction } from '.../shared/components/data-table';
 *
 * This mirrors the sibling `shared/*` barrel convention (`shared/pipes/index.ts`,
 * `shared/directives/index.ts`, `shared/components/confirmation-dialog/index.ts`)
 * and `core/models/index.ts`.
 *
 * Star (`export *`) re-exports are `isolatedModules`-safe for mixing the component
 * class value with the type-only model symbols; no per-symbol `export type` is
 * required. Pure aggregator: no logic, no side effects, no default export. The
 * `.spec` test file is intentionally NOT re-exported, keeping test code out of the
 * public barrel and the production bundle. `DataTableComponent` is a standalone
 * (Angular v19) component -- consumers still add it to their own component
 * `imports: [...]` array; this barrel only simplifies the import path.
 */
export * from './data-table.component';
export * from './data-table.models';
