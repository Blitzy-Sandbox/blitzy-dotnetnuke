// MIGRATION: Reinterprets the legacy DNN "Page Settings" advanced configuration surface as an
// Angular 19 standalone screen. Where tab-form edits a page's CONTENT/SEO fields, this screen manages
// the APPEARANCE (skin/container/icon), SCHEDULING (start/end date), navigation VISIBILITY and the
// SECURITY (HTTPS) settings of an EXISTING page. It loads the tab by ':id', patches the settings
// subset, and persists via PUT (carrying over the page's content fields unchanged). Tab identity is
// portal-scoped, so the owning portal is sourced from the authenticated user (JWT).
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

import type { Tab, UpdateTab } from '../../models';
import { TabService } from '../../services';
import type { ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';

/** Settings subset of a Tab (appearance + scheduling + visibility + security). */
interface TabSettingsForm {
  isVisible: FormControl<boolean>;
  isSecure: FormControl<boolean>;
  refreshInterval: FormControl<number | null>;
  iconFile: FormControl<string>;
  skinSrc: FormControl<string>;
  containerSrc: FormControl<string>;
  startDate: FormControl<string | null>;
  endDate: FormControl<string | null>;
}

@Component({
  selector: 'app-tab-settings',
  templateUrl: './tab-settings.component.html',
  styleUrl: './tab-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    FormControlsComponent,
    HasPermissionDirective,
    LoadingSpinnerComponent,
  ],
})
export class TabSettingsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly tabService = inject(TabService);
  private readonly auth = inject(AuthService);

  readonly tabId = signal<number>(0);
  readonly tab = signal<Tab | null>(null);
  readonly loading = signal<boolean>(true);
  readonly saving = signal<boolean>(false);
  readonly saved = signal<boolean>(false);
  readonly loadError = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string[]> | null>(null);

  readonly refreshIntervalMessages: Record<string, string> = {
    min: 'Refresh Interval must be greater than or equal to zero.',
  };

  /** Read-only page-name context shown in the header. */
  readonly pageName = computed<string>(() => this.tab()?.tabName ?? '');

  // MIGRATION: portal-scoped; the owning portal is read from the authenticated user (wire `portalID`).
  private readonly currentPortalId = computed<number>(
    () => this.auth.currentUser()?.portalID ?? 0,
  );

  readonly form: FormGroup<TabSettingsForm> = new FormGroup<TabSettingsForm>({
    isVisible: new FormControl(true, { nonNullable: true }),
    isSecure: new FormControl(false, { nonNullable: true }),
    refreshInterval: new FormControl<number | null>(null, { validators: [Validators.min(0)] }),
    iconFile: new FormControl('', { nonNullable: true }),
    skinSrc: new FormControl('', { nonNullable: true }),
    containerSrc: new FormControl('', { nonNullable: true }),
    startDate: new FormControl<string | null>(null),
    endDate: new FormControl<string | null>(null),
  });

  get controls(): TabSettingsForm {
    return this.form.controls;
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const id = Number(idParam);
    if (!idParam || Number.isNaN(id)) {
      this.loading.set(false);
      this.loadError.set('Invalid page id.');
      return;
    }
    this.tabId.set(id);
    this.loadTab(id);
  }

  private loadTab(id: number): void {
    this.loading.set(true);
    this.tabService.getTab(id, this.currentPortalId()).subscribe({
      next: (tab) => {
        this.tab.set(tab);
        this.patchForm(tab);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loading.set(false);
        this.loadError.set(problem.detail ?? problem.title ?? 'Failed to load page settings.');
      },
    });
  }

  private patchForm(tab: Tab): void {
    this.form.patchValue({
      isVisible: tab.isVisible,
      isSecure: tab.isSecure,
      refreshInterval: tab.refreshInterval,
      iconFile: tab.iconFile ?? '',
      skinSrc: tab.skinSrc ?? '',
      containerSrc: tab.containerSrc ?? '',
      startDate: this.toDateInput(tab.startDate),
      endDate: this.toDateInput(tab.endDate),
    });
  }

  onSubmit(): void {
    this.saved.set(false);
    this.serverErrors.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const tab = this.tab();
    if (tab === null) {
      return;
    }

    const v = this.form.getRawValue();
    // MIGRATION: carry the page's content fields over unchanged; only the appearance/scheduling/
    // visibility/security subset is editable here. tabID is forced to the route id (PUT guards
    // path id == body TabID).
    const dto: UpdateTab = {
      tabID: this.tabId(),
      portalID: tab.portalID,
      tabName: tab.tabName,
      parentId: tab.parentId,
      title: tab.title,
      description: tab.description,
      keyWords: tab.keyWords,
      url: tab.url,
      tabOrder: tab.tabOrder,
      isVisible: v.isVisible,
      iconFile: this.nullIfEmpty(v.iconFile),
      skinSrc: this.nullIfEmpty(v.skinSrc),
      containerSrc: this.nullIfEmpty(v.containerSrc),
      startDate: this.nullIfEmpty(v.startDate),
      endDate: this.nullIfEmpty(v.endDate),
      refreshInterval: v.refreshInterval,
      isSecure: v.isSecure,
    };

    this.saving.set(true);
    this.tabService.updateTab(this.tabId(), dto).subscribe({
      next: (updated) => {
        this.tab.set(updated);
        this.patchForm(updated);
        this.form.markAsPristine();
        this.saving.set(false);
        this.saved.set(true);
      },
      error: (problem: ProblemDetails) => {
        this.serverErrors.set(problem.errors ?? null);
        this.saving.set(false);
      },
    });
  }

  onCancel(): void {
    void this.router.navigate(['/tabs']);
  }

  private nullIfEmpty(value: string | null): string | null {
    return value === null || value.trim() === '' ? null : value;
  }

  private toDateInput(value: string | null): string | null {
    return value === null || value.length === 0 ? null : value.slice(0, 10);
  }
}
