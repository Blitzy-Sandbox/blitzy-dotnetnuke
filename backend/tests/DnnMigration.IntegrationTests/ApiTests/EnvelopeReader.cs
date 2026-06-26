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
}
