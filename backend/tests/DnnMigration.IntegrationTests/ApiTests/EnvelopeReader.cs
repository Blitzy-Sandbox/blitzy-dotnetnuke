// MIGRATION: [QA-1 INFO-2 / AAP Gate 5] Shared helpers for the CRUD integration tests. The migrated API wraps every
// success body in the standard envelope { "data": {...}, "meta": {...} } (ApiControllerBase.Envelope, QA-1 F-G), so
// reading a server-generated key (portalId/userId/moduleId) means projecting into the "data" object. Centralized here
// so the per-resource CRUD tests stay focused on the Gate-5 status-code assertions.

using System.Text.Json;

namespace DnnMigration.IntegrationTests.ApiTests;

internal static class EnvelopeReader
{
    /// <summary>
    /// Web (camelCase) serializer options matching the API's JSON contract. Anonymous request bodies are serialized
    /// with these options so property names emit as camelCase (the API binds case-insensitively regardless).
    /// </summary>
    internal static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reads an integer property from the success envelope's <c>data</c> object (e.g. the server-generated id returned
    /// by a 201 Created response).
    /// </summary>
    internal static async Task<int> ReadDataIntAsync(HttpResponseMessage response, string propertyName)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("data").GetProperty(propertyName).GetInt32();
    }

    /// <summary>
    /// A minimal typed view of the RFC 7807 ProblemDetails error body the API emits for failures
    /// (<c>application/problem+json</c>: <c>{ type, title, status, detail, errors, correlationId, traceId }</c>).
    /// Only the members the CRUD integration tests assert on are projected; the remaining members are ignored.
    /// </summary>
    /// <param name="Type">The problem type URI (e.g. <c>urn:dnnmigration:error:not-found</c>).</param>
    /// <param name="Title">The status-aligned title (e.g. <c>Not Found</c>).</param>
    /// <param name="Status">The HTTP status code echoed in the body.</param>
    /// <param name="Detail">
    /// The human-readable failure message. For Result failures, <c>ApiControllerBase.Failure</c> joins
    /// <c>Result.Errors</c> into this field, so a single-error failure surfaces the service message verbatim here.
    /// </param>
    internal sealed record ProblemDetailView(string? Type, string? Title, int Status, string? Detail);

    /// <summary>
    /// Reads the RFC 7807 ProblemDetails body into a <see cref="ProblemDetailView"/> so a test can assert the
    /// EXACT <c>title</c>/<c>status</c>/<c>detail</c> the API emitted for a failure (the CP4-required not-found
    /// message assertion). The body is serialized camelCase (<c>ExceptionHandlingMiddleware.ProblemJsonOptions</c>
    /// / the built-in <c>ProblemDetails</c> JSON names), so the keys read here are <c>type/title/status/detail</c>.
    /// </summary>
    internal static async Task<ProblemDetailView> ReadProblemDetailAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        string? type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
        string? title = root.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
        int status = root.TryGetProperty("status", out var statusElement)
                     && statusElement.ValueKind == JsonValueKind.Number
            ? statusElement.GetInt32()
            : 0;
        string? detail = root.TryGetProperty("detail", out var detailElement) ? detailElement.GetString() : null;

        return new ProblemDetailView(type, title, status, detail);
    }
}
