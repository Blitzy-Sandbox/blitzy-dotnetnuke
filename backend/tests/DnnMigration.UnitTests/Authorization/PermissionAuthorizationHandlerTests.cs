using System.Security.Claims;
using DnnMigration.Api.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace DnnMigration.UnitTests.Authorization;

/// <summary>
/// Unit tests for <see cref="PermissionAuthorizationHandler"/> — the server-side enforcement point that
/// replaces the legacy <c>PortalSecurity.HasNecessaryPermission</c> (Finding CP4-1 / AAP §0.6.2).
/// </summary>
/// <remarks>
/// The handler must mirror the frontend <c>has-permission</c> directive exactly: a SuperUser
/// (<c>IsSuperUser</c> claim) OR a member of one of the requirement's allowed roles is granted; every other
/// authenticated principal is denied (fail-closed), which the middleware surfaces as a 403.
/// </remarks>
public class PermissionAuthorizationHandlerTests
{
    private static readonly PermissionRequirement EditRequirement =
        new(Permissions.Edit, new[] { AuthorizationRoles.Administrators });

    private static AuthorizationHandlerContext ContextFor(ClaimsPrincipal user) =>
        new(new[] { EditRequirement }, user, resource: null);

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "TestJwt"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    [Fact]
    public async Task Administrators_role_member_is_granted()
    {
        var user = Authenticated(new Claim(ClaimTypes.Role, AuthorizationRoles.Administrators));
        var context = ContextFor(user);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task SuperUser_claim_is_granted_even_without_any_role()
    {
        // MIGRATION: mirrors the legacy `If User.IsSuperUser Then blnAuthorized = True` shortcut. The claim
        // value matches JwtService's emission of user.IsSuperUser.ToString() ("True").
        var user = Authenticated(new Claim(PermissionAuthorizationHandler.SuperUserClaimType, "True"));
        var context = ContextFor(user);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    public async Task SuperUser_claim_parses_case_insensitively(string claimValue)
    {
        var user = Authenticated(new Claim(PermissionAuthorizationHandler.SuperUserClaimType, claimValue));
        var context = ContextFor(user);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Authenticated_user_without_role_or_superuser_is_denied()
    {
        // The decisive checkpoint assertion: an authenticated-but-unauthorized principal must NOT succeed
        // (the policy pipeline then returns 403 rather than silently allowing the request).
        var user = Authenticated(
            new Claim(ClaimTypes.Name, "regular"),
            new Claim(PermissionAuthorizationHandler.SuperUserClaimType, "False"),
            new Claim(ClaimTypes.Role, "Registered Users"));
        var context = ContextFor(user);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Anonymous_principal_is_denied()
    {
        var context = ContextFor(Anonymous());

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Malformed_superuser_claim_is_treated_as_not_superuser()
    {
        // A non-boolean IsSuperUser value must not grant access and must not throw.
        var user = Authenticated(new Claim(PermissionAuthorizationHandler.SuperUserClaimType, "yes-please"));
        var context = ContextFor(user);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
