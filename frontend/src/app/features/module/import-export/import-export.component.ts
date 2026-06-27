// MIGRATION: Website/admin/Modules/Export.ascx.vb (227 lines) + Import.ascx.vb (245 lines) -> Angular 19
// ImportExportComponent. The legacy controls performed IPortable module-content export/import: the
// content.<CleanName(ModuleName)>.<CleanName(file)>.xml naming convention, the
// PortalController.HasSpaceAvailable disk-quota check, XmlDocument root-`type` validation, version extraction,
// and IPortable.ExportModule / ImportModule. All Web Forms postback / ViewState / Skin.AddModuleMessage / .resx
// machinery is discarded.
//
// SCOPE BOUNDARY (AAP Section 0.6.2 -- Explicitly Out of Scope): legacy DNN module content import/export is
// implemented through IPortable.ExportModule / ImportModule, dispatched via the reflection-based
// BusinessControllerClass module-loader. AAP Section 0.6.2 places "the legacy module-loader infrastructure"
// explicitly OUT OF SCOPE for this migration phase, so there is deliberately NO export/import endpoint on the
// frozen ModulesController (CRUD + by-portal/by-tab only) and IModuleService has no IPortable counterpart. This
// screen therefore presents a documented scope-boundary notice -- a deliberate scope decision, NOT incomplete or
// deferred work. The target module is still pre-loaded (GET /api/modules/{id}?portalId=) so the screen can name
// it and exercise the tenant-scoped read; module create/configure/remove remain fully supported. Recorded in
// MIGRATION_NOTES.md.
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';

import { ModuleService } from '../module.service';
import { AuthService } from '../../../core/auth/auth.service';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-import-export',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LoadingSpinnerComponent],
  templateUrl: './import-export.component.html',
  styleUrl: './import-export.component.scss',
})
export class ImportExportComponent {
  private readonly moduleService = inject(ModuleService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  // MIGRATION: withComponentInputBinding() (app.config.ts) binds the :id route param to this input. The legacy
  // controls read the target module via the ModuleId query string; here it arrives as the :id route param
  // (STRING) and is parsed to a number for the moduleService.getById call.
  readonly id = input.required<string>();

  /** The pre-loaded module (its title is surfaced in the scope-boundary notice). Held by ModuleService.selected. */
  readonly module = this.moduleService.selected;

  /** True while the module pre-load is in flight. */
  readonly loading = this.moduleService.loading;

  constructor() {
    // MIGRATION: Export/Import Page_Load -> GetModule(ModuleId, TabId, False) pre-load (Export.ascx.vb L82-86 /
    // Import.ascx.vb L75-79). Load the module so the scope-boundary notice can name the target module. portalId
    // (multi-tenant scoping, AAP Section 0.7.1) is REQUIRED on the backend GET /api/modules/{id}; it is sourced
    // from the authenticated user's portal context. Reading the required `id` input inside the effect is safe
    // because the router binds it before the first change detection (and tests setInput() before
    // detectChanges()). The `selected` signal is intentionally NOT read here, so the effect does not re-fire
    // when the load completes.
    effect(() => {
      const moduleId = Number(this.id());
      const portalId = this.auth.currentUser()?.portalId ?? -1;
      this.moduleService
        .getById(moduleId, portalId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          error: () => {
            // The global error interceptor surfaces/logs the ProblemDetails; nothing else to do here.
          },
        });
    });
  }

  // MIGRATION: Export/Import cmdCancel_Click -> Response.Redirect(NavigateURL()). Returns to module settings.
  back(): void {
    void this.router.navigate(['/modules', this.id(), 'settings']);
  }
}
