/**
 * ModuleFormComponent — Module settings create + edit screen (Angular 19 standalone).
 *
 * A single net-new standalone Angular 19 component that reproduces the legacy DNN admin
 * module-settings editor. It is lazy-loaded by the sibling `module.routes.ts` at BOTH
 * `'new'` (create) and `':id'` (edit) via
 *   import('./module-form/module-form.component').then((m) => m.ModuleFormComponent)
 *
 * MIGRATION lineage (REFERENCE ONLY — the VB is NOT transliterated; AAP §0.6.3):
 *   Website/admin/Modules/ModuleSettings.ascx(.vb) — ModuleSettingsPage : PortalModuleBase.
 *     - BindData()        (L85-169)  read -> UI        -> loadModule() edit-mode fetch + patchValue
 *     - Page_Load         (L225-227) create defaults   -> create-mode form defaults
 *     - cmdUpdate_Click   (L326-427) UI -> ModuleInfo  -> onSubmit() PUT /api/modules/{id}
 *     - (create)                                        -> onSubmit() POST /api/modules
 *     - cmdDelete_Click   (L300-312) DeleteTabModule   -> onDeleteConfirmed() DELETE /api/modules/{id}
 *     - cmdCancel_Click   (L279-286) Response.Redirect -> onCancel() router.navigate(['/modules'])
 *
 * Postback / ViewState / IClientAPICallbackEventHandler are ELIMINATED (stateless HTTP + Signals).
 * All HTTP flows through the injected ModuleService; the { data, meta } envelope-unwrapping and the
 * RFC 7807 ProblemDetails normalization live centrally in ApiService — this component only maps a
 * ProblemDetails to a top banner + field-level errors. UI functional parity (AAP §0.7.1): validators
 * and error-message text mirror the legacy screen; non-obvious conversions are marked // MIGRATION:.
 */
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { CreateModuleRequest, Module, ProblemDetails, UpdateModuleRequest } from '../../../core/models';
import { ModuleService } from '../module.service';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';
import { FormFieldComponent, FormFieldOption } from '../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * Strongly-typed control map for the single module-settings form (create + edit).
 * MIGRATION: mirrors the legacy ModuleSettings.ascx inputs <-> ModuleInfo fields. The identity
 * controls (portalID/tabID/moduleDefID) exist in BOTH modes but are required + submitted ONLY in
 * create mode (see ngOnInit / onSubmit).
 */
interface ModuleFormControls {
  moduleTitle: FormControl<string>;
  paneName: FormControl<string>;
  moduleOrder: FormControl<number>;
  cacheTime: FormControl<number>;
  visibility: FormControl<number>;
  allTabs: FormControl<boolean>;
  alignment: FormControl<string>;
  color: FormControl<string>;
  border: FormControl<string>;
  iconFile: FormControl<string>;
  header: FormControl<string>;
  footer: FormControl<string>;
  startDate: FormControl<string>;
  endDate: FormControl<string>;
  containerSrc: FormControl<string>;
  displayTitle: FormControl<boolean>;
  displayPrint: FormControl<boolean>;
  displaySyndicate: FormControl<boolean>;
  inheritViewPermissions: FormControl<boolean>;
  // MIGRATION: legacy add-module context (portal/tab/module definition) captured as explicit
  // create-mode form fields; immutable in edit mode (taken from the loaded Module, NOT sent in
  // UpdateModuleRequest).
  portalID: FormControl<number>;
  tabID: FormControl<number>;
  moduleDefID: FormControl<number>;
}

/** The typed reactive form group for this component. */
type ModuleFormGroup = FormGroup<ModuleFormControls>;

