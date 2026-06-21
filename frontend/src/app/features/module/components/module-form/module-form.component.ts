// MIGRATION: Replaces the legacy Web Forms module editor Website/admin/Modules/ModuleSettings.ascx.vb
// (postback/ViewState/code-behind) with a stateless, standalone Angular 19 reactive form.
//   - cmdUpdate_Click            -> onSubmit()
//   - Page.IsValid gate (L328)   -> reactive validators (form.invalid guard + markAllAsTouched)
//   - ModuleController.AddModule/UpdateModule -> ModuleService.createModule/updateModule (BFF REST)
// The legacy permissions grid (dgPermissions/chkInheritPermissions), scheduling (txtStartDate/txtEndDate),
// cache-time (txtCacheTime), container/skin (ctlModuleContainer), custom (ctlSpecific) settings, and
// move/copy-to-all-tabs (cboTab/chkDefault/chkAllModules) logic are OUT OF SCOPE (AAP §0.2.2): they are
// not part of the Module/CreateModuleDto/UpdateModuleDto contract. Non-edited-but-required DTO fields are
// defaulted in create mode and carried verbatim from the loaded entity in edit mode.
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { CreateModuleDto, Module, UpdateModuleDto, VisibilityState } from '../../models';
import { ModuleService } from '../../services';
import { ProblemDetails } from '../../../../core/services/api.service';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { ValidationHighlightDirective } from '../../../../shared/directives/validation-highlight';

/**
 * Strongly-typed reactive form model exposing ONLY the editable subset of a module — the fields the
 * legacy cmdUpdate_Click maps (L343-382) that also exist on the Module/DTO wire contract. String fields
 * are nullable to mirror the model; `visibility` and the four display/placement flags are non-nullable.
 */
interface ModuleFormModel {
  moduleTitle: FormControl<string | null>;
  alignment: FormControl<string | null>;
  color: FormControl<string | null>;
  border: FormControl<string | null>;
  iconFile: FormControl<string | null>;
  header: FormControl<string | null>;
  footer: FormControl<string | null>;
  visibility: FormControl<VisibilityState>;
  allTabs: FormControl<boolean>;
  displayTitle: FormControl<boolean>;
  displayPrint: FormControl<boolean>;
  displaySyndicate: FormControl<boolean>;
}

/** A selectable visibility choice rendered by the template `<select>` (numeric enum value via `[ngValue]`). */
interface VisibilityOption {
  readonly value: VisibilityState;
  readonly label: string;
}

