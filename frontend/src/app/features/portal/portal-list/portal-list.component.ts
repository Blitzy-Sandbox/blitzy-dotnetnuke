import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { PortalService } from '../portal.service';
import { Portal } from '../../../core/models';
import {
  DataTableComponent,
  ColumnDef,
  RowAction,
  RowActionEvent,
  FilterChangeEvent,
} from '../../../shared/components/data-table';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * PortalListComponent — the host-portals administration LIST screen of the migrated
 * Angular 19 SPA (`dnn-migration`).
 *
 * It renders a searchable, paged data table of {@link Portal} records with per-row
 * Edit / Delete actions, a destructive-delete confirmation dialog, a loading overlay,
 * and an "Add New Portal" action — reproducing the workflows of the legacy DotNetNuke
 * Web Forms host-portals screen while remaining a thin, presentation-oriented view.
 *
 * MIGRATION: net-new re-architecture of the DNN Web Forms host-portals list
 * (Website/admin/Portal/portals.ascx `grdPortals` <asp:DataGrid> +
 * Website/admin/Portal/Portals.ascx.vb code-behind). No VB.NET is transliterated; the
 * legacy artifacts are REFERENCE-only for UI/behavior parity (AAP §0.7.1). All HTTP
 * flows through {@link PortalService} (AAP §0.7.1: Angular services handle API
 * communication only; components hold no business logic).
 *
 * CRITICAL CLASS-NAME CONTRACT: the exported class is named exactly
 * `PortalListComponent`; the parent `../portal.routes.ts` lazy-loads it at route path
 * `''` via `import('./portal-list/portal-list.component').then((m) => m.PortalListComponent)`.
 * Renaming this class breaks that route.
 *
 * MIGRATION (access control): the legacy host-only gate — `Portals.ascx.vb` Page_Load
 * (L339) `If Not UserInfo.IsSuperUser Then Response.Redirect(NavigateURL("Access Denied"))`
 * — is NOT re-implemented in this component. In the SPA it is enforced UPSTREAM by the
 * route `authGuard` on the `/portals` branch (owned by parent `portal.routes.ts` /
 * `app.routes.ts`), keeping this component a pure view.
 *
 * MIGRATION (paging / "virtual scrolling"): the legacy `dnn:pagingcontrol`
 * (ctlPagingControl) is replaced by the DataTable's BUILT-IN pagination. The project
 * ships NO `@angular/cdk`, so there is no `cdk-virtual-scroll`; the folder
 * requirement's "virtual scrolling for large lists" is satisfied by that built-in
 * pagination, which bounds the rows rendered at once.
 */
@Component({
  selector: 'app-portal-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DataTableComponent, ConfirmationDialogComponent, LoadingSpinnerComponent],
  template: `
    <section class="portal-list">
      <header class="portal-list__header">
        <h1 class="portal-list__title">Portals</h1>
        <button
          type="button"
          class="portal-list__add"
          (click)="onAddNew()"
          aria-label="Add new portal"
        >
          Add New Portal
        </button>
      </header>

      @if (error(); as message) {
        <div class="portal-list__error" role="alert">{{ message }}</div>
      }

      <!-- position: relative wrapper so the overlay spinner anchors to the table area. -->
      <div class="portal-list__table-wrap">
        <app-data-table
          [data]="portals()"
          [columns]="columns"
          [loading]="loading()"
          [actions]="rowActions"
          [rowKey]="'portalID'"
          (rowAction)="onRowAction($event)"
          (filterChange)="onFilter($event)"
          caption="Portals"
          emptyMessage="No portals found."
        />
        <app-loading-spinner [loading]="loading()" [overlay]="true" message="Loading portals…" />
      </div>

      <app-confirmation-dialog
        [(open)]="confirmOpen"
        [danger]="true"
        title="Delete Portal"
        [message]="deleteMessage()"
        (confirm)="onConfirmDelete()"
        (cancel)="pendingDelete.set(null)"
      />
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .portal-list__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      margin-block-end: 1rem;
    }

    .portal-list__title {
      margin: 0;
      font-size: 1.5rem;
    }

    .portal-list__add {
      padding: 0.5rem 1rem;
      border: 1px solid transparent;
      border-radius: var(--radius, 0.375rem);
      background: var(--color-primary, #2563eb);
      color: var(--color-primary-contrast, #ffffff);
      font: inherit;
      cursor: pointer;
    }

    .portal-list__add:hover {
      filter: brightness(0.95);
    }

    .portal-list__error {
      margin-block-end: 1rem;
      padding: 0.75rem 1rem;
      border: 1px solid var(--color-danger, #dc2626);
      border-radius: var(--radius, 0.375rem);
      background: var(--color-danger-surface, #fef2f2);
      color: var(--color-danger, #b91c1c);
    }

    .portal-list__table-wrap {
      position: relative;
    }
  `,
})
export class PortalListComponent implements OnInit {
  // Injected dependencies (Angular v19 `inject()` idiom, not constructor params).
  // `private readonly`: the colocated spec provides them via TestBed DI and asserts on
  // the spy directly, so these fields are never read from outside the component.
  private readonly portalService = inject(PortalService);
  private readonly router = inject(Router);

