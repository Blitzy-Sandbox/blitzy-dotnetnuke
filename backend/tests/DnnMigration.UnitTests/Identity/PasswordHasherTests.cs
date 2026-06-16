// MIGRATION: Legacy DNN PortalSecurity.Encrypt/Decrypt (Library/Components/Security/PortalSecurity.vb L138-L220)
// used REVERSIBLE 56-bit DES symmetric encryption. It is replaced by ONE-WAY BCrypt adaptive hashing
// (DnnMigration.Infrastructure.Identity.PasswordHasher) — the migration's single sanctioned behavior change
// (AAP 0.6.2 / 0.7.1). These tests assert the new one-way contract: a $2-prefixed salted hash that never
// equals the plaintext, a fresh random salt per call, and correct Verify(true/false) semantics.
// See root MIGRATION_NOTES.md.
using DnnMigration.Infrastructure.Identity;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests.Identity;

/// <summary>
/// Unit tests for the concrete BCrypt-backed <see cref="PasswordHasher"/>. The suite locks in the
/// migration's one-way hashing contract (a salted, <c>$2</c>-prefixed digest that never echoes the
/// plaintext and uses a fresh random salt per call) and the round-trip <c>Verify</c> semantics that
/// replace the legacy reversible DES <c>Encrypt</c>/<c>Decrypt</c> routines. The system under test is
/// stateless, so a single shared instance is reused across every test case.
/// </summary>
public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    /// <summary>
    /// A hash must be a non-empty, BCrypt-formatted (<c>$2</c>-prefixed) value that is never equal to
    /// the supplied plaintext — the core one-way guarantee versus the legacy reversible DES cipher.
    /// </summary>
    [Fact]
    public void Hash_GivenPassword_ReturnsBcryptHashThatIsNotPlaintext()
    {
        const string password = "P@ssw0rd!";

        var hash = _sut.Hash(password);

        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
        hash.Should().StartWith("$2");
    }

    /// <summary>
    /// Hashing the same password twice yields two distinct digests (a fresh random salt per call),
    /// and both digests still verify successfully against the original plaintext.
    /// </summary>
    [Fact]
    public void Hash_CalledTwiceForSamePassword_ReturnsDifferentHashes_AndBothVerify()
    {
        const string password = "same-input-123";

        var hash1 = _sut.Hash(password);
        var hash2 = _sut.Hash(password);

        hash1.Should().NotBe(hash2);
        _sut.Verify(password, hash1).Should().BeTrue();
        _sut.Verify(password, hash2).Should().BeTrue();
    }

    /// <summary>
    /// A password verifies against its own freshly computed hash across a representative range of
    /// inputs, including a single character and a non-ASCII (Unicode) password.
    /// </summary>
    [Theory]
    [InlineData("a")]
    [InlineData("P@ssw0rd!")]
    [InlineData("Tr0ub4dor&3")]
    [InlineData("Ünïcödé-pass")]
    public void Verify_WithCorrectPassword_ReturnsTrue(string password)
    {
        var hash = _sut.Hash(password);

        _sut.Verify(password, hash).Should().BeTrue();
    }

    /// <summary>
    /// Verification of a password that does not match the stored hash returns <c>false</c>.
    /// </summary>
    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("correct-horse");

        _sut.Verify("wrong-horse", hash).Should().BeFalse();
    }
}
