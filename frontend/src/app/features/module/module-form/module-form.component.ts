// MIGRATION: Website/admin/Modules/ModuleSettings.ascx.vb (486 lines) -> Angular 19 ModuleFormComponent.
// Re-expresses the legacy DotNetNuke module-settings workflow: BindData (L85-169), Page_Load access
// gate (L191-193), cmdUpdate_Click save + field mapping (L326-427), cmdDelete_Click (L300-312) and
// cmdCancel_Click (L279-286). All Web Forms postback/ViewState/PortalModuleBase/Framework.Reflection/
// Skin.AddModuleMessage/.resx/InvokePopupCal machinery is discarded; only the validation + orchestration
// logic is migrated. The module Move/Copy/DeleteAll lifecycle (L398-418) is orchestrated SERVER-SIDE from
// the submitted end-state (allTabs/tabId) -- no imperative move/copy endpoints exist (see module.service.ts).
// Per-module custom "Settings" controls (ModuleControlController.GetModuleControlsByKey("Settings", ...),
// L231-240/L458) are out of scope -- they load per-module-type controls via the ModuleControlController
// reflection module-loader (AAP Section 0.6.2); only the generic module settings are represented.
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { ModuleService } from '../module.service';
import type { ModuleUpdateRequest } from '../module.service';
import { AuthService } from '../../../core/auth/auth.service';
import { FormControlComponent } from '../../../shared/components/form-controls/form-control.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { Module, ModulePermission, ProblemDetails } from '../../../core/models';
// MIGRATION: [QA F4-003] shared focus-first-invalid helper; [QA F4-013] shared error normaliser (maps the
// status-0 transport case to a friendly message instead of the previous null/blind-cast).
import { focusFirstInvalidControl } from '../../../shared/utils/focus-first-invalid.util';
import { toProblemDetails as normalizeProblemDetails } from '../../../core/services/problem-details.util';

// MIGRATION: strongly-typed Reactive Form mirroring the legacy ModuleSettings.ascx server controls
// (txtTitle, ctlIcon, cboAlign, txtColor, txtBorder, ctlModuleContainer, chkDisplay* flags, txtHeader/
// txtFooter, cboVisibility, chkInheritPermissions, txtStartDate/txtEndDate, txtCacheTime, cboTab,
// chkAllTabs, chkDefault, chkAllModules). The permissions grid (dgPermissions.Permissions) is held in a
// separate signal because it is an editable collection, not a scalar control.
interface ModuleSettingsModel {
  moduleTitle: FormControl<string>;
  iconFile: FormControl<string>;
  alignment: FormControl<string>;
  color: FormControl<string>;
  border: FormControl<string>;
  containerSrc: FormControl<string>;
  displayTitle: FormControl<boolean>;
  displayPrint: FormControl<boolean>;
  displaySyndicate: FormControl<boolean>;
  header: FormControl<string>;
  footer: FormControl<string>;
  visibility: FormControl<number>;
  inheritViewPermissions: FormControl<boolean>;
  startDate: FormControl<string | null>;
  endDate: FormControl<string | null>;
  cacheTime: FormControl<number>;
  tabId: FormControl<number>;
  allTabs: FormControl<boolean>;
  isDefaultModule: FormControl<boolean>;
  allModules: FormControl<boolean>;
}

