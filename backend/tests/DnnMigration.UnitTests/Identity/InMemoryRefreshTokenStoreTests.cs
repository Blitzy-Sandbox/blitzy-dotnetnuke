// MIGRATION (CP-FINAL / Code-Review G2): unit tests for the server-side refresh-token state that makes JWT
// refresh rotation REVOKING and replay-resistant. The store tracks the CURRENT token id per family; a valid
// rotation supersedes the previous id, presenting a superseded id is a replay that revokes the whole family,
// and logout revokes every family for a user. The DNN 4.9.0.85 schema is preserved unchanged (ADR-002), so
// the Phase-1 store is in-memory; these tests exercise that implementation directly. See MIGRATION_NOTES.md.

using DnnMigration.Application.Interfaces;
using DnnMigration.Infrastructure.Identity;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests.Identity;

/// <summary>
/// Unit tests for <see cref="InMemoryRefreshTokenStore"/> (the <see cref="IRefreshTokenStore"/> implementation):
/// registration, revoking rotation, replay (reuse) detection, expiry pruning, and revocation by family and by
/// user. Exercised in isolation with no DI host, HTTP, or database. Validates Gate 2 (<c>dotnet test</c>).
/// </summary>
public sealed class InMemoryRefreshTokenStoreTests
{
    private static DateTime Future => DateTime.UtcNow.AddDays(7);

    private static IRefreshTokenStore CreateSut() => new InMemoryRefreshTokenStore();

    [Fact]
    public void TryRotate_with_current_token_id_succeeds()
    {
        var sut = CreateSut();
        sut.Register("fam", "t1", userId: 1, Future);

        sut.TryRotate("fam", "t1", "t2", Future).Should().BeTrue();
    }

    [Fact]
    public void TryRotate_supports_a_full_rotation_chain()
    {
        var sut = CreateSut();
        sut.Register("fam", "t1", 1, Future);

        sut.TryRotate("fam", "t1", "t2", Future).Should().BeTrue();
        sut.TryRotate("fam", "t2", "t3", Future).Should().BeTrue(); // the new current id rotates next
        sut.TryRotate("fam", "t3", "t4", Future).Should().BeTrue();
    }

    [Fact]
    public void TryRotate_on_unknown_family_returns_false()
    {
        CreateSut().TryRotate("ghost", "t1", "t2", Future).Should().BeFalse();
    }

    [Fact]
    public void TryRotate_with_superseded_token_id_is_replay_and_revokes_the_whole_family()
    {
        var sut = CreateSut();
        sut.Register("fam", "t1", 1, Future);

        // Legitimate rotation: t1 -> t2 (t1 is now superseded).
        sut.TryRotate("fam", "t1", "t2", Future).Should().BeTrue();

        // REPLAY: presenting the superseded t1 again must fail AND revoke the family.
        sut.TryRotate("fam", "t1", "tX", Future).Should().BeFalse();

        // Family is revoked: even the previously-valid current id (t2) can no longer rotate.
        sut.TryRotate("fam", "t2", "t3", Future).Should().BeFalse();
    }

    [Fact]
    public void TryRotate_on_expired_entry_returns_false_and_prunes()
    {
        var sut = CreateSut();
        sut.Register("fam", "t1", 1, DateTime.UtcNow.AddSeconds(-1)); // already expired

        sut.TryRotate("fam", "t1", "t2", Future).Should().BeFalse();
        // Pruned: a second attempt also fails (family removed).
        sut.TryRotate("fam", "t1", "t2", Future).Should().BeFalse();
    }

    [Fact]
    public void RevokeFamily_invalidates_only_that_family()
    {
        var sut = CreateSut();
        sut.Register("famA", "a1", 1, Future);
        sut.Register("famB", "b1", 1, Future);

        sut.RevokeFamily("famA");

        sut.TryRotate("famA", "a1", "a2", Future).Should().BeFalse(); // revoked
        sut.TryRotate("famB", "b1", "b2", Future).Should().BeTrue();  // untouched
    }

    [Fact]
    public void RevokeAllForUser_revokes_every_family_for_that_user_only()
    {
        var sut = CreateSut();
        sut.Register("u1-famA", "a1", userId: 1, Future);
        sut.Register("u1-famB", "b1", userId: 1, Future);
        sut.Register("u2-fam", "c1", userId: 2, Future);

        sut.RevokeAllForUser(1);

        sut.TryRotate("u1-famA", "a1", "a2", Future).Should().BeFalse(); // user 1 revoked
        sut.TryRotate("u1-famB", "b1", "b2", Future).Should().BeFalse(); // user 1 revoked
        sut.TryRotate("u2-fam", "c1", "c2", Future).Should().BeTrue();   // user 2 untouched
    }

    [Fact]
    public void Register_is_idempotent_per_family_and_resets_current_token()
    {
        // Property 1: re-registering an existing family id (defensive: a brand-new login that reuses a family
        // id) RESETS the current token, so the NEWLY registered id rotates successfully.
        var resetThenRotate = CreateSut();
        resetThenRotate.Register("fam", "t1", 1, Future);
        resetThenRotate.Register("fam", "t9", 1, Future); // re-register resets current -> t9

        resetThenRotate.TryRotate("fam", "t9", "t10", Future).Should().BeTrue(); // new id is current

        // Property 2: after the reset the SUPERSEDED old id ("t1") is no longer current. Presenting it is
        // treated as replay — it returns false AND revokes the whole family (the same theft-defense applied to
        // any superseded-token reuse), so the once-current "t9" can no longer rotate either. A fresh instance
        // isolates this from Property 1 (whose rotation already advanced the family to t10).
        var resetThenReplay = CreateSut();
        resetThenReplay.Register("fam", "t1", 1, Future);
        resetThenReplay.Register("fam", "t9", 1, Future); // re-register resets current -> t9

        resetThenReplay.TryRotate("fam", "t1", "t2", Future).Should().BeFalse();  // old id rejected as replay...
        resetThenReplay.TryRotate("fam", "t9", "t10", Future).Should().BeFalse(); // ...which burned the family
    }
}
