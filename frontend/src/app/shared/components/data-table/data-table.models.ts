// MIGRATION: Type contract for the generic DataTableComponent<T>. Re-expresses the
// column/command/paging model of the legacy DNN <asp:DataGrid AutoGenerateColumns="false">
// grids (Website/admin/Portal/portals.ascx grdPortals, Website/admin/Users/users.ascx
// grdUsers, Website/admin/Security/roles.ascx grdRoles) as strongly-typed, framework-agnostic
// TypeScript. No `any` — generic over the row type T.

/** Sort direction for a column. */
export type SortDirection = 'asc' | 'desc';

/**
 * Cell rendering type.
 * MIGRATION: dnn:textcolumn / asp:BoundColumn -> 'text' | 'number';
 * DataFormatString "{0:0.00}" (portals.ascx HostFee, roles.ascx ServiceFee/TrialFee) -> 'currency';
 * FormatExpiryDate / DisplayDate (portals.ascx Expires, users.ascx CreatedDate/LastLogin) -> 'date';
 * checked.gif / unchecked.gif (users.ascx Authorized, roles.ascx Public/Auto) -> 'boolean'.
 */
export type ColumnType = 'text' | 'number' | 'currency' | 'date' | 'boolean';

/** Horizontal cell alignment. */
export type ColumnAlign = 'left' | 'center' | 'right';

/**
 * Column definition for the DataTable.
 * MIGRATION: one ColumnDef<T> replaces one asp:BoundColumn / asp:TemplateColumn / dnn:textcolumn.
 */
export interface ColumnDef<T> {
  /** Property key on the row used for value access, sorting and filtering. */
  field: keyof T;
  /** Column header text. */
  header: string;
  /** Whether this column is sortable (default: not sortable). */
  sortable?: boolean;
  /** Cell rendering type (default: 'text'). */
  type?: ColumnType;
  /** Optional format token (e.g. a date format passed to the dateFormat pipe). */
  format?: string;
  /** Optional fixed column width (any CSS width value). */
  width?: string;
  /** Optional horizontal alignment. */
  align?: ColumnAlign;
  /** Optional truncation limit for long text cells (feeds the truncate pipe). */
  truncate?: number;
  /**
   * Optional accessor for derived / computed cell values.
   * MIGRATION: replaces DNN template-column helpers such as FormatPortalAliases(PortalID)
   * (portals.ascx) and DisplayAddress(Profile) (users.ascx).
   */
  value?: (row: T) => unknown;
}

/**
 * Row action descriptor (a command button rendered in the actions column).
 * MIGRATION: replaces dnn:imagecommandcolumn (CommandName="Edit"/"Delete"/"UserRoles",
 * KeyField="PortalID"/"UserID"/"RoleID").
 */
export interface RowAction {
  /** Action identifier emitted via rowAction (e.g. 'edit' | 'delete' | 'userRoles'). */
  action: string;
  /** Visible label / accessible name for the action button. */
  label: string;
  /** Optional CSS class or glyph for an icon (rendered aria-hidden). */
  icon?: string;
  /** Optional tooltip / help text (rendered via appTooltip). */
  tooltip?: string;
  /** Optional role(s) required to see this action (gated via *appHasPermission). */
  requiredRoles?: string | string[];
}

/** Current sort state (which column and direction). */
export interface SortState<T> {
  field: keyof T;
  direction: SortDirection;
}

/** Payload emitted when the page changes. */
export interface PageChangeEvent {
  page: number;
  pageSize: number;
}

/**
 * Payload emitted when the filter / search changes.
 * MIGRATION: replaces users.ascx txtSearch + ddlSearchType + btnSearch and the
 * rptLetterSearch A–Z letter filter.
 */
export interface FilterChangeEvent {
  term: string;
  field?: string;
}

/** Payload emitted when a row action button is clicked. */
export interface RowActionEvent<T> {
  action: string;
  row: T;
}
