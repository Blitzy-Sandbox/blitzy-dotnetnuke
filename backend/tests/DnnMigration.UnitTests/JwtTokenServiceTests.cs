// -----------------------------------------------------------------------------
//  JwtTokenServiceTests.cs
//
//  xUnit unit tests for DnnMigration.Infrastructure.Identity.JwtTokenService — the
//  HS256 JWT bearer token factory/validator that replaces the legacy DotNetNuke
//  Forms Authentication ticket issuance and PortalSecurity crypto machinery
//  (Library/Components/Security/PortalSecurity.vb: SignOut L77 FormsAuthentication,
//  reversible DES Encrypt/Decrypt, and CreateKey via RNGCryptoServiceProvider L564).
//
//  Behavioural contract pinned by these tests:
//    1. Construction fails fast with InvalidOperationException when the HS256 signing
//       key is missing/whitespace or shorter than 32 bytes (256 bits); a null options
//       argument throws ArgumentNullException.
//    2. CreateToken issues a well-formed, 3-segment signed JWT whose ExpiresAt is
//       ~AccessTokenExpirationMinutes (15) in the future.
//    3. A token issued by an instance validates back — via the SAME instance / signing
//       key — to a ClaimsPrincipal carrying the expected NameIdentifier/Name/Role
//       claims (plus the custom portalId/isSuperUser and optional email/displayName).
//    4. ValidateToken is TOTAL: garbage, null, empty, whitespace and wrong-key tokens
//       all return null and NEVER throw.
//    5. GenerateAccessToken re-issues a validatable token from an explicit claim set;
//       GenerateRefreshToken returns a non-empty, unique, Base64 (512-bit) opaque value.
//
//  The SUT is pure in-memory crypto (no DB, no I/O) and IJwtTokenService is deliberately
//  SYNCHRONOUS, so none of these calls are awaited. JwtSettings is supplied through the
//  Options pattern via Options.Create(...). Because the token is written from claims that
//  carry explicit ClaimTypes.* URIs and validated with MapInboundClaims=false, the claim
//  types round-trip identically and are asserted with the ClaimTypes.* constants exactly
//  as the service writes them.
// -----------------------------------------------------------------------------

using System.Security.Claims;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Unit tests for <see cref="JwtTokenService"/> covering constructor fail-fast validation,
/// access-token issuance (from a <see cref="UserDto"/> and from an explicit claim set),
/// token validation round-trips and failure paths, and refresh-token generation.
/// </summary>
public class JwtTokenServiceTests
{
    /// <summary>
    /// A valid HS256 signing key: 50 ASCII characters = 50 UTF-8 bytes, comfortably above the
    /// 32-byte (256-bit) minimum the constructor enforces. This is an obvious, non-secret test
    /// value and never matches any real provider key format.
    /// </summary>
    private const string ValidSecretKey = "super-secret-signing-key-for-unit-tests-1234567890";

    /// <summary>
    /// A second, DISTINCT valid signing key (also &gt;= 32 UTF-8 bytes) used only by the wrong-key
    /// negative test so that a token signed with one key fails validation under the other.
    /// </summary>
    private const string AltSecretKey = "another-totally-different-valid-signing-key-0987654321";

    // =========================================================================
    //  Test helpers
    // =========================================================================

