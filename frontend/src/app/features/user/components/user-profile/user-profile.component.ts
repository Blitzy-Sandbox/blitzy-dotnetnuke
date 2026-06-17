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
import { MembershipDto, UpdateMembershipDto, UpdateUserRequest } from '../../models';
import { UserService } from '../../services';

// MIGRATION (MIGRATION_NOTES.md D-034): the legacy Membership.ascx.vb exposed four command buttons
// (Authorize / Unauthorize / Unlock / Force Password Change). Three of the four are reproduced with a
// persistable contract:
//   - Authorize / Unauthorize -> the `approved` flag on UpdateUserRequest (PUT /api/v1/users/{id}).
//   - Force Password Change -> the REAL `dbo.Users.UpdatePassword` column via
//     POST /api/v1/users/{id}/force-password-change (UserService.forcePasswordChange). The transition is
//     reversible (require / clear). This OVERTURNS the prior "later checkpoint" deferral now that the
//     backend endpoint and the UserDto.UpdatePassword field exist.
// Only Unlock (cmdUnLock -> UnLockUser) remains unimplemented: the lockout state lives on the GUID-keyed,
// out-of-scope `aspnet_Membership` provider table (NOT the in-scope dbo.Users entity), and ADR-002 /
// AAP Â§0.2.2 forbid schema changes and exclude that table â€” an AAP-grounded scope reduction documented in
// MIGRATION_NOTES.md (D-034). It is therefore shown read-only with no transition.
/** The persistable membership-state transitions ported from the legacy Membership.ascx.vb command buttons. */
type MembershipAction =
  | 'authorize'
  | 'unauthorize'
  | 'force-password-change'
  | 'clear-force-password-change';

interface ActionConfig {
  readonly title: string;
  readonly message: string;
  readonly confirmText: string;
  readonly destructive: boolean;
}