// MIGRATION: the update payload is the backend-aligned ModuleUpdateRequest (imported from module.service.ts).
// cmdUpdate_Click also set objModule.IsDefaultModule (chkDefault, L383) and objModule.AllModules
// (chkAllModules, L384) before invoking CopyModule/DeleteAllModules; the server consumes those two flags
// together with the allTabs/tabId end-state to orchestrate Move/Copy/DeleteAll (L398-418). The permission
// collection travels as the contract `permissions` field (NOT `modulePermissions`); `portalId` is a query
// param (NOT a body field); `moduleId` is the route segment; `isDeleted` is not part of the update contract.
@Component({
  selector: 'app-module-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    FormControlComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
  ],
  templateUrl: './module-form.component.html',
  styleUrl: './module-form.component.scss',
})
export class ModuleFormComponent {
  private readonly moduleService = inject(ModuleService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  // MIGRATION: [QA F4-003] host element used to locate the first invalid control on a failed submit.
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  // MIGRATION: the legacy control read ModuleId from the query string in Page_Init (L448-450). Here
  // withComponentInputBinding() binds the :id route parameter (a STRING) to this input; it is parsed with
  // Number(...) for the service calls.
  readonly id = input.required<string>();

  /** Loading state surfaced by the ModuleService (true while getById is in flight). */
  readonly loading = this.moduleService.loading;

  /** The module loaded for editing; drives caching-row visibility and multi-tenant (portalId) scoping. */
  private readonly loadedModule = signal<Module | null>(null);

  /** MIGRATION: dgPermissions.Permissions (L378) -- module permission rows, edited in place. */
  readonly modulePermissions = signal<ModulePermission[]>([]);

  /** True while the save request is in flight. */
  readonly submitting = signal(false);

  /** True after a successful save (in-page success surface). */
  readonly saved = signal(false);

  /** Controls the conditional mounting of the delete confirmation dialog. */
  readonly showConfirm = signal(false);

  /** RFC 7807 problem body from the most recent failed request (mapped to fields by app-form-control). */
  readonly problem = signal<ProblemDetails | null>(null);

  // MIGRATION: enum-as-int visibility options (VisibilityState: Maximized=0, Minimized=1, None=2; legacy
  // Select Case cmdUpdate L359-363). Bound with [ngValue] so the control value stays a number.
  readonly visibilityOptions: ReadonlyArray<{ value: number; label: string }> = [
    { value: 0, label: 'Maximized' },
    { value: 1, label: 'Minimized' },
    { value: 2, label: 'None' },
  ];

  readonly form = new FormGroup<ModuleSettingsModel>({
    moduleTitle: new FormControl('', { nonNullable: true }),
    iconFile: new FormControl('', { nonNullable: true }),
    alignment: new FormControl('', { nonNullable: true }),
    color: new FormControl('', { nonNullable: true }),
    border: new FormControl('', { nonNullable: true }),
    containerSrc: new FormControl('', { nonNullable: true }),
    displayTitle: new FormControl(true, { nonNullable: true }),
    displayPrint: new FormControl(true, { nonNullable: true }),
    displaySyndicate: new FormControl(false, { nonNullable: true }),
    header: new FormControl('', { nonNullable: true }),
    footer: new FormControl('', { nonNullable: true }),
    visibility: new FormControl(0, { nonNullable: true }),
    inheritViewPermissions: new FormControl(true, { nonNullable: true }),
    startDate: new FormControl<string | null>(null),
    endDate: new FormControl<string | null>(null),
    // MIGRATION: inferred non-negative constraint on the legacy txtCacheTime integer field (empty -> 0, L349-353).
    cacheTime: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    tabId: new FormControl(-1, { nonNullable: true }),
    allTabs: new FormControl(false, { nonNullable: true }),
    isDefaultModule: new FormControl(false, { nonNullable: true }),
    allModules: new FormControl(false, { nonNullable: true }),
  });

  // MIGRATION: ModuleSettings.ascx.vb Page_Load (L191-193) -- access was denied unless the user was in the
  // portal AdministratorRoleName OR the active tab's AdministratorRoles. Here: superuser OR the portal
  // "Administrators" role. The tab-level ActiveTab.AdministratorRoles check is a documented follow-up.
  readonly isPortalAdmin = computed<boolean>(() => {
    const user = this.auth.currentUser();
    if (user === null) {
      return false;
    }
    return user.isSuperUser || user.roles.includes('Administrators');
  });

  // MIGRATION: ModuleSettings.ascx.vb BindData (L138-139) -- the cache row is hidden when the module
  // definition's DefaultCacheTime == Null.NullInteger (-1), i.e. the module does not support caching.
  readonly supportsCaching = computed<boolean>(() => {
    const module = this.loadedModule();
    return module !== null && module.defaultCacheTime !== -1;
  });

  /** Flat top-level error messages (RFC 7807 errors as a string[]) for the form-level summary. */
  readonly errorSummary = computed<string[]>(() => {
    const problem = this.problem();
    if (problem === null) {
      return [];
    }
    const errors = problem.errors;
    // ApiControllerBase Result.Errors -> flat business-error list: surfaced verbatim (unchanged behavior).
    if (Array.isArray(errors)) {
      return errors;
    }
    // MIGRATION: (QA Issue 1, secondary impact) a NON-array `errors` is the [ApiController] per-field
    // validation object (rendered inline by <app-form-control>) OR an empty object on a 500. The per-field map
    // is not itself a summary, so surface the RFC 7807 title + detail here so a 500 -- and any non-field error
    // -- is never silent (previously this returned [] and title/detail were never rendered). Mirrors the
    // parseProblemDetails pattern used by login/profile/role-assignment (AAP 0.1.1 / 0.7.5).
    const messages: string[] = [];
    if (typeof problem.title === 'string' && problem.title.length > 0) {
      messages.push(problem.title);
    }
    if (typeof problem.detail === 'string' && problem.detail.length > 0) {
      messages.push(problem.detail);
    }
    return messages;
  });

  constructor() {
    // MIGRATION: load the module for editing (legacy BindData ran on the non-postback Page_Load, L222-223).
    // The effect re-runs if the :id route input changes (component reuse across module ids).
    effect(() => {
      const moduleId = Number(this.id());
      this.loadModule(moduleId);
    });

    // MIGRATION: Page_Load/cmdUpdate disabled chkAllTabs/chkDefault/chkAllModules/cboTab for non-portal-admins
    // (tab administrators, L215-220 / L333-338). Re-expressed by disabling the lifecycle controls whenever the
    // current user is not a portal administrator. (Tab-admin access itself is a documented follow-up.)
    effect(() => {
      const portalAdmin = this.isPortalAdmin();
      const lifecycleControls = [
        this.form.controls.allTabs,
        this.form.controls.isDefaultModule,
        this.form.controls.allModules,
        this.form.controls.tabId,
      ];
      for (const control of lifecycleControls) {
        if (portalAdmin) {
          control.enable({ emitEvent: false });
        } else {
          control.disable({ emitEvent: false });
        }
      }
    });
  }

  // MIGRATION: cmdUpdate_Click (L326-427) -- guard Page.IsValid (L328), map the form back onto the module and
  // persist via UpdateModule (L385). IsDeleted is reset to False on update (L364). Move/Copy/DeleteAll
  // (L398-418) are orchestrated server-side from allTabs/tabId.
  submit(): void {
    this.saved.set(false);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // MIGRATION: [QA F4-003] move focus + scroll to the first invalid control so an invalid submit gives
      // immediate, visible feedback (the shared <app-form-control> renders the per-field message, F4-002).
      focusFirstInvalidControl(this.host.nativeElement);
      return;
    }

    const raw = this.form.getRawValue();
    // MIGRATION: multi-tenant scoping (review CP3) -- portalId is a REQUIRED query param on the backend update
    // endpoint (EnforceTenant); it is NOT a body field. Sourced from the loaded module's PortalId (L354).
    const portalId = this.resolvePortalId();
    const dto: ModuleUpdateRequest = {
      // MIGRATION: page scoping (TabId, L354) travels in the body. moduleId is the route :id segment;
      // portalId is the query param above; isDeleted is not part of the backend update contract.
      tabId: raw.tabId ?? -1,
      moduleTitle: raw.moduleTitle,
      iconFile: raw.iconFile,
      alignment: raw.alignment,
      color: raw.color,
      border: raw.border,
      containerSrc: raw.containerSrc,
      displayTitle: raw.displayTitle,
      displayPrint: raw.displayPrint,
      displaySyndicate: raw.displaySyndicate,
      header: raw.header,
      footer: raw.footer,
      visibility: raw.visibility,
      inheritViewPermissions: raw.inheritViewPermissions,
      // MIGRATION: empty date -> null mirrors the legacy Null.NullDate handling (L367-376).
      startDate: raw.startDate ? raw.startDate : null,
      endDate: raw.endDate ? raw.endDate : null,
      // MIGRATION: empty cache time -> 0 (legacy default when txtCacheTime is blank, L349-353).
      cacheTime: raw.cacheTime ?? 0,
      allTabs: raw.allTabs,
      // MIGRATION: DTO drift fix (review CP3) -- the editable permission grid is sent as the contract
      // `permissions` field (List<ModulePermissionDto>), NOT the legacy `modulePermissions`.
      permissions: this.modulePermissions(),
      isDefaultModule: raw.isDefaultModule,
      allModules: raw.allModules,
    };

    this.submitting.set(true);
    this.problem.set(null);
    this.moduleService
      .update(Number(this.id()), portalId, dto)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          // MIGRATION: legacy cmdUpdate did Response.Redirect(NavigateURL()) (L421). The SPA has no module-list
          // route (AAP 0.4.2), so a successful save surfaces an in-page success state; Cancel/Delete use the Router.
          this.saved.set(true);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.problem.set(this.toProblemDetails(err));
        },
      });
  }

  // MIGRATION: cmdDelete_Click (L300-312) wired ClientAPI.AddButtonConfirm (L205) -- delete REQUIRES confirmation.
  requestDelete(): void {
    this.showConfirm.set(true);
  }

  // MIGRATION: confirmed delete -> DeleteTabModule(TabId, ModuleId) (L305); then navigate away (module is gone).
  confirmDelete(): void {
    this.showConfirm.set(false);
    this.problem.set(null);
    this.moduleService
      .remove(Number(this.id()), this.resolvePortalId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          void this.router.navigate(['/portals']);
        },
        error: (err: unknown) => {
          this.problem.set(this.toProblemDetails(err));
        },
      });
  }

  /** Dismiss the delete confirmation dialog without deleting. */
  cancelDelete(): void {
    this.showConfirm.set(false);
  }

  // MIGRATION: cmdCancel_Click (L279-286) -- Response.Redirect(NavigateURL()); navigate back to the dashboard.
  cancel(): void {
    void this.router.navigate(['/portals']);
  }

  // MIGRATION: re-expresses editing a row of the legacy permissions grid (dgPermissions) -- toggles AllowAccess
  // immutably so the OnPush view updates.
  onPermissionToggle(index: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.modulePermissions.update((permissions) =>
      permissions.map((permission, position) =>
        position === index ? { ...permission, allowAccess: checked } : permission,
      ),
    );
  }

  // MIGRATION: multi-tenant scoping (review CP3, AAP Section 0.7.1). The backend module GET / PUT / DELETE
  // endpoints all REQUIRE a portalId query param (EnforceTenant). For the user-triggered save / delete paths
  // the portal context is the loaded module's PortalId (falling back to the authenticated user's portal).
  // -1 is the DNN Null.NullInteger sentinel (no portal context). NOTE: this reads the loadedModule signal, so
  // it is used ONLY from save / delete (button handlers) -- never from the load effect, which would otherwise
  // re-fire every time loadModule replaces the loadedModule reference (infinite reload loop).
  private resolvePortalId(): number {
    return this.loadedModule()?.portalId ?? this.auth.currentUser()?.portalId ?? -1;
  }

  private loadModule(moduleId: number): void {
    // MIGRATION: at load the module is not yet known, so the tenant context for the REQUIRED portalId query is
    // the authenticated user's portal. The loadedModule signal is intentionally NOT read on this path (see
    // resolvePortalId) to keep the load effect from depending on it.
    const portalId = this.auth.currentUser()?.portalId ?? -1;
    this.moduleService
      .getById(moduleId, portalId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (module) => {
          this.loadedModule.set(module);
          this.patchForm(module);
        },
      });
  }

  // MIGRATION: BindData (L85-169) -- populate the form from the loaded module. Nullable model fields are coerced
  // to the non-null control types; ISO date strings are sliced to yyyy-MM-dd for the native date inputs.
  private patchForm(module: Module): void {
    // MIGRATION: DTO drift fix (review CP3) -- the backend ModuleResponse OMITS the permission collection (only
    // the scalar `permissions` string is read), so the editable grid starts empty; the user re-specifies
    // permissions on save, sent as the contract `permissions` field. Recorded in MIGRATION_NOTES.md.
    this.modulePermissions.set([]);
    this.form.patchValue({
      moduleTitle: module.moduleTitle ?? '',
      iconFile: module.iconFile ?? '',
      alignment: module.alignment ?? '',
      color: module.color ?? '',
      border: module.border ?? '',
      containerSrc: module.containerSrc ?? '',
      displayTitle: module.displayTitle,
      displayPrint: module.displayPrint,
      displaySyndicate: module.displaySyndicate,
      header: module.header ?? '',
      footer: module.footer ?? '',
      visibility: module.visibility,
      inheritViewPermissions: module.inheritViewPermissions,
      startDate: module.startDate ? module.startDate.substring(0, 10) : null,
      endDate: module.endDate ? module.endDate.substring(0, 10) : null,
      cacheTime: module.cacheTime,
      tabId: module.tabId ?? -1,
      allTabs: module.allTabs,
    });
  }

  // MIGRATION: [QA F4-013] delegate to the shared normaliser so a status-0 transport failure surfaces the
  // friendly "Unable to reach the server." envelope (the previous blind cast returned a ProgressEvent-shaped
  // object with no title/detail, producing a silent failure) while structured RFC 7807 errors are preserved.
  private toProblemDetails(error: unknown): ProblemDetails {
    return normalizeProblemDetails(error);
  }
}
