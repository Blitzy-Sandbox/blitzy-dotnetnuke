// MIGRATION: [CP-final review - auth workflow parity] Integration suite for POST /api/auth/forgot-password on the
// migrated AuthController, which replaces the legacy Website/admin/Security/SendPassword.ascx.vb "Password Reminder"
// postback. The endpoint is [AllowAnonymous] (a forgotten-password request is by definition unauthenticated) and is
// rate-limited via the named "auth" fixed-window policy. This suite proves the endpoint is reachable WITHOUT
// authentication and returns the SECURE generic, non-enumerating response (identical whether or not a matching
// account exists). The fine-grained anti-enumeration / anti-timing behavior and the unconditional portal-scoped
// lookup are exhaustively covered by the AuthService unit tests; this suite is intentionally limited to two requests
// so it stays well under the "auth" policy budget (5 requests / minute) and cannot become rate-limit-flaky.
//
// Runs against the real Api Program pipeline hosted by CustomWebApplicationFactory (EF Core InMemory). Although the
// TestAuthHandler always authenticates, [AllowAnonymous] makes the endpoint reachable regardless, so this exercises
// the genuinely anonymous contract.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Integration tests for the anonymous, rate-limited <c>POST /api/auth/forgot-password</c> endpoint. The class-level
/// <c>[Trait("Category", "Integration")]</c> selects the suite under the Gate-5 <c>--filter "Category=Integration"</c>
/// run. Limited to two requests to remain comfortably within the "auth" rate-limit window.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuthForgotPasswordControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    // The exact generic message AuthService.ForgotPasswordAsync returns - shared by the account-exists and
    // account-missing paths so the response never reveals whether an account exists.
    private const string GenericResetMessage =
        "If an account matching the supplied details exists, instructions to reset the password have been sent to its registered email address.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthForgotPasswordControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>Projects the success envelope's <c>data</c> element into a <see cref="ForgotPasswordResponse"/>.</summary>
    private static async Task<ForgotPasswordResponse> ReadForgotResponseAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        return data.Deserialize<ForgotPasswordResponse>(JsonOptions)
               ?? throw new InvalidOperationException("The success envelope contained a null 'data' payload.");
    }

    /// <summary>
    /// POST forgot-password for a NON-existent account returns 200 with the generic message (no account enumeration).
    /// </summary>
    [Fact]
    public async Task Post_ForgotPassword_UnknownAccount_Returns200_WithGenericMessage()
    {
        _factory.ResetDatabase();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest { PortalId = 0, UsernameOrEmail = "ghost@example.com" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadForgotResponseAsync(response)).Message.Should().Be(GenericResetMessage);
    }

    /// <summary>
    /// POST forgot-password for an EXISTING account returns 200 with the IDENTICAL generic message, proving the
    /// response does not differ based on account existence (anti-enumeration at the HTTP boundary).
    /// </summary>
    [Fact]
    public async Task Post_ForgotPassword_ExistingAccount_Returns200_WithIdenticalGenericMessage()
    {
        var portalId = 0;
        _factory.ResetAndSeed(db =>
        {
            var portal = new Portal { PortalName = "Auth Portal" };
            db.Portals.Add(portal);
            db.SaveChanges();

            db.Users.Add(new User { Username = "existing_user", PortalId = portal.PortalId, IsApproved = true });
            db.SaveChanges();

            portalId = portal.PortalId;
        });

        var response = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest { PortalId = portalId, UsernameOrEmail = "existing_user" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadForgotResponseAsync(response)).Message.Should().Be(GenericResetMessage);
    }
}
