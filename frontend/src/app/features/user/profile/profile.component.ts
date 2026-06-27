// MIGRATION: Angular 19 standalone replacement for the legacy DNN "Admin -> User Profile" editor
// (Website/admin/Users/Profile.ascx.vb, 238 lines, class Profile : ProfileUserControlBase).
//
// CP-final review (profile workflow parity): the profile workflow is now IMPLEMENTED end-to-end. The backend
// exposes GET/PUT /api/users/{id}/profile, projecting to/from the EXISTING DNN profile EAV
// ([ProfilePropertyDefinition] + [UserProfile]) with no schema change (AAP 0.7.1). This screen binds those
// endpoints to a typed reactive form over the well-known DNN profile properties. All Web Forms postback /
// ViewState / ProfileUserControlBase / PropertyEditor machinery is discarded.
//
// VALIDATION (AAP 0.7.3 -- services are API communication only; no business logic in the SPA): the legacy
// per-property Required / Length / ValidationExpression rules are DATA-DRIVEN by each portal's
// [ProfilePropertyDefinition] rows, which are NOT exposed to the SPA. The form therefore submits and surfaces
// the SERVER's RFC 7807 validation messages (the authoritative, portal-specific rules) rather than hardcoding
// client validators that could diverge from a portal's configuration. The flat form shows the standard DNN
// profile fields; properties a portal does not define are simply no-ops on save (matching SetProfileProperty).
// Recorded in MIGRATION_NOTES.md.
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';

import { UserService } from '../user.service';
import { AuthService } from '../../../core/auth/auth.service';
import { parseProblemDetails } from '../../../core/interceptors/error.interceptor';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { User, UserProfile } from '../../../core/models';

// MIGRATION: typed reactive form over the well-known DNN profile properties (UserProfile.vb accessors).
// All text fields are non-nullable string controls (empty string == Null.NullString); timeZone is the
// legacy Integer (-1 == Null.NullInteger when unset). fullName is server-composed and is NOT an editable
// control (it is shown read-only in the header).
interface ProfileForm {
  firstName: FormControl<string>;
  lastName: FormControl<string>;
  cell: FormControl<string>;
  telephone: FormControl<string>;
  fax: FormControl<string>;
  im: FormControl<string>;
  street: FormControl<string>;
  unit: FormControl<string>;
  city: FormControl<string>;
  region: FormControl<string>;
  country: FormControl<string>;
  postalCode: FormControl<string>;
  preferredLocale: FormControl<string>;
  timeZone: FormControl<number>;
  website: FormControl<string>;
}

