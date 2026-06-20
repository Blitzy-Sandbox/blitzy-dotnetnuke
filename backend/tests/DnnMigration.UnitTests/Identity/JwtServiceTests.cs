// MIGRATION: Legacy DNN Forms Authentication (PortalSecurity.SignOut + portalroles/authentication cookies,
// Library/Components/Security/PortalSecurity.vb L77-L95) was stateful and cookie-coupled. It is replaced by
// STATELESS JWT Bearer (HS256, 60-minute access tokens) + signed-JWT refresh-token rotation
// (DnnMigration.Infrastructure.Identity.JwtService) — the migration's single sanctioned behavior change
// (AAP 0.6.2 / 0.7.1 / 0.4.2). These tests assert the new stateless contract: a signed HS256 access token
// carrying identity/role claims, ValidateToken accepting fresh tokens and rejecting tampered/expired/foreign
// ones, and a signed, non-empty refresh token (token_use=refresh) that round-trips through ValidateToken.
// See root MIGRATION_NOTES.md.
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
/// Unit tests for the concrete <see cref="JwtService"/> HS256 token issuance/validation contract.
/// </summary>
public sealed class JwtServiceTests
{
    // Both signing keys are deliberately ≥32 chars (40 each): HMAC-SHA256 requires a 256-bit key, and a
    // shorter SymmetricSecurityKey/SigningCredentials throws IDX10653 — so these are intentionally long.
    private const string ValidKey = "DnnMigration-Test-Signing-Key-0123456789";
    private const string ForeignKey = "Different-Foreign-Signing-Key-0123456789";

    /// <summary>Builds the canonical settings the SUT is constructed with (issuer/audience = "DnnMigration").</summary>
    private static JwtSettings NewSettings(int expirationMinutes = 60) => new()
    {
        Issuer = "DnnMigration",
        Audience = "DnnMigration",
        Key = ValidKey,
        AccessTokenExpirationMinutes = expirationMinutes,
        RefreshTokenExpirationDays = 7
    };

    /// <summary>Creates the system-under-test from the supplied (or default) settings via IOptions.</summary>
    private static JwtService CreateSut(JwtSettings? settings = null) =>
        new(Options.Create(settings ?? NewSettings()));

    /// <summary>The canonical user whose identity/role claims the access token must carry.</summary>
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
    /// Mints an arbitrary HS256 JWT directly (bypassing the SUT) so the expired-lifetime and wrong-key
    /// negative cases can be exercised without the SUT's own generation guards (IDX12401 on expires &lt;= notBefore).
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
        // Assert the RAW decoded token by VALUES: non-"sub" claim TYPE strings may be remapped by the
        // handler's OutboundClaimTypeMap, so only the value presence (plus alg/issuer/audience/subject and
        // the never-remapped custom "IsSuperUser" type) is asserted.
        var token = CreateSut().GenerateAccessToken(TestUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256); // "HS256"
        jwt.Issuer.Should().Be("DnnMigration");
        jwt.Audiences.Should().Contain("DnnMigration");
        jwt.Subject.Should().Be("42");

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

        token.Split('.').Should().HaveCount(3); // header.payload.signature
    }

    [Fact]
    public void ValidateToken_WithFreshlyIssuedToken_ReturnsPrincipalWithExpectedClaims()
    {
        var sut = CreateSut();
        var token = sut.GenerateAccessToken(TestUser());

        var principal = sut.ValidateToken(token);

        principal.Should().NotBeNull();
        // '!' (never '?.') so a real null dereference fails the test rather than silently short-circuiting.
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

        // Tamper the signature DETERMINISTICALLY by flipping its FIRST character.
        // The previous "flip the final char" approach was flaky: a JWS HMAC-SHA256 signature is 32 bytes,
        // whose base64url encoding is 43 chars, and the final char carries only 4 significant bits plus 2
        // ignored padding bits. Because the token embeds a random `jti` and live timestamps, the signature
        // (and its last char) differs every run; on runs where flipping the last char altered only the
        // padding bits, the signature decoded to identical bytes and validation correctly SUCCEEDED, which
        // failed the BeNull() assertion (~1 in 4 runs). Every signature char EXCEPT the last is fully
        // significant, so flipping the first char always changes the decoded signature bytes and reliably
        // exercises the signature-rejection path.
        var parts = token.Split('.');
        var signature = parts[2];
        parts[2] = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];
        var tampered = string.Join('.', parts);

        sut.ValidateToken(tampered).Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithExpiredToken_ReturnsNull()
    {
        // Built manually: passing a non-positive lifetime to the SUT would throw IDX12401 at generation,
        // so an already-expired token is minted directly to exercise ValidateLifetime + ClockSkew=Zero.
        var expired = BuildToken(ValidKey, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));

        CreateSut().ValidateToken(expired).Should().BeNull();
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
        // Parameter is string? so [InlineData(null)] compiles under nullable; 'token!' avoids CS8604.
        CreateSut().ValidateToken(token!).Should().BeNull();
    }

    // MIGRATION (DEV-032 / C6): the refresh token is a SIGNED HS256 JWT carrying token_use=refresh — NOT the
    // opaque random Base64 string the original plan assumed. The opaque form could never pass ValidateToken
    // (which accepts only signed JWTs), which permanently broke /api/auth/refresh; the signed-JWT form is the
    // committed contract (DnnMigration.Infrastructure.Identity.JwtService.GenerateRefreshToken(User)). These
    // assertions pin that contract: non-empty, three compact segments, a fresh value per call (jti rotation),
    // and a token that validates with token_use=refresh. See root MIGRATION_NOTES.md.
    [Fact]
    public void GenerateRefreshToken_ReturnsSignedJwtThatValidatesAndDiffersPerCall()
    {
        var sut = CreateSut();
        var refresh = sut.GenerateRefreshToken(TestUser());

        refresh.Should().NotBeNullOrWhiteSpace();
        refresh.Split('.').Should().HaveCount(3);                     // compact signed JWT, not opaque Base64
        sut.GenerateRefreshToken(TestUser()).Should().NotBe(refresh); // unique jti -> rotates each call

        var principal = sut.ValidateToken(refresh);
        principal.Should().NotBeNull();
        principal!.FindFirst("token_use")!.Value.Should().Be("refresh");
    }

    [Fact]
    public void AccessTokenExpirationMinutes_ReflectsConfiguration()
    {
        CreateSut(NewSettings(60)).AccessTokenExpirationMinutes.Should().Be(60);
        CreateSut(NewSettings(30)).AccessTokenExpirationMinutes.Should().Be(30); // proves pass-through, not a constant
    }
}
