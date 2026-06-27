using System.Security.Claims;
using DnnMigration.Application.Security;

namespace DnnMigration.Api.Authorization;

// MIGRATION: Api-layer glue that projects the authenticated ClaimsPrincipal onto the Application-layer
// SecurityContext consumed by IPermissionEvaluator. This is the BFF replacement for the ambient inputs the legacy
// PortalSecurity helpers read — UserController.GetCurrentUserInfo() and HttpContext.Current.Request.IsAuthenticated
// (Library/Components/Security/PortalSecurity.vb). It reads exactly the claims JwtService emits (and that the
// existing AuthorizationPolicies / ApiControllerBase already rely on), keeping the claim names in one place:
//   - one ClaimTypes.Role claim per role  -> SecurityContext.Roles
//   - DnnClaims.IsSuperUser ("isSuperUser", a bool.ToString() value) -> SecurityContext.IsSuperUser
//   - the principal's authentication state  -> SecurityContext.IsAuthenticated
//   - sub -> ClaimTypes.NameIdentifier (userId) -> SecurityContext.UserId
// Living in the Api layer preserves the Clean/Onion dependency direction (Api -> Application); the Application
// layer never references ASP.NET Core or these claim constants.
public static class ClaimsPrincipalSecurityExtensions
{
    /// <summary>
    /// Builds the <see cref="SecurityContext"/> for the supplied principal from its JWT claims. Safe for an
    /// anonymous principal: roles are empty, IsSuperUser/IsAuthenticated are false and UserId is null.
    /// </summary>
    /// <param name="principal">The authenticated (or anonymous) request principal.</param>
    /// <returns>An immutable security context for permission evaluation.</returns>
    public static SecurityContext ToSecurityContext(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // One ClaimTypes.Role claim per role (issued by JwtService) -> the principal's role-name membership.
        string[] roles = principal.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray();

        // "isSuperUser" is a bool.ToString() value ("True"/"False"); parse case-insensitively, matching
        // ApiControllerBase.IsSuperUser().
        bool isSuperUser = bool.TryParse(principal.FindFirstValue(DnnClaims.IsSuperUser), out bool parsedSuperUser)
                           && parsedSuperUser;

        bool isAuthenticated = principal.Identity?.IsAuthenticated ?? false;

        // sub is mapped to ClaimTypes.NameIdentifier under the default JwtBearer inbound claim mapping.
        int? userId = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedUserId)
            ? parsedUserId
            : null;

        return new SecurityContext
        {
            Roles = roles,
            IsSuperUser = isSuperUser,
            IsAuthenticated = isAuthenticated,
            UserId = userId
        };
    }
}
