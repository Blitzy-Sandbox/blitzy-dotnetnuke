// MIGRATION: Legacy DNN Forms Authentication (PortalSecurity.SignOut + portalroles/authentication cookies,
// Library/Components/Security/PortalSecurity.vb L77-L95) was stateful and cookie-coupled. It is replaced by
// STATELESS JWT Bearer (HS256, 60-minute access tokens) + opaque refresh-token rotation
// (DnnMigration.Infrastructure.Identity.JwtService) — the migration's single sanctioned behavior change
// (AAP 0.6.2 / 0.7.1 / 0.4.2). These tests assert the new stateless contract: a signed HS256 access token
// carrying identity/role claims, ValidateToken accepting fresh tokens and rejecting tampered/expired/foreign
// ones, and an opaque non-empty refresh token. See root MIGRATION_NOTES.md.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DnnMigration.UnitTests.Identity;

/// <summary>
/// Unit tests for the concrete <see cref="JwtService"/> (HS256 access-token issuance/validation and
/// refresh-token issuance). Exercises the service in isolation — no DI host, no HTTP, no database — by
/// constructing it directly from an <c>IOptions&lt;JwtSettings&gt;</c> snapshot. Validates Gate 2
/// (<c>dotnet test --configuration Release</c> → exit 0, 100% pass).
/// </summary>
public sealed class JwtServiceTests
{
    // Both signing keys are 40 characters (≥ 32 / 256 bits) so SymmetricSecurityKey never throws IDX10653
    // and the JwtService constructor's fail-fast key-length guard is satisfied.
    private const string ValidKey = "DnnMigration-Test-Signing-Key-0123456789";
    private const string ForeignKey = "Different-Foreign-Signing-Key-0123456789";

    /// <summary>Builds a valid <see cref="JwtSettings"/> using the in-test <see cref="ValidKey"/>.</summary>
    private static JwtSettings NewSettings(int expirationMinutes = 60) => new()
    {
        Issuer = "DnnMigration",
        Audience = "DnnMigration",
        Key = ValidKey,
        AccessTokenExpirationMinutes = expirationMinutes,
        RefreshTokenExpirationDays = 7
    };

    /// <summary>Constructs the system under test from the supplied (or default) settings.</summary>
    private static JwtService CreateSut(JwtSettings? settings = null) =>
        new(Options.Create(settings ?? NewSettings()));

    /// <summary>The canonical, fully-populated user used across the token-generation tests.</summary>
    private static User TestUser() => new()
    {
        UserID = 42,
        PortalID = 0,
        Username = "testuser",
        DisplayName = "Test User",
        Email = "test@example.com",
        FirstName = "Test",
        LastName = "User",
        IsSuperUser = true,
        Roles = new[] { "Administrators", "Registered Users" }
    };