    /// <summary>Builds a fully-populated, valid <see cref="JwtSettings"/> for the happy-path SUT.</summary>
    private static JwtSettings ValidSettings() => new()
    {
        Issuer = "DnnMigration",
        Audience = "DnnMigrationClient",
        SecretKey = ValidSecretKey,
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    /// <summary>
    /// Constructs the system under test from the supplied settings (or <see cref="ValidSettings"/>
    /// when none is given), wrapping them in <see cref="IOptions{TOptions}"/> via
    /// <see cref="Options.Create{TOptions}(TOptions)"/> exactly as the Api composition root would.
    /// </summary>
    private static JwtTokenService CreateSut(JwtSettings? settings = null) =>
        new(Options.Create(settings ?? ValidSettings()));

    /// <summary>
    /// A representative <see cref="UserDto"/>. Only the fields projected into token claims are set;
    /// the remaining init-only properties (AffiliateID, Roles, Membership, Profile) fall back to the
    /// record's own defaults, so the token's role set comes solely from the explicit roles argument.
    /// </summary>
    private static UserDto SampleUser() => new()
    {
        UserID = 42,
        PortalID = 0,
        Username = "admin",
        DisplayName = "Administrator",
        FirstName = "Ad",
        LastName = "Min",
        Email = "admin@example.com",
        IsSuperUser = true
    };

    // =========================================================================
    //  Constructor validation
    // =========================================================================

    [Fact]
    public void Constructor_WithShortSecretKey_ThrowsInvalidOperationException()
    {
        // "short" is 5 UTF-8 bytes — below the 256-bit HS256 minimum — so the
        // Encoding.UTF8.GetByteCount(SecretKey) < 32 guard rejects it at construction.
        Action act = () => new JwtTokenService(
            Options.Create(new JwtSettings { Issuer = "i", Audience = "a", SecretKey = "short" }));

        act.Should().Throw<InvalidOperationException>(
            "an HS256 signing key shorter than 32 bytes (256 bits) must fail fast at construction");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Constructor_WithEmptyOrWhitespaceSecretKey_ThrowsInvalidOperationException(string secretKey)
    {
        // string.IsNullOrWhiteSpace(SecretKey) short-circuits before the byte-count check, so an
        // unset/blank key also fails fast rather than producing an unusable, silently-weak service.
        Action act = () => new JwtTokenService(
            Options.Create(new JwtSettings { Issuer = "i", Audience = "a", SecretKey = secretKey }));

        act.Should().Throw<InvalidOperationException>(
            "a missing/whitespace signing key must fail fast at construction");
    }

    [Theory]
    [InlineData(31, true)]   // 31 ASCII bytes -> below the 256-bit minimum -> must throw
    [InlineData(32, false)]  // exactly 32 ASCII bytes -> meets the minimum -> must construct
    public void Constructor_EnforcesMinimumThirtyTwoByteSigningKey(int keyLength, bool shouldThrow)
    {
        // Pins the exact "< 32" boundary of the constructor guard. 'k' is a single-byte ASCII char,
        // so string length equals the UTF-8 byte count Encoding.UTF8.GetByteCount measures.
        var secretKey = new string('k', keyLength);
        Action act = () => new JwtTokenService(
            Options.Create(new JwtSettings { Issuer = "i", Audience = "a", SecretKey = secretKey }));

        if (shouldThrow)
        {
            act.Should().Throw<InvalidOperationException>(
                "31 bytes is one byte below the 256-bit HS256 minimum");
        }
        else
        {
            act.Should().NotThrow("exactly 32 bytes (256 bits) satisfies the HS256 minimum");
        }
    }

    [Fact]
    public void Constructor_WithValidSettings_DoesNotThrow()
    {
        Action act = () => CreateSut();

        act.Should().NotThrow("valid settings with a >= 32-byte signing key must construct successfully");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // The constructor's first line is ArgumentNullException.ThrowIfNull(options); the
        // null-forgiving 'null!' feeds null past nullable analysis to exercise that guard.
        Action act = () => new JwtTokenService(null!);

        act.Should().Throw<ArgumentNullException>(
            "the constructor guards its options argument with ArgumentNullException.ThrowIfNull");
    }

    [Fact]
    public void JwtTokenService_IsAssignableToIJwtTokenService()
    {
        // The Application layer depends only on the IJwtTokenService port; the concrete
        // implementation must remain substitutable for it via dependency injection.
        CreateSut().Should().BeAssignableTo<IJwtTokenService>(
            "JwtTokenService is the IJwtTokenService implementation registered in the DI container");
    }

    // =========================================================================
    //  CreateToken(UserDto, roles)
    // =========================================================================

    [Fact]
    public void CreateToken_ReturnsNonEmptyThreeSegmentToken()
    {
        var (token, _) = CreateSut().CreateToken(
            SampleUser(), new[] { "Administrators", "Registered Users" });

        token.Should().NotBeNullOrEmpty("CreateToken must return a serialized JWT");
        token.Split('.').Should().HaveCount(3,
            "a JWS compact serialization is header.payload.signature");
    }

    [Fact]
    public void CreateToken_SetsExpiryApproximatelyAccessTokenLifetimeInTheFuture()
    {
        var before = DateTime.UtcNow;

        var (_, expiresAt) = CreateSut().CreateToken(SampleUser(), Array.Empty<string>());

        expiresAt.Should().BeAfter(before, "the access token must expire in the future");
        expiresAt.Should().BeCloseTo(before.AddMinutes(15), TimeSpan.FromMinutes(1),
            "ExpiresAt must be ~AccessTokenExpirationMinutes (15) ahead of issuance");
    }

    [Fact]
    public void CreateToken_WithNullUser_ThrowsArgumentNullException()
    {
        Action act = () => CreateSut().CreateToken(null!, Array.Empty<string>());

        act.Should().Throw<ArgumentNullException>("CreateToken guards its user argument");
    }

    // =========================================================================
    //  ValidateToken(token) — round-trip
    // =========================================================================

    [Fact]
    public void ValidateToken_RoundTripsIssuedToken_ToPrincipalWithIdentityAndRoleClaims()
    {
        // Reuse a SINGLE instance so issue and validate share the same signing key.
        var sut = CreateSut();
        var (token, _) = sut.CreateToken(
            SampleUser(), new[] { "Administrators", "Registered Users" });

        var principal = sut.ValidateToken(token);

        principal.Should().NotBeNull(
            "a token issued by this instance must validate against the same signing key");
        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("42",
            "the NameIdentifier claim carries UserID");
        principal.FindFirst(ClaimTypes.Name)!.Value.Should().Be("admin",
            "the Name claim carries Username");
        principal.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Should().Contain(new[] { "Administrators", "Registered Users" },
                "each supplied role becomes a ClaimTypes.Role claim");
    }

    [Fact]
    public void ValidateToken_RoundTripsCustomAndOptionalClaims()
    {
        var sut = CreateSut();
        var (token, _) = sut.CreateToken(SampleUser(), Array.Empty<string>());

        var principal = sut.ValidateToken(token);

        principal.Should().NotBeNull();
        // MIGRATION: portalId + isSuperUser replace the legacy PortalSecurity portal/host context that
        // the DNN SecurityAccessLevel (View/Edit/Admin/Host) checks relied on.
        principal!.FindFirst("portalId")!.Value.Should().Be("0", "the portalId claim carries PortalID");
        principal.FindFirst("isSuperUser")!.Value.Should().Be("true",
            "the isSuperUser claim reflects the host-administrator flag");
        principal.FindFirst(ClaimTypes.Email)!.Value.Should().Be("admin@example.com",
            "the Email claim is emitted when the user has an email");
        principal.FindFirst("displayName")!.Value.Should().Be("Administrator",
            "the displayName claim is emitted when the user has a display name");
    }

    [Fact]
    public void ValidateToken_OmitsOptionalClaimsWhenSourceFieldsAreBlank()
    {
        // A user with no email/display name must not produce empty optional claims (BuildClaims skips
        // null/whitespace values); the mandatory identity claims remain present.
        var sut = CreateSut();
        var bareUser = new UserDto { UserID = 7, PortalID = 3, Username = "svc", IsSuperUser = false };
        var (token, _) = sut.CreateToken(bareUser, Array.Empty<string>());

        var principal = sut.ValidateToken(token);

        principal.Should().NotBeNull();
        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("7");
        principal.FindFirst("isSuperUser")!.Value.Should().Be("false",
            "a non-super user is stamped isSuperUser=false");
        principal.FindFirst(ClaimTypes.Email).Should().BeNull("no Email claim is emitted for a blank email");
        principal.FindFirst("displayName").Should().BeNull(
            "no displayName claim is emitted for a blank display name");
    }

    // =========================================================================
    //  ValidateToken(token) — failure paths (total: return null, never throw)
    // =========================================================================

    [Theory]
    [InlineData("this.is.garbage")]
    [InlineData("aaa.bbb.ccc")]
    [InlineData("not-even-a-jwt")]
    [InlineData("header.payload")] // only two segments
    public void ValidateToken_WithMalformedToken_ReturnsNull(string token)
    {
        CreateSut().ValidateToken(token).Should().BeNull(
            "a malformed/garbage token must fail validation and yield null, not throw");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateToken_WithNullEmptyOrWhitespace_ReturnsNull(string? token)
    {
        // The string.IsNullOrWhiteSpace(token) guard short-circuits these inputs to null before any
        // parsing is attempted (null! satisfies the non-nullable parameter under nullable analysis).
        CreateSut().ValidateToken(token!).Should().BeNull(
            "null/empty/whitespace input is short-circuited to a null principal");
    }

    [Fact]
    public void ValidateToken_NeverThrows_EvenForGarbageInput()
    {
        var sut = CreateSut();

        Action act = () => sut.ValidateToken("clearly-not-a-valid-jwt-token");

        act.Should().NotThrow(
            "ValidateToken must be total and translate every validation failure into a null result");
    }

    [Fact]
    public void ValidateToken_WithTokenSignedByDifferentKey_ReturnsNull()
    {
        // Both instances share Issuer/Audience, so the ONLY difference is the signing key: this
        // isolates signature validation as the sole reason the cross-instance token is rejected.
        var issuer = CreateSut(); // signs with ValidSecretKey
        var validator = CreateSut(new JwtSettings
        {
            Issuer = "DnnMigration",
            Audience = "DnnMigrationClient",
            SecretKey = AltSecretKey, // a different, still-valid (>= 32 byte) key
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        });

        var (token, _) = issuer.CreateToken(SampleUser(), Array.Empty<string>());

        validator.ValidateToken(token).Should().BeNull(
            "a token whose HS256 signature was produced with a different key must fail signature validation");
    }

    [Fact]
    public void ValidateToken_WithMismatchedIssuer_ReturnsNull()
    {
        // A token minted for a different issuer must be rejected because ValidateIssuer is enabled.
        var issuer = CreateSut(new JwtSettings
        {
            Issuer = "SomeOtherIssuer",
            Audience = "DnnMigrationClient",
            SecretKey = ValidSecretKey,
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        });
        var validator = CreateSut(); // expects Issuer = "DnnMigration"

        var (token, _) = issuer.CreateToken(SampleUser(), Array.Empty<string>());

        validator.ValidateToken(token).Should().BeNull(
            "issuer validation rejects a token minted under a different issuer");
    }

    // =========================================================================
    //  GenerateAccessToken(claims)
    // =========================================================================

    [Fact]
    public void GenerateAccessToken_FromExplicitClaims_ProducesValidatableToken()
    {
        var sut = CreateSut();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Name, "editor"),
            new Claim(ClaimTypes.Role, "Editors")
        };

        var token = sut.GenerateAccessToken(claims);

        token.Should().NotBeNullOrEmpty("GenerateAccessToken must return a serialized JWT");
        token.Split('.').Should().HaveCount(3, "a JWS compact serialization has three segments");

        // MIGRATION: this low-level path re-issues a token from claims already present on a validated
        // principal (the refresh flow), so the produced token must itself validate on the same instance.
        var principal = sut.ValidateToken(token);
        principal.Should().NotBeNull(
            "a token generated from explicit claims must validate on the same instance");
        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("7");
        principal.FindFirst(ClaimTypes.Name)!.Value.Should().Be("editor");
        principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Should().Contain("Editors");
    }

    [Fact]
    public void GenerateAccessToken_WithNullClaims_ThrowsArgumentNullException()
    {
        Action act = () => CreateSut().GenerateAccessToken(null!);

        act.Should().Throw<ArgumentNullException>("GenerateAccessToken guards its claims argument");
    }

    // =========================================================================
    //  GenerateRefreshToken()
    // =========================================================================

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyToken()
    {
        CreateSut().GenerateRefreshToken().Should().NotBeNullOrEmpty(
            "a refresh token must be a usable, storable opaque value");
    }

    [Fact]
    public void GenerateRefreshToken_ProducesUniqueValues()
    {
        var sut = CreateSut();

        sut.GenerateRefreshToken().Should().NotBe(sut.GenerateRefreshToken(),
            "each refresh token embeds fresh cryptographic randomness (RandomNumberGenerator.GetBytes)");
    }

    [Fact]
    public void GenerateRefreshToken_IsValidBase64EncodingFiveHundredTwelveBitsOfEntropy()
    {
        var token = CreateSut().GenerateRefreshToken();

        // MIGRATION: replaces PortalSecurity.CreateKey (RNGCryptoServiceProvider + hex) with a Base64
        // encoding of 64 random bytes (512 bits) of entropy.
        Action decode = () => Convert.FromBase64String(token);
        decode.Should().NotThrow("the refresh token must be valid Base64");

        Convert.FromBase64String(token).Should().HaveCount(64,
            "the token encodes 64 random bytes (512 bits) of entropy");
    }
}
