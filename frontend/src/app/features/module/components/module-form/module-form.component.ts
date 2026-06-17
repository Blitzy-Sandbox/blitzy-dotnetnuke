// MIGRATION: ModuleFormComponent replaces the legacy Web Forms ModuleSettings editor
// (Website/admin/Modules/ModuleSettings.ascx.vb, 486 lines) — a server-rendered .ascx user
// control driven by postback/ViewState — with a stateless, client-rendered Angular 19
// standalone reactive-form screen. Behavioral parity is preserved for the CORE create/edit
// workflow only:
//   - cmdUpdate_Click (L326-427)                 -> onSubmit()
//   - Page.IsValid gate (L328)                   -> reactive Validators + form.invalid guard
//   - ModuleController.AddModule/UpdateModule     -> ModuleService.createModule/updateModule
//   - BindData() edit-mode hydration (L116-166)  -> getModule() + patchForm()
// The legacy module-settings PERIPHERY is intentionally OUT OF SCOPE (AAP §0.2.2) because none
// of it exists on the Module / CreateModuleDto / UpdateModuleDto contract: the permissions grid
// (dgPermissions) and the inherit-view-permissions toggle (chkInheritPermissions), scheduling
// (txtStartDate/txtEndDate), cache-time (txtCacheTime), container/skin selection
// (ctlModuleContainer), custom module-specific settings (ctlSpecific), and the move-to-tab
// (cboTab) / copy-to-all-tabs (chkAllTabs job) / default-module (chkDefault) / all-modules
// (chkAllModules) placement logic.
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { CreateModuleDto, Module, UpdateModuleDto, VisibilityState } from '../../models';
import { ModuleService } from '../../services';
import { ProblemDetails } from '../../../../core/services/api.service';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { ValidationHighlightDirective } from '../../../../shared/directives/validation-highlight';

/**
 * Strongly-typed reactive-form model exposing ONLY the editable subset of a Module that the
 * legacy ModuleSettings.ascx.vb `cmdUpdate_Click` maps AND that exists on the Module/DTO
 * contract. String fields are nullable (matching the wire model); `visibility` is a
 * non-nullable numeric enum; the four display/placement flags are non-nullable booleans.
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

/** A single selectable option for the module visibility `<select>`. */
interface VisibilityOption {
  readonly value: VisibilityState;
  readonly label: string;
}

/**
 * ModuleFormComponent — standalone create/edit screen for the core Module fields.
 *
 * Routing: the SAME component serves two parent-owned routes — `'new'` (create mode) and
 * `':moduleId/edit'` (edit mode); the mode is derived from the `:moduleId` route param. The
 * parent `'modules'` route owns `authGuard`, so no auth is declared here.
 */
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

  /** Target module id in edit mode; `null` in create mode. */
  readonly moduleId = signal<number | null>(null);

  /** True when editing an existing module (drives the heading + submit-button label). */
  readonly isEditMode = computed<boolean>(() => this.moduleId() !== null);

  /** True while fetching an existing module (edit mode). */
  readonly loading = signal<boolean>(false);

  /** True while a create/update request is in flight. */
  readonly saving = signal<boolean>(false);

  /** RFC 7807 field errors (ProblemDetails.errors) keyed by camelCase control id. */
  readonly serverErrors = signal<Record<string, string[]> | null>(null);

  /** Human-readable message shown when the edit-mode load fails. */
  readonly loadError = signal<string | null>(null);

  /** Retained loaded entity (edit mode) so non-edited UpdateModuleDto fields survive a save. */
  private loadedModule: Module | null = null;

  /**
   * Visibility choices rendered by the template `@for`. The numeric enum value is bound via
   * `[ngValue]` (NOT a string `[value]`) so the control keeps the VisibilityState number.
   */
  readonly visibilityOptions: ReadonlyArray<VisibilityOption> = [
    { value: VisibilityState.Maximized, label: 'Maximized' },
    { value: VisibilityState.Minimized, label: 'Minimized' },
    { value: VisibilityState.None, label: 'None' },
  ];

  /** Screen heading, switching between create and edit copy. */
  readonly heading = computed<string>(() => (this.isEditMode() ? 'Edit Module' : 'New Module'));

  // MIGRATION: legacy NEW-module defaults (ModuleSettings.ascx.vb L225-226: cboVisibility.SelectedIndex = 0
  // -> Maximized, chkAllTabs.Checked = False) plus ModuleInfo.vb ctor display-flag defaults
  // (DisplayTitle=True, DisplayPrint=True, DisplaySyndicate=False) reproduced as create-mode form defaults.
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

  /** Exposes the typed controls to the template (e.g. `[control]="controls.moduleTitle"`). */
  get controls(): ModuleFormModel {
    return this.form.controls;
  }

  /**
   * Resolves create vs. edit mode from the `:moduleId` route param (read from the snapshot —
   * the component is re-created per route via `loadComponent`). Create mode keeps the pristine
   * defaults; edit mode loads the module and patches the form.
   */
  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('moduleId');
    if (idParam === null) {
      return; // create mode: pristine defaults
    }
    const id = Number(idParam);
    this.moduleId.set(id);
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

  /** MIGRATION: legacy BindData() (L116-166) edit-mode hydration -> typed patch of the 12 editable controls. */
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

  /**
   * MIGRATION: cmdUpdate_Click (L326-427) + Page.IsValid gate (L328). Invalid forms surface their
   * validation messages (markAllAsTouched) and abort; valid forms branch to update (edit mode,
   * when an entity is loaded) or create (new mode).
   */
  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.serverErrors.set(null);

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
   * Builds the full 23-field CreateModuleDto: the 12 edited fields plus the 11 non-edited
   * structural fields defaulted to 0/null/false. The server assigns real placement values
   * (portal/tab/definition/order/pane) after creation.
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
   * Builds an UpdateModuleDto by spreading the loaded entity (which carries the non-edited but
   * required fields — moduleOrder, paneName, cacheTime, startDate, endDate, containerSrc,
   * inheritViewPermissions) and overriding the id plus the 12 edited fields. The extra Module
   * fields absent from UpdateModuleDto are spread-origin (excess-property-check exempt) and are
   * ignored by the backend's System.Text.Json deserializer.
   */
  private buildUpdateDto(id: number, loaded: Module): UpdateModuleDto {
    const v = this.form.getRawValue();
    return {
      ...loaded,
      moduleID: id, // MIGRATION: request.moduleID must equal the route :moduleId (the server validates the match).
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

  /**
   * Abandon the form and return to the module list WITHOUT saving. Mirrors the
   * post-save navigation target (`/modules`). MIGRATION: restores the legacy admin
   * Update/Cancel affordance pattern and brings module-form to parity with
   * user-form / role-form, which already expose an in-form Cancel (F4-FORM-01).
   */
  cancel(): void {
    void this.router.navigate(['/modules']);
  }

  /** On a successful save, return to the module list (the parent feature route). */
  private onSaveSuccess(): void {
    this.saving.set(false);
    void this.router.navigate(['/modules']);
  }

  // MIGRATION: postback/ViewState error round-tripping is replaced by the RFC 7807 Problem Details
  // model — ProblemDetails.errors (camelCase field name -> messages) is forwarded to each
  // <app-form-controls>, which renders the messages for its own control id.
  private onSaveError(problem: ProblemDetails): void {
    this.saving.set(false);
    this.serverErrors.set(problem.errors ?? null);
  }
}
