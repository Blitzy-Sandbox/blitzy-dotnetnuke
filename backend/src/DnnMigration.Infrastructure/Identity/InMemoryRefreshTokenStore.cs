using System.Collections.Concurrent;
using DnnMigration.Application.Interfaces;

namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// Thread-safe, in-process implementation of <see cref="IRefreshTokenStore"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by refresh-token family.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION (CP-FINAL auth-chain hardening / Code-Review G2): provides the server-side revocation and
/// replay-detection state the stateless JWT refresh design lacked. Each family maps to its CURRENT token id;
/// rotation supersedes the previous id, presenting a superseded id revokes the whole family (theft defense),
/// and logout revokes every family for a user.
/// </para>
/// <para>
/// SCHEMA NOTE (ADR-002 / AAP §0.2.2): the DNN <c>4.9.0.85</c> schema is preserved unchanged and no new
/// tables may be added, so the family state lives in process memory and is registered as a SINGLETON in the
/// API composition root (<c>Program.cs</c>). This is correct for the single-container Phase-1 topology; a
/// horizontally-scaled deployment substitutes a shared backing store (e.g. Redis or a dedicated table
/// outside the preserved schema) for this type without changing the Application layer, because consumers
/// depend only on <see cref="IRefreshTokenStore"/>. State is intentionally non-durable: a process restart
/// drops all families, which simply forces clients to re-authenticate (it never weakens security).
/// Documented in root MIGRATION_NOTES.md.
/// </para>
/// </remarks>
public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    /// <summary>
    /// The current refresh-token state for one family: the active token id, the owning user, and the
    /// absolute UTC expiry. A <see langword="record"/> so <see cref="ConcurrentDictionary{TKey,TValue}.TryUpdate"/>
    /// can perform a value-based compare-and-swap during rotation.
    /// </summary>
    /// <param name="TokenId">The family's CURRENT (most recently issued) token id (<c>jti</c>).</param>
    /// <param name="UserId">The owning user's id.</param>
    /// <param name="ExpiresUtc">Absolute UTC expiry of the current refresh token.</param>
    private sealed record FamilyState(string TokenId, int UserId, DateTime ExpiresUtc);

    /// <summary>Family id -&gt; current <see cref="FamilyState"/>. Concurrent for the shared singleton instance.</summary>
    private readonly ConcurrentDictionary<string, FamilyState> _families = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(string tokenFamily, string tokenId, int userId, DateTime expiresUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        // A brand-new family established at login. Assigning by indexer initializes (or, defensively,
        // overwrites) the family's current state.
        _families[tokenFamily] = new FamilyState(tokenId, userId, expiresUtc);
    }

    /// <inheritdoc />
    public bool TryRotate(string tokenFamily, string presentedTokenId, string newTokenId, DateTime newExpiresUtc)
    {
        if (string.IsNullOrWhiteSpace(tokenFamily) || string.IsNullOrWhiteSpace(presentedTokenId) ||
            string.IsNullOrWhiteSpace(newTokenId))
        {
            return false;
        }

        if (!_families.TryGetValue(tokenFamily, out var current))
        {
            // Unknown or already-revoked family (e.g. revoked by a prior reuse, or by logout) — not rotatable.
            return false;
        }

        // Defensive pruning: the signed-JWT lifetime is also validated upstream by IJwtService.ValidateToken,
        // but a stored entry that has passed its expiry is treated as inactive and removed.
        if (current.ExpiresUtc <= DateTime.UtcNow)
        {
            _families.TryRemove(tokenFamily, out _);
            return false;
        }

        if (!string.Equals(current.TokenId, presentedTokenId, StringComparison.Ordinal))
        {
            // REPLAY DETECTED: a superseded (already-rotated) token id from this family was presented. The
            // legitimate holder and the attacker now both hold tokens of this family, so the only safe action
            // is to revoke the ENTIRE family, forcing re-authentication.
            _families.TryRemove(tokenFamily, out _);
            return false;
        }

        // Valid rotation: atomically swap the current state from `current` to the rotated state. TryUpdate
        // uses value equality on the record, so a concurrent rotation that already advanced the family makes
        // this call return false (the losing caller is rejected) without nuking the family.
        var rotated = current with { TokenId = newTokenId, ExpiresUtc = newExpiresUtc };
        return _families.TryUpdate(tokenFamily, rotated, current);
    }

    /// <inheritdoc />
    public void RevokeFamily(string tokenFamily)
    {
        if (string.IsNullOrWhiteSpace(tokenFamily))
        {
            return;
        }

        _families.TryRemove(tokenFamily, out _);
    }

    /// <inheritdoc />
    public void RevokeAllForUser(int userId)
    {
        // Enumerating a ConcurrentDictionary yields a moment-in-time snapshot and tolerates concurrent
        // mutation, so removing matching families while iterating is safe.
        foreach (var pair in _families)
        {
            if (pair.Value.UserId == userId)
            {
                _families.TryRemove(pair.Key, out _);
            }
        }
    }
}