@Component({
  selector: 'app-module-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormFieldComponent, ConfirmationDialogComponent, LoadingSpinnerComponent],
  template: `
    <form class="module-form" [formGroup]="form" (ngSubmit)="onSubmit()" novalidate>
      <app-loading-spinner [loading]="loading() || submitting()" [overlay]="true" message="Please wait…" />

      <h1 class="module-form__title">{{ pageTitle() }}</h1>

      @if (error()) {
        <div class="form-error-banner" role="alert">{{ error() }}</div>
      }

      <!-- MIGRATION: legacy add-module context (portal/tab/module definition) — create mode only. -->
      @if (!isEditMode()) {
        <app-form-field
          [control]="form.controls.portalID"
          controlId="portalID"
          label="Portal"
          controlType="number"
          [required]="true"
          [errorMessages]="{ required: 'Portal is required', min: 'Portal must be a valid reference (ID 1 or greater).' }"
        />
        <app-form-field
          [control]="form.controls.tabID"
          controlId="tabID"
          label="Page (Tab)"
          controlType="number"
          [required]="true"
          [errorMessages]="{ required: 'Page (Tab) is required', min: 'Page (Tab) must be a valid reference (ID 1 or greater).' }"
        />
        <app-form-field
          [control]="form.controls.moduleDefID"
          controlId="moduleDefID"
          label="Module Definition"
          controlType="number"
          [required]="true"
          [errorMessages]="{ required: 'Module definition is required', min: 'Module definition must be a valid reference (ID 1 or greater).' }"
        />
      }

      <!-- MIGRATION: txtFriendlyName (L125) — display-only, never written back. Rendered
           as a semantic description list (dt/dd): a <label> with no associated control is
           invalid, so a dl conveys the read-only name↔value relationship accessibly. -->
      @if (isEditMode()) {
        <dl class="module-form__readonly-field">
          <dt class="module-form__readonly-label">Friendly Name</dt>
          <dd class="module-form__readonly">{{ friendlyName() }}</dd>
        </dl>
      }

      <app-form-field
        [control]="form.controls.moduleTitle"
        controlId="moduleTitle"
        label="Title"
        controlType="text"
        [required]="true"
        [autofocus]="true"
        [errorMessages]="{ required: 'Title is required' }"
      />

      <app-form-field [control]="form.controls.paneName" controlId="paneName" label="Pane" controlType="text" />

      <app-form-field
        [control]="form.controls.moduleOrder"
        controlId="moduleOrder"
        label="Module Order"
        controlType="number"
        [errorMessages]="{ min: 'Order must be 0 or greater' }"
      />

      <app-form-field
        [control]="form.controls.cacheTime"
        controlId="cacheTime"
        label="Cache Time (seconds)"
        controlType="number"
        [errorMessages]="{ min: 'Cache time must be 0 or greater' }"
      />

      <app-form-field
        [control]="form.controls.visibility"
        controlId="visibility"
        label="Visibility"
        controlType="select"
        [required]="true"
        [options]="visibilityOptions"
        [errorMessages]="{ required: 'Visibility is required' }"
      />

      <app-form-field
        [control]="form.controls.alignment"
        controlId="alignment"
        label="Alignment"
        controlType="select"
        [options]="alignmentOptions"
      />

      <app-form-field [control]="form.controls.color" controlId="color" label="Color" controlType="text" />

      <!-- MIGRATION: legacy Integer DataTypeCheck ('Invalid Border ...') relaxed to optional free text. -->
      <app-form-field [control]="form.controls.border" controlId="border" label="Border" controlType="text" />

      <app-form-field [control]="form.controls.iconFile" controlId="iconFile" label="Icon File" controlType="text" />

      <app-form-field
        [control]="form.controls.containerSrc"
        controlId="containerSrc"
        label="Container"
        controlType="text"
      />

      <app-form-field
        [control]="form.controls.header"
        controlId="header"
        label="Header"
        controlType="textarea"
        [rows]="3"
      />

      <app-form-field
        [control]="form.controls.footer"
        controlId="footer"
        label="Footer"
        controlType="textarea"
        [rows]="3"
      />

      <!-- MIGRATION: txtStartDate/txtEndDate calendar popup -> native date inputs projected into the
           shared field's custom slot; formControlName resolves against this parent's [formGroup]. -->
      <app-form-field
        #startDateFf="appFormField"
        controlType="custom"
        [control]="form.controls.startDate"
        controlId="startDate"
        label="Start Date"
      >
        <input
          type="date"
          class="form-field__control"
          [id]="startDateFf.fieldId()"
          formControlName="startDate"
          [attr.aria-describedby]="startDateFf.describedBy()"
          [attr.aria-required]="startDateFf.required() ? 'true' : null"
        />
      </app-form-field>

      <app-form-field
        #endDateFf="appFormField"
        controlType="custom"
        [control]="form.controls.endDate"
        controlId="endDate"
        label="End Date"
      >
        <input
          type="date"
          class="form-field__control"
          [id]="endDateFf.fieldId()"
          formControlName="endDate"
          [attr.aria-describedby]="endDateFf.describedBy()"
          [attr.aria-required]="endDateFf.required() ? 'true' : null"
        />
      </app-form-field>

      <!-- MIGRATION: parity-plus cross-field check — end date must be on or after start date. -->
      @if (
        form.errors?.['dateRange'] &&
        (form.controls.startDate.touched ||
          form.controls.endDate.touched ||
          form.controls.startDate.dirty ||
          form.controls.endDate.dirty)
      ) {
        <p class="form-field__error" role="alert">End date must be on or after start date</p>
      }

      <!-- MIGRATION: chkAllTabs/chkDisplayTitle/chkDisplayPrint/chkDisplaySyndicate/chkInheritPermissions.
           FormFieldComponent has no 'checkbox' type; rendered directly with CheckboxControlValueAccessor. -->
      <fieldset class="module-form__checks">
        <label class="form-check">
          <input type="checkbox" formControlName="allTabs" /> All Pages
        </label>
        <label class="form-check">
          <input type="checkbox" formControlName="displayTitle" /> Display Title
        </label>
        <label class="form-check">
          <input type="checkbox" formControlName="displayPrint" /> Display Print
        </label>
        <label class="form-check">
          <input type="checkbox" formControlName="displaySyndicate" /> Display Syndicate
        </label>
        <label class="form-check">
          <input type="checkbox" formControlName="inheritViewPermissions" /> Inherit View Permissions
        </label>
      </fieldset>

      <div class="module-form__actions">
        <!-- MIGRATION: cmdUpdate — POST (create) / PUT (edit). -->
        <button type="submit" class="btn btn--primary" [disabled]="submitting()">
          {{ isEditMode() ? 'Update' : 'Create' }}
        </button>
        <!-- MIGRATION: cmdDelete — hidden in create per Page_Load L227. -->
        @if (isEditMode()) {
          <button
            type="button"
            class="btn btn--danger"
            (click)="showDeleteDialog.set(true)"
            [disabled]="submitting()"
          >
            Delete
          </button>
        }
        <!-- MIGRATION: cmdCancel — Response.Redirect(NavigateURL()). -->
        <button type="button" class="btn" (click)="onCancel()">Cancel</button>
      </div>
    </form>

    <!-- MIGRATION: legacy ClientAPI.AddButtonConfirm(cmdDelete, "DeleteItem") -> shared confirm modal. -->
    <app-confirmation-dialog
      [(open)]="showDeleteDialog"
      title="Delete Module"
      message="Are you sure you want to delete this module?"
      confirmLabel="Delete"
      [danger]="true"
      (confirm)="onDeleteConfirmed()"
    />
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .module-form {
        position: relative;
        max-width: 40rem;
        /* QA F-G: center the form within the content area, matching every other
           feature form (portal-form/role-form/user-form all use margin: 0 auto).
           Previously no margin was set, so the constrained form sat left-aligned. */
        margin: 0 auto;
      }

      .module-form__title {
        margin: 0 0 var(--space-4);
        font-size: 1.25rem;
        font-weight: 600;
      }

      .module-form__readonly-field {
        margin: 0 0 var(--space-3);
      }

      .module-form__readonly-label {
        display: block;
        margin-bottom: var(--space-1);
        font-weight: 500;
      }

      .module-form__readonly {
        /* margin:0 also removes the browser-default <dd> inline-start indent. */
        margin: 0;
        padding: var(--space-2) 0;
        color: var(--color-text);
      }

      .module-form__checks {
        margin: var(--space-3) 0;
        padding: 0;
        border: 0;
        display: flex;
        flex-direction: column;
        gap: var(--space-2);
      }

      .form-check {
        display: flex;
        align-items: center;
        gap: var(--space-2);
        font-weight: 500;
      }

      .form-error-banner {
        margin-bottom: var(--space-4);
        padding: var(--space-3);
        border: 1px solid var(--color-danger);
        border-radius: var(--radius);
        background: var(--color-danger-bg);
        color: var(--color-danger);
      }

      .form-field__error {
        margin: var(--space-1) 0 0;
        color: var(--color-danger);
        font-size: var(--font-size-sm);
      }

      .module-form__actions {
        display: flex;
        gap: var(--space-2);
        margin-top: var(--space-4);
      }

      .btn {
        padding: var(--space-2) var(--space-4);
        font: inherit;
        border: 1px solid var(--color-border);
        border-radius: var(--radius);
        background: var(--color-surface);
        color: var(--color-text);
        cursor: pointer;
      }

      .btn:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }

      .btn--primary {
        border-color: var(--color-primary);
        background: var(--color-primary);
        color: var(--color-primary-contrast);
      }

      .btn--danger {
        border-color: var(--color-danger);
        background: var(--color-danger);
        color: var(--color-danger-contrast);
      }

      /* QA F-J: hover states (every form button previously had only :disabled).
         Base .btn is a neutral/secondary (Cancel) button; the primary/danger
         modifiers are declared after the base hover so they win for those
         variants (equal specificity, source order decides). A button is only
         ever primary OR danger, so those two never conflict.
         QA INFO: subtle :active press feedback across all buttons. */
      .btn:hover:not(:disabled) {
        background: var(--color-surface-hover);
      }

      .btn--primary:hover:not(:disabled) {
        background: var(--color-primary-hover);
        border-color: var(--color-primary-hover);
      }

      .btn--danger:hover:not(:disabled) {
        background: var(--color-danger-hover);
        border-color: var(--color-danger-hover);
      }

      .btn:active:not(:disabled) {
        transform: translateY(1px);
      }

      /*
       * Explicit responsive handling for this dense admin form. On narrow
       * (mobile) viewports the constrained max-width no longer applies, the
       * checkbox group keeps its vertical stack, and the action buttons wrap and
       * grow to full width so they remain comfortable touch targets. Uses the
       * shared 640px breakpoint (matching the user-form component) so the SPA's
       * responsive behaviour stays consistent across feature forms.
       */
      @media (max-width: 640px) {
        .module-form {
          max-width: 100%;
        }

        .module-form__actions {
          flex-wrap: wrap;
        }

        .module-form__actions .btn {
          flex: 1 1 auto;
        }
      }
    `,
  ],
})
export class ModuleFormComponent implements OnInit {
  // --- Dependency injection (Angular 19 inject() idiom; AAP §0.3.3 DI over statics) ---
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly moduleService = inject(ModuleService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  // MIGRATION: cboVisibility RadioButtonList (numeric VisibilityState). 0=Maximized,1=Minimized,2=None.
  readonly visibilityOptions: readonly FormFieldOption[] = [
    { value: 0, label: 'Maximized' },
    { value: 1, label: 'Minimized' },
    { value: 2, label: 'None' },
  ];

  // MIGRATION: cboAlign RadioButtonList (left/center/right/'' Not Specified).
  readonly alignmentOptions: readonly FormFieldOption[] = [
    { value: '', label: 'Not Specified' },
    { value: 'left', label: 'Left' },
    { value: 'center', label: 'Center' },
    { value: 'right', label: 'Right' },
  ];

  // MIGRATION: parity-plus cross-field check (legacy had per-field Date DataTypeCheck only).
  // Declared BEFORE `form` because tsconfig `useDefineForClassFields:false` runs field initializers
  // in declaration order and buildForm() (invoked by the `form` initializer) reads this validator.
  private readonly dateRangeValidator: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
    const start = group.get('startDate')?.value as string;
    const end = group.get('endDate')?.value as string;
    if (start && end && new Date(start) > new Date(end)) {
      return { dateRange: true };
    }
    return null;
  };

