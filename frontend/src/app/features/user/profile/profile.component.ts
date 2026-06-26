// MIGRATION: Angular 19 replacement for the legacy DNN "Admin -> User Profile" editor
// (Website/admin/Users/Profile.ascx.vb, 238 lines, class Profile : ProfileUserControlBase). Only the
// dynamic-form construction + visibility/required rules are re-expressed here; ALL Web Forms
// postback/ViewState/ProfileUserControlBase/PropertyEditor machinery (DataBind, ShowUpdate, EditorMode,
// OnProfileUpdated/OnProfileUpdateCompleted events) is discarded. The profile-definition MANAGEMENT wizard
// (EditProfileDefinition.ascx.vb) is OUT OF SCOPE: definitions are READ here, not edited. The dynamic typed
// Reactive Form is built from the ProfilePropertyValue[] returned by UserService.getProfile(id) (which
// mirrors Library/Components/Users/Profile/ProfilePropertyDefinition.vb).
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import {
  FormControl,
  FormGroup,
  FormRecord,
  ReactiveFormsModule,
  Validators,
  type ValidatorFn,
} from '@angular/forms';
import { Router } from '@angular/router';

import { UserService, type ProfilePropertyValue } from '../user.service';
import { AuthService } from '../../../core/auth/auth.service';
import { FormControlComponent } from '../../../shared/components/form-controls/form-control.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { ProblemDetails } from '../../../core/models';

// MIGRATION: UserVisibilityMode mirrors ProfilePropertyDefinition.vb Initialize (L348-359): AllUsers=0,
// MembersOnly=1, AdminOnly=2 (the DNN default is AdminOnly when the Profile_DefaultVisibility setting is 2 or
// Null.NullInteger). Used by the per-field visibility selector shown for self-editing users.
export enum UserVisibilityMode {
  AllUsers = 0,
  MembersOnly = 1,
  AdminOnly = 2,
}

// MIGRATION: per-property typed form group. The legacy editor bound each ProfilePropertyDefinition to a row
// with a value editor plus (conditionally) a visibility selector; here every property maps to a FormGroup with
// a `value` control (FormControl<string | null>) and a `visibility` control (FormControl<number>).
interface ProfileFieldForm {
  value: FormControl<string | null>;
  visibility: FormControl<number>;
}

