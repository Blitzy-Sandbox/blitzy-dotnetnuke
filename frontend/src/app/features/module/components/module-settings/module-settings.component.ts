import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';

import { ProblemDetails, summarizeProblem } from '../../../../core/services/api.service';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { ValidationHighlightDirective } from '../../../../shared/directives/validation-highlight';
import { Module, ModuleDefinition, UpdateModuleDto } from '../../models';
import { ModuleService } from '../../services';

/**
 * Strongly-typed reactive-form contract for the module configuration editor.
 * MIGRATION: reproduces ONLY the in-scope settings of legacy ModuleSettings.ascx.vb
 * (cache time, start/end scheduling, container, display options, inherit-view permissions);
 * out-of-scope concerns (custom settings, move/copy, default-module flags) are not edited here.
 */
interface ModuleSettingsForm {
  cacheTime: FormControl<number>;
  startDate: FormControl<string>;
  endDate: FormControl<string>;
  containerSrc: FormControl<string | null>;
  inheritViewPermissions: FormControl<boolean>;
  displayTitle: FormControl<boolean>;
  displayPrint: FormControl<boolean>;
  displaySyndicate: FormControl<boolean>;
}

// MIGRATION: legacy dgPermissions row model. The 5-method ModuleService exposes no
// permission-collection endpoint and the Module model omits permission types (AAP minimalism),
// so this is a component-local view-model placeholder pending a dedicated endpoint.
interface ModulePermissionRow {
  roleId: number;
  roleName: string;
  view: boolean;
  edit: boolean;
}

/**
 * ModuleSettingsComponent — the module configuration editor.
 *
 * Reproduces the configuration concerns of legacy DNN `Website/admin/Modules/ModuleSettings.ascx.vb`
 * (cache time, start/end scheduling, container, display options, inherit-view permissions, delete)
 * as a standalone Angular 19 reactive-form screen with UI functional parity (AAP §0.3.4 / §0.7.1).
 *
 * MIGRATION: the legacy Web Forms postback/ViewState + code-behind event model is replaced by a
 * stateless typed reactive form; data access flows exclusively through {@link ModuleService}
 * (no direct HttpClient), and every server error is surfaced as RFC 7807 {@link ProblemDetails}.
 */
