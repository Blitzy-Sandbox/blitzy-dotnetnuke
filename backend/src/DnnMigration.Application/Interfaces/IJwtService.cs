using System.Security.Claims;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// JWT token abstraction. MIGRATION: replaces legacy ASP.NET Forms Authentication
/// (PortalSecurity.vb / FormsAuthentication) with stateless JWT Bearer tokens plus
/// refresh-token rotation. Implemented by Infrastructure/Identity/JwtService.cs.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Issues a signed JWT access token for the supplied user, including identity and
    /// role claims. Role claims are emitted from <see cref="User.Roles"/> (populated by
    /// the service layer prior to this call).
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>Generates a cryptographically random opaque refresh token.</summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates the supplied JWT and returns its <see cref="ClaimsPrincipal"/>, or
    /// <c>null</c> if the token is invalid, expired, or tampered with.
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// The configured access-token lifetime in minutes (AAP: 60). Used to populate the
    /// expiry fields on the auth response without binding the Application layer to the
    /// Infrastructure JwtSettings options type.
    /// </summary>
    int AccessTokenExpirationMinutes { get; }
}