@Component({
  selector: 'app-profile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormControlComponent, LoadingSpinnerComponent],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent {
  private readonly userService = inject(UserService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  // MIGRATION: route input binding (withComponentInputBinding() is enabled in app.config.ts). `id` is the
  // user whose profile is being edited (the route ':id/profile' param). Parsed to a number for service calls.
  readonly id = input<string>();
  readonly userId = computed<number>(() => {
    const raw = this.id();
    const parsed = raw != null ? Number(raw) : Number.NaN;
    return Number.isNaN(parsed) ? 0 : parsed;
  });

  // MIGRATION: admin-vs-self determination. Profile.ascx.vb used the inherited IsAdmin (current user is an
  // Administrator/host) and IsUser (the profile belongs to the current user) flags. isAdmin = acting user is a
  // superuser or holds the Administrators role; isSelf = editing one's own profile.
  readonly isAdmin = computed<boolean>(() => {
    const current = this.auth.currentUser();
    if (current == null) {
      return false;
    }
    return current.isSuperUser || current.roles.includes('Administrators');
  });
  readonly isSelf = computed<boolean>(() => {
    const current = this.auth.currentUser();
    return current != null && current.userId === this.userId();
  });

  // MIGRATION: ShowVisibility (Profile.ascx.vb L58-63): `CType(setting, Boolean) And IsUser`, where setting is
  // the Profile_DisplayVisibility portal setting. That portal setting is not migrated in this phase, so we
  // default to SHOWING the per-field visibility selector for self-edit and HIDING it for admins.
  readonly showVisibilitySelector = computed<boolean>(() => this.isSelf() && !this.isAdmin());

  readonly definitions = signal<ProfilePropertyValue[]>([]);
  readonly loading = signal<boolean>(false);
  readonly submitting = signal<boolean>(false);
  readonly saved = signal<boolean>(false);
  readonly serverErrors = signal<ProblemDetails | null>(null);

  // MIGRATION: DataBind (Profile.ascx.vb L153-174). For admins every property is forced visible
  // (`If IsAdmin Then profProperty.Visible = True`, L164-168); non-admins (self) only see properties with
  // Visible = True. Fields are ordered by ViewOrder ascending (ProfileDefinitions column / Profile render order).
  readonly orderedDefinitions = computed<ProfilePropertyValue[]>(() => {
    const defs = this.definitions();
    const visibleDefs = this.isAdmin() ? defs : defs.filter((def) => def.visible);
    return [...visibleDefs].sort((a, b) => a.viewOrder - b.viewOrder);
  });

  // MIGRATION: surface a flat string[] ProblemDetails.errors payload (the Result.Errors shape) as a form-level
  // summary; <app-form-control> only renders the per-field Record<string, string[]> shape.
  readonly errorSummary = computed<string[]>(() => {
    const errors = this.serverErrors()?.errors;
    return Array.isArray(errors) ? errors : [];
  });

  // MIGRATION: the dynamic typed Reactive Form. Keyed by ProfilePropertyValue.propertyName; each entry is a
  // ProfileFieldForm group. Built dynamically once the definitions load (see the load effect / buildForm).
  readonly form = new FormRecord<FormGroup<ProfileFieldForm>>({});

  readonly visibilityOptions: ReadonlyArray<{ value: UserVisibilityMode; label: string }> = [
    { value: UserVisibilityMode.AllUsers, label: 'All Users' },
    { value: UserVisibilityMode.MembersOnly, label: 'Members Only' },
    { value: UserVisibilityMode.AdminOnly, label: 'Admin Only' },
  ];

  constructor() {
    // MIGRATION: replaces Page_Load (Profile.ascx.vb L204-208) initialisation. Loads the profile property
    // values/definitions for the routed user and builds the form. untracked() keeps the load side-effects from
    // becoming reactive dependencies of this effect (only userId() is tracked), so it runs once per id change.
    effect(() => {
      const targetId = this.userId();
      if (targetId > 0) {
        untracked(() => this.loadProfile(targetId));
      }
    });
  }

  private loadProfile(targetId: number): void {
    this.loading.set(true);
    this.serverErrors.set(null);
    // MIGRATION: replaces ProfileController.GetPropertyDefinitionsByPortal + value binding. The exact endpoint
    // sub-path (users/{id}/profile) is reconciled with the backend UsersController/AuthController; definitions
    // and current values are returned together as ProfilePropertyValue[].
    this.userService.getProfile(targetId).subscribe({
      next: (defs) => {
        this.definitions.set(defs);
        this.buildForm(this.orderedDefinitions());
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.serverErrors.set(this.toProblemDetails(err));
        this.loading.set(false);
      },
    });
  }

  // MIGRATION: dynamic form construction from the ProfilePropertyValue definitions. Each control is keyed by
  // propertyName and initialised to `propertyValue ?? ''` (the migrated ProfilePropertyValue DTO carries no
  // DefaultValue field, so the legacy `propertyValue ?? defaultValue ?? ''` reduces to `propertyValue ?? ''`).
  // Validators preserve the legacy ProfilePropertyDefinition rules EXACTLY: Required -> Validators.required
  // (relaxed for admins per the IsValid `Or IsAdmin` bypass, L94-104), ValidationExpression -> pattern,
  // Length -> maxLength. Controls are added ONLY for the rendered (ordered/visible) definitions.
  private buildForm(defs: ReadonlyArray<ProfilePropertyValue>): void {
    for (const key of Object.keys(this.form.controls)) {
      this.form.removeControl(key);
    }

    const admin = this.isAdmin();
    const showVisibility = this.showVisibilitySelector();

    for (const def of defs) {
      const validators: ValidatorFn[] = [];
      // MIGRATION: IsValid `Or IsAdmin` (Profile.ascx.vb L98) -> admins bypass required-field validation, so
      // Validators.required is NOT attached for admins. Non-admins MUST satisfy required (no silent change).
      if (def.required && !admin) {
        validators.push(Validators.required);
      }
      if (def.validationExpression != null && def.validationExpression.length > 0) {
        validators.push(Validators.pattern(def.validationExpression));
      }
      if (def.length != null && def.length > 0) {
        validators.push(Validators.maxLength(def.length));
      }

      const initialValue: string | null = def.propertyValue ?? '';
      const visibilityControl = new FormControl<number>(def.visibility ?? UserVisibilityMode.AdminOnly, {
        nonNullable: true,
      });
      if (!showVisibility) {
        visibilityControl.disable({ emitEvent: false });
      }

      const group = new FormGroup<ProfileFieldForm>({
        value: new FormControl<string | null>(initialValue, { validators }),
        visibility: visibilityControl,
      });
      this.form.addControl(def.propertyName, group);
    }
  }

  // MIGRATION: cmdUpdate_Click (Profile.ascx.vb L220-232). Legacy guarded on `IsValid` (= ProfileProperties
  // valid OR IsAdmin, L94-104) then called ProfileController.UpdateUserProfile(User, properties). Here we apply
  // the same admin bypass, assemble the updated ProfilePropertyValue[], and call UserService.updateProfile.
  submit(): void {
    if (this.form.invalid && !this.isAdmin()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.saved.set(false);
    this.serverErrors.set(null);

    const properties = this.assembleProperties();
    this.userService.updateProfile(this.userId(), properties).subscribe({
      next: () => {
        this.submitting.set(false);
        // MIGRATION: legacy raised OnProfileUpdated / OnProfileUpdateCompleted (L228-229); the SPA surfaces a
        // success confirmation instead.
        this.saved.set(true);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        // MIGRATION: server-side validation mapping -> RFC 7807 ProblemDetails, surfaced per field via
        // <app-form-control> [errors] and (for flat-array errors) the form-level summary.
        this.serverErrors.set(this.toProblemDetails(err));
      },
    });
  }

  cancel(): void {
    void this.router.navigate(['/users']);
  }

  // MIGRATION: assembles the ProfilePropertyValue[] passed to UpdateUserProfile (Profile.ascx.vb L223-226).
  // Rendered properties carry their edited value (and edited visibility when the self-edit selector is shown);
  // non-rendered properties (hidden from a non-admin) carry their original values unchanged.
  private assembleProperties(): ProfilePropertyValue[] {
    const showVisibility = this.showVisibilitySelector();
    const controls = this.form.controls;
    return this.definitions().map((def) => {
      if (!Object.prototype.hasOwnProperty.call(controls, def.propertyName)) {
        return { ...def };
      }
      const group = controls[def.propertyName];
      const value = group.controls.value.value;
      const visibility = showVisibility ? group.controls.visibility.value : def.visibility;
      return { ...def, propertyValue: value, visibility };
    });
  }

  // The global error interceptor re-throws the original HttpErrorResponse; the RFC 7807 body is on `error`.
  private toProblemDetails(err: unknown): ProblemDetails | null {
    if (err instanceof HttpErrorResponse) {
      const body: unknown = err.error;
      if (typeof body === 'object' && body !== null) {
        return body as ProblemDetails;
      }
    }
    return null;
  }
}
