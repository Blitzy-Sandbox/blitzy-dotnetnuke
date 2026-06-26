// MIGRATION: Website/admin/Portal/SiteSettings.ascx.vb Page_Load portal-field population
// (objPortalController.GetPortal(intPortalId) L266 -> PortalName L268 / Description L271 / KeyWords L272 /
// GUID L273 / FooterText L276 / Currency L324-327 / ExpiryDate L341-342 / HostFee L344 / HomeDirectory L419)
// -> read-only Angular 19 detail projection. The Web Forms postback/ViewState lifecycle and all editable
// server controls are discarded; this is the read-only "Detail" leg of the CRUD workflow (AAP Section 0.3.6).
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { PortalService } from '../portal.service';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-portal-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, LoadingSpinnerComponent],
  templateUrl: './portal-detail.component.html',
  styleUrl: './portal-detail.component.scss',
})
export class PortalDetailComponent {
  // MIGRATION: DataProvider.Instance() reflection singleton -> injected feature service (AAP Section 0.7.3).
  // The component talks ONLY to the feature service, never HttpClient/ApiService directly.
  protected readonly portalService = inject(PortalService);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * Route ':id' param, bound directly via withComponentInputBinding() (app.config.ts).
   * MIGRATION: replaces SiteSettings.ascx.vb Request.QueryString("pid"); do NOT use ActivatedRoute.
   */
  readonly id = input.required<string>();

  constructor() {
    // MIGRATION: SiteSettings.ascx.vb Page_Load objPortalController.GetPortal(intPortalId) (L266) -> fetch on
    // activation. The service populates its `selected` signal (read in the template). Multi-tenant: only the
    // requested portal's data is fetched (the backend scopes by id/PortalId, AAP Section 0.7.1).
    effect(() => {
      const portalId = this.id();
      if (portalId) {
        this.portalService
          .getById(portalId)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe();
      }
    });
  }
}
