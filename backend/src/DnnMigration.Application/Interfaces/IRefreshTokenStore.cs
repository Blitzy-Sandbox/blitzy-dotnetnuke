namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Server-side state for refresh-token rotation with replay (reuse) detection and revocation.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION (CP-FINAL auth-chain hardening / Code-Review G2): the earlier refresh design issued a
/// self-contained signed JWT that <see cref="IJwtService.ValidateToken"/> could validate but that the
/// server could never <i>invalidate</i> — so an old (already-rotated) refresh token stayed valid until its
/// natural expiry, rotation was non-revoking, and <c>/api/auth/logout</c> was a no-op. This store closes
/// that gap by tracking the <b>current</b> token id for each refresh-token <i>family</i> (a logical session
/// established at login). Each rotation supersedes the previous token id within the family; presenting a
/// superseded id is treated as a replay and revokes the entire family (defense against token theft), and
/// logout revokes every family belonging to the user.
/// </para>
/// <para>
/// SCHEMA NOTE (ADR-002): the DNN <c>4.9.0.85</c> schema is mapped UNCHANGED and no new tables may be
/// introduced (AAP §0.2.2 / §0.7.1), so this Phase-1 implementation keeps the family state in process
/// memory (<c>Infrastructure/Identity/InMemoryRefreshTokenStore</c>, registered as a singleton). The
/// abstraction is deliberately persistence-agnostic: a multi-instance deployment swaps the in-memory
/// implementation for a shared store (e.g. Redis or a dedicated refresh-token table outside the preserved
/// DNN schema) without touching the Application layer. Documented in root MIGRATION_NOTES.md.
/// </para>
/// <para>
/// Implementations MUST be thread-safe: a singleton instance is shared across all concurrent requests.
/// </para>
/// </remarks>
public interface IRefreshTokenStore
{
    /// <summary>
    /// Records the initial (current) refresh token for a newly-established family. Called at login when a
    /// fresh family is minted. Overwrites any existing state for <paramref name="tokenFamily"/> (families are
    /// globally-unique GUIDs, so this only ever initializes a brand-new family).
    /// </summary>
    /// <param name="tokenFamily">The unique family identifier carried in the refresh token's <c>token_family</c> claim.</param>
    /// <param name="tokenId">The current token id carried in the refresh token's <c>jti</c> claim.</param>
    /// <param name="userId">The owning user's id (used by <see cref="RevokeAllForUser"/> at logout).</param>
    /// <param name="expiresUtc">Absolute UTC expiry of the refresh token (used for opportunistic pruning).</param>
    void Register(string tokenFamily, string tokenId, int userId, DateTime expiresUtc);

    /// <summary>
    /// Atomically validates and rotates a presented refresh token within its family.
    /// </summary>
    /// <param name="tokenFamily">The family identifier from the presented token's <c>token_family</c> claim.</param>
    /// <param name="presentedTokenId">The <c>jti</c> of the presented (incoming) refresh token.</param>
    /// <param name="newTokenId">The <c>jti</c> of the replacement refresh token to record as the new current id.</param>
    /// <param name="newExpiresUtc">Absolute UTC expiry of the replacement refresh token.</param>
    /// <returns>
    /// <see langword="true"/> when the presented token is the family's current token and rotation succeeded
    /// (the family now tracks <paramref name="newTokenId"/>); otherwise <see langword="false"/>. A
    /// <see langword="false"/> result means the token is unusable because the family is unknown/revoked, the
    /// stored entry has expired, or — critically — a <b>superseded</b> token id was presented, which is
    /// treated as a replay and causes the WHOLE family to be revoked.
    /// </returns>
    bool TryRotate(string tokenFamily, string presentedTokenId, string newTokenId, DateTime newExpiresUtc);

    /// <summary>Revokes a single refresh-token family, immediately invalidating its current token.</summary>
    /// <param name="tokenFamily">The family identifier to revoke. Unknown families are ignored.</param>
    void RevokeFamily(string tokenFamily);

    /// <summary>
    /// Revokes every refresh-token family owned by the user. Called at logout so the user's refresh tokens
    /// can no longer be rotated (the access token still expires naturally within its short lifetime).
    /// </summary>
    /// <param name="userId">The owning user's id whose families should be revoked.</param>
    void RevokeAllForUser(int userId);
}
