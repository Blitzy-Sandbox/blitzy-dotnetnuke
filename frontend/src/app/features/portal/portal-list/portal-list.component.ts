// MIGRATION: This component re-expresses the orchestration + validation behavior of the
// legacy DNN Web Forms admin portals grid `Website/admin/Portal/Portals.ascx.vb` (447 lines).
// All postback / ViewState / PortalModuleBase / IActionable / DataGrid / NavigateURL /
// ClientAPI / ProcessModuleLoadException machinery is intentionally discarded; only the
// orchestration and validation rules are preserved (see per-member MIGRATION notes + cited lines).
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { PortalService } from '../portal.service';
import { AuthService } from '../../../core/auth/auth.service';
// MIGRATION: [QA F4-006] reuse the canonical RFC 7807 parser (role-assignment gold-standard pattern) so a
// failed DELETE surfaces a friendly message instead of escaping to the global ErrorHandler.
import { parseProblemDetails } from '../../../core/interceptors/error.interceptor';
import {
  DataTableComponent,
  type ColumnDef,
} from '../../../shared/components/data-table/data-table.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { Portal } from '../../../core/models';

@Component({
  selector: 'app-portal-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DataTableComponent, ConfirmationDialogComponent, LoadingSpinnerComponent],
  templateUrl: './portal-list.component.html',
  styleUrl: './portal-list.component.scss',
})
export class PortalListComponent implements OnInit {
  // inject() DI (NOT constructor injection) — Angular 19 convention.
  protected readonly portalService = inject(PortalService);
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  // MIGRATION: legacy `PageSize` ReadOnly = 20 (Portals.ascx.vb L92-98).
  protected readonly pageSize = 20;

  // Local UI state as signals.
  // MIGRATION: zero-based paging — legacy passed `CurrentPage - 1` to the data layer (L142).
  protected readonly currentPageIndex = signal<number>(0);
  // MIGRATION: filter defaults to "All"; legacy `_Filter = ""` + "All" letter (L46, L170-179, L351-353).
  protected readonly currentFilter = signal<string>('All');

  // Single-delete confirmation state (replaces legacy confirm() JS gate, L299-301/L437).
  protected readonly pendingDelete = signal<Portal | null>(null);
  protected readonly showDeleteConfirm = signal<boolean>(false);

  // MIGRATION: [QA F4-006] friendly delete-failure message (role-assignment gold-standard pattern). Previously
  // onConfirmDelete subscribed SUCCESS-ONLY, so a failed DELETE left the dialog stuck open with no feedback and
  // the HttpErrorResponse escaped to Angular's global ErrorHandler. Now the error callback closes the dialog and
  // sets this signal, which the template renders as a role="alert" banner. Null = no error.
  protected readonly actionError = signal<string | null>(null);

  // MIGRATION: [QA F4-014] in-flight guard for the confirmed delete. Set true synchronously before the DELETE
  // request and cleared in both callbacks; bound to the confirmation dialog's [busy] input (disables Confirm +
  // suppresses re-entrant emits) and consulted by onConfirmDelete to short-circuit duplicate submissions so
  // rapid repeated Confirm clicks fire exactly one DELETE.
  protected readonly deleting = signal<boolean>(false);

  // MIGRATION: HOST-ONLY access — legacy `If Not UserInfo.IsSuperUser Then Redirect("Access Denied")` (L339-341).
  protected readonly isSuperUser = computed<boolean>(
    () => this.auth.currentUser()?.isSuperUser ?? false,
  );

  // MIGRATION: letter/text filter — legacy CreateLetterSearch = A..Z + "All" + "Expired" (L170-179). The legacy
  // "Expired" entry is OMITTED: the frozen backend portal contract (AAP Section 0.3.4) has no `portals/expired`
  // endpoint, so the expired-portals view is out of scope for this migration (the frozen API surface is
  // deliberately CRUD-only; recorded in MIGRATION_NOTES.md). Filter is now All + A..Z.
  // NOTE: typed as `string[]` (not `readonly string[]`) to match DataTableComponent's `filters` input type.
  protected readonly filters: string[] = [
    'All',
    ...Array.from({ length: 26 }, (_, i) => String.fromCharCode(65 + i)),
  ];

  // Columns over Portal model fields (camelCase).
  // MIGRATION: legacy alias column (FormatPortalAliases, L273-288) is OMITTED — no alias field on the
  // Portal resource projection. Legacy FormatExpiryDate (L250-260) formatted the expiry date for display;
  // [QA F4-009] this column now renders through the shared date pipe ('mediumDate') rather than the raw ISO
  // timestamp, consistent with the Users grid Created Date column and closer to the legacy formatted output.
  protected readonly columns: ColumnDef<Portal>[] = [
    { key: 'portalName', header: 'Portal Name' },
    { key: 'description', header: 'Description', truncate: 100 },
    { key: 'expiryDate', header: 'Expiry Date', date: true },
  ];

