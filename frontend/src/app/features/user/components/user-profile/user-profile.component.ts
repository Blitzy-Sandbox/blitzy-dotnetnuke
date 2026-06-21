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
import { forkJoin, Observable } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { User } from '../../../../core/models/user.model';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';
import { MembershipDto, UpdateMembershipDto } from '../../models';
import { UserService } from '../../services';

// MIGRATION (DEV-067): all four legacy Membership.ascx.vb command buttons are wired end-to-end.
// force-password-change sets the mapped [Users].UpdatePassword column; authorize / unauthorize / unlock
// target the [aspnet_Membership] approval/lockout state (now mapped per InstallMembership.sql, bridged from
// [Users].Username) via dedicated backend routes. See root MIGRATION_NOTES.md.
type MembershipAction = 'force-password' | 'authorize' | 'unauthorize' | 'unlock';

interface ActionConfig {
  readonly title: string;
  readonly message: string;
  readonly confirmText: string;
  readonly destructive: boolean;
}

/**
 * UserProfileComponent — reproduces the legacy DotNetNuke Admin > Users membership control
 * (Website/admin/Users/Membership.ascx.vb): a read-only membership view (approved / lockedOut /
 * updatePassword status) plus the four membership-state transitions (force-password-change, authorize,
 * unauthorize, unlock), each gated by the legacy button-visibility rules. MIGRATION (DEV-067): all four
 * transitions are implemented end-to-end — force-password-change sets the mapped [Users].UpdatePassword
 * column, while authorize / unauthorize / unlock target the [aspnet_Membership] approval/lockout state
 * (now mapped per InstallMembership.sql, bridged from [Users].Username). The membership status is read from
 * the GET /api/v1/users/{id}/membership projection on load. See root MIGRATION_NOTES.md.
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

  // MIGRATION (DEV-067): the four button-visibility rules, faithful to Membership.ascx.vb DataBind
  // (L135-145): editing your OWN account hides every button; otherwise Authorize shows when not yet
  // approved, Unauthorize when approved, Unlock when locked out, and Force-Password when a change is not
  // already pending. Each predicate mirrors the legacy `cmdX.Visible = ...` assignment.
  readonly canAuthorize = computed(() => {
    const membership = this.membership();
    return !this.isOwnAccount() && membership !== null && !membership.approved;
  });

  readonly canUnauthorize = computed(() => {
    const membership = this.membership();
    return !this.isOwnAccount() && membership !== null && membership.approved;
  });

  readonly canUnlock = computed(() => {
    const membership = this.membership();
    return !this.isOwnAccount() && membership !== null && membership.lockedOut;
  });

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
    authorize: {
      title: 'Authorize User',
      message: "Approve this user's membership so they can sign in?",
      confirmText: 'Authorize',
      destructive: false,
    },
    unauthorize: {
      title: 'Unauthorize User',
      message: "Revoke this user's membership approval? They will no longer be able to sign in.",
      confirmText: 'Unauthorize',
      destructive: true,
    },
    unlock: {
      title: 'Unlock User',
      message: "Clear the lockout on this user's account so they can attempt to sign in again?",
      confirmText: 'Unlock',
      destructive: false,
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
    // MIGRATION (DEV-067): the membership status now comes from the REAL backend projection
    // (GET /api/v1/users/{id}/membership -> MembershipDto), loaded in parallel with the core user record.
    // This replaces the previous hard-coded baseline: the displayed approved / lockedOut / updatePassword
    // flags, and therefore the button-visibility matrix, reflect the persisted [aspnet_Membership] state.
    forkJoin({
      user: this.userService.getUser(id),
      membership: this.userService.getMembership(id),
    }).subscribe({
      next: ({ user, membership }) => {
        this.user.set(user);
        this.membership.set(membership);
        this.syncMembershipForm();
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load the user.');
        this.loading.set(false);
      },
    });
  }

  private operationFor(action: MembershipAction): Observable<User | MembershipDto> {
    const id = this.userId();
    switch (action) {
      case 'force-password':
        return this.userService.forcePasswordChange(id);
      case 'authorize':
        return this.userService.authorizeUser(id);
      case 'unauthorize':
        return this.userService.unauthorizeUser(id);
      case 'unlock':
        return this.userService.unlockUser(id);
      default:
        return this.assertNever(action);
    }
  }

  private applyOptimistic(action: MembershipAction): void {
    const change = this.changeFor(action);
    this.membership.update((current) => (current ? { ...current, ...change } : current));
    this.syncMembershipForm();
  }

  // MIGRATION (DEV-067): each transition's deterministic effect on the displayed membership flags, faithful
  // to the legacy Membership.ascx.vb handlers — cmdPassword_Click (UpdatePassword=True), cmdAuthorize_Click
  // (Approved=True), cmdUnAuthorize_Click (Approved=False), cmdUnLock_Click (LockedOut=False). The server is
  // the source of truth; this projection is applied once the REST call succeeds so the read-only status view
  // and the button-visibility matrix update without a full reload, with a confirmation dialog layered on top
  // of the legacy immediate-execute behavior for safety.
  private changeFor(action: MembershipAction): Partial<UpdateMembershipDto> {
    switch (action) {
      case 'force-password':
        return { updatePassword: true };
      case 'authorize':
        return { approved: true };
      case 'unauthorize':
        return { approved: false };
      case 'unlock':
        return { lockedOut: false };
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
