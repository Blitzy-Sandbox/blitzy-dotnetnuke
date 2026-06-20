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
/// Unit tests for the concrete <see cref="PasswordHasher"/> (BCrypt.Net-Next) that replaces the
/// legacy reversible DES <c>Encrypt</c>/<c>Decrypt</c> pair from <c>PortalSecurity.vb</c>. The suite
/// verifies the one-way migration contract end-to-end: <see cref="PasswordHasher.Hash"/> emits a
/// non-empty, <c>$2</c>-prefixed salted hash that never echoes the plaintext and differs on every
/// call (fresh per-call salt), while <see cref="PasswordHasher.Verify"/> accepts a matching password
/// and rejects a non-matching one. The hasher is stateless, so a single shared instance is exercised.
/// </summary>
public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    /// <summary>
    /// Hashing a password must yield a non-empty BCrypt hash that is NOT the plaintext and carries the
    /// BCrypt <c>$2</c> version prefix. This is the core security assertion versus the reversible DES
    /// cipher it supersedes: the output is a one-way digest, never the original secret.
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
    /// BCrypt embeds a fresh random salt in every hash, so hashing the same password twice produces two
    /// distinct strings — yet both must verify against the original password. This guards against any
    /// regression toward a deterministic (unsalted) digest.
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
    /// A password verified against a hash produced from the very same password must return <c>true</c>.
    /// Covers a single character, symbol-rich, mixed, and non-ASCII (Unicode) inputs to confirm correct
    /// round-tripping across encodings — all kept well under BCrypt's 72-byte truncation boundary.
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
    /// Verifying a different password against an existing hash must return <c>false</c>, confirming the
    /// hasher does not yield false positives.
    /// </summary>
    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("correct-horse");

        _sut.Verify("wrong-horse", hash).Should().BeFalse();
    }
}
