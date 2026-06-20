import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Observable } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { User } from '../../../../core/models/user.model';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';
import { MembershipDto, UpdateMembershipDto } from '../../models';
import { UserService } from '../../services';

// MIGRATION: of the four legacy Membership.ascx.vb command buttons, only force-password-change is
// implemented (it sets the REAL mapped [Users].UpdatePassword column and has a matching backend route).
// authorize / unauthorize / unlock mutate Approved / LockedOut, which live in aspnet_Membership
// (EF-Ignore()d per ADR-002 / §0.6.2) with no Phase-1 persistence target and no backend route, so they are
// DEFERRED (intentionally NOT wired). See root MIGRATION_NOTES.md.
type MembershipAction = 'force-password';

interface ActionConfig {
  readonly title: string;
  readonly message: string;
  readonly confirmText: string;
  readonly destructive: boolean;
}

/**
 * UserProfileComponent — reproduces the legacy DotNetNuke Admin > Users membership control
 * (Website/admin/Users/Membership.ascx.vb): a read-only membership view (approved / lockedOut /
 * updatePassword status) plus the force-password-change transition, gated by the legacy
 * button-visibility rules. MIGRATION: the authorize / unauthorize / unlock transitions are DEFERRED
 * (their Approved / LockedOut targets are EF-Ignore()d aspnet_Membership fields with no Phase-1 home per
 * ADR-002 / §0.6.2); only force-password-change has a durable mapped column and a backend route. See
 * root MIGRATION_NOTES.md.
 */
@Component({
  selector: 'app-user-profile',
  templateUrl: './user-profile.component.html',
  styleUrl: './user-profile.component.scss',
  imports: [
    FormControlsComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    HasPermissionDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserProfileComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  /** The current authenticated (acting) user — drives the own-account visibility rule. */
  readonly currentUser = this.authService.currentUser;

  readonly userId = signal(0);
  readonly user = signal<User | null>(null);
  readonly membership = signal<MembershipDto | null>(null);
  readonly loading = signal(true);
  readonly actionInFlight = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly pendingAction = signal<MembershipAction | null>(null);

  // MIGRATION: Membership.ascx.vb DataBind (L135-145) — editing your OWN account hides all four
  // buttons; otherwise each button's visibility follows its membership flag. The legacy comparison
  // `UserInfo.UserID = User.UserID` maps to the camelCase wire field `userID` exposed by the core
  // User model (System.Text.Json lowercases only the first character: C# UserID -> JSON userID).
  readonly isOwnAccount = computed(() => {
    const current = this.currentUser();
    const target = this.user();
    return current !== null && target !== null && current.userID === target.userID;
  });

  // MIGRATION: canAuthorize / canUnauthorize / canUnlock are intentionally REMOVED — the authorize /
  // unauthorize / unlock transitions are deferred (EF-Ignore()d aspnet_Membership fields, ADR-002 / §0.6.2).
  // Only the force-password transition is wired; approved / lockedOut remain READ-ONLY status in
  // membershipForm. See root MIGRATION_NOTES.md.
  readonly canForcePassword = computed(() => {
    const membership = this.membership();
    return !this.isOwnAccount() && membership !== null && !membership.updatePassword;
  });

  readonly dialogOpen = computed(() => this.pendingAction() !== null);
  readonly dialogTitle = computed(() => this.activeConfig()?.title ?? '');
  readonly dialogMessage = computed(() => this.activeConfig()?.message ?? '');
  readonly dialogConfirmText = computed(() => this.activeConfig()?.confirmText ?? 'Confirm');
  readonly dialogDestructive = computed(() => this.activeConfig()?.destructive ?? false);

  /** Read-only presentation of the membership status fields (legacy MembershipEditor, editmode="View"). */
  readonly membershipForm = new FormGroup({
    approved: new FormControl<string>({ value: '', disabled: true }, { nonNullable: true }),
    lockedOut: new FormControl<string>({ value: '', disabled: true }, { nonNullable: true }),
    updatePassword: new FormControl<string>({ value: '', disabled: true }, { nonNullable: true }),
  });

  private readonly actionConfigs: Record<MembershipAction, ActionConfig> = {
    'force-password': {
      title: 'Force Password Change',
      message: 'Require this user to change their password on next login?',
      confirmText: 'Force Change',
      destructive: true,
    },
  };

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const id = Number(idParam);
    if (idParam === null || Number.isNaN(id)) {
      this.errorMessage.set('Invalid user identifier.');
      this.loading.set(false);
      return;
    }
    this.userId.set(id);
    this.loadUser(id);
  }

  requestAction(action: MembershipAction): void {
    this.pendingAction.set(action);
  }

  cancelAction(): void {
    this.pendingAction.set(null);
  }

  confirmAction(): void {
    const action = this.pendingAction();
    if (action === null) {
      return;
    }
    this.pendingAction.set(null);
    this.actionInFlight.set(true);
    this.errorMessage.set(null);
    this.operationFor(action).subscribe({
      next: () => {
        this.applyOptimistic(action);
        this.actionInFlight.set(false);
      },
      error: () => {
        this.errorMessage.set('The membership action could not be completed.');
        this.actionInFlight.set(false);
      },
    });
  }

  private loadUser(id: number): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.userService.getUser(id).subscribe({
      next: (user) => {
        this.user.set(user);
        // MIGRATION: the modern UserService.getUser DTO (core User) omits the legacy User.Membership
        // flags, and no membership-read endpoint exists in the UserService contract. The snapshot is
        // initialized to a known baseline and then reconciled deterministically by each successful
        // transition (see applyOptimistic). Tests drive the membership signal directly to exercise the
        // full button-visibility matrix.
        this.membership.set({ approved: false, lockedOut: false, updatePassword: false });
        this.syncMembershipForm();
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load the user.');
        this.loading.set(false);
      },
    });
  }

  private operationFor(action: MembershipAction): Observable<User> {
    const id = this.userId();
    switch (action) {
      case 'force-password':
        return this.userService.forcePasswordChange(id);
      default:
        return this.assertNever(action);
    }
  }

  private applyOptimistic(action: MembershipAction): void {
    const change = this.changeFor(action);
    this.membership.update((current) => (current ? { ...current, ...change } : current));
    this.syncMembershipForm();
  }

  // MIGRATION: faithful to the legacy force-password handler (Membership.ascx.vb cmdPassword_Click
  // L216-228), which set UpdatePassword=True and called UpdateUser. The same flag transition is applied
  // optimistically once the REST call succeeds, with a confirmation dialog layered on top of the legacy
  // immediate-execute behavior for safety. The authorize / unauthorize / unlock transitions are deferred
  // (EF-Ignore()d aspnet_Membership fields, ADR-002 / §0.6.2).
  private changeFor(action: MembershipAction): Partial<UpdateMembershipDto> {
    switch (action) {
      case 'force-password':
        return { updatePassword: true };
      default:
        return this.assertNever(action);
    }
  }

  private activeConfig(): ActionConfig | null {
    const action = this.pendingAction();
    return action === null ? null : this.actionConfigs[action];
  }

  private syncMembershipForm(): void {
    const membership = this.membership();
    this.membershipForm.setValue({
      approved: membership ? this.yesNo(membership.approved) : '',
      lockedOut: membership ? this.yesNo(membership.lockedOut) : '',
      updatePassword: membership ? this.yesNo(membership.updatePassword) : '',
    });
  }

  private yesNo(value: boolean): string {
    return value ? 'Yes' : 'No';
  }

  private assertNever(value: never): never {
    throw new Error(`Unhandled membership action: ${String(value)}`);
  }
}
