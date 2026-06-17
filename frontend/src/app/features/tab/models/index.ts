// Tab feature model barrel.
// MIGRATION: `TabType` is a NUMERIC enum (a runtime value), so it is re-exported with a plain
// `export` (an `export type` re-export would erase the runtime value under `isolatedModules`). The
// remaining symbols are pure types and are re-exported with `export type` per `isolatedModules`.
export { TabType } from './tab.model';
export type { Tab, CreateTab, UpdateTab } from './tab.model';
