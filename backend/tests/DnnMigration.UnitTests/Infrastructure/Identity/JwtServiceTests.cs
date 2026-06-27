using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DnnMigration.Application.Interfaces;
using DnnMigration.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace DnnMigration.UnitTests.Infrastructure.Identity;

// MIGRATION (QA F7 — Issue #1): direct unit coverage for the security-critical JWT issuer
// (DnnMigration.Infrastructure.Identity.JwtService, AAP §0.7.6 — replaces AspNetSqlMembershipProvider +
// Forms-auth ticket). The F7 checkpoint flagged JwtService as having ZERO direct or indirect coverage: every
// service test mocks IJwtService and the integration suite bypasses real auth via TestAuthHandler, so the
// signing-key path, HMAC-SHA256 signing, claim construction, and the tenant-bound refresh store were never
// exercised. These tests construct the REAL JwtService from an in-memory IConfiguration (mocked via Moq — the
// only config member JwtService reads is the string indexer, and the UnitTests project does not reference the
// concrete Microsoft.Extensions.Configuration builder/in-memory provider, only the Abstractions) and assert the
// behaviors the checkpoint named: valid-key token + claims + expiry that validates under the same
// key/issuer/audience; empty-key fail-fast; refresh round-trip (tenant-bound); fail-closed refresh; and revoke.
//
// RISK AMPLIFIER ADDRESSED: JwtService.cs documents a previously-fixed bug ("// MIGRATION (CP2 review)") where
// the wrong config key (Jwt:SigningKey vs Jwt:Key) left the signing key empty so every GenerateAccessToken
// threw. The valid-key tests below read "Jwt:Key" exactly as the JWT Bearer validation parameters do, so a
// regression of that key-name class would now fail this suite (not silently slip past).
public sealed class JwtServiceTests
{
    // A well-formed test issuer/audience and a signing key of >= 32 bytes (256 bits), the minimum HMAC-SHA256
    // requires. const so they can be used as default parameter values and reused by token validation.
    private const string Issuer = "dnnmigration-issuer";
    private const string Audience = "dnnmigration-audience";
    private const string SigningKey = "dnnmigration-unit-test-signing-key-0123456789-abcdef"; // 52 chars

    // Builds an IConfiguration test double. JwtService reads ONLY the string indexer for the four "Jwt:*"
    // keys; any key not configured returns null (Moq loose default), matching a real IConfiguration miss and
    // the service's own "?? string.Empty" / int.TryParse fallbacks.
    private static IConfiguration BuildConfig(
        string? signingKey = SigningKey,
        string? accessTokenMinutes = "60",
        string? issuer = Issuer,
        string? audience = Audience)
    {
        var config = new Mock<IConfiguration>(MockBehavior.Loose);
        config.Setup(c => c["Jwt:Issuer"]).Returns(issuer);
        config.Setup(c => c["Jwt:Audience"]).Returns(audience);
        config.Setup(c => c["Jwt:Key"]).Returns(signingKey);
        config.Setup(c => c["Jwt:AccessTokenMinutes"]).Returns(accessTokenMinutes);
        return config.Object;
    }

    private static JwtService CreateService(string? signingKey = SigningKey, string? accessTokenMinutes = "60")
        => new(BuildConfig(signingKey, accessTokenMinutes));

    // ---------- (a) valid signing key: token, claims, expiry, and validity under the same key ----------

    [Fact]
    public void GenerateAccessToken_WithValidKey_EmitsAllExpectedClaims()
    {
        var sut = CreateService();

        var (accessToken, _, _) = sut.GenerateAccessToken(
            userId: 42, username: "jane", portalId: 7, isSuperUser: true,
            roles: new[] { "Administrators", "Editors" });

        accessToken.Should().NotBeNullOrWhiteSpace();

        // Read the RAW payload claims (ReadJwtToken applies no inbound mapping), so the asserted claim TYPES are
        // exactly what JwtService writes: sub / unique_name / portalId / isSuperUser / jti / one role per role.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        jwt.Claims.Should().ContainSingle(c => c.Type == JwtRegisteredClaimNames.Sub).Which.Value.Should().Be("42");
        jwt.Claims.Should().ContainSingle(c => c.Type == JwtRegisteredClaimNames.UniqueName).Which.Value.Should().Be("jane");
        jwt.Claims.Should().ContainSingle(c => c.Type == "portalId").Which.Value.Should().Be("7");
        jwt.Claims.Should().ContainSingle(c => c.Type == "isSuperUser").Which.Value.Should().Be(true.ToString());
        jwt.Claims.Should().ContainSingle(c => c.Type == JwtRegisteredClaimNames.Jti).Which.Value.Should().NotBeNullOrWhiteSpace();

        // One ClaimTypes.Role claim per supplied role (preserves the legacy user -> role -> permission model).
        jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
            .Should().BeEquivalentTo(new[] { "Administrators", "Editors" });
    }

