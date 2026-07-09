import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ModuleService } from '../module.service';
import { Module } from '../../../core/models';
import {
  DataTableComponent,
  ColumnDef,
  RowAction,
  RowActionEvent,
  FilterChangeEvent,
} from '../../../shared/components/data-table';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

// MIGRATION: legacy DNN VisibilityState (numeric code on ModuleInfo.Visibility) had no
// TS enum in the migrated Module model (it is kept as `number`). Map the code to a label
// for display parity: 0 = Maximized, 1 = Minimized, 2 = None.
function visibilityLabel(visibility: number): string {
  switch (visibility) {
    case 0:
      return 'Maximized';
    case 1:
      return 'Minimized';
    case 2:
      return 'None';
    default:
      return String(visibility);
  }
}

/**
 * ModuleListComponent — module inventory list/grid screen.
 *
 * MIGRATION: re-expresses the legacy DNN module inventory (Website/admin/Modules/**).
 * Web Forms List/Grid page -> GET /api/modules (via ModuleService.getModules). Postback/
 * ViewState eliminated -> stateless HTTP + Signals. The legacy delete-with-confirm workflow
 * (ModuleSettings.ascx.vb L205 ClientAPI.AddButtonConfirm + L305 ModuleController.DeleteTabModule)
 * becomes an ARIA confirmation dialog + DELETE /api/modules/{id} (ModuleService.deleteModule),
 * then a list reload (the legacy Response.Redirect reload).
 *
 * Presentation/orchestration only — all HTTP flows through the injected ModuleService (AAP §0.7.1).
 */
@Component({
  selector: 'app-module-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DataTableComponent, ConfirmationDialogComponent, LoadingSpinnerComponent],
  template: `
    <div class="module-list">
      <header class="module-list__header">
        <h1 class="module-list__title">Modules</h1>
        <button type="button" class="module-list__add" (click)="onAddNew()" aria-label="Add new module">
          New Module
        </button>
      </header>

      @if (error(); as message) {
        <div class="module-list__error" role="alert">{{ message }}</div>
      }

      <div class="module-list__table-wrap">
        <app-data-table
          [data]="modules()"
          [columns]="columns"
          [loading]="loading()"
          [actions]="rowActions"
          [rowKey]="'moduleID'"
          (rowAction)="onRowAction($event)"
          (filterChange)="onFilterChange($event)"
          caption="Modules"
          emptyMessage="No modules found." />
        <app-loading-spinner [loading]="loading()" [overlay]="true" message="Loading modules…" />
      </div>

      <app-confirmation-dialog
        [(open)]="confirmOpen"
        [danger]="true"
        title="Delete Module"
        [message]="deleteMessage()"
        confirmLabel="Delete"
        (confirm)="onConfirmDelete()"
        (cancel)="pendingDelete.set(null)" />
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .module-list__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1rem;
        margin-bottom: 1rem;
      }

      .module-list__title {
        margin: 0;
        font-size: 1.5rem;
      }

      .module-list__add {
        padding: 0.5rem 1rem;
        font: inherit;
        border: 1px solid transparent;
        border-radius: var(--radius);
        background: var(--color-primary);
        color: var(--color-primary-contrast);
        cursor: pointer;
      }

      .module-list__error {
        margin-bottom: 1rem;
        padding: 0.625rem 0.875rem;
        border: 1px solid var(--color-danger);
        border-radius: var(--radius);
        background: var(--color-danger-bg);
        color: var(--color-danger);
      }

      .module-list__table-wrap {
        position: relative;
      }
    `,
  ],
})
export class ModuleListComponent implements OnInit {
  private readonly moduleService = inject(ModuleService);
  private readonly router = inject(Router);

  // ---- State (Signals) — PUBLIC so the spec can read/drive them ----
  readonly modules = signal<Module[]>([]);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly confirmOpen = signal<boolean>(false);
  readonly pendingDelete = signal<Module | null>(null);

  // MIGRATION: legacy Localization.GetString("DeleteItem") confirm text -> a descriptive per-row message.
  readonly deleteMessage = computed(() => {
    const m = this.pendingDelete();
    return m ? `Are you sure you want to delete '${m.moduleTitle}'?` : '';
  });

