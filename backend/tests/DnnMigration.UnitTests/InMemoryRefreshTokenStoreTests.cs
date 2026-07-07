// -----------------------------------------------------------------------------
//  InMemoryRefreshTokenStoreTests.cs
//
//  xUnit unit tests for DnnMigration.Infrastructure.Identity.InMemoryRefreshTokenStore,
//  the server-side refresh-token store that backs the F2 fix (refresh-token
//  validation / rotation / revocation). The store is a real, in-memory
//  implementation — no mocking — configured through Options.Create<JwtSettings>.
//
//  These tests pin the behaviours AuthService relies on:
//    * a stored token validates back to its owning user id (validation by LOOKUP,
//      not by parsing the opaque token as a JWT);
//    * an unknown / empty token never validates;
//    * a revoked token no longer validates (single-token rotation);
//    * RevokeAll removes every token for one user while leaving other users' tokens
//      intact (logout);
//    * revocation is idempotent (revoking an unknown token is a harmless no-op);
//    * a non-positive configured lifetime degrades gracefully to the default rather
//      than expiring every token immediately.
//
//  Real-time expiry (the multi-day TTL) is intentionally not exercised here: the
//  store reads the wall clock directly (no injected clock, to keep it minimal), so
//  a deterministic time-travel test is out of scope for this boundary. The TTL
//  arithmetic is simple and verified by inspection.
// -----------------------------------------------------------------------------

using DnnMigration.Application.Interfaces;
using DnnMigration.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Unit tests for <see cref="InMemoryRefreshTokenStore"/> covering store/validate round-trips, single
/// and bulk revocation, idempotency, and the non-positive-lifetime guard.
/// </summary>
public class InMemoryRefreshTokenStoreTests
{
    /// <summary>
    /// Builds a store with the given refresh-token lifetime (default 7 days), wiring the required
    /// <see cref="JwtSettings"/> through the Options pattern with a valid signing key.
    /// </summary>
    private static InMemoryRefreshTokenStore CreateStore(int lifetimeDays = 7)
        => new(Options.Create(new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SecretKey = "0123456789ABCDEF0123456789ABCDEF", // 32 bytes; unused by the store but part of the POCO
            RefreshTokenExpirationDays = lifetimeDays
        }));

    [Fact]
    public async Task StoreAsync_ThenValidateAsync_ReturnsOwningUserId()
    {
        var store = CreateStore();
        await store.StoreAsync(42, "token-a");

        var userId = await store.ValidateAsync("token-a");

        userId.Should().Be(42, "a stored token must validate back to the user it was issued for");
    }

    [Fact]
    public async Task ValidateAsync_UnknownToken_ReturnsNull()
    {
        var store = CreateStore();

        var userId = await store.ValidateAsync("never-stored");

        userId.Should().BeNull("a token that was never stored must not validate");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ValidateAsync_NullOrEmptyToken_ReturnsNull(string? token)
    {
        var store = CreateStore();

        var userId = await store.ValidateAsync(token!);

        userId.Should().BeNull("an empty/null token can never identify a user");
    }

    [Fact]
    public async Task RevokeAsync_MakesTokenNoLongerValid()
    {
        var store = CreateStore();
        await store.StoreAsync(1, "token-b");

        await store.RevokeAsync("token-b");

        (await store.ValidateAsync("token-b")).Should().BeNull("a revoked token must not validate");
    }

    [Fact]
    public async Task RevokeAsync_UnknownToken_DoesNotThrow()
    {
        var store = CreateStore();

        var act = async () => await store.RevokeAsync("not-present");

        await act.Should().NotThrowAsync("revoking an unknown token is an idempotent no-op");
    }

    [Fact]
    public async Task StoreAsync_TwoTokensForSameUser_BothValidateIndependently()
    {
        var store = CreateStore();
        await store.StoreAsync(3, "first");
        await store.StoreAsync(3, "second");

        (await store.ValidateAsync("first")).Should().Be(3);
        (await store.ValidateAsync("second")).Should().Be(3);

        // Revoking one must not affect the other (single-token rotation, not bulk).
        await store.RevokeAsync("first");
        (await store.ValidateAsync("first")).Should().BeNull("only the revoked token is invalidated");
        (await store.ValidateAsync("second")).Should().Be(3, "the sibling token remains valid");
    }

    [Fact]
    public async Task RevokeAllAsync_RemovesOnlyTheTargetUsersTokens()
    {
        var store = CreateStore();
        await store.StoreAsync(5, "u5-a");
        await store.StoreAsync(5, "u5-b");
        await store.StoreAsync(9, "u9-a");

        await store.RevokeAllAsync(5);

        (await store.ValidateAsync("u5-a")).Should().BeNull("logout revokes all of user 5's tokens");
        (await store.ValidateAsync("u5-b")).Should().BeNull("logout revokes all of user 5's tokens");
        (await store.ValidateAsync("u9-a")).Should().Be(9, "another user's tokens must be untouched");
    }

    [Fact]
    public async Task RevokeAllAsync_ForUserWithNoTokens_DoesNotThrow()
    {
        var store = CreateStore();

        var act = async () => await store.RevokeAllAsync(1234);

        await act.Should().NotThrowAsync("revoking with no stored tokens is an idempotent no-op");
    }

    [Fact]
    public async Task StoreAsync_WithNonPositiveConfiguredLifetime_StillYieldsAValidatableToken()
    {
        // A misconfigured (<= 0) lifetime must degrade to the default rather than expiring every token
        // immediately, which would silently disable refresh entirely.
        var store = CreateStore(lifetimeDays: 0);
        await store.StoreAsync(8, "token-c");

        (await store.ValidateAsync("token-c")).Should().Be(8,
            "a non-positive lifetime falls back to the default rather than expiring the token on store");
    }

    [Fact]
    public void InMemoryRefreshTokenStore_ImplementsIRefreshTokenStore()
    {
        var store = CreateStore();

        store.Should().BeAssignableTo<IRefreshTokenStore>(
            "the store is the IRefreshTokenStore implementation registered (as a singleton) in the DI container");
    }
}
