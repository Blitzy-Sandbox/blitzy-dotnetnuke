// -----------------------------------------------------------------------------
//  PasswordHasherTests.cs
//
//  xUnit unit tests for DnnMigration.Infrastructure.Identity.PasswordHasher — the
//  BCrypt-based, one-way password hasher that replaces the legacy DotNetNuke
//  security machinery:
//    * PortalSecurity.Encrypt / PortalSecurity.Decrypt reversible DES encryption
//      (Library/Components/Security/PortalSecurity.vb L138 / L175), and
//    * the aspnet_Membership salted-SHA1 password storage.
//
//  These tests pin the two behaviours the migration depends on:
//    1. Hash + Verify round-trips succeed and produce salted, self-describing
//       BCrypt hashes (so no separate salt column is required).
//    2. Verify is TOTAL: wrong passwords, null/empty arguments, and — critically
//       for the documented "forward-hash-on-login" credential-migration strategy —
//       malformed or legacy (non-BCrypt) stored hashes return false rather than
//       throwing (see MIGRATION_NOTES.md).
//
//  The SUT is pure in-memory crypto (no DB, no I/O), is thread-safe, and exposes a
//  parameterless constructor, so it is instantiated directly and never mocked.
//  IPasswordHasher is deliberately SYNCHRONOUS, so none of these calls are awaited.
// -----------------------------------------------------------------------------

using DnnMigration.Application.Interfaces;
using DnnMigration.Infrastructure.Identity;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Unit tests for <see cref="PasswordHasher"/> covering hash production, verification
/// round-trips, argument guards, and the malformed/legacy-hash parity guard that makes
/// credential migration from the existing <c>aspnet_Membership</c> store possible.
/// </summary>
public class PasswordHasherTests
{
    /// <summary>
    /// System under test. <see cref="PasswordHasher"/> is stateless and thread-safe with a
    /// parameterless constructor, so a single instance is reused across the (independent) cases.
    /// </summary>
    private readonly PasswordHasher _sut = new();

    /// <summary>
    /// Canonical BCrypt hash structure (exactly 60 characters):
    /// <c>"$2"</c> + optional variant letter + <c>"$"</c> + 2-digit cost + <c>"$"</c> +
    /// 22-character salt + 31-character digest (53 chars from the BCrypt base-64 alphabet
    /// <c>[A-Za-z0-9./]</c>).
    /// </summary>
    private const string BcryptStructurePattern = @"^\$2[a-z]?\$\d{2}\$[A-Za-z0-9./]{53}$";

    // =========================================================================
    //  Hash(...) behaviour
    // =========================================================================

    [Fact]
    public void Hash_WithValidPassword_ReturnsNonEmptyHashDifferentFromPlaintext()
    {
        const string password = "P@ssw0rd!";

        string hash = _sut.Hash(password);

        hash.Should().NotBeNullOrEmpty("a produced hash must be a usable, storable value");
        hash.Should().NotBe(password, "a hash must never equal the plaintext it protects");
        hash.Should().StartWith("$2", "BCrypt hashes are prefixed with the '$2' algorithm marker");
    }

    [Fact]
    public void Hash_ProducesCanonicalBcryptStructureWithConfiguredWorkFactor()
    {
        string hash = _sut.Hash("P@ssw0rd!");

        hash.Should().HaveLength(60, "a canonical BCrypt hash is exactly 60 characters");
        hash.Should().MatchRegex(BcryptStructurePattern, "the value must be a well-formed BCrypt hash");
        // MIGRATION: PasswordHasher pins a BCrypt work factor of 11 (PasswordHasher.WorkFactor).
        // The "$11$" cost segment proves the configured cost is actually applied during hashing.
        hash.Should().Contain("$11$", "the hasher is configured with a BCrypt work factor of 11");
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        // BCrypt embeds a freshly generated random salt in every hash, so hashing the
        // same input twice must yield two distinct strings (no deterministic output).
        string first = _sut.Hash("abc");
        string second = _sut.Hash("abc");

        first.Should().NotBe(second, "each hash embeds a fresh random salt");
    }

    [Fact]
    public void Hash_WithNullPassword_ThrowsArgumentNullException()
    {
        // Nullable reference types are enabled; the null-forgiving 'null!' deliberately feeds
        // null into the non-nullable parameter to exercise the runtime
        // ArgumentNullException.ThrowIfNull(password) guard.
        Action act = () => _sut.Hash(null!);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("password", "the guard is ArgumentNullException.ThrowIfNull(password)");
    }

