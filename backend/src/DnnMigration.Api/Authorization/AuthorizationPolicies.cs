namespace DnnMigration.Api.Authorization;

// MIGRATION: New authorization infrastructure with no single legacy DotNetNuke analog. It encodes, as explicit
// ASP.NET Core authorization, the DNN access-control model that the legacy Web Forms admin pages enforced
// implicitly through the page/module permission system:
//   - HOST SuperUsers (UserInfo.IsSuperUser) administer host-level concerns — creating, listing, updating and
//     deleting PORTALS. The legacy Host > Portals workflow was SuperUser-only.
//   - PORTAL Administrators (members of the portal's "Administrators" role, RoleType=Administrator) administer
//     users, roles, pages (tabs) and modules WITHIN their own portal (the legacy Admin > * pages).
// JwtService issues the "portalId" and "isSuperUser" claims and one ClaimTypes.Role claim per role, so the
// constants below are the single source of truth shared by Program.cs (policy registration), ApiControllerBase
// (the tenant-isolation helper) and TestAuthHandler (the Gate 5 integration-test principal). Centralizing them
// here prevents the claim-name drift that the CP2 review flagged between the issuer, the validator and config.

/// <summary>
/// Names of the authorization policies registered in <c>Program.cs</c> and applied to the resource
/// controllers via <c>[Authorize(Policy = ...)]</c>. Using shared constants (rather than string literals at
/// each call site) guarantees the policy referenced by a controller always matches a registered policy.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Host-level administration (portal create / list / read / update / delete). Satisfied only by a DNN host
    /// SuperUser, mirroring the SuperUser-only legacy Host &gt; Portals workflow.
    /// </summary>
    public const string HostAdministrator = "HostAdministrator";

    /// <summary>
    /// Portal-level administration (users, roles, pages/tabs and modules within a portal). Satisfied by a
    /// member of the portal's "Administrators" role or by a host SuperUser (who administers every portal).
    /// </summary>
    public const string PortalAdministrator = "PortalAdministrator";
}

/// <summary>
/// JWT claim types and the DNN administrator role name shared across token issuance (<c>JwtService</c>),
/// authorization-policy evaluation (<c>Program.cs</c>) and tenant-isolation enforcement
/// (<c>ApiControllerBase</c>). These mirror exactly the claims emitted by <c>JwtService.GenerateAccessToken</c>.
/// </summary>
public static class DnnClaims
{
    /// <summary>
    /// The portal (tenant) the principal belongs to. Issued by <c>JwtService</c> as a custom claim and used to
    /// enforce that a non-SuperUser only ever acts on its own portal (DNN multi-tenant isolation, AAP §0.7.1).
    /// </summary>
    public const string PortalId = "portalId";

    /// <summary>
    /// Whether the principal is a DNN host SuperUser (the legacy <c>UserInfo.IsSuperUser</c> flag). Issued by
    /// <c>JwtService</c> as a custom claim whose value is <c>bool.ToString()</c> ("True"/"False"), so it must be
    /// read with a case-insensitive <c>bool.TryParse</c>.
    /// </summary>
    public const string IsSuperUser = "isSuperUser";

    /// <summary>
    /// The DNN portal administrator role name. DNN's default administrator role is "Administrators"
    /// (<c>PortalInfo.AdministratorRoleName</c>); role names are issued as <c>ClaimTypes.Role</c> by
    /// <c>JwtService</c>, so <c>User.IsInRole(AdministratorRole)</c> matches them under the default role-claim type.
    /// </summary>
    public const string AdministratorRole = "Administrators";
}