    /// <summary>
    /// Mints an arbitrary HS256-signed JWT directly (bypassing the SUT) so the lifetime and wrong-key
    /// negative cases can be expressed without driving <see cref="JwtService"/> into an invalid state.
    /// Building an expired token via the SUT is impossible: a non-positive lifetime makes the
    /// <see cref="JwtSecurityToken"/> constructor throw IDX12401 at generation time.
    /// </summary>
    private static string BuildToken(string key, DateTime notBefore, DateTime expires,
        string issuer = "DnnMigration", string audience = "DnnMigration")
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer, audience,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, "42") },
            notBefore: notBefore, expires: expires,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void GenerateAccessToken_EmitsHs256TokenWithExpectedClaims()
    {
        var token = CreateSut().GenerateAccessToken(TestUser());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256); // "HS256"
        jwt.Issuer.Should().Be("DnnMigration");
        jwt.Audiences.Should().Contain("DnnMigration");
        jwt.Subject.Should().Be("42");

        // Assert by VALUE: OutboundClaimTypeMap may rewrite non-sub claim TYPE strings, but values survive.
        var values = jwt.Claims.Select(c => c.Value).ToList();
        values.Should().Contain("testuser");
        values.Should().Contain("test@example.com");
        values.Should().Contain("Administrators");
        values.Should().Contain("Registered Users");
        jwt.Claims.Should().Contain(c => c.Type == "IsSuperUser" && c.Value == "True"); // custom claim never remapped
    }

    [Fact]
    public void GenerateAccessToken_ReturnsCompactJwtWithThreeSegments()
    {
        var token = CreateSut().GenerateAccessToken(TestUser());

        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void ValidateToken_WithFreshlyIssuedToken_ReturnsPrincipalWithExpectedClaims()
    {
        var sut = CreateSut();
        var token = sut.GenerateAccessToken(TestUser());

        var principal = sut.ValidateToken(token);

        principal.Should().NotBeNull();
        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("42"); // explicit NameIdentifier + sub mapping
        principal.Claims.Should().Contain(c => c.Value == "testuser");            // value-presence: type-agnostic
        principal.Claims.Should().Contain(c => c.Value == "test@example.com");
        principal.IsInRole("Administrators").Should().BeTrue();                    // default RoleClaimType = ClaimTypes.Role
        principal.IsInRole("Registered Users").Should().BeTrue();
        principal.FindFirst("IsSuperUser")!.Value.Should().Be("True");
    }

    [Fact]
    public void ValidateToken_WithTamperedToken_ReturnsNull()
    {
        var sut = CreateSut();
        var token = sut.GenerateAccessToken(TestUser());

        // A JWT is "header.payload.signature". Tamper the FIRST character of the
        // signature segment: all 6 of its bits are significant, so any change alters
        // the decoded signature and HMAC validation must fail. (Flipping the LAST
        // base64url char is unreliable: its low 2 bits are padding ignored on decode,
        // so the signature is unchanged ~1/16 of the time and the token still validates.)
        var sigStart = token.LastIndexOf('.') + 1;
        var sigChar = token[sigStart];
        var tampered = token[..sigStart] + (sigChar == 'a' ? 'b' : 'a') + token[(sigStart + 1)..];

        sut.ValidateToken(tampered).Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithExpiredToken_ReturnsNull()
    {
        // Built manually: expires is in the past but still > notBefore, so JwtSecurityToken's ctor accepts it.
        var expired = BuildToken(ValidKey, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));

        CreateSut().ValidateToken(expired).Should().BeNull(); // ValidateLifetime + ClockSkew=Zero
    }

    [Fact]
    public void ValidateToken_WithTokenSignedByDifferentKey_ReturnsNull()
    {
        var foreign = BuildToken(ForeignKey, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(60));

        CreateSut().ValidateToken(foreign).Should().BeNull(); // signing-key mismatch
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-jwt")]
    [InlineData("aaa.bbb.ccc")]
    public void ValidateToken_WithInvalidInput_ReturnsNull(string? token)
    {
        // Parameter is string? so [InlineData(null)] compiles under nullable; token! avoids CS8604.
        CreateSut().ValidateToken(token!).Should().BeNull();
    }

    // CONTRACT NOTE (verified against depends_on_files): the concrete IJwtService.GenerateRefreshToken takes
    // the authenticated User PLUS a rotation family and a per-token id, and returns a SIGNED JWT refresh token
    // (token_type=refresh) carrying token_family = the family and jti = the supplied token id. This is the
    // CP-FINAL / Code-Review G2 hardening (server-side revoking rotation + replay detection) layered on the
    // CP2 signed-refresh-token contract recorded in JwtService.cs / IJwtService.cs — a signed refresh token
    // can be re-validated by ValidateToken, and its family/jti let the store revoke and reuse-detect it. The
    // test asserts the token is non-empty, validatable, differs per call when the token id differs, and
    // surfaces the supplied family/jti. Documented in root MIGRATION_NOTES.md.
    [Fact]
    public void GenerateRefreshToken_EmitsSignedTokenCarryingSuppliedFamilyAndTokenId()
    {
        var sut = CreateSut();
        var user = TestUser();

        var refresh = sut.GenerateRefreshToken(user, tokenFamily: "fam-123", tokenId: "tok-abc");

        refresh.Should().NotBeNullOrWhiteSpace();

        var principal = sut.ValidateToken(refresh);            // signed JWT, validatable (CP2 contract)
        principal.Should().NotBeNull();
        principal!.FindFirst(IJwtService.TokenTypeClaim)!.Value.Should().Be(IJwtService.RefreshTokenType);
        principal.FindFirst(IJwtService.TokenFamilyClaim)!.Value.Should().Be("fam-123"); // family carried verbatim
        principal.FindFirst("jti")!.Value.Should().Be("tok-abc");                        // jti == supplied token id

        // A different token id yields a different token string (rotation produces distinct tokens).
        sut.GenerateRefreshToken(user, "fam-123", "tok-def").Should().NotBe(refresh);
    }

    [Fact]
    public void AccessTokenExpirationMinutes_ReflectsConfiguration()
    {
        CreateSut(NewSettings(60)).AccessTokenExpirationMinutes.Should().Be(60);
        CreateSut(NewSettings(30)).AccessTokenExpirationMinutes.Should().Be(30); // proves pass-through, not a constant
    }

    [Fact]
    public void RefreshTokenExpirationDays_ReflectsConfiguration()
    {
        // MIGRATION (CP-FINAL / Code-Review G2): exposed so AuthService can compute the store entry's expiry.
        var settings = NewSettings();
        settings.RefreshTokenExpirationDays = 14;
        CreateSut(settings).RefreshTokenExpirationDays.Should().Be(14);
    }
}
