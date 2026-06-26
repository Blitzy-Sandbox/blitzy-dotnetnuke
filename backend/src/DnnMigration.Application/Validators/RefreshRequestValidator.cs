using FluentValidation;
using DnnMigration.Application.DTOs.Auth;

namespace DnnMigration.Application.Validators;

// MIGRATION: CP1 review (AuthService #5 / Security #3) — API-contract validator for the refresh-token rotation
// endpoint POST /api/auth/refresh (RefreshRequest is also reused as the POST /api/auth/logout body). DNN had NO
// refresh-token concept — it used a persistent Forms-auth cookie (PortalSecurity.vb / UserController.UserLogin
// 'CreatePersistentCookie') — so there is NO legacy field-validator or legacy message to transcribe. This validator
// only enforces presence of the required opaque refresh token so a blank/missing token fails closed at the Api
// boundary before AuthService is invoked (AuthService also null-guards the request defensively).
//
// MIGRATION: NotEmpty (which also rejects whitespace-only values) is the correct idiomatic choice here and raises NO
// parity concern: unlike CreatePortalValidator — which had to match a legacy Web Forms RequiredFieldValidator and so
// could not tighten whitespace handling (CP1 review CreatePortalValidator #1) — the refresh token is a NEW JWT-model
// field with no legacy validator baseline, and a whitespace-only token is meaningless and must be rejected.
//
// Synchronous-only validator (no repository/data access): token validity, expiry, revocation and the tenant
// (user + portal) binding are resolved by IJwtService.ValidateRefreshToken in Infrastructure, NOT here.
public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        // MIGRATION: Required — the opaque refresh token must be supplied (non-empty / non-whitespace).
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
