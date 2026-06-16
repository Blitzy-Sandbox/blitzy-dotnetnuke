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

        var lastChar = token[^1];
        var tampered = token[..^1] + (lastChar == 'a' ? 'b' : 'a'); // flip final char → signature breaks

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
    // the authenticated User and returns a SIGNED JWT refresh token (token_type=refresh), NOT an opaque
    // Base64 blob of 64 random bytes. This is the CP2 auth-chain hardening recorded in JwtService.cs /
    // IJwtService.cs — a signed refresh token can be re-validated by ValidateToken, which is what makes
    // /api/auth/refresh and the frontend 401-recovery flow work. The test therefore asserts the refresh
    // token is non-empty, unique per call (fresh Jti), and validatable, rather than decoding 64 raw bytes.
    // Documented in root MIGRATION_NOTES.md.
    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyValueThatDiffersPerCall()
    {
        var sut = CreateSut();
        var user = TestUser();

        var refresh = sut.GenerateRefreshToken(user);

        refresh.Should().NotBeNullOrWhiteSpace();
        sut.GenerateRefreshToken(user).Should().NotBe(refresh); // unique Jti per call
        sut.ValidateToken(refresh).Should().NotBeNull();        // signed JWT, validatable (CP2 contract)
    }

    [Fact]
    public void AccessTokenExpirationMinutes_ReflectsConfiguration()
    {
        CreateSut(NewSettings(60)).AccessTokenExpirationMinutes.Should().Be(60);
        CreateSut(NewSettings(30)).AccessTokenExpirationMinutes.Should().Be(30); // proves pass-through, not a constant
    }
}
