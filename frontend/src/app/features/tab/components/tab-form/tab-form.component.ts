// MIGRATION: Reinterprets the legacy DotNetNuke Web Forms "Page/Tab" admin edit control
// (Website/admin/Tabs/*.ascx.vb) as a stateless Angular 19 standalone reactive form. The
// postback/ViewState code-behind is replaced by client-side reactive validation that mirrors the
// backend FluentValidation rules (CreateTabValidator/UpdateTabValidator). Create vs. edit mode is
// derived from the ':id' route param. Tab identity is portal-scoped, so the owning portal is sourced
// from the authenticated user (JWT) — there is no PortalModuleBase.PortalId in the SPA.
import {
  ChangeDetectionStrategy,
  Component,
  type OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import type { CreateTab, Tab, UpdateTab } from '../../models';
import { TabService } from '../../services';
import type { ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';

/**
 * Strongly-typed reactive form model for the writable subset of a Tab (Page). Mixed nullability:
 * text fields are non-nullable (empty string = "unset", coalesced to null on save); the nullable
 * wire fields (parentId, refreshInterval, startDate, endDate) are modeled as `T | null` controls.
 */
interface TabFormModel {
  tabName: FormControl<string>;
  title: FormControl<string>;
  description: FormControl<string>;
  keyWords: FormControl<string>;
  url: FormControl<string>;
  iconFile: FormControl<string>;
  skinSrc: FormControl<string>;
  containerSrc: FormControl<string>;
  tabOrder: FormControl<number>;
  refreshInterval: FormControl<number | null>;
  parentId: FormControl<number | null>;
  startDate: FormControl<string | null>;
  endDate: FormControl<string | null>;
  isVisible: FormControl<boolean>;
  isSecure: FormControl<boolean>;
}

@Component({
  selector: 'app-tab-form',
  templateUrl: './tab-form.component.html',
  styleUrl: './tab-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    FormControlsComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    HasPermissionDirective,
  ],
})
export class TabFormComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly tabService = inject(TabService);
  private readonly auth = inject(AuthService);

  /** Route ':id' parsed to a number, or null in create mode. */
  readonly tabId = signal<number | null>(null);
  readonly isEditMode = computed<boolean>(() => this.tabId() !== null);
  readonly heading = computed<string>(() => (this.isEditMode() ? 'Edit Page' : 'Add Page'));

  readonly loadingTab = signal<boolean>(false);
  readonly submitting = signal<boolean>(false);
  readonly loadError = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string[]> | null>(null);
  readonly showDeleteDialog = signal<boolean>(false);

  /** Per-validator-key message overrides for inline display (parity with backend messages). */
  readonly tabNameMessages: Record<string, string> = {
    required: 'Tab Name Is Required',
  };
  readonly refreshIntervalMessages: Record<string, string> = {
    min: 'Refresh Interval must be greater than or equal to zero.',
  };

  /** Loaded tab retained in edit mode for carry-over fields not exposed on the form. */
  private loadedTab: Tab | null = null;

  // MIGRATION: Tab endpoints are portal-scoped; the owning portal is read from the authenticated user
  // (UserDto.PortalID -> wire `portalID`). Defaulting to 0 matches the legacy default-portal id.
  private readonly currentPortalId = computed<number>(
    () => this.auth.currentUser()?.portalID ?? 0,
  );

  readonly form: FormGroup<TabFormModel> = new FormGroup<TabFormModel>({
    tabName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    title: new FormControl('', { nonNullable: true }),
    description: new FormControl('', { nonNullable: true }),
    keyWords: new FormControl('', { nonNullable: true }),
    url: new FormControl('', { nonNullable: true }),
    iconFile: new FormControl('', { nonNullable: true }),
    skinSrc: new FormControl('', { nonNullable: true }),
    containerSrc: new FormControl('', { nonNullable: true }),
    tabOrder: new FormControl(0, { nonNullable: true }),
    // MIGRATION: backend rule RefreshInterval >= 0 when supplied; Validators.min(0) is a no-op on null.
    refreshInterval: new FormControl<number | null>(null, { validators: [Validators.min(0)] }),
    // MIGRATION: legacy Null.NullInteger parent sentinel -> null = root/top-level page.
    parentId: new FormControl<number | null>(null),
    startDate: new FormControl<string | null>(null),
    endDate: new FormControl<string | null>(null),
    isVisible: new FormControl(true, { nonNullable: true }),
    isSecure: new FormControl(false, { nonNullable: true }),
  });

  get controls(): TabFormModel {
    return this.form.controls;
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam === null) {
      return;
    }
    const id = Number(idParam);
    if (Number.isNaN(id)) {
      this.loadError.set('Invalid page id.');
      return;
    }
    this.tabId.set(id);
    this.loadTab(id);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.serverErrors.set(null);
    this.submitting.set(true);

    const id = this.tabId();
    if (id !== null) {
      this.tabService.updateTab(id, this.buildUpdateTab(id)).subscribe({
        next: () => this.onSaveSuccess(),
        error: (problem: ProblemDetails) => this.onSaveError(problem),
      });
    } else {
      this.tabService.createTab(this.buildCreateTab()).subscribe({
        next: () => this.onSaveSuccess(),
        error: (problem: ProblemDetails) => this.onSaveError(problem),
      });
    }
  }

  requestDelete(): void {
    this.showDeleteDialog.set(true);
  }

  cancelDelete(): void {
    this.showDeleteDialog.set(false);
  }

  confirmDelete(): void {
    const id = this.tabId();
    if (id === null) {
      return;
    }
    this.showDeleteDialog.set(false);
    this.submitting.set(true);
    // MIGRATION: legacy DeleteTab(TabId, PortalId) is a portal-scoped SOFT delete.
    this.tabService.deleteTab(id, this.currentPortalId()).subscribe({
      next: () => this.onSaveSuccess(),
      error: (problem: ProblemDetails) => this.onSaveError(problem),
    });
  }

  cancel(): void {
    void this.router.navigate(['/tabs']);
  }

  private loadTab(id: number): void {
    this.loadingTab.set(true);
    this.tabService.getTab(id, this.currentPortalId()).subscribe({
      next: (tab) => {
        this.loadedTab = tab;
        this.patchForm(tab);
        this.loadingTab.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(problem.detail ?? problem.title ?? 'Failed to load the page.');
        this.loadingTab.set(false);
      },
    });
  }

  private patchForm(tab: Tab): void {
    // MIGRATION: nullable wire fields are coalesced to the control defaults; ISO date-times are
    // sliced to the YYYY-MM-DD shape the native date input expects.
    this.form.patchValue({
      tabName: tab.tabName ?? '',
      title: tab.title ?? '',
      description: tab.description ?? '',
      keyWords: tab.keyWords ?? '',
      url: tab.url ?? '',
      iconFile: tab.iconFile ?? '',
      skinSrc: tab.skinSrc ?? '',
      containerSrc: tab.containerSrc ?? '',
      tabOrder: tab.tabOrder,
      refreshInterval: tab.refreshInterval,
      parentId: tab.parentId,
      startDate: this.toDateInput(tab.startDate),
      endDate: this.toDateInput(tab.endDate),
      isVisible: tab.isVisible,
      isSecure: tab.isSecure,
    });
  }

  private buildCreateTab(): CreateTab {
    return { ...this.collectWritableFields() };
  }

  private buildUpdateTab(id: number): UpdateTab {
    // MIGRATION: the update contract requires tabID populated (the backend guards path id == body
    // TabID). portalID and all writable fields come from collectWritableFields().
    return { tabID: id, ...this.collectWritableFields() };
  }

  /** Projects the form values onto the shared writable DTO field set (CreateTab shape). */
  private collectWritableFields(): CreateTab {
    const v = this.form.getRawValue();
    return {
      portalID: this.currentPortalId(),
      tabName: v.tabName,
      parentId: v.parentId,
      title: this.nullIfEmpty(v.title),
      description: this.nullIfEmpty(v.description),
      keyWords: this.nullIfEmpty(v.keyWords),
      isVisible: v.isVisible,
      iconFile: this.nullIfEmpty(v.iconFile),
      url: this.nullIfEmpty(v.url),
      skinSrc: this.nullIfEmpty(v.skinSrc),
      containerSrc: this.nullIfEmpty(v.containerSrc),
      startDate: this.nullIfEmpty(v.startDate),
      endDate: this.nullIfEmpty(v.endDate),
      refreshInterval: v.refreshInterval,
      isSecure: v.isSecure,
      tabOrder: v.tabOrder,
    };
  }

  /** Normalizes empty/whitespace text to null (parity with the legacy Null.NullString sentinel). */
  private nullIfEmpty(value: string | null): string | null {
    return value === null || value.trim() === '' ? null : value;
  }

  /** Slices an ISO-8601 date-time to the YYYY-MM-DD shape the native date input expects. */
  private toDateInput(value: string | null): string | null {
    return value === null || value.length === 0 ? null : value.slice(0, 10);
  }

  private onSaveSuccess(): void {
    this.submitting.set(false);
    void this.router.navigate(['/tabs']);
  }

  private onSaveError(problem: ProblemDetails): void {
    this.submitting.set(false);
    this.serverErrors.set(problem.errors ?? null);
  }
}