@Component({
  selector: 'app-module-settings',
  // MIGRATION: legacy admin/tab-admin access (ModuleSettings.ascx.vb L191 PortalSecurity.IsInRoles)
  // is enforced SERVER-SIDE via JWT/RBAC policy on the module endpoints. The client-side RBAC UI gate
  // (HasPermissionDirective / *appHasPermission) is intentionally NOT imported here: the committed
  // template (module-settings.component.html, see its M18 notes) does not bind appHasPermission, so
  // importing the directive would be an unused standalone import and break the zero-warning strict
  // build (unusedStandaloneImports). Re-add HasPermissionDirective to imports[] if the template
  // re-applies the gate.
  imports: [
    ReactiveFormsModule,
    FormControlsComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    ValidationHighlightDirective,
  ],
  templateUrl: './module-settings.component.html',
  styleUrl: './module-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ModuleSettingsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly moduleService = inject(ModuleService);

  readonly moduleId = signal(0);
  readonly module = signal<Module | null>(null);
  // MIGRATION: ModuleService has no getModuleDefinition; stays null in production (cache row shown)
  // and is settable in tests to exercise the hide-when-null branch.
  readonly moduleDefinition = signal<ModuleDefinition | null>(null);
  // MIGRATION: permission-collection endpoint pending; grid view-model defaults empty.
  readonly permissions = signal<ModulePermissionRow[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string[]> | null>(null);
  readonly showDeleteDialog = signal(false);

  readonly form = new FormGroup<ModuleSettingsForm>({
    cacheTime: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    startDate: new FormControl('', { nonNullable: true }),
    endDate: new FormControl('', { nonNullable: true }),
    containerSrc: new FormControl<string | null>(null),
    inheritViewPermissions: new FormControl(false, { nonNullable: true }),
    displayTitle: new FormControl(true, { nonNullable: true }),
    displayPrint: new FormControl(false, { nonNullable: true }),
    displaySyndicate: new FormControl(false, { nonNullable: true }),
  });

  // MIGRATION: legacy hides rowCache when ModuleDefinition.DefaultCacheTime == Null.NullInteger.
  readonly showCacheRow = computed(() => {
    const definition = this.moduleDefinition();
    return definition === null || definition.defaultCacheTime !== null;
  });

  // MIGRATION: legacy cmdDelete.Visible = False when ModuleId = -1 (new/unsaved module).
  readonly canDelete = computed(() => this.moduleId() > 0);

  readonly deleteMessage = computed(
    () =>
      `Are you sure you want to delete "${this.module()?.moduleTitle ?? 'this module'}"? ` +
      'This action cannot be undone.',
  );

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('moduleId');
    const id = Number(rawId);
    if (rawId === null || Number.isNaN(id)) {
      this.loadError.set('Invalid module identifier.');
      this.loading.set(false);
      return;
    }
    this.moduleId.set(id);
    this.loadModule(id);
  }

  private loadModule(id: number): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.moduleService.getModule(id).subscribe({
      next: (module) => {
        this.module.set(module);
        this.patchForm(module);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(summarizeProblem(problem, 'Failed to load the module.'));
        this.loading.set(false);
      },
    });
  }

  private patchForm(module: Module): void {
    this.form.patchValue({
      cacheTime: module.cacheTime,
      startDate: this.toDateInput(module.startDate),
      endDate: this.toDateInput(module.endDate),
      containerSrc: module.containerSrc,
      inheritViewPermissions: module.inheritViewPermissions,
      displayTitle: module.displayTitle,
      displayPrint: module.displayPrint,
      displaySyndicate: module.displaySyndicate,
    });
  }

  onSubmit(): void {
    this.serverErrors.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const current = this.module();
    if (current === null) {
      return;
    }

    const raw = this.form.getRawValue();
    // MIGRATION: build the full UpdateModuleDto explicitly — carry over read-only fields from the
    // loaded module, merge edited settings; force moduleID from the route (legacy objModule.ModuleID = ModuleId).
    const request: UpdateModuleDto = {
      moduleID: this.moduleId(),
      moduleOrder: current.moduleOrder,
      paneName: current.paneName,
      moduleTitle: current.moduleTitle,
      cacheTime: this.parseCacheTime(raw.cacheTime),
      alignment: current.alignment,
      color: current.color,
      border: current.border,
      iconFile: current.iconFile,
      allTabs: current.allTabs,
      visibility: current.visibility,
      displayTitle: raw.displayTitle,
      displayPrint: raw.displayPrint,
      displaySyndicate: raw.displaySyndicate,
      header: current.header,
      footer: current.footer,
      // MIGRATION: legacy empty date -> Null.NullDate; empty input -> null here.
      startDate: raw.startDate ? raw.startDate : null,
      endDate: raw.endDate ? raw.endDate : null,
      containerSrc: raw.containerSrc,
      inheritViewPermissions: raw.inheritViewPermissions,
    };

    this.saving.set(true);
    this.moduleService.updateModule(this.moduleId(), request).subscribe({
      next: () => {
        this.saving.set(false);
        // MIGRATION: legacy cmdUpdate_Click redirected to NavigateURL() after UpdateModule.
        void this.router.navigate(['/modules']);
      },
      error: (problem: ProblemDetails) => {
        this.serverErrors.set(problem.errors ?? null);
        this.saving.set(false);
      },
    });
  }

  onDeleteClick(): void {
    this.showDeleteDialog.set(true);
  }

  onConfirmDelete(): void {
    this.showDeleteDialog.set(false);
    // MIGRATION: legacy cmdDelete_Click -> ModuleController.DeleteTabModule(TabId, ModuleId).
    this.moduleService.deleteModule(this.moduleId()).subscribe({
      next: () => {
        void this.router.navigate(['/modules']);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(summarizeProblem(problem, 'Failed to delete the module.'));
      },
    });
  }

  onCancelDelete(): void {
    this.showDeleteDialog.set(false);
  }

  onCancel(): void {
    // MIGRATION: legacy cmdCancel_Click -> Response.Redirect(NavigateURL()).
    void this.router.navigate(['/modules']);
  }

  // MIGRATION: legacy `If txtCacheTime.Text <> "" Then Int32.Parse(...) Else 0`.
  private parseCacheTime(value: number): number {
    return Number.isFinite(value) ? Math.trunc(value) : 0;
  }

  // MIGRATION: legacy displayed dates only when Not Null.IsNull(date); here ISO -> yyyy-MM-dd, null -> ''.
  private toDateInput(value: string | null): string {
    return value ? value.substring(0, 10) : '';
  }
}