    [Fact]
    public void GenerateAccessToken_WithNonSuperUser_AndNoRoles_EmitsFalseFlag_AndNoRoleClaims()
    {
        var sut = CreateService();

        var (accessToken, _, _) = sut.GenerateAccessToken(
            userId: 5, username: "bob", portalId: 1, isSuperUser: false,
            roles: Array.Empty<string>());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        jwt.Claims.Should().ContainSingle(c => c.Type == "isSuperUser").Which.Value.Should().Be(false.ToString());
        jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Should().BeEmpty();
    }

    [Theory]
    [InlineData("60", 3600)]
    [InlineData("30", 1800)]
    public void GenerateAccessToken_ExpiresInSeconds_EqualsAccessTokenMinutesTimes60(string minutes, int expectedSeconds)
    {
        var sut = CreateService(accessTokenMinutes: minutes);

        var (_, expiresAtUtc, expiresInSeconds) = sut.GenerateAccessToken(
            userId: 1, username: "u", portalId: 0, isSuperUser: false, roles: Array.Empty<string>());

        expiresInSeconds.Should().Be(expectedSeconds);
        // The absolute UTC expiry is "now + AccessTokenMinutes"; allow generous slack for execution time.
        expiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(expectedSeconds), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GenerateAccessToken_WhenAccessTokenMinutesMissingOrUnparseable_DefaultsTo60Minutes()
    {
        // Unparseable value falls back to the AAP §0.7.6 default of 60 minutes (int.TryParse fails -> 60).
        var sut = CreateService(accessTokenMinutes: "not-a-number");

        var (_, _, expiresInSeconds) = sut.GenerateAccessToken(
            userId: 1, username: "u", portalId: 0, isSuperUser: false, roles: Array.Empty<string>());

        expiresInSeconds.Should().Be(3600);
    }

    [Fact]
    public void GenerateAccessToken_ProducesTokenThatValidatesUnderTheSameKeyIssuerAndAudience()
    {
        var sut = CreateService();

        var (accessToken, _, _) = sut.GenerateAccessToken(
            userId: 99, username: "host", portalId: 0, isSuperUser: true, roles: new[] { "Administrators" });

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // ValidateToken throws SecurityTokenException on any failure (bad signature, wrong issuer/audience,
        // expired); a non-throwing call that yields an authenticated principal proves the token is valid.
        var handler = new JwtSecurityTokenHandler();
        Action validate = () => handler.ValidateToken(accessToken, validationParameters, out _);

        validate.Should().NotThrow();
        var principal = handler.ValidateToken(accessToken, validationParameters, out var validatedToken);
        principal.Identity!.IsAuthenticated.Should().BeTrue();
        validatedToken.Should().BeOfType<JwtSecurityToken>();
        ((JwtSecurityToken)validatedToken).Issuer.Should().Be(Issuer);
    }

    [Fact]
    public void GenerateAccessToken_TokenDoesNotValidate_UnderADifferentSigningKey()
    {
        // Negative control for the signing path: a token signed with the configured key must FAIL validation
        // against a different key (proves the signature actually depends on Jwt:Key, not a no-op).
        var sut = CreateService();
        var (accessToken, _, _) = sut.GenerateAccessToken(
            userId: 1, username: "u", portalId: 0, isSuperUser: false, roles: Array.Empty<string>());

        var wrongKeyParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("a-completely-different-signing-key-0123456789")),
            ValidateLifetime = false,
        };

        Action validate = () => new JwtSecurityTokenHandler().ValidateToken(accessToken, wrongKeyParameters, out _);
        validate.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void GenerateAccessToken_IssuesAUniqueJtiPerCall()
    {
        var sut = CreateService();
        var handler = new JwtSecurityTokenHandler();

        var (first, _, _) = sut.GenerateAccessToken(1, "u", 0, false, Array.Empty<string>());
        var (second, _, _) = sut.GenerateAccessToken(1, "u", 0, false, Array.Empty<string>());

        first.Should().NotBe(second, "each token carries a unique jti");
        var jti1 = handler.ReadJwtToken(first).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = handler.ReadJwtToken(second).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        jti1.Should().NotBe(jti2);
    }