  // --- Reactive UI state (Signals) ---
  /** True while the edit-mode fetch (getModule) is in flight. */
  readonly loading = signal(false);
  /** True while a create/update/delete request is in flight. */
  readonly submitting = signal(false);
  /** Top-level error banner text (from ProblemDetails), or null when clear. */
  readonly error = signal<string | null>(null);
  /** True in edit mode (':id' route), false in create mode ('new'). */
  readonly isEditMode = signal(false);
  /** Controls the delete confirmation modal. */
  readonly showDeleteDialog = signal(false);
  // MIGRATION: txtFriendlyName (L125) — display-only, never written back.
  readonly friendlyName = signal('');
  /** MIGRATION: legacy page heading — "Edit Module Settings" vs "Add New Module". */
  readonly pageTitle = computed(() => (this.isEditMode() ? 'Edit Module Settings' : 'Add New Module'));

  /** Module id in edit mode (from the ':id' route param); null in create mode. */
  private moduleId: number | null = null;
  /** Immutable placement identity captured on load; not re-sent in UpdateModuleRequest. */
  private loadedModule: Module | null = null;

  // MIGRATION (OUT OF SCOPE — not in Module/Create/Update DTOs, intentionally omitted):
  //  - chkDefault  -> ModuleInfo.IsDefaultModule (cmdUpdate_Click L383)
  //  - chkAllModules -> ModuleInfo.AllModules (L384)
  //  - dgPermissions -> ModuleInfo.ModulePermissions grid (L378) — module permission editing deferred
  //  - cboTab move/copy + AllTabs copy/delete-across-tabs (L403-418) — placement moves deferred
  //  - ctlSpecific custom module-specific settings (LoadSettings/UpdateSettings) — deferred
  //  - Import.ascx.vb / Export.ascx.vb secondary workflows — deferred, no components created
  // friendlyName (txtFriendlyName, L125) is DISPLAY-ONLY: bound for display in edit mode, NEVER written back.