  // ---- Columns (ColumnType: 'text'|'number'|'currency'|'date'|'boolean'). Each field is a real keyof Module. ----
  // NOTE: the element TYPE is the mutable `ColumnDef<Module>[]` (not a `ReadonlyArray`) so it
  // binds to DataTableComponent's `columns = input<ColumnDef<T>[]>()` under strictTemplates (TS4104/NG4).
  readonly columns: ColumnDef<Module>[] = [
    { field: 'moduleTitle', header: 'Title', sortable: true, type: 'text' },
    { field: 'friendlyName', header: 'Module', sortable: true, type: 'text' },
    { field: 'paneName', header: 'Pane', type: 'text' },
    // MIGRATION: legacy VisibilityState numeric code -> label via a derived value accessor.
    { field: 'visibility', header: 'Visibility', value: (row) => visibilityLabel(row.visibility) },
    { field: 'allTabs', header: 'All Pages', type: 'boolean' },
    { field: 'cacheTime', header: 'Cache (s)', type: 'number' },
    { field: 'startDate', header: 'Start', type: 'date' },
    { field: 'endDate', header: 'End', type: 'date' },
  ];

  // ---- Row actions (no requiredRoles -> DataTable renders plain buttons, no AuthService dependency). ----
  // NOTE: mutable `RowAction[]` element type (not `ReadonlyArray`) to bind to
  // DataTableComponent's `actions = input<RowAction[]>()` under strictTemplates (TS4104/NG4).
  readonly rowActions: RowAction[] = [
    { action: 'edit', label: 'Edit', tooltip: 'Edit module' },
    { action: 'delete', label: 'Delete', tooltip: 'Delete module' },
  ];

  ngOnInit(): void {
    this.load();
  }

  // MIGRATION: legacy module-inventory bind (Website/admin/Modules/**) -> GET /api/modules.
  // Server-search parity (AAP §0.7.2 Search/Filter -> GET /api/modules?query=...): when a
  // filter term is present it is passed as { query }. Client-side filtering of the returned
  // set is harmless because the server-filtered rows still contain the term.
  private load(query?: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.moduleService.getModules(query ? { query } : undefined).subscribe({
      next: (rows) => {
        this.modules.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        // err is the normalized RFC 7807 ProblemDetails rethrown by ApiService; err?.title is a string.
        this.error.set(err?.title ?? 'Failed to load modules');
        this.loading.set(false);
      },
    });
  }

  onRowAction(event: RowActionEvent<Module>): void {
    if (event.action === 'edit') {
      // MIGRATION: legacy edit link -> navigate to the module form (edit mode) at /modules/{id}.
      this.router.navigate(['/modules', event.row.moduleID]);
    } else if (event.action === 'delete') {
      // MIGRATION: legacy ClientAPI.AddButtonConfirm(cmdDelete, ...) -> open the confirmation dialog.
      this.pendingDelete.set(event.row);
      this.confirmOpen.set(true);
    }
  }

  // MIGRATION: legacy grid search -> re-query with the term (GET /api/modules?query=...).
  // FilterChangeEvent is an OBJECT { term, field? } — read event.term.
  onFilterChange(event: FilterChangeEvent): void {
    this.load(event.term);
  }

  // MIGRATION: ModuleSettings.ascx.vb cmdDelete_Click -> ModuleController.DeleteTabModule(TabId, ModuleId)
  // (L305) -> DELETE /api/modules/{id}; the Response.Redirect reload -> load().
  onConfirmDelete(): void {
    const target = this.pendingDelete();
    if (!target) {
      return;
    }
    this.moduleService.deleteModule(target.moduleID).subscribe({
      next: () => {
        this.pendingDelete.set(null);
        this.confirmOpen.set(false);
        this.load();
      },
      error: (err) => this.error.set(err?.title ?? 'Delete failed'),
    });
  }

  // MIGRATION: legacy "Add Module" -> POST /api/modules create form at /modules/new.
  onAddNew(): void {
    this.router.navigate(['/modules', 'new']);
  }
}
