// MIGRATION: Angular 19 standalone replacement for the legacy DNN "Admin -> User Profile" editor
// (Website/admin/Users/Profile.ascx.vb, 238 lines, class Profile : ProfileUserControlBase).
//
// DEFERRAL (AAP D1 / 0.3.4): the legacy profile workflow has NO endpoint in the frozen backend contract. The
// AAP explicitly defers it for this phase: "the legacy profile workflow (UserProfileDto) is out of scope for
// this phase per the AAP." The previous implementation called UserService.getProfile()/updateProfile() against
// `users/{id}/profile`, which does not exist on the backend and returns 404. Rather than add a backend endpoint
// (which would contradict the frozen contract), the frontend is aligned by converting this screen to a clear,
// accessible deferred-notice -- consistent with the forgot-password and module import/export deferrals. All Web
// Forms postback/ViewState/ProfileUserControlBase/PropertyEditor machinery and the dynamic ProfilePropertyValue
// form are discarded. The screen still resolves the routed user (tenant-scoped GET /users/{id}?portalId=) so the
// notice can name the account whose profile editing is pending. Recorded in MIGRATION_NOTES.md.
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
import { Router } from '@angular/router';

import { UserService } from '../user.service';
import { AuthService } from '../../../core/auth/auth.service';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { User } from '../../../core/models';

@Component({
  selector: 'app-profile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LoadingSpinnerComponent],
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
  // whose profile would be edited (the ':id/profile' route param). Parsed to a number for the context lookup.
  readonly id = input<string>();
  readonly userId = computed<number>(() => {
    const raw = this.id();
    const parsed = raw != null ? Number(raw) : Number.NaN;
    return Number.isNaN(parsed) ? 0 : parsed;
  });

  // The user whose profile is pending, loaded for display context only (notice copy + back navigation).
  readonly loadedUser = signal<User | null>(null);
  readonly loading = signal<boolean>(false);

  constructor() {
    // MIGRATION: replaces Page_Load (Profile.ascx.vb L204-208). Loads the routed user for display context. The
    // tenant `portalId` query (AAP Section 0.7.1) is sourced from the authenticated principal ONLY -- the effect
    // must not read loadedUser(), otherwise setting it on load completion would re-fire the effect (infinite
    // reload loop). A failed lookup is non-fatal: the deferred notice renders regardless.
    effect(() => {
      const targetId = this.userId();
      if (targetId <= 0) {
        return;
      }
      const portalId = this.auth.currentUser()?.portalId ?? -1;
      this.loading.set(true);
      this.userService
        .getById(targetId, portalId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (user) => {
            this.loadedUser.set(user);
            this.loading.set(false);
          },
          error: () => {
            this.loading.set(false);
          },
        });
    });
  }

  // MIGRATION: replaces cmdCancel / the legacy return-to-list navigation. Returns to the user grid.
  back(): void {
    void this.router.navigate(['/users']);
  }
}