  ngOnInit(): void {
    // MIGRATION: legacy Page_Load only binds the grid for super users (L339-341, L355-359).
    if (this.isSuperUser()) {
      this.load();
    }
  }

  // Central refresh respecting the active filter (mirrors legacy BindData, L131-158).
  private load(): void {
    const filter = this.currentFilter();
    // MIGRATION: 'All' => empty filter (L351-353); a letter => that letter (L142). Zero-based page index.
    // The legacy "Expired" branch (GetExpiredPortals) is removed — no backend endpoint in the frozen contract.
    const effectiveFilter = filter === 'All' ? '' : filter;
    this.portalService.list(this.currentPageIndex(), this.pageSize, effectiveFilter).subscribe();
  }

  // Zero-based throughout — NO ±1 conversion (L142).
  protected onPage(pageIndex: number): void {
    this.currentPageIndex.set(pageIndex);
    this.load();
  }

  protected onFilter(filter: string): void {
    this.currentFilter.set(filter);
    this.currentPageIndex.set(0);
    this.load();
  }

  protected onView(portal: Portal): void {
    void this.router.navigate(['/portals', portal.portalId]);
  }

  // MIGRATION: legacy edit column navigated to "Site Settings" with `pid=KEYFIELD` (L303-311).
  protected onEdit(portal: Portal): void {
    void this.router.navigate(['/portals', portal.portalId, 'edit']);
  }

  // MIGRATION: legacy ModuleActions AddContent "Signup" (host-only) (L435).
  protected onNew(): void {
    void this.router.navigate(['/portals', 'new']);
  }

  protected onDelete(portal: Portal): void {
    // MIGRATION: PREVENT DELETING THE ACTIVE/CURRENT PORTAL.
    // Legacy hid the per-row delete icon: `delImage.Visible = Not (portal.PortalID = PortalSettings.PortalId)` (L423).
    // The shared data-table exposes only a table-level `showDelete` (no per-row hide), so the rule is
    // enforced here in the handler: for the active portal, do NOT open the confirmation dialog (no-op).
    if (portal.portalId === this.auth.currentUser()?.portalId) {
      return;
    }
    // MIGRATION: delete requires confirmation — legacy OnClickJS confirm() gate (L299-301/L437).
    // MIGRATION: [QA F4-006] clear any stale failure banner from a previous attempt when opening the dialog.
    this.actionError.set(null);
    this.pendingDelete.set(portal);
    this.showDeleteConfirm.set(true);
  }

  protected onConfirmDelete(): void {
    const portal = this.pendingDelete();
    if (portal === null) {
      return;
    }
    // MIGRATION: [QA F4-014] re-entrancy guard -- ignore a confirm while a DELETE is already in flight so rapid
    // repeated Confirm clicks (the dialog stays mounted until the request resolves) fire exactly one request.
    if (this.deleting()) {
      return;
    }
    // MIGRATION: legacy grdPortals_DeleteCommand => PortalController.DeletePortal + BindData refresh (L388-409).
    // MIGRATION: [QA F4-006] subscribe with BOTH next and error (was success-only). On success: close the dialog
    // and reload. On failure: close the dialog, surface a friendly RFC 7807 message via actionError (role-assignment
    // gold-standard pattern) and clear the busy flag -- the error is HANDLED here, so it no longer escapes to
    // Angular's global ErrorHandler. // MIGRATION: [QA F4-014] deleting() gates the dialog's [busy] input.
    this.actionError.set(null);
    this.deleting.set(true);
    this.portalService.delete(portal.portalId).subscribe({
      next: () => {
        this.deleting.set(false);
        this.closeDeleteConfirm();
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.deleting.set(false);
        this.closeDeleteConfirm();
        this.actionError.set(
          this.firstMessage(err, 'The portal could not be deleted. Please try again.'),
        );
      },
    });
  }

  protected onCancelDelete(): void {
    this.closeDeleteConfirm();
  }

  private closeDeleteConfirm(): void {
    this.showDeleteConfirm.set(false);
    this.pendingDelete.set(null);
  }

  // MIGRATION: [QA F4-006] extract the first user-facing message from a backend RFC 7807 failure (reuses the
  // canonical interceptor parser), falling back to the supplied default when the body carries no message (e.g. a
  // status-0 transport failure where err.error is a ProgressEvent). Mirrors role-assignment's firstMessage helper.
  private firstMessage(error: HttpErrorResponse, fallback: string): string {
    const parsed = parseProblemDetails(error.error);
    return parsed.messages.length > 0 ? parsed.messages[0] : fallback;
  }

  // MIGRATION: the legacy bulk "Delete Expired" ModuleAction (Portals.ascx.vb L379-386 / L189-198,
  // PortalController.DeleteExpiredPortals) is NOT migrated — the frozen backend portal contract (AAP Section
  // 0.3.4) exposes CRUD only and has no `portals/expired` endpoint. The action, its confirmation state, and the
  // service call are deliberately not part of this migration's frozen portal contract; this scope decision is
  // recorded in MIGRATION_NOTES.md.
}
