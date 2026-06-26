// MIGRATION: Test-only authentication handler created for the .NET 8 migration's
// integration-test suite (AAP Gate 5 — API Integration Tests). The migrated API
// (Program.cs) registers JWT Bearer as the DEFAULT authentication scheme, and every CRUD
// controller (PortalsController, ModulesController, UsersController, RolesController,
// TabsController) plus AuthController.logout/me is decorated with [Authorize], so
// unauthenticated requests return 401. During this migration phase AuthService.LoginAsync
// fails closed (the credential store is intentionally empty, so login always returns 400)
// and a real JWT cannot be minted for tests. This stub therefore always authenticates the
// caller as a seeded, fully-privileged administrator. CustomWebApplicationFactory registers
// it as the default authenticate + challenge scheme ("Test"), overriding the production JWT
// default so [Authorize] controllers return 2xx instead of 401 under test. It is a test
// double — NOT a port of the legacy PortalSecurity.vb authentication logic.

using System.Security.Claims;
using System.Text.Encodings.Web;
using DnnMigration.Api.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// A stub <see cref="AuthenticationHandler{TOptions}"/> that unconditionally authenticates
/// every incoming request as a seeded, fully-privileged administrator user.
/// </summary>
/// <remarks>
/// <para>
/// This handler exists exclusively to support the integration-test suite. The production API
/// uses JWT Bearer as its default authentication scheme and protects all resource controllers
/// with <c>[Authorize]</c>. Because the migrated credential store is intentionally empty in this
/// phase, a genuine bearer token cannot be issued, which would otherwise cause every protected
/// endpoint to respond with <c>401 Unauthorized</c> during testing.
/// </para>
/// <para>
/// The <c>CustomWebApplicationFactory</c> registers this handler under the
/// <see cref="SchemeName"/> scheme and promotes it to the default authenticate and challenge
/// scheme, replacing the production JWT default. As a result, the CRUD status-code contract
/// validated by AAP Gate 5 (POST → 201, GET → 200, PUT → 200, DELETE → 204) can be exercised
/// without minting real tokens.
/// </para>
/// </remarks>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// The name of the authentication scheme handled by this stub. Tests and the
    /// web-application factory reference this constant instead of a hard-coded literal so the
    /// scheme registration and any scheme-specific assertions stay in sync.
    /// </summary>
    public const string SchemeName = "Test";

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAuthHandler"/> class.
    /// </summary>
    /// <param name="options">Monitors the options for this authentication scheme.</param>
    /// <param name="logger">The factory used to create loggers for the handler.</param>
    /// <param name="encoder">The URL encoder used by the authentication pipeline.</param>
    /// <remarks>
    /// This deliberately uses the three-parameter base constructor. The four-parameter overload
    /// that accepts <c>ISystemClock</c> is marked <see langword="obsolete"/> in .NET 8 (superseded
    /// by <c>TimeProvider</c>), and referencing it would fail the build under
    /// <c>--warnaserror</c> (CS0618).
    /// </remarks>
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// Builds a <see cref="ClaimsPrincipal"/> for a seeded administrator and always returns a
    /// successful authentication result.
    /// </summary>
    /// <returns>
    /// A completed task wrapping <see cref="AuthenticateResult.Success(AuthenticationTicket)"/>
    /// for the seeded principal.
    /// </returns>
    /// <remarks>
    /// The method is intentionally not marked <see langword="async"/> (it performs no awaitable
    /// work); using <see cref="Task.FromResult{TResult}(TResult)"/> avoids the CS1998 warning that
    /// would otherwise become an error under <c>--warnaserror</c>. Supplying <see cref="SchemeName"/>
    /// as the identity's authentication type makes <c>User.Identity.IsAuthenticated</c> evaluate to
    /// <see langword="true"/>, which is what satisfies the <c>[Authorize]</c> filter.
    /// </remarks>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // The NameIdentifier claim is mandatory: AuthController.me reads it (and the portalId claim) and returns
        // 401 when either is absent. The Role + isSuperUser + portalId claims make this a fully-privileged
        // principal: ClaimTypes.Role "Administrators" satisfies the PortalAdministrator policy; the isSuperUser
        // flag satisfies the HostAdministrator policy AND bypasses ApiControllerBase.EnforceTenant for any
        // requested portalId (so the Gate 5 CRUD contract stays green); and the portalId claim supplies the
        // tenant the production token would carry. The constants come from the Api project so the seeded claim
        // types can never drift from what JwtService issues and the policies/EnforceTenant read.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, DnnClaims.AdministratorRole),
            new Claim(DnnClaims.IsSuperUser, "true"),
            new Claim(DnnClaims.PortalId, "0")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