  /**
   * The typed reactive form (create + edit). Identity requireds (portalID/tabID/moduleDefID) are
   * added per-mode in ngOnInit; the group-level dateRangeValidator is always attached.
   */
  readonly form: ModuleFormGroup = this.buildForm();

  /**
   * Build the strongly-typed FormGroup. Field <-> validator <-> DTO mapping mirrors the legacy
   * ModuleSettings.ascx controls EXACTLY (AAP §0.7.1). Create-mode defaults (Page_Load L225-227) are
   * encoded here: visibility=0 (Maximized), allTabs=false, cacheTime=0, moduleOrder=0, booleans false.
   */
  private buildForm(): ModuleFormGroup {
    return this.fb.group<ModuleFormControls>(
      {
        // MIGRATION: ModuleSettings.ascx.vb txtTitle -> ModuleTitle. Legacy markup lacked an explicit
        // RequiredFieldValidator, but ModuleTitle is a core non-optional field
        // (Create/UpdateModuleRequest.moduleTitle) so a required validator is applied for parity with
        // the domain contract.
        moduleTitle: this.fb.control('', { validators: [Validators.required] }),
        paneName: this.fb.control(''),
        moduleOrder: this.fb.control(0, { validators: [Validators.min(0)] }),
        // MIGRATION: cmdUpdate_Click L349-353 — If txtCacheTime.Text <> "" Then Int32.Parse Else 0.
        // Empty coerced to 0 at submit (see toInt).
        cacheTime: this.fb.control(0, { validators: [Validators.min(0)] }),
        // MIGRATION: cboVisibility numeric VisibilityState (0=Maximized,1=Minimized,2=None) — NOT a TS
        // enum; the shared select uses [value] so it emits strings, coerce with Number() on submit
        // (mirrors Int32.Parse(cboVisibility.SelectedItem.Value)).
        visibility: this.fb.control(0, { validators: [Validators.required] }),
        allTabs: this.fb.control(false),
        alignment: this.fb.control(''),
        color: this.fb.control(''),
        // MIGRATION: legacy Integer DataTypeCheck ('Invalid Border ...') relaxed to an optional
        // free-text field per the string? DTO shape.
        border: this.fb.control(''),
        iconFile: this.fb.control(''),
        header: this.fb.control(''),
        footer: this.fb.control(''),
        startDate: this.fb.control(''),
        endDate: this.fb.control(''),
        containerSrc: this.fb.control(''),
        displayTitle: this.fb.control(false),
        displayPrint: this.fb.control(false),
        displaySyndicate: this.fb.control(false),
        inheritViewPermissions: this.fb.control(false),
        // MIGRATION: identity context — validator-free at construction; Validators.required is added
        // in create mode only (ngOnInit). In edit mode these are patched from the loaded Module for
        // round-trip and are EXCLUDED from UpdateModuleRequest.
        portalID: this.fb.control(0),
        tabID: this.fb.control(0),
        moduleDefID: this.fb.control(0),
      },
      { validators: [this.dateRangeValidator] },
    );
  }

