using DnnMigration.Infrastructure.Identity;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests.Infrastructure.Identity;

// MIGRATION (QA F7 — Issue #2): direct unit coverage for the security-critical password hasher
// (DnnMigration.Infrastructure.Identity.PasswordHasher, AAP §0.7.6 — replaces the legacy reversible DES cipher
// in PortalSecurity.vb with one-way BCrypt). The F7 checkpoint flagged PasswordHasher as having ZERO coverage:
// IPasswordHasher appears only as Mock<IPasswordHasher> in the service tests, and the integration login fails
// closed against an empty credential store, so real BCrypt Hash/Verify and BOTH custom fail-closed branches were
// never executed. These tests construct the REAL PasswordHasher and assert the behaviors the checkpoint named:
// the BCrypt round-trip, wrong-password rejection, the empty/null fail-closed branch, the malformed/non-BCrypt
// (SaltParseException) fail-closed branch that must NOT throw, and the per-hash random salt.
public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    private const string Password = "correct-horse-battery-staple";

    // ---------- (a) BCrypt round-trip ----------

    [Fact]
    public void Verify_AgainstAHashOfTheSamePassword_ReturnsTrue()
    {
        var hash = _sut.Hash(Password);

        _sut.Verify(Password, hash).Should().BeTrue();
    }

    // ---------- (b) wrong password ----------

    [Fact]
    public void Verify_WithAWrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash(Password);

        _sut.Verify("not-the-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_IsCaseSensitive_ForThePassword()
    {
        // BCrypt is byte-exact; a case variant of the correct password must not verify.
        var hash = _sut.Hash(Password);

        _sut.Verify(Password.ToUpperInvariant(), hash).Should().BeFalse();
    }

    // ---------- (c) empty / null stored hash: fail closed ----------

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Verify_WithEmptyOrNullStoredHash_ReturnsFalse(string? storedHash)
    {
        // string.IsNullOrEmpty(hash) short-circuits to false before BCrypt is ever called.
        _sut.Verify(Password, storedHash!).Should().BeFalse();
    }

    // ---------- (d) malformed / legacy non-BCrypt hash: fail closed, never throw ----------

    [Theory]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("plaintextpassword")]
    [InlineData("bX9k2Lp0Qw==")] // a leftover legacy DES-style Base64 string (PortalSecurity.vb Encrypt output shape)
    public void Verify_WithAMalformedOrNonBCryptHash_ReturnsFalse_AndDoesNotThrow(string storedHash)
    {
        // The catch(SaltParseException) branch must swallow the parse failure and return false (fail closed),
        // not propagate — a corrupted/legacy hash must deny access, not crash the login path.
        Func<bool> verify = () => _sut.Verify(Password, storedHash);

        verify.Should().NotThrow().Which.Should().BeFalse();
    }

    // ---------- (e) per-hash random salt ----------

    [Fact]
    public void Hash_CalledTwiceForTheSamePassword_ProducesDifferentHashes()
    {
        var first = _sut.Hash(Password);
        var second = _sut.Hash(Password);

        first.Should().NotBe(second, "BCrypt generates and embeds a fresh random salt per hash");
        // Both independently-salted hashes must still verify against the original password.
        _sut.Verify(Password, first).Should().BeTrue();
        _sut.Verify(Password, second).Should().BeTrue();
    }

    // ---------- format / work-factor guard ----------

    [Fact]
    public void Hash_ProducesAStandardBCryptHashWithWorkFactor11()
    {
        var hash = _sut.Hash(Password);

        // A standard BCrypt hash is 60 chars, starts with the "$2" version marker, and carries the cost segment.
        hash.Should().StartWith("$2");
        hash.Should().HaveLength(60);
        hash.Should().Contain("$11$", "the hasher is configured with work factor 11");
    }
}
