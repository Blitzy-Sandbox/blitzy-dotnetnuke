/**
 * Unit tests (Gate 4: `ng test --watch=false --browsers=ChromeHeadless --code-coverage`)
 * for {@link ForgotPasswordComponent}.
 *
 * MIGRATION (UI functional parity, AAP §0.7.1): these tests pin the BEHAVIOUR of the
 * migrated "Forgot Password" screen against the legacy DotNetNuke 4.x SendPassword
 * control (Website/admin/Security/SendPassword.ascx[.vb], `cmdSendPassword_Click`
 * L178-191). The parity contract has three branches:
 *   1. the username-OR-email required branch  -> group-level `atLeastOneOf` validator,
 *   2. the email-format branch                -> `Validators.email`,
 *   3. the (collapsed) success outcome         -> a single, security-conscious,
 *      non-revealing confirmation on EVERY valid submit (DNN's PasswordSent /
 *      UsernameError / EmailError outcomes are intentionally unified — there is NO
 *      backend recovery endpoint in scope, AAP §0.2.2).
 *
 * The suite deliberately exercises the MODEL (reactive-form validity + the component's
 * public signal outcomes) rather than deep DOM structure, so it stays robust to cosmetic
 * template changes while still proving the parity contract. FormFieldComponent's own
 * rendering is covered by its dedicated spec, so it is not re-asserted here.
 *
 * The confirmation / required strings are IMPORTED from the component (never duplicated),
 * so rewording those constants will not break these tests — they pin which BRANCH sets
 * which signal, which is the parity contract.
 */
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import {
  ENTER_USERNAME_EMAIL,
  ForgotPasswordComponent,
  GENERIC_CONFIRMATION,
  PASSWORD_REMINDER_SIMULATED_LATENCY_MS,
} from './forgot-password.component';

describe('ForgotPasswordComponent', () => {
  let fixture: ComponentFixture<ForgotPasswordComponent>;
  let component: ForgotPasswordComponent;

  beforeEach(async () => {
    // The component INJECTS AuthService (a reserved seam for a future recovery
    // endpoint, AAP §0.2.2) but never CALLS it, so a bare spy with no configured
    // return values keeps the test isolated from the real service's transitive
    // HttpClient / ApiService dependencies. A typed spy (matching the project's
    // sibling specs) also satisfies the strict "no any" constraint.
    const authServiceSpy: jasmine.SpyObj<AuthService> = jasmine.createSpyObj<AuthService>(
      'AuthService',
      ['login', 'refresh', 'loadCurrentUser', 'logout', 'getAccessToken'],
    );

    await TestBed.configureTestingModule({
      // Standalone component -> `imports`, NEVER `declarations`. Its own imports
      // (ReactiveFormsModule, RouterLink, FormFieldComponent, LoadingSpinnerComponent)
      // are pulled in automatically.
      imports: [ForgotPasswordComponent],
      providers: [
        provideRouter([]), // satisfies RouterLink (avoids "no provider for Router/ActivatedRoute")
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ForgotPasswordComponent);
    component = fixture.componentInstance;
    // Initializes the component and its child <app-form-field>/<app-loading-spinner>
    // (their required inputs are satisfied by the parent template bindings).
    fixture.detectChanges();
  });

  it('creates', () => {
    expect(component).toBeTruthy();
  });

  // (a) Parity: the legacy username-OR-email required branch. With BOTH fields blank the
  // group-level cross-field validator fails, so the form is invalid.
  it('is INVALID when both username and email are empty (atLeastOneOf cross-field)', () => {
    component.form.controls.username.setValue('');
    component.form.controls.email.setValue('');

    expect(component.form.invalid).toBe(true);
    expect(component.form.hasError('atLeastOneOf')).toBe(true);
  });

  // (b) Parity: supplying only the username satisfies the required branch. `Validators.email`
  // treats the empty email as valid, so the whole form is valid.
  it('is VALID with a username only', () => {
    component.form.controls.username.setValue('jdoe');
    component.form.controls.email.setValue('');

    expect(component.form.valid).toBe(true);
    expect(component.form.hasError('atLeastOneOf')).toBe(false);
  });

  // (c) Parity: supplying only a well-formed email is the alternative required-branch path.
  it('is VALID with a well-formed email only', () => {
    component.form.controls.username.setValue('');
    component.form.controls.email.setValue('user@example.com');

    expect(component.form.valid).toBe(true);
    expect(component.form.controls.email.valid).toBe(true);
  });

  // (d) Parity: the email-format branch. A malformed email fails `Validators.email` and
  // invalidates the form even though the cross-field required rule is satisfied.
  it('is INVALID when the email is malformed (Validators.email)', () => {
    component.form.controls.email.setValue('not-an-email');

    expect(component.form.controls.email.hasError('email')).toBe(true);
    expect(component.form.invalid).toBe(true);
  });

  // Invalid submit: the group-level required message is surfaced via `errorMessage()`
  // and the confirmation is NOT shown (submitted stays false).
  it('surfaces the group required message and does NOT confirm on invalid submit', () => {
    component.onSubmit();

    expect(component.errorMessage()).toBe(ENTER_USERNAME_EMAIL);
    expect(component.submitted()).toBe(false);
  });

  // (e) Parity: the security-conscious success outcome. A valid submit flips `submitting()`
  // true, then (after the simulated latency) shows the single non-revealing confirmation.
  // `timer(...)` emits once and completes, so `fakeAsync` ends with no pending timers.
  it('shows the security-conscious confirmation on valid submit', fakeAsync(() => {
    component.form.controls.username.setValue('jdoe');

    component.onSubmit();
    expect(component.submitting()).toBe(true);
    expect(component.submitted()).toBe(false);

    tick(PASSWORD_REMINDER_SIMULATED_LATENCY_MS);

    expect(component.submitting()).toBe(false);
    expect(component.submitted()).toBe(true);
    expect(component.successMessage()).toBe(GENERIC_CONFIRMATION);
  }));
});