  /**
   * Mode detection. The `'new'` route carries no ':id' param (matched before ':id' in
   * module.routes.ts), so a numeric ':id' means edit; anything else is create.
   * MIGRATION: Page_Init read ModuleId from the querystring; here it is the route param.
   */
  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id'); // 'new' route has no :id
    if (idParam !== null && /^\d+$/.test(idParam)) {
      this.isEditMode.set(true);
      this.moduleId = Number(idParam);
      this.loadModule(this.moduleId);
    } else {
      // CREATE MODE — MIGRATION: Page_Load L225-227 defaults already set by group init
      // (visibility=0 Maximized, allTabs=false, cacheTime=0, moduleOrder=0, booleans false; delete hidden).
      // Identity fields become required ONLY in create mode.
      // R10 Issue 4: the identity controls are foreign-key references to existing rows.
      // Their default value 0 satisfies Validators.required (Angular treats 0 as "present";
      // only null/undefined/'' fail required), so an invalid reference was submitted silently.
      // Validators.min(1) rejects zero/negative IDs client-side because SQL Server IDENTITY
      // keys begin at 1, so any valid Portal/Tab/ModuleDefinition reference is >= 1.
      this.form.controls.portalID.addValidators([Validators.required, Validators.min(1)]);
      this.form.controls.tabID.addValidators([Validators.required, Validators.min(1)]);
      this.form.controls.moduleDefID.addValidators([Validators.required, Validators.min(1)]);
      this.form.controls.portalID.updateValueAndValidity();
      this.form.controls.tabID.updateValueAndValidity();
      this.form.controls.moduleDefID.updateValueAndValidity();
    }
  }

  /**
   * MIGRATION: BindData L85-169 — fetch the module and patch the form (edit mode). Dates are mapped
   * to the native date-input format; visibility stays numeric; booleans map directly. The placement
   * identity is patched for round-trip only (it is excluded from UpdateModuleRequest).
   */
  private loadModule(id: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.moduleService.getModule(id).subscribe({
      next: (module: Module): void => {
        this.loadedModule = module;
        // MIGRATION: txtFriendlyName (L125) — display-only, never written back.
        this.friendlyName.set(module.friendlyName);
        this.form.patchValue({
          moduleTitle: module.moduleTitle,
          paneName: module.paneName,
          moduleOrder: module.moduleOrder,
          cacheTime: module.cacheTime,
          visibility: module.visibility,
          allTabs: module.allTabs,
          // MIGRATION QA finding (unset alignment showed blank): an unset alignment arrives as null;
          // coerce it to '' so the value matches the '' ("Not Specified") option in alignmentOptions
          // and the RadioButtonList/select renders "Not Specified" instead of an empty selection.
          alignment: module.alignment ?? '',
          color: module.color,
          border: module.border,
          iconFile: module.iconFile,
          header: module.header,
          footer: module.footer,
          // MIGRATION: legacy StartDate.ToShortDateString shown only If Not Null.IsNull(...).
          startDate: this.toDateInput(module.startDate),
          endDate: this.toDateInput(module.endDate),
          containerSrc: module.containerSrc,
          displayTitle: module.displayTitle,
          displayPrint: module.displayPrint,
          displaySyndicate: module.displaySyndicate,
          inheritViewPermissions: module.inheritViewPermissions,
          // Identity round-trip (kept for reference; excluded from the update body).
          portalID: module.portalID,
          tabID: module.tabID,
          moduleDefID: module.moduleDefID,
        });
        this.loading.set(false);
      },
      error: (problem: ProblemDetails): void => {
        this.error.set(problem.detail ?? problem.title ?? 'Failed to load module.');
        this.loading.set(false);
      },
    });
  }

  /** ISO 'yyyy-MM-ddTHH:mm:ss' -> 'yyyy-MM-dd' for a native <input type="date">; '' when empty. */
  private toDateInput(iso: string): string {
    return iso ? iso.substring(0, 10) : '';
  }

  /**
   * MIGRATION: cmdUpdate_Click L326-427 — validate (legacy `If Page.IsValid`), assemble the DTO, and
   * POST (create) / PUT (edit). Empty optional strings collapse to `undefined`; numeric fields are
   * coerced via toInt (select emits strings; number inputs may emit null).
   */
  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    const raw = this.form.getRawValue();

    if (this.isEditMode() && this.moduleId !== null) {
      // MIGRATION: PUT -> 200. UpdateModuleRequest carries NO id and NO placement identity
      // (portalID/tabID/moduleDefID) — those are immutable and taken from the loaded module.
      const updateBody: UpdateModuleRequest = {
        moduleTitle: raw.moduleTitle,
        paneName: this.optionalStr(raw.paneName),
        moduleOrder: this.toInt(raw.moduleOrder),
        cacheTime: this.toInt(raw.cacheTime),
        alignment: this.optionalStr(raw.alignment),
        color: this.optionalStr(raw.color),
        border: this.optionalStr(raw.border),
        iconFile: this.optionalStr(raw.iconFile),
        allTabs: raw.allTabs,
        visibility: this.toInt(raw.visibility),
        header: this.optionalStr(raw.header),
        footer: this.optionalStr(raw.footer),
        startDate: this.optionalStr(raw.startDate),
        endDate: this.optionalStr(raw.endDate),
        containerSrc: this.optionalStr(raw.containerSrc),
        displayTitle: raw.displayTitle,
        displayPrint: raw.displayPrint,
        displaySyndicate: raw.displaySyndicate,
        inheritViewPermissions: raw.inheritViewPermissions,
      };
      this.moduleService.updateModule(this.moduleId, updateBody).subscribe({
        next: (): void => {
          this.submitting.set(false);
          void this.router.navigate(['/modules']);
        },
        error: (problem: ProblemDetails): void => this.handleApiError(problem),
      });
      return;
    }

    // MIGRATION: POST -> 201. CreateModuleRequest INCLUDES the placement identity.
    const createBody: CreateModuleRequest = {
      portalID: this.toInt(raw.portalID),
      tabID: this.toInt(raw.tabID),
      moduleDefID: this.toInt(raw.moduleDefID),
      moduleTitle: raw.moduleTitle,
      paneName: raw.paneName,
      moduleOrder: this.toInt(raw.moduleOrder),
      cacheTime: this.toInt(raw.cacheTime),
      alignment: this.optionalStr(raw.alignment),
      color: this.optionalStr(raw.color),
      border: this.optionalStr(raw.border),
      iconFile: this.optionalStr(raw.iconFile),
      allTabs: raw.allTabs,
      visibility: this.toInt(raw.visibility),
      header: this.optionalStr(raw.header),
      footer: this.optionalStr(raw.footer),
      startDate: this.optionalStr(raw.startDate),
      endDate: this.optionalStr(raw.endDate),
      containerSrc: this.optionalStr(raw.containerSrc),
      displayTitle: raw.displayTitle,
      displayPrint: raw.displayPrint,
      displaySyndicate: raw.displaySyndicate,
      inheritViewPermissions: raw.inheritViewPermissions,
    };
    this.moduleService.createModule(createBody).subscribe({
      next: (): void => {
        this.submitting.set(false);
        void this.router.navigate(['/modules']);
      },
      error: (problem: ProblemDetails): void => this.handleApiError(problem),
    });
  }

  /**
   * MIGRATION: cmdDelete_Click L300-312 — DeleteTabModule(TabId, ModuleId) -> DELETE /api/modules/{id}
   * (204). Invoked from the confirmation dialog's (confirm) output (edit mode only).
   */
  onDeleteConfirmed(): void {
    if (this.moduleId === null) {
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    this.moduleService.deleteModule(this.moduleId).subscribe({
      next: (): void => {
        this.submitting.set(false);
        void this.router.navigate(['/modules']);
      },
      error: (problem: ProblemDetails): void => this.handleApiError(problem),
    });
  }

  /** MIGRATION: cmdCancel_Click L279-286 — Response.Redirect(NavigateURL()). */
  onCancel(): void {
    void this.router.navigate(['/modules']);
  }

  /** Trim optional strings; collapse empties to `undefined` so they are omitted from the JSON body. */
  private optionalStr(v: string): string | undefined {
    const t = (v ?? '').trim();
    return t.length > 0 ? t : undefined;
  }

  /**
   * Coerce a form value to an integer. Handles the shared <select>'s string emission (visibility) and
   * the number input's `null` on empty (cacheTime/moduleOrder). Empty/invalid -> 0 (parity with legacy
   * `If txtCacheTime.Text <> "" Then Int32.Parse Else 0`).
   */
  private toInt(v: number): number {
    const n = Number(v);
    return Number.isFinite(n) ? Math.trunc(n) : 0;
  }

  /** MIGRATION: RFC 7807 ProblemDetails -> top error banner (primary surface) + field-level highlights. */
  private handleApiError(problem: ProblemDetails): void {
    this.submitting.set(false);
    this.error.set(problem.detail ?? problem.title ?? 'Save failed.');
    this.mapServerErrors(problem);
  }

  /**
   * Map RFC 7807 `errors` (server field name -> messages) onto the matching form control, setting a
   * `server` validation error and marking it touched so the inline message renders. Server field names
   * may be PascalCase (e.g. `ModuleTitle`); they are compared case-insensitively to the control names.
   * MIGRATION: field-level parity for the legacy per-control validators, sourced from the API contract.
   */
  private mapServerErrors(problem: ProblemDetails): void {
    const errors = problem.errors;
    if (!errors) {
      return;
    }
    const controlNames = Object.keys(this.form.controls);
    for (const key of Object.keys(errors)) {
      const match = controlNames.find((name) => name.toLowerCase() === key.toLowerCase());
      if (match === undefined) {
        continue;
      }
      // Bracket notation is mandatory under noPropertyAccessFromIndexSignature.
      const messages = errors[key];
      const message = messages.length > 0 ? messages[0] : 'Invalid value.';
      const control = this.form.get(match);
      if (control !== null) {
        control.setErrors({ server: message });
        control.markAsTouched();
      }
    }
  }
}