  // ---- View state (Angular Signals). PUBLIC (default visibility) so the colocated
  // spec can read `component.portals()` and drive `component.pendingDelete.set(...)`.
  // `readonly` guards the signal REFERENCE only; `.set()` / `.update()` still work. ----

  /** Rows currently displayed in the table. */
  readonly portals = signal<Portal[]>([]);
  /** Whether a portals request (list or delete-triggered reload) is in flight. */
  readonly loading = signal<boolean>(false);
  /** Last error message to surface in the banner, or `null` when there is none. */
  readonly error = signal<string | null>(null);
  /** Current free-text search term fed to the list endpoint. */
  readonly query = signal<string>('');
  /** Whether the delete-confirmation dialog is open (two-way bound to the dialog). */
  readonly confirmOpen = signal<boolean>(false);
  /** The portal awaiting delete confirmation, or `null` when none is pending. */
  readonly pendingDelete = signal<Portal | null>(null);

  // Confirmation-dialog message; MIGRATION: legacy delete confirmed via injected client-side JS confirm() (Page_Init) → SPA confirmation dialog.
  readonly deleteMessage = computed(() => {
    const p = this.pendingDelete();
    return p
      ? `Are you sure you want to delete portal "${p.portalName}" (ID ${p.portalID})? This action cannot be undone.`
      : '';
  });

  // ---- Table configuration ----

  // 7 columns in the EXACT order of the legacy grdPortals grid (portals.ascx), minus
  // the omitted "Portal Aliases" column. Headers use the spaced forms per the folder
  // requirement even though the legacy raw HeaderText values had no spaces.
  //
  // NOTE: the element TYPE is the mutable `ColumnDef<Portal>[]` (not a `ReadonlyArray`)
  // so it binds to DataTableComponent's `columns = input<ColumnDef<T>[]>()` under
  // strictTemplates (a `readonly` array is not assignable to a mutable-array input —
  // TS4104). The `readonly` FIELD modifier still prevents reassignment; the array is
  // never mutated in this component.
  readonly columns: ColumnDef<Portal>[] = [
    { field: 'portalID', header: 'Portal Id', type: 'number', sortable: true }, // legacy PortalId TemplateColumn
    { field: 'portalName', header: 'Title', type: 'text', sortable: true }, // legacy Title (PortalName)
    { field: 'users', header: 'Users', type: 'number', align: 'right' }, // legacy Users textcolumn
    { field: 'pages', header: 'Pages', type: 'number', align: 'right' }, // legacy Pages textcolumn
    { field: 'hostSpace', header: 'Disk Space', type: 'number', align: 'right' }, // legacy DiskSpace (DataField=HostSpace)
    { field: 'hostFee', header: 'Hosting Fee', type: 'currency', align: 'right' }, // legacy HostingFee (DataFormatString {0:0.00} → currency/2-dp)
    { field: 'expiryDate', header: 'Expires', type: 'date' }, // legacy Expires (FormatExpiryDate)
  ];
  // MIGRATION: the legacy grid's "Portal Aliases" TemplateColumn — which rendered an
  // <a> list via FormatPortalAliases(PortalID) (portals.ascx L37-43) — is OMITTED here.
  // The Portal read DTO in core/models has NO aliases field; portal aliases require a
  // separate lookup / dedicated alias screen that is out of scope for this list.

