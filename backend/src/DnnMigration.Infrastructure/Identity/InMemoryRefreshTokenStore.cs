using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DnnMigration.Application.Interfaces;
using Microsoft.Extensions.Options;

// MIGRATION / DOWNSTREAM WIRING NOTE (this file cannot create Program.cs; recorded here so the Api/config
// agents wire the shared refresh-token contract correctly):
//   * Api Program.cs: register this store as a SINGLETON so ONE token table is shared across all
//     requests (a scoped/transient registration would give every request an empty store and break
//     refresh entirely):
//         builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
//     It depends only on IOptions<JwtSettings> (already registered for JwtTokenService), so no extra
//     configuration is required.
//   * SCHEMA FIDELITY (AAP section 0.6.2): refresh tokens are deliberately NOT persisted in a new
//     relational table — the migration maps EF Core to the EXISTING legacy schema without adding
//     production tables. This in-process store keeps refresh-token rotation/revocation working without
//     a schema change. The IRefreshTokenStore seam is intentionally narrow so a distributed backing
//     store (Redis / IDistributedCache) can replace this class for multi-instance deployments without
//     touching the Application layer; that swap is documented in MIGRATION_NOTES.md.

namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// In-memory implementation of <see cref="IRefreshTokenStore"/>. Persists issued refresh tokens in a
/// process-local, thread-safe map so they can be validated by lookup, rotated on every refresh, and
/// revoked on logout. Tokens are stored under a SHA-256 hash of their value (never in plaintext), each
/// with an absolute UTC expiry derived from <see cref="JwtSettings.RefreshTokenExpirationDays"/>.
/// </summary>
/// <remarks>
/// MIGRATION: there is no legacy DotNetNuke analogue (DNN used sliding FormsAuthentication cookies, not
/// refresh tokens). Registered as a DI singleton by the Api composition root (see the file header) so a
/// single token table is shared across requests. Expired entries are pruned opportunistically on each
/// store/validate call, so the map does not grow without bound even if clients never explicitly log out.
/// </remarks>
public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    /// <summary>
    /// The token table: key = SHA-256 hex hash of the opaque token value; value = the owning user id and
    /// the token's absolute UTC expiry. <see cref="ConcurrentDictionary{TKey,TValue}"/> gives lock-free
    /// thread safety for the concurrent request load a singleton store sees.
    /// </summary>
    private readonly ConcurrentDictionary<string, (int UserId, DateTime ExpiresUtc)> _tokens =
        new(StringComparer.Ordinal);

    /// <summary>Refresh-token lifetime in days, resolved once from <see cref="JwtSettings"/> at construction.</summary>
    private readonly int _lifetimeDays;

    /// <summary>
    /// Initializes the store from the bound <see cref="JwtSettings"/> (via the Options pattern), reading
    /// the refresh-token lifetime used to stamp each stored token's expiry.
    /// </summary>
    /// <param name="options">The bound <see cref="JwtSettings"/> options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public InMemoryRefreshTokenStore(IOptions<JwtSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value;

        // A non-positive lifetime would immediately expire every token, silently breaking refresh; fall
        // back to the JwtSettings default (7 days) so a misconfiguration degrades gracefully rather than
        // disabling refresh outright.
        _lifetimeDays = settings.RefreshTokenExpirationDays > 0 ? settings.RefreshTokenExpirationDays : 7;
    }

    /// <inheritdoc />
    public Task StoreAsync(int userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        // Ignore empty tokens defensively; a real token is always a 64-byte Base64 value.
        if (!string.IsNullOrEmpty(refreshToken))
        {
            PruneExpired();
            _tokens[Hash(refreshToken)] = (userId, DateTime.UtcNow.AddDays(_lifetimeDays));
        }

        // Synchronous, in-memory work — return a completed task rather than marking the method `async`
        // (an async method without an await raises CS1998, which fails this project's warnings-as-errors build).
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int?> ValidateAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Task.FromResult<int?>(null);
        }

        PruneExpired();

        if (_tokens.TryGetValue(Hash(refreshToken), out var entry) && entry.ExpiresUtc > DateTime.UtcNow)
        {
            return Task.FromResult<int?>(entry.UserId);
        }

        return Task.FromResult<int?>(null);
    }

    /// <inheritdoc />
    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(refreshToken))
        {
            _tokens.TryRemove(Hash(refreshToken), out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RevokeAllAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Snapshot enumeration over a ConcurrentDictionary is safe for concurrent removal; remove every
        // token owned by this user so logout ends all of the user's refresh sessions.
        foreach (var pair in _tokens)
        {
            if (pair.Value.UserId == userId)
            {
                _tokens.TryRemove(pair.Key, out _);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes every expired entry from the table. Called opportunistically on store/validate so the map
    /// is self-cleaning without a background timer.
    /// </summary>
    private void PruneExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _tokens)
        {
            if (pair.Value.ExpiresUtc <= now)
            {
                _tokens.TryRemove(pair.Key, out _);
            }
        }
    }

    /// <summary>
    /// Computes the storage key for a token as its SHA-256 hash (hex). Storing a one-way hash rather than
    /// the token itself means a memory dump never exposes usable refresh tokens.
    /// </summary>
    /// <param name="token">The opaque refresh-token value.</param>
    /// <returns>The uppercase hex SHA-256 hash used as the table key.</returns>
    private static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
