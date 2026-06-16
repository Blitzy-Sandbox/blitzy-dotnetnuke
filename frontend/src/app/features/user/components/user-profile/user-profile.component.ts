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
// (Authorize / Unauthorize / Unlock / Force Password Change). Only the `approved` flag has a
// persistable contract on the modern UsersController (it travels on UpdateUserRequest). The Unlock
// (cmdUnLock -> UnLockUser) and Force Password Change (cmdPassword) transitions have NO backend
// endpoint or DTO field this checkpoint, so a click could only mutate local state and was lost on
// reload. Those two buttons are therefore removed pending a persisted membership contract, leaving
// the two real transitions that round-trip through UserService.updateUser.
/** The two persistable membership-state transitions ported from the legacy Membership.ascx.vb command buttons. */
type MembershipAction = 'authorize' | 'unauthorize';

interface ActionConfig {
  readonly title: string;
  readonly message: string;
  readonly confirmText: string;
  readonly destructive: boolean;
}

/**
 * UserProfileComponent — reproduces the legacy DotNetNuke Admin > Users membership control
 * (Website/admin/Users/Membership.ascx.vb) with UI functional parity: a read-only membership
 * view plus the two persistable membership-state transitions, gated by the legacy
 * button-visibility rules.
 *
 * MIGRATION (MIGRATION_NOTES.md D-034): the legacy control executed each transition immediately on
 * click (cmdAuthorize / cmdUnAuthorize -> UserController.UpdateUser; cmdUnLock ->
 * UserController.UnLockUser; cmdPassword -> UserController.UpdateUser). The modern UsersController
 * exposes list/get/create/update/delete only — it has no UnLockUser or force-password endpoint, and
 * UpdateUserRequest carries no lockedOut/updatePassword field. Only Authorize / Unauthorize (the
 * `approved` flag) have a persistable contract, so the Unlock and Force Password Change buttons —
 * which could only mutate local state and were lost on reload — are removed pending a backend
 * membership contract. This SPA port adds an explicit confirmation step (ConfirmationDialogComponent)
 * on top of the surviving legacy immediate-execute buttons, and reconstructs the membership snapshot
 * client-side (see loadUser). All data access is delegated to the typed UserService — HttpClient is
 * NEVER injected here.
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
        // the `approved` membership flag but omits lockedOut/updatePassword, and no membership-read
        // endpoint exists in the UserService contract. Seed the snapshot from the real `approved`
        // value; lockedOut/updatePassword have no wire contract this checkpoint, so they default to
        // false and are shown read-only — no transition mutates them now that Unlock / Force Password
        // Change have been removed pending a backend membership contract. Tests drive the membership
        // signal directly to exercise the button-visibility matrix.
        this.membership.set({ approved: user.approved, lockedOut: false, updatePassword: false });
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
    // MIGRATION (MIGRATION_NOTES.md D-034): the legacy Membership.ascx command handlers
    // (cmdAuthorize / cmdUnAuthorize -> UserController.UpdateUser) map onto the modern UsersController,
    // which exposes list/get/create/update/delete only. The single persistable membership flag,
    // `approved`, travels on UpdateUserRequest, so each surviving transition round-trips through the
    // real, tested UserService.updateUser. UpdateUserRequest is the full edit payload, so the
    // unchanged profile fields are carried over from the loaded user (legacy cmdAuthorize_Click
    // likewise re-persisted the whole User record). The legacy cmdUnLock (UnLockUser) and cmdPassword
    // (force-password) handlers have no endpoint or DTO field on the modern controller, so those two
    // buttons were removed rather than faked with non-persisting client-side state.
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
