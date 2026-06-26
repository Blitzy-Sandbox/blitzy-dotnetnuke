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

import { PortalService } from '../portal.service';
import { AuthService } from '../../../core/auth/auth.service';
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

  // Bulk "Delete Expired" confirmation state (legacy ModuleAction "Delete", L379-386/L437).
  protected readonly showDeleteExpiredConfirm = signal<boolean>(false);

  // MIGRATION: HOST-ONLY access — legacy `If Not UserInfo.IsSuperUser Then Redirect("Access Denied")` (L339-341).
  protected readonly isSuperUser = computed<boolean>(
    () => this.auth.currentUser()?.isSuperUser ?? false,
  );

  // MIGRATION: letter/text filter — legacy CreateLetterSearch = A..Z + "All" + "Expired" (L170-179).
  // NOTE: typed as `string[]` (not `readonly string[]`) to match DataTableComponent's `filters` input type.
  protected readonly filters: string[] = [
    'All',
    ...Array.from({ length: 26 }, (_, i) => String.fromCharCode(65 + i)),
    'Expired',
  ];

  // Columns over Portal model fields (camelCase).
  // MIGRATION: legacy alias column (FormatPortalAliases, L273-288) is OMITTED — no alias field on the
  // Portal resource projection. Legacy FormatExpiryDate (L250-260) is simplified to a plain column.
  protected readonly columns: ColumnDef<Portal>[] = [
    { key: 'portalName', header: 'Portal Name' },
    { key: 'description', header: 'Description', truncate: 100 },
    { key: 'expiryDate', header: 'Expiry Date' },
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
    if (filter === 'Expired') {
      // MIGRATION: legacy `PortalController.GetExpiredPortals()` + pager hidden (L138-140).
      this.portalService.getExpired().subscribe();
      return;
    }
    // MIGRATION: 'All' => empty filter (L351-353); a letter => that letter (L142). Zero-based page index.
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
    this.pendingDelete.set(portal);
    this.showDeleteConfirm.set(true);
  }

  protected onConfirmDelete(): void {
    const portal = this.pendingDelete();
    if (portal === null) {
      return;
    }
    // MIGRATION: legacy grdPortals_DeleteCommand => PortalController.DeletePortal + BindData refresh (L388-409).
    this.portalService.delete(portal.portalId).subscribe(() => {
      this.closeDeleteConfirm();
      this.load();
    });
  }

  protected onCancelDelete(): void {
    this.closeDeleteConfirm();
  }

  private closeDeleteConfirm(): void {
    this.showDeleteConfirm.set(false);
    this.pendingDelete.set(null);
  }

  // MIGRATION: legacy ModuleAction "Delete" => DeleteExpiredPortals() with confirm() (L379-386/L437).
  protected onDeleteExpired(): void {
    this.showDeleteExpiredConfirm.set(true);
  }

  protected onConfirmDeleteExpired(): void {
    // MIGRATION: legacy DeleteExpiredPortals() => PortalController.DeleteExpiredPortals + BindData (L189-198).
    this.portalService.deleteExpired().subscribe(() => {
      this.showDeleteExpiredConfirm.set(false);
      this.load();
    });
  }

  protected onCancelDeleteExpired(): void {
    this.showDeleteExpiredConfirm.set(false);
  }
}