    [Fact]
    public void Hash_WithEmptyString_ReturnsNonEmptyHashAndDoesNotThrow()
    {
        // An empty string is NOT null, so ThrowIfNull passes and BCrypt hashes it like any
        // other value — the guard only rejects null, never empty.
        string hash = string.Empty;
        Action act = () => hash = _sut.Hash(string.Empty);

        act.Should().NotThrow("empty is a valid, non-null password");
        hash.Should().NotBeNullOrEmpty().And.StartWith("$2");
    }

    [Theory]
    [InlineData("Secret123!")]
    [InlineData("aA1!aA1!")]
    [InlineData("x")]
    [InlineData("pass phrase with spaces")]
    [InlineData("tab\tand\nnewline")]
    [InlineData("Ünîcödé_Pässwörd_123")]
    [InlineData("LongButUnder72Bytes_0123456789_ABCDEFGHIJKLMNOPQRSTUV")]
    public void Hash_ThenVerify_RoundTripsForVariousPasswords(string password)
    {
        // End-to-end contract: whatever Hash produces, Verify must accept for the same
        // plaintext. Inputs are intentionally kept under BCrypt's 72-byte input limit so the
        // round-trip is unambiguous.
        string hash = _sut.Hash(password);

        _sut.Verify(password, hash).Should().BeTrue(
            "a freshly hashed password must verify against its own hash");
    }

    // =========================================================================
    //  Verify(...) behaviour
    // =========================================================================

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        const string password = "Secret123!";
        string hash = _sut.Hash(password);

        _sut.Verify(password, hash).Should().BeTrue(
            "the correct password must verify against the hash it produced");
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        string hash = _sut.Hash("Secret123!");

        _sut.Verify("WrongPassword", hash).Should().BeFalse(
            "a non-matching password must not verify");
    }

    [Fact]
    public void Verify_IsCaseSensitive()
    {
        string hash = _sut.Hash("CaseSensitive");

        _sut.Verify("casesensitive", hash).Should().BeFalse(
            "BCrypt verification is case-sensitive, so a differing case must not match");
    }

    [Fact]
    public void Verify_WithNullPassword_ReturnsFalseWithoutThrowing()
    {
        string hash = _sut.Hash("Secret123!");

        bool result = true;
        Action act = () => result = _sut.Verify(null!, hash);

        act.Should().NotThrow("the null-password guard must be total and never throw");
        result.Should().BeFalse("a null password can never match a stored hash");
    }

    [Fact]
    public void Verify_WithEmptyPassword_ReturnsFalse()
    {
        string hash = _sut.Hash("Secret123!");

        // NOTE: Verify guards string.IsNullOrEmpty(password) BEFORE consulting BCrypt, so an
        // empty password is rejected outright and can never verify against any hash.
        _sut.Verify(string.Empty, hash).Should().BeFalse(
            "an empty password is rejected by the guard before BCrypt is invoked");
    }

    [Fact]
    public void Verify_WithNullHash_ReturnsFalse()
    {
        bool result = true;
        Action act = () => result = _sut.Verify("Secret123!", null!);

        act.Should().NotThrow("the null-hash guard must be total and never throw");
        result.Should().BeFalse("a null stored hash cannot match any password");
    }

    [Fact]
    public void Verify_WithEmptyHash_ReturnsFalse()
    {
        _sut.Verify("Secret123!", string.Empty).Should().BeFalse(
            "an empty stored hash cannot match any password");
    }

    [Theory]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("12345")]
    [InlineData("plaintextpassword")]
    [InlineData("gWQNGXwR3rQfE3Vy0m2t4qMwLZo=")] // legacy aspnet_Membership base64 SHA1 look-alike
    public void Verify_WithMalformedOrLegacyHash_ReturnsFalseAndDoesNotThrow(string storedHash)
    {
        // MIGRATION: legacy DES / aspnet_Membership credential values stored in the existing
        // database are NOT parseable BCrypt hashes. BCrypt.Verify raises a SaltParseException on
        // such input, which PasswordHasher catches and reports as a simple non-match. This is the
        // guard that keeps the forward-hash-on-login strategy (verify legacy -> re-hash with
        // BCrypt) from crashing on a migrated user's first login. See MIGRATION_NOTES.md.
        bool result = true;
        Action act = () => result = _sut.Verify("anything", storedHash);

        act.Should().NotThrow("a malformed/legacy stored hash must fail gracefully, not throw");
        result.Should().BeFalse("a non-BCrypt stored hash can never match a password");
    }

    // =========================================================================
    //  Interface contract
    // =========================================================================

    [Fact]
    public void PasswordHasher_ImplementsIPasswordHasher()
    {
        // The Application layer depends only on the IPasswordHasher port; the concrete BCrypt
        // implementation must remain substitutable for it via dependency injection.
        _sut.Should().BeAssignableTo<IPasswordHasher>(
            "PasswordHasher is the IPasswordHasher implementation registered in the DI container");
    }
}