    // ---------- (b) empty / missing signing key: fail fast ----------

    [Theory]
    [InlineData("")]      // configured but blank
    [InlineData(null)]    // not configured at all (indexer returns null -> "?? string.Empty")
    public void GenerateAccessToken_WhenSigningKeyIsEmptyOrMissing_ThrowsInvalidOperationException(string? key)
    {
        var sut = CreateService(signingKey: key);

        Action act = () => sut.GenerateAccessToken(
            userId: 1, username: "u", portalId: 0, isSuperUser: false, roles: Array.Empty<string>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT signing key is not configured (Jwt:Key).*");
    }

    [Fact]
    public void GenerateAccessToken_FailFastMessage_NeverLeaksTheKeyValue()
    {
        // Security: the fail-fast message must NOT include the configured key value (AAP §0.7.6 — no secret leakage).
        var sut = CreateService(signingKey: "");

        var act = () => sut.GenerateAccessToken(1, "u", 0, false, Array.Empty<string>());

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().NotContain(SigningKey);
    }

    // ---------- (c) refresh round-trip: tenant-bound identity ----------

    [Fact]
    public void GenerateRefreshToken_ThenValidate_ReturnsTheTenantBoundIdentity()
    {
        var sut = CreateService();

        var refreshToken = sut.GenerateRefreshToken(userId: 5, portalId: 1);

        refreshToken.Should().NotBeNullOrWhiteSpace();
        var info = sut.ValidateRefreshToken(refreshToken);
        info.Should().NotBeNull();
        info!.UserId.Should().Be(5);
        info.PortalId.Should().Be(1);
        // RefreshTokenInfo is a record: value equality confirms BOTH the user and the portal binding.
        info.Should().Be(new RefreshTokenInfo(5, 1));
    }

    [Fact]
    public void GenerateRefreshToken_BindsTheTokenToTheIssuingPortal_NotADifferentPortal()
    {
        var sut = CreateService();

        // Two tokens for the same user but DIFFERENT portals must each resolve to their own portal.
        var portalOneToken = sut.GenerateRefreshToken(userId: 5, portalId: 1);
        var portalTwoToken = sut.GenerateRefreshToken(userId: 5, portalId: 2);

        sut.ValidateRefreshToken(portalOneToken)!.PortalId.Should().Be(1);
        sut.ValidateRefreshToken(portalTwoToken)!.PortalId.Should().Be(2);
    }

    [Fact]
    public void GenerateRefreshToken_ProducesAUniqueOpaqueValuePerCall()
    {
        var sut = CreateService();

        var first = sut.GenerateRefreshToken(5, 1);
        var second = sut.GenerateRefreshToken(5, 1);

        first.Should().NotBe(second, "refresh tokens are cryptographically random");
    }

    // ---------- (d) fail-closed: unknown / empty / null refresh tokens ----------

    [Fact]
    public void ValidateRefreshToken_ForAnUnknownToken_ReturnsNull()
    {
        var sut = CreateService();

        sut.ValidateRefreshToken("never-issued-token").Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateRefreshToken_ForEmptyOrNullToken_ReturnsNull(string? token)
    {
        var sut = CreateService();

        sut.ValidateRefreshToken(token!).Should().BeNull();
    }

    // ---------- (e) revoke removes the token (logout) ----------

    [Fact]
    public void RevokeRefreshToken_RemovesTheToken_SoSubsequentValidationFailsClosed()
    {
        var sut = CreateService();
        var refreshToken = sut.GenerateRefreshToken(5, 1);
        sut.ValidateRefreshToken(refreshToken).Should().NotBeNull("the token is valid before revocation");

        sut.RevokeRefreshToken(refreshToken);

        sut.ValidateRefreshToken(refreshToken).Should().BeNull("a revoked token can no longer be used");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("unknown-token")]
    public void RevokeRefreshToken_ForEmptyNullOrUnknownToken_IsANoOp_AndDoesNotThrow(string? token)
    {
        var sut = CreateService();

        Action act = () => sut.RevokeRefreshToken(token!);

        act.Should().NotThrow();
    }
}