@Component({
  selector: 'app-module-form',
  templateUrl: './module-form.component.html',
  styleUrl: './module-form.component.scss',
  imports: [ReactiveFormsModule, FormControlsComponent, ValidationHighlightDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ModuleFormComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly moduleService = inject(ModuleService);

  /** Target module id (edit mode); `null` in create mode. */
  readonly moduleId = signal<number | null>(null);

  /** True when editing an existing module (drives the heading and the submit-button label). */
  readonly isEditMode = computed<boolean>(() => this.moduleId() !== null);

  /** True while fetching an existing module (edit mode). */
  readonly loading = signal<boolean>(false);

  /** True while a create/update request is in flight (disables the submit affordance). */
  readonly saving = signal<boolean>(false);

  // MIGRATION: RFC 7807 Problem Details field errors (ProblemDetails.errors) replace the legacy
  // per-control ASP.NET validator ErrorMessage rendering; keyed by camelCase controlId (e.g. moduleTitle).
  readonly serverErrors = signal<Record<string, string[]> | null>(null);

  // MIGRATION (QA Finding A): a general submit-error banner message for save failures that carry NO
  // ProblemDetails.errors dictionary (503/500/network errors). Mirrors the blessed user-form `submitError`
  // signal so a failed save is never swallowed silently — distinct from the per-field `serverErrors`
  // (FluentValidation 400 keyed by control) which app-form-controls renders inline.
  readonly submitError = signal<string | null>(null);

  /** Set when the edit-mode load fails, so the template can surface a load error instead of the form. */
  readonly loadError = signal<string | null>(null);

  /** The entity loaded in edit mode, retained so the full UpdateModuleDto can be reconstructed on submit. */
  private loadedModule: Module | null = null;

  /** Visibility choices for the template `<select>` (preserves the verbatim numeric enum ordering). */
  readonly visibilityOptions: ReadonlyArray<VisibilityOption> = [
    { value: VisibilityState.Maximized, label: 'Maximized' },
    { value: VisibilityState.Minimized, label: 'Minimized' },
    { value: VisibilityState.None, label: 'None' },
  ];

  /** Screen heading; switches between create and edit wording. */
  readonly heading = computed<string>(() => (this.isEditMode() ? 'Edit Module' : 'New Module'));

  // MIGRATION: legacy NEW-module defaults (ModuleSettings.ascx.vb L225-226: cboVisibility.SelectedIndex = 0
  // -> Maximized, chkAllTabs.Checked = False) plus ModuleInfo.vb ctor display-flag defaults
  // (DisplayTitle=True, DisplayPrint=True, DisplaySyndicate=False) reproduced as create-mode form defaults.
  // `moduleTitle` is the only validated control (legacy txtTitle RequiredFieldValidator + Page.IsValid gate,
  // mirrored by the backend FluentValidation CreateModuleValidator/UpdateModuleValidator: title required).
  readonly form = new FormGroup<ModuleFormModel>({
    moduleTitle: new FormControl<string | null>(null, { validators: [Validators.required] }),
    alignment: new FormControl<string | null>(null),
    color: new FormControl<string | null>(null),
    border: new FormControl<string | null>(null),
    iconFile: new FormControl<string | null>(null),
    header: new FormControl<string | null>(null),
    footer: new FormControl<string | null>(null),
    visibility: new FormControl<VisibilityState>(VisibilityState.Maximized, { nonNullable: true }),
    allTabs: new FormControl<boolean>(false, { nonNullable: true }),
    displayTitle: new FormControl<boolean>(true, { nonNullable: true }),
    displayPrint: new FormControl<boolean>(true, { nonNullable: true }),
    displaySyndicate: new FormControl<boolean>(false, { nonNullable: true }),
  });

  /** Typed access to the individual controls for the template (`[control]="controls.moduleTitle"`). */
  get controls(): ModuleFormModel {
    return this.form.controls;
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('moduleId');
    if (idParam === null) {
      return; // create mode: pristine defaults
    }
    const id = Number(idParam);
    this.moduleId.set(id);
    this.loadModule(id);
  }

  // MIGRATION (QA Finding B): the edit-mode load is extracted into a re-runnable method so the template can
  // offer a "Retry" affordance when it fails. CRITICAL: the template hides the form entirely while the load
  // is in flight OR has failed (see module-form.component.html), so a failed edit-load can never fall through
  // to the create (POST) branch of onSubmit() and silently CREATE a duplicate module.
  private loadModule(id: number): void {
    this.loadError.set(null);
    this.loading.set(true);
    this.moduleService.getModule(id).subscribe({
      next: (module) => {
        this.loadedModule = module;
        this.patchForm(module);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(problem.detail ?? problem.title ?? 'Failed to load the module.');
        this.loading.set(false);
      },
    });
  }

  /** Retries a failed edit-mode load (template "Retry" affordance). No-op in create mode. */
  retryLoad(): void {
    const id = this.moduleId();
    if (id !== null) {
      this.loadModule(id);
    }
  }

  /** Populates the form from a loaded module (edit mode), mapping the 12 editable fields. */
  private patchForm(module: Module): void {
    this.form.patchValue({
      moduleTitle: module.moduleTitle,
      alignment: module.alignment,
      color: module.color,
      border: module.border,
      iconFile: module.iconFile,
      header: module.header,
      footer: module.footer,
      visibility: module.visibility,
      allTabs: module.allTabs,
      displayTitle: module.displayTitle,
      displayPrint: module.displayPrint,
      displaySyndicate: module.displaySyndicate,
    });
  }

  // MIGRATION: cmdUpdate_Click + `If Page.IsValid` (L328). The postback save becomes a stateless REST call;
  // invalid forms are gated client-side (markAllAsTouched surfaces the field messages) before issuing a request.
  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    // MIGRATION (QA Finding B): defense-in-depth guard. In edit mode, never fall through to the create
    // (POST) branch when the existing module failed to load — without the loaded entity the full
    // UpdateModuleDto cannot be reconstructed (buildUpdateDto spreads ...loaded), and issuing a POST would
    // silently CREATE a duplicate instead of updating. The template already hides the form on a load
    // failure; this guard ensures onSubmit can never mis-route by HTTP method even if invoked directly.
    if (this.isEditMode() && this.loadedModule === null) {
      return;
    }
    this.saving.set(true);
    this.serverErrors.set(null);
    this.submitError.set(null); // MIGRATION (QA Finding A): clear any prior general submit-error banner.

    const id = this.moduleId();
    if (id !== null && this.loadedModule !== null) {
      const request = this.buildUpdateDto(id, this.loadedModule);
      this.moduleService.updateModule(id, request).subscribe({
        next: () => this.onSaveSuccess(),
        error: (problem: ProblemDetails) => this.onSaveError(problem),
      });
    } else {
      const request = this.buildCreateDto();
      this.moduleService.createModule(request).subscribe({
        next: () => this.onSaveSuccess(),
        error: (problem: ProblemDetails) => this.onSaveError(problem),
      });
    }
  }

  /**
   * Builds the full CreateModuleDto (all fields required on the contract). The form edits 12 fields; the
   * remaining 11 structural/placement fields are defaulted to 0/null/false — the server assigns the real
   * placement (portal, tab, definition, order, pane) when the module is created.
   */
  private buildCreateDto(): CreateModuleDto {
    const v = this.form.getRawValue();
    return {
      // --- edited fields (legacy ModuleSettings.ascx.vb cmdUpdate_Click L344-382) ---
      moduleTitle: v.moduleTitle,
      alignment: v.alignment,
      color: v.color,
      border: v.border,
      iconFile: v.iconFile,
      header: v.header,
      footer: v.footer,
      visibility: v.visibility,
      allTabs: v.allTabs,
      displayTitle: v.displayTitle,
      displayPrint: v.displayPrint,
      displaySyndicate: v.displaySyndicate,
      // --- non-edited structural defaults (server assigns real placement) ---
      portalID: 0,
      tabID: 0,
      moduleDefID: 0,
      desktopModuleID: 0,
      moduleOrder: 0,
      paneName: null,
      cacheTime: 0,
      startDate: null,
      endDate: null,
      containerSrc: null,
      inheritViewPermissions: false,
    };
  }

  /**
   * Builds the full UpdateModuleDto by spreading the loaded entity (which carries the non-edited but
   * required fields — moduleOrder, paneName, cacheTime, startDate, endDate, containerSrc,
   * inheritViewPermissions) and overriding the route id plus the 12 editable fields. The spread origin
   * exempts the extra Module-only properties from excess-property checks; the backend's System.Text.Json
   * ignores unknown properties.
   */
  private buildUpdateDto(id: number, loaded: Module): UpdateModuleDto {
    const v = this.form.getRawValue();
    return {
      ...loaded,
      moduleID: id, // MIGRATION: request.moduleID must equal the route :moduleId (server validates the match)
      moduleTitle: v.moduleTitle,
      alignment: v.alignment,
      color: v.color,
      border: v.border,
      iconFile: v.iconFile,
      header: v.header,
      footer: v.footer,
      visibility: v.visibility,
      allTabs: v.allTabs,
      displayTitle: v.displayTitle,
      displayPrint: v.displayPrint,
      displaySyndicate: v.displaySyndicate,
    };
  }

  /** On a successful create/update, return to the module list (parent feature route). */
  private onSaveSuccess(): void {
    this.saving.set(false);
    void this.router.navigate(['/modules']);
  }

  // MIGRATION: RFC 7807 error model — server-side FluentValidation failures arrive as ProblemDetails.errors
  // (field name -> messages) and are surfaced inline by app-form-controls, replacing the legacy per-validator
  // postback ErrorMessage display.
  private onSaveError(problem: ProblemDetails): void {
    this.saving.set(false);
    this.serverErrors.set(problem.errors ?? null);
    // MIGRATION (QA Finding A): also surface a general banner message so 503/500/network failures (which
    // carry NO `errors` dictionary) are not swallowed silently. Mirrors the blessed user-form pattern
    // (title ?? detail ?? fallback): a 503 shows its concise "Service Unavailable" title.
    this.submitError.set(problem.title ?? problem.detail ?? 'An error occurred while saving the module.');
  }
}