  // Row command buttons. MIGRATION: the two legacy dnn:imagecommandcolumn entries
  // (Edit + Delete, KeyField="PortalID"). No `requiredRoles` is set, so the DataTable
  // renders its plain-button branch (no HasPermissionDirective / AuthService pulled in).
  //
  // NOTE: mutable `RowAction[]` element type (not `ReadonlyArray`) to bind to
  // DataTableComponent's `actions = input<RowAction[]>()` under strictTemplates (TS4104).
  // The `readonly` FIELD modifier still prevents reassignment.
  readonly rowActions: RowAction[] = [
    { action: 'edit', label: 'Edit', icon: 'edit', tooltip: 'Edit portal' }, // legacy Edit imagecommandcolumn
    { action: 'delete', label: 'Delete', icon: 'delete', tooltip: 'Delete portal' }, // legacy Delete imagecommandcolumn
  ];

  // ---- Lifecycle & handlers (all PUBLIC so the colocated spec can invoke them) ----

  // MIGRATION: Portals.ascx.vb Page_Load (L333) non-postback BindData → ngOnInit.
  ngOnInit(): void {
    this.load();
  }

  // MIGRATION: Portals.ascx.vb BindData (L131) — GetPortalsByName(filter+'%', page, size)/GetExpiredPortals → single list() call.
  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.portalService.list({ query: this.query() }).subscribe({
      next: (rows) => {
        this.portals.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        // err is the normalized RFC 7807 ProblemDetails rethrown by ApiService; err?.title is a string.
        this.error.set(err?.title ?? 'Failed to load portals');
        this.loading.set(false);
      },
    });
  }

  onRowAction(event: RowActionEvent<Portal>): void {
    if (event.action === 'edit') {
      // MIGRATION: legacy Edit imagecommandcolumn (EditMode=URL → Site Settings) → navigate to portal-form edit mode.
      this.router.navigate(['/portals', event.row.portalID]);
    } else if (event.action === 'delete') {
      // MIGRATION: legacy Delete imagecommandcolumn → open confirmation dialog before removing.
      this.pendingDelete.set(event.row);
      this.confirmOpen.set(true);
    }
  }

  // MIGRATION: legacy A–Z letter search + All/Expired (CreateLetterSearch L170; GetPortalsByName(filter+'%')) → single text/prefix search.
  // The FilterChangeEvent is an OBJECT { term, field? }; read e.term (NOT the event itself).
  // MIGRATION (gap): the legacy special "Expired" filter (Portals.ascx.vb L138 →
  // GetExpiredPortals) maps to a query param (e.g. { query: 'expired' }) IF the backend
  // supports it; otherwise it is a documented gap and does NOT block the build.
  onFilter(event: FilterChangeEvent): void {
    this.query.set(event.term);
    this.load();
  }

  // MIGRATION: Portals.ascx.vb grdPortals_DeleteCommand (L388) → GetPortal(id) + PortalController.DeletePortal(portal) + rebind.
  // MIGRATION (dropped guard): legacy grdPortals_ItemDataBound (L423) HID the delete
  // button for the CURRENT portal (`delImage.Visible = Not (portal.PortalID =
  // PortalSettings.PortalId)`). The stateless SPA has no single "current portal", so
  // that guard is DROPPED; a defensive guard could be re-added here if a current-portal
  // id ever becomes available to this view.
  onConfirmDelete(): void {
    const portal = this.pendingDelete();
    if (!portal) {
      return;
    }
    this.portalService.remove(portal.portalID).subscribe({
      next: () => {
        this.pendingDelete.set(null);
        this.confirmOpen.set(false);
        this.load();
      },
      error: (err) => this.error.set(err?.title ?? 'Delete failed'),
    });
  }

  // MIGRATION: legacy ModuleActions "Add New Portal" → EditUrl("Signup") create screen → portal-form create mode.
  onAddNew(): void {
    this.router.navigate(['/portals', 'new']);
  }
}
