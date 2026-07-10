using FluentValidation;
using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="LoginRequestDto"/>, executed for
/// <c>POST /api/auth/login</c> before the credentials reach
/// <c>AuthService.LoginAsync</c>.
/// </summary>
/// <remarks>
/// MIGRATION: replaces the implicit field checks the legacy DotNetNuke sign-in
/// screen performed before calling <c>PortalSecurity</c> / provider membership
/// (<c>Library/Components/Security/PortalSecurity.vb</c>). The Web Forms login
/// control rendered <c>RequiredFieldValidator</c>s on the user-name and password
/// text boxes so an empty credential never reached the membership provider; this
/// validator reproduces that outcome for the JSON Backend-for-Frontend contract by
/// rejecting blank credentials with a deterministic RFC 7807 <c>400</c> instead of
/// letting them fall through to a generic "invalid credentials" path.
///
/// This is a login validator, NOT a password-policy validator: it only asserts
/// that the fields are present and within sane length bounds. Password
/// <em>complexity</em> is enforced when a password is created or changed
/// (<see cref="CreateUserDtoValidator"/> / <see cref="ChangePasswordDtoValidator"/>),
/// never at login, because an existing credential may predate any current policy
/// and must still be able to authenticate.
///
/// The class is <c>public</c> so the Api host discovers and registers it through
/// FluentValidation assembly scanning (<c>AddValidatorsFromAssembly</c> in
/// <c>Program.cs</c>) at start-up, and <c>sealed</c> because it is not designed to
/// be extended. It performs validation only: no state, no data access, no business
/// logic.
/// </remarks>
public sealed class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    // MIGRATION: aspnet_Users.UserName is nvarchar(256) in the authoritative legacy
    // schema (Website/Providers/DataProviders/SqlDataProvider/InstallCommon.sql), so the
    // user-name upper bound mirrors the column width. The password cap is a defensive
    // request-size guard (it is not a policy rule) that bounds the work done during
    // credential verification without rejecting any realistic password.
    private const int MaxUsernameLength = 256;
    private const int MaxPasswordLength = 256;

    /// <summary>
    /// Configures the validation rules for a login request.
    /// </summary>
    public LoginRequestDtoValidator()
    {
        // MIGRATION: RequiredFieldValidator on the legacy user-name text box -> NotEmpty.
        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(MaxUsernameLength);

        // MIGRATION: RequiredFieldValidator on the legacy password text box -> NotEmpty.
        // No MinimumLength / complexity rule here: login must accept any previously
        // accepted credential regardless of the current creation policy.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(MaxPasswordLength);

        // PortalId is optional (the portal may instead be derived from host/alias
        // context). When a value IS supplied it must be a non-negative identifier,
        // because portal keys are IDENTITY(0,1) in the legacy schema, so the smallest
        // legal portal id is 0. The rule only runs when the value is present.
        RuleFor(x => x.PortalId!.Value)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PortalId.HasValue)
            .OverridePropertyName(nameof(LoginRequestDto.PortalId));
    }
}

/// <summary>
/// FluentValidation validator for <see cref="RefreshRequestDto"/>, executed for
/// <c>POST /api/auth/refresh</c> before the token reaches
/// <c>AuthService.RefreshAsync</c>.
/// </summary>
/// <remarks>
/// MIGRATION: there is no legacy analogue — DotNetNuke relied on sliding
/// <c>FormsAuthentication</c> cookies rather than refresh tokens. Silent
/// re-authentication in the modern SPA depends on a non-empty refresh token, so
/// this validator rejects a blank token with a deterministic <c>400</c> rather
/// than letting an empty string reach the refresh-token store (where it would be
/// an unavoidable miss). A generous upper length bound guards request size without
/// constraining the opaque token format issued by <c>JwtTokenService</c>.
///
/// Registered by assembly scanning; validation-only, stateless.
/// </remarks>
public sealed class RefreshRequestDtoValidator : AbstractValidator<RefreshRequestDto>
{
    // Opaque refresh tokens are short (base64 of random bytes), but the format is an
    // implementation detail of JwtTokenService; a 4096-character ceiling is a size
    // guard only and comfortably accommodates any token the store issues.
    private const int MaxRefreshTokenLength = 4096;

    /// <summary>
    /// Configures the validation rules for a token-refresh request.
    /// </summary>
    public RefreshRequestDtoValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(MaxRefreshTokenLength);
    }
}

// MIGRATION / DESIGN NOTE: no LogoutRequestDtoValidator is defined, and this omission
// is deliberate. LogoutRequestDto (POST /api/auth/logout) documents an INTENTIONALLY
// idempotent contract: revoking an unknown or blank refresh token is a no-op, and the
// Angular auth interceptor does not attach a bearer to auth-flow routes, so at logout
// time the client may legitimately have no live token to send. Requiring a non-empty
// token here would convert a benign, always-safe logout into a 400 and break that
// contract (see AuthDtos.cs LogoutRequestDto remarks and AuthService.LogoutAsync,
// which revokes by store lookup). The code-review findings (validators for
// LoginRequestDto, RefreshRequestDto, and ChangePasswordDto) scope validation to the
// three credential-bearing DTOs above; logout is correctly excluded.