@Component({
  selector: 'app-profile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, LoadingSpinnerComponent],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent {
  // MIGRATION: components talk only to the feature service (never ApiService/HttpClient directly).
  private readonly userService = inject(UserService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  // MIGRATION: route input binding (withComponentInputBinding() is enabled in app.config.ts). `id` is the user
  // whose profile is edited (the ':id/profile' route param). Parsed to a number for the tenant-scoped lookups.
  readonly id = input<string>();
  readonly userId = computed<number>(() => {
    const raw = this.id();
    const parsed = raw != null ? Number(raw) : Number.NaN;
    return Number.isNaN(parsed) ? 0 : parsed;
  });

  // The user whose profile is being edited, loaded for header display context (display name / username).
  readonly loadedUser = signal<User | null>(null);
  // The server-composed full name (FirstName + " " + LastName), shown read-only.
  readonly fullName = signal<string | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly saved = signal<boolean>(false);
  // MIGRATION: the backend returns its data-driven validation failures as a flat RFC 7807 errors[] list
  // (ApiControllerBase Result.Errors). They are surfaced verbatim as the portal-authoritative messages.
  readonly errorMessages = signal<string[]>([]);

  // MIGRATION: typed reactive form replacing the dynamic PropertyEditor (Profile.ascx.vb). Empty string maps to
  // Null.NullString on save; timeZone defaults to -1 (Null.NullInteger) until the user enters a value.
  readonly form = new FormGroup<ProfileForm>({
    firstName: new FormControl<string>('', { nonNullable: true }),
    lastName: new FormControl<string>('', { nonNullable: true }),
    cell: new FormControl<string>('', { nonNullable: true }),
    telephone: new FormControl<string>('', { nonNullable: true }),
    fax: new FormControl<string>('', { nonNullable: true }),
    im: new FormControl<string>('', { nonNullable: true }),
    street: new FormControl<string>('', { nonNullable: true }),
    unit: new FormControl<string>('', { nonNullable: true }),
    city: new FormControl<string>('', { nonNullable: true }),
    region: new FormControl<string>('', { nonNullable: true }),
    country: new FormControl<string>('', { nonNullable: true }),
    postalCode: new FormControl<string>('', { nonNullable: true }),
    preferredLocale: new FormControl<string>('', { nonNullable: true }),
    timeZone: new FormControl<number>(-1, { nonNullable: true }),
    website: new FormControl<string>('', { nonNullable: true }),
  });

  constructor() {
    // MIGRATION: replaces Page_Load (Profile.ascx.vb L204-208). Loads the routed user (header context) and the
    // user's profile (form). The tenant `portalId` query (AAP Section 0.7.1) is sourced from the authenticated
    // principal ONLY -- the effect must not read loadedUser()/fullName(), otherwise setting them on load
    // completion would re-fire the effect (infinite reload loop).
    effect(() => {
      const targetId = this.userId();
      if (targetId <= 0) {
        return;
      }
      const portalId = this.auth.currentUser()?.portalId ?? -1;
      this.loading.set(true);
      this.errorMessages.set([]);

      // Header context (display name / username). A failed lookup is non-fatal -- the form still loads.
      this.userService
        .getById(targetId, portalId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (user) => this.loadedUser.set(user),
          error: () => undefined,
        });

      // The editable profile.
      this.userService
        .getProfile(targetId, portalId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (profile) => {
            this.patchForm(profile);
            this.loading.set(false);
          },
          error: (err: HttpErrorResponse) => {
            this.loading.set(false);
            this.errorMessages.set(this.messagesFrom(err, 'The profile could not be loaded.'));
          },
        });
    });
  }

  // MIGRATION: cmdUpdate_Click (Profile.ascx.vb) -> PUT /users/{id}/profile. The server enforces the portal's
  // data-driven Required / Length / ValidationExpression rules and returns the re-projected profile on success.
  onSave(): void {
    const targetId = this.userId();
    if (targetId <= 0) {
      return;
    }
    const portalId = this.auth.currentUser()?.portalId ?? -1;
    const dto = this.buildDto();

    this.saved.set(false);
    this.errorMessages.set([]);
    this.saving.set(true);
    this.userService
      .updateProfile(targetId, portalId, dto)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (profile) => {
          // Re-bind from the canonical persisted representation (e.g. the server-composed fullName).
          this.patchForm(profile);
          this.saving.set(false);
          this.saved.set(true);
        },
        error: (err: HttpErrorResponse) => {
          this.saving.set(false);
          this.errorMessages.set(this.messagesFrom(err, 'The profile could not be saved.'));
        },
      });
  }

  // MIGRATION: replaces cmdCancel / the legacy return-to-list navigation. Returns to the user grid.
  back(): void {
    void this.router.navigate(['/users']);
  }

  // MIGRATION: patches the form from a server profile, coalescing Null.NullString -> '' and a missing/negative
  // timeZone -> -1. Also captures the server-composed fullName for the header.
  private patchForm(profile: UserProfile): void {
    this.fullName.set(profile.fullName ?? null);
    this.form.patchValue({
      firstName: profile.firstName ?? '',
      lastName: profile.lastName ?? '',
      cell: profile.cell ?? '',
      telephone: profile.telephone ?? '',
      fax: profile.fax ?? '',
      im: profile.im ?? '',
      street: profile.street ?? '',
      unit: profile.unit ?? '',
      city: profile.city ?? '',
      region: profile.region ?? '',
      country: profile.country ?? '',
      postalCode: profile.postalCode ?? '',
      preferredLocale: profile.preferredLocale ?? '',
      timeZone: this.normalizeTimeZone(profile.timeZone),
      website: profile.website ?? '',
    });
  }

  // MIGRATION: builds the outbound UserProfile from the form. Empty strings map to null (Null.NullString); the
  // server-composed fullName is intentionally omitted (it is read-only). timeZone is forwarded as an integer.
  private buildDto(): UserProfile {
    const v = this.form.getRawValue();
    return {
      firstName: this.emptyToNull(v.firstName),
      lastName: this.emptyToNull(v.lastName),
      cell: this.emptyToNull(v.cell),
      telephone: this.emptyToNull(v.telephone),
      fax: this.emptyToNull(v.fax),
      im: this.emptyToNull(v.im),
      street: this.emptyToNull(v.street),
      unit: this.emptyToNull(v.unit),
      city: this.emptyToNull(v.city),
      region: this.emptyToNull(v.region),
      country: this.emptyToNull(v.country),
      postalCode: this.emptyToNull(v.postalCode),
      preferredLocale: this.emptyToNull(v.preferredLocale),
      timeZone: this.normalizeTimeZone(v.timeZone),
      website: this.emptyToNull(v.website),
    };
  }

  // MIGRATION: a number <input> can yield null at runtime when cleared (despite the non-nullable type) -- coerce
  // any null/NaN to -1 (Null.NullInteger), matching the legacy unset TimeZone.
  private normalizeTimeZone(value: number | null | undefined): number {
    return value == null || Number.isNaN(value) ? -1 : value;
  }

  private emptyToNull(value: string): string | null {
    return value === '' ? null : value;
  }

  // Extracts the user-facing messages from a backend RFC 7807 failure (reuses the canonical interceptor parser),
  // falling back to the supplied default when the body carries no message.
  private messagesFrom(error: HttpErrorResponse, fallback: string): string[] {
    const parsed = parseProblemDetails(error.error);
    return parsed.messages.length > 0 ? parsed.messages : [fallback];
  }
}
