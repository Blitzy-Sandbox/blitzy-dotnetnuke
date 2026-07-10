namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Port for server-side persistence, validation, rotation, and revocation of opaque refresh tokens.
/// Consumed by <c>AuthService</c> to back the <c>/api/auth/refresh</c> and <c>/api/auth/logout</c>
/// flows, and implemented in <c>DnnMigration.Infrastructure.Identity</c> (dependency inversion keeps
/// the Application layer free of Infrastructure references).
/// </summary>
/// <remarks>
/// MIGRATION: there is no legacy DotNetNuke analogue — DNN relied on sliding
/// <c>FormsAuthentication</c> cookies (<c>PortalSecurity.SignOut</c>,
/// <c>Library/Components/Security/PortalSecurity.vb</c> L77) rather than refresh tokens. The opaque
/// refresh token itself is minted by <c>IJwtTokenService.GenerateRefreshToken</c> (a
/// cryptographically-random value, NOT a JWT), so it can only be validated by looking it up in this
/// store — never by parsing/validating it as a token. This port exists precisely so the refresh token
/// is treated as a lookup key rather than a self-describing credential.
/// <para>
/// SCHEMA FIDELITY (AAP §0.6.2): the migration maps EF Core entities to the EXISTING legacy schema and
/// does not add new production tables, so refresh tokens are NOT persisted in a new relational table.
/// The default implementation stores them in-process; the seam is deliberately narrow so a distributed
/// backing store (e.g. Redis / IDistributedCache) can replace it without touching the Application layer.
/// </para>
/// <para>
/// DOWNSTREAM WIRING NOTE (CP4 / Api composition root, recorded here because this layer cannot create
/// <c>Program.cs</c>): the concrete implementation MUST be registered as a SINGLETON —
/// <c>builder.Services.AddSingleton&lt;IRefreshTokenStore, InMemoryRefreshTokenStore&gt;();</c> — so a
/// single token table is shared across all requests. A scoped/transient registration would give each
/// request its own empty store and every refresh would fail.
/// </para>
/// </remarks>
public interface IRefreshTokenStore
{
    /// <summary>
    /// Persists a newly issued refresh token for the given user, stamping it with the configured
    /// refresh-token lifetime. The token is stored by a one-way hash of its value, never in plaintext.
    /// </summary>
    /// <param name="userId">The identifier of the user the token is issued to.</param>
    /// <param name="refreshToken">The opaque refresh-token value to persist.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task StoreAsync(int userId, string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a presented refresh token. Returns the associated user id when the token exists and has
    /// not expired, or <c>null</c> when the token is unknown, already revoked, or past its expiry.
    /// </summary>
    /// <param name="refreshToken">The opaque refresh-token value presented by the client.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The owning user id, or <c>null</c> if the token is not valid.</returns>
    Task<int?> ValidateAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a single refresh token so it can no longer be exchanged. Used to rotate the previous token
    /// out on every successful refresh. Revoking an unknown token is a no-op (idempotent).
    /// </summary>
    /// <param name="refreshToken">The opaque refresh-token value to revoke.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every refresh token currently held for a user. Used by logout to end all of the user's
    /// refresh sessions. Revoking when the user has no stored tokens is a no-op (idempotent).
    /// </summary>
    /// <param name="userId">The identifier of the user whose tokens should be revoked.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task RevokeAllAsync(int userId, CancellationToken cancellationToken = default);
}
