using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Authorization;

/// <summary>
/// An <see cref="IAuthorizationMiddlewareResultHandler"/> that renders authorization failures as
/// RFC 7807 <c>application/problem+json</c> Problem Details responses, consistent with
/// <see cref="DnnMigration.Api.Middleware.ExceptionHandlingMiddleware"/>.
/// </summary>
/// <remarks>
/// <para>
/// By default the authorization middleware emits an empty-bodied 401 (challenge) or 403 (forbid). The API
/// contract (AAP §0.7.2) requires <em>every</em> error — including authorization failures — to use the RFC 7807
/// envelope, so this handler intercepts the challenged/forbidden results and writes a Problem Details payload
/// that is byte-for-byte shaped like the exception middleware's output (same <c>type</c> base URI, content type,
/// camelCase serialization and root <c>traceId</c>). The successful case is delegated unchanged to the framework
/// <see cref="AuthorizationMiddlewareResultHandler"/>, which invokes the rest of the pipeline.
/// </para>
/// <para>
/// MIGRATION: pairs with <see cref="PermissionAuthorizationHandler"/> to deliver the AAP §0.6.2 contract —
/// unauthenticated → 401, authenticated-but-unauthorized → 403, unknown permission key → 403.
/// </para>
/// </remarks>
public sealed class ProblemDetailsAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    // These mirror ExceptionHandlingMiddleware so authorization failures and exception failures are
    // indistinguishable to clients. Kept local (rather than coupling to the middleware's private constants)
    // to keep that middleware untouched; the values are asserted identical by the adjacent review.
    private const string ProblemJsonContentType = "application/problem+json";
    private const string ErrorTypeBaseUri = "https://dnnmigration.com/errors/";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { DictionaryKeyPolicy = JsonNamingPolicy.CamelCase };

    private static readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

    /// <summary>
    /// Handles the authorization result: writes a Problem Details response for a challenge (401) or forbid (403),
    /// otherwise defers to the default handler (which continues the pipeline).
    /// </summary>
    /// <param name="next">The next delegate in the request pipeline.</param>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="policy">The authorization policy that was evaluated.</param>
    /// <param name="authorizeResult">The outcome of the authorization evaluation.</param>
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        // Do not attempt to rewrite a response that has already begun streaming.
        if (!context.Response.HasStarted)
        {
            if (authorizeResult.Challenged)
            {
                // Authentication is required but absent/invalid → 401.
                context.Response.Headers.Append("WWW-Authenticate", "Bearer");
                await WriteProblemDetailsAsync(
                    context,
                    typeSlug: "unauthorized",
                    title: "Unauthorized",
                    status: StatusCodes.Status401Unauthorized,
                    detail: "Authentication is required to access this resource.");
                return;
            }

            if (authorizeResult.Forbidden)
            {
                // Authenticated but lacking the required permission (or an unknown permission key) → 403.
                await WriteProblemDetailsAsync(
                    context,
                    typeSlug: "forbidden",
                    title: "Forbidden",
                    status: StatusCodes.Status403Forbidden,
                    detail: "You do not have permission to perform this action.");
                return;
            }
        }

        // Success (or response already started): let the framework handler continue the pipeline.
        await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    /// <summary>
    /// Writes a single RFC 7807 Problem Details response with the shared envelope shape.
    /// </summary>
    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        string typeSlug,
        string title,
        int status,
        string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Type = ErrorTypeBaseUri + typeSlug,
            Title = title,
            Status = status,
            Detail = detail
        };

        // Surface the correlation id at the JSON root, matching ExceptionHandlingMiddleware.
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = status;
        context.Response.ContentType = ProblemJsonContentType;

        var payload = JsonSerializer.Serialize(problemDetails, SerializerOptions);
        await context.Response.WriteAsync(payload);
    }
}