/**
 * UserProfileComponent — reproduces the legacy DotNetNuke Admin > Users membership control
 * (Website/admin/Users/Membership.ascx.vb) with UI functional parity: a read-only membership
 * view plus the persistable membership-state transitions, gated by the legacy
 * button-visibility rules.
 *
 * MIGRATION (MIGRATION_NOTES.md D-034): the legacy control executed each transition immediately on
 * click (cmdAuthorize / cmdUnAuthorize -> UserController.UpdateUser; cmdUnLock ->
 * UserController.UnLockUser; cmdPassword -> UserController.UpdateUser). The modern UsersController
 * reproduces three of the four: Authorize / Unauthorize via the `approved` flag on UpdateUserRequest
 * (PUT /api/v1/users/{id}), and Force Password Change via the dedicated
 * POST /api/v1/users/{id}/force-password-change endpoint, which persists the real
 * `dbo.Users.UpdatePassword` column (UserService.forcePasswordChange; reversible require/clear). Only
 * Unlock remains unimplemented — lockout lives on the out-of-scope, GUID-keyed aspnet_Membership table
 * and ADR-002 / AAP §0.2.2 forbid the schema change, so it is shown read-only (AAP-grounded scope
 * reduction). This SPA port adds an explicit confirmation step (ConfirmationDialogComponent) on top of
 * the surviving legacy immediate-execute buttons, and reconstructs the membership snapshot client-side
 * (see loadUser). All data access is delegated to the typed UserService — HttpClient is NEVER injected
 * here.
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
  // buttons; otherwise each button's visibility follows its membership flag. The legacy identity
  // check `UserInfo.UserID = User.UserID` maps to comparing the acting user (AuthService.currentUser)
  // with the loaded target user. The core User model's identifier is `userID` — the System.Text.Json
  // camelCase serialization of the backend C# `UserID` property.
  readonly isOwnAccount = computed(() => {
    const current = this.currentUser();
    const target = this.user();
    return current !== null && target !== null && current.userID === target.userID;
  });

  readonly canAuthorize = computed(() => {
    const membership = this.membership();
    return !this.isOwnAccount() && membership !== null && !membership.approved;
  });

  readonly canUnauthorize = computed(() => {
    const membership = this.membership();
    return !this.isOwnAccount() && membership !== null && membership.approved;
  });

  // MIGRATION: Membership.ascx.vb cmdPassword visibility (L213). The Force Password Change button shows
  // when the user is NOT already required to change their password; the inverse "Clear" button shows when
  // they are. As with Authorize/Unauthorize, both are hidden when editing your OWN account.
  readonly canForcePasswordChange = computed(() => {
    const membership = this.membership();
    return !this.isOwnAccount() && membership !== null && !membership.updatePassword;
  });

  readonly canClearForcePasswordChange = computed(() => {
    const membership = this.membership();
    return !this.isOwnAccount() && membership !== null && membership.updatePassword;
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
    authorize: {
      title: 'Authorize User',
      message: 'Authorize this user account?',
      confirmText: 'Authorize',
      destructive: false,
    },
    unauthorize: {
      title: 'Unauthorize User',
      message: 'Remove authorization from this user account?',
      confirmText: 'Unauthorize',
      destructive: true,
    },
    'force-password-change': {
      title: 'Force Password Change',
      message: 'Require this user to change their password at next login?',
      confirmText: 'Require Change',
      destructive: false,
    },
    'clear-force-password-change': {
      title: 'Clear Password-Change Requirement',
      message: 'Remove the requirement for this user to change their password at next login?',
      confirmText: 'Clear Requirement',
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
    const operation = this.operationFor(action);
    // MIGRATION: a visible action button implies a loaded user + membership snapshot (every canX
    // computed gates on both being non-null), so `operation` is null only in defensive/edge cases
    // (e.g. the confirm fires with no loaded target). Surface a generic error rather than calling the
    // service without a target.
    if (operation === null) {
      this.errorMessage.set('The membership action could not be completed.');
      return;
    }
    this.actionInFlight.set(true);
    this.errorMessage.set(null);
    operation.subscribe({
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
        // MIGRATION (MIGRATION_NOTES.md D-034): the modern UserService.getUser DTO (core User) carries
        // both the `approved` AND `updatePassword` membership flags (UpdatePassword is a real dbo.Users
        // column), so the snapshot is seeded from the real wire values for each. `lockedOut` has no
        // in-scope wire contract (lockout lives on the out-of-scope aspnet_Membership table), so it
        // defaults to false and is shown read-only with no transition (AAP-grounded scope reduction;
        // see the class doc). Tests drive the membership signal directly to exercise the
        // button-visibility matrix.
        this.membership.set({ approved: user.approved, lockedOut: false, updatePassword: user.updatePassword });
        this.syncMembershipForm();
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load the user.');
        this.loading.set(false);
      },
    });
  }

  private operationFor(action: MembershipAction): Observable<User> | null {
    const user = this.user();
    const membership = this.membership();
    if (user === null || membership === null) {
      return null;
    }
    // MIGRATION (MIGRATION_NOTES.md D-034): Force Password Change persists to the REAL
    // dbo.Users.UpdatePassword column via the dedicated POST /api/v1/users/{id}/force-password-change
    // endpoint (UserService.forcePasswordChange), which returns the updated UserDto. The `require` flag is
    // true for "force" and false for the reversible "clear". This restores the legacy cmdPassword transition.
    if (action === 'force-password-change' || action === 'clear-force-password-change') {
      return this.userService.forcePasswordChange(this.userId(), action === 'force-password-change');
    }
    // MIGRATION (MIGRATION_NOTES.md D-034): the legacy Membership.ascx command handlers
    // (cmdAuthorize / cmdUnAuthorize -> UserController.UpdateUser) map onto the modern UsersController.
    // The persistable membership flag `approved` travels on UpdateUserRequest, so each Authorize /
    // Unauthorize transition round-trips through the real, tested UserService.updateUser. UpdateUserRequest
    // is the full edit payload, so the unchanged profile fields are carried over from the loaded user
    // (legacy cmdAuthorize_Click likewise re-persisted the whole User record). The legacy cmdUnLock
    // (UnLockUser) handler has no endpoint on the modern controller (lockout lives on the out-of-scope
    // aspnet_Membership table), so Unlock is the one transition not reproduced (see the class doc).
    const change = this.changeFor(action);
    const request: UpdateUserRequest = {
      userID: user.userID,
      displayName: user.displayName ?? '',
      email: user.email ?? '',
      firstName: user.firstName ?? '',
      lastName: user.lastName ?? '',
      isSuperUser: user.isSuperUser,
      approved: change.approved ?? membership.approved,
    };
    return this.userService.updateUser(this.userId(), request);
  }

  private applyOptimistic(action: MembershipAction): void {
    const change = this.changeFor(action);
    this.membership.update((current) => (current ? { ...current, ...change } : current));
    this.syncMembershipForm();
  }

  private changeFor(action: MembershipAction): Partial<UpdateMembershipDto> {
    switch (action) {
      case 'authorize':
        return { approved: true };
      case 'unauthorize':
        return { approved: false };
      case 'force-password-change':
        return { updatePassword: true };
      case 'clear-force-password-change':
        return { updatePassword: false };
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
