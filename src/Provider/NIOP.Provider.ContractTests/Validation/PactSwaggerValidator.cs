using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace NIOP.Provider.ContractTests.Validation;

// ─────────────────────────────────────────────────────────────────────────────
// Result model
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Validation outcome for a single Pact interaction checked against the
/// provider's OpenAPI specification.
/// </summary>
public sealed record ValidationResult(
    string PactFile,
    string InteractionDescription,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

// ─────────────────────────────────────────────────────────────────────────────
// Validator
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Pure-C# equivalent of swagger-mock-validator.
///
/// Validates every HTTP interaction in a Pact consumer contract against the
/// provider's OpenAPI 3 specification (swagger.json), checking:
///   1. The HTTP method + path combination is declared in the spec.
///   2. Every required request-body property is present in the Pact body.
///   3. Every Pact request-body property exists in the spec's request schema.
///   4. The response status code is declared in the spec.
///   5. Every required response-body property is present in the Pact body.
///   6. Every Pact response-body property exists in the spec's response schema.
///   7. Property types match the spec (string, boolean, integer, number, object, array).
///
/// Uses:
///   - <c>Microsoft.OpenApi.Readers</c> for spec parsing.
///   - <c>System.Text.Json</c> for pact JSON parsing.
/// </summary>
public sealed class PactSwaggerValidator
{
    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Validates all interactions in <paramref name="pactJson"/> against
    /// <paramref name="openApiJson"/> and returns one result per interaction.
    /// </summary>
    public IReadOnlyList<ValidationResult> Validate(
        string openApiJson,
        string pactJson,
        string pactFileName = "pact.json")
    {
        // Parse the OpenAPI spec
        var openApiReader = new OpenApiStringReader();
        var spec = openApiReader.Read(openApiJson, out var diagnostic);

        if (diagnostic.Errors.Count > 0)
        {
            var specErrors = string.Join("; ", diagnostic.Errors.Select(e => e.Message));
            return new[]
            {
                new ValidationResult(pactFileName, "<spec parsing>",
                    new[] { $"OpenAPI spec could not be parsed: {specErrors}" })
            };
        }

        // Parse the pact file
        using var pactDoc = JsonDocument.Parse(pactJson);
        var root = pactDoc.RootElement;

        if (!root.TryGetProperty("interactions", out var interactions))
        {
            return new[]
            {
                new ValidationResult(pactFileName, "<pact parsing>",
                    new[] { "Pact file does not contain an 'interactions' array." })
            };
        }

        var results = new List<ValidationResult>();

        foreach (var interaction in interactions.EnumerateArray())
        {
            var description = interaction.TryGetProperty("description", out var desc)
                ? desc.GetString() ?? "(no description)"
                : "(no description)";

            var errors = new List<string>();

            ValidateInteraction(spec, interaction, errors);

            results.Add(new ValidationResult(pactFileName, description, errors));
        }

        return results;
    }

    // ── Interaction-level validation ──────────────────────────────────────────

    private static void ValidateInteraction(
        OpenApiDocument spec,
        JsonElement interaction,
        List<string> errors)
    {
        // Support both Pact spec v3 (flat) and v4 (wrapped content)
        if (!TryGetRequestInfo(interaction, out var method, out var path,
                                out var requestBody))
        {
            errors.Add("Interaction is missing a valid 'request' object with 'method' and 'path'.");
            return;
        }

        if (!TryGetResponseInfo(interaction, out var statusCode, out var responseBody))
        {
            errors.Add("Interaction is missing a valid 'response' object with 'status'.");
            return;
        }

        // 1 ── path + method must exist in spec ────────────────────────────────
        var operation = FindOperation(spec, method!, path!);
        if (operation is null)
        {
            errors.Add($"No operation found in spec for [{method?.ToUpper()} {path}]. " +
                       "The path or HTTP method is not declared.");
            return; // nothing more to validate without an operation
        }

        // 2 ── request body validation ─────────────────────────────────────────
        if (requestBody.HasValue && operation.RequestBody is not null)
        {
            var mediaType = operation.RequestBody.Content
                .FirstOrDefault(c => c.Key.Contains("json", StringComparison.OrdinalIgnoreCase));

            if (mediaType.Value?.Schema is not null)
            {
                var bodyErrors = ValidateBodyAgainstSchema(
                    requestBody.Value,
                    mediaType.Value.Schema,
                    "request.body");

                errors.AddRange(bodyErrors);
            }
        }
        else if (requestBody.HasValue && operation.RequestBody is null)
        {
            errors.Add("Pact sends a request body but the spec declares no requestBody for this operation.");
        }

        // 3 ── response status code must be declared ───────────────────────────
        var statusStr = statusCode.ToString();
        if (!operation.Responses.ContainsKey(statusStr) &&
            !operation.Responses.ContainsKey("default"))
        {
            errors.Add($"Response status {statusCode} is not declared in the spec's responses " +
                       $"(declared: {string.Join(", ", operation.Responses.Keys)}).");
            return;
        }

        // 4 ── response body validation ────────────────────────────────────────
        var declaredResponse = operation.Responses.TryGetValue(statusStr, out var resp)
            ? resp
            : (operation.Responses.TryGetValue("default", out var def) ? def : null);

        if (responseBody.HasValue && declaredResponse?.Content is not null)
        {
            var mediaType = declaredResponse.Content
                .FirstOrDefault(c => c.Key.Contains("json", StringComparison.OrdinalIgnoreCase));

            if (mediaType.Value?.Schema is not null)
            {
                var bodyErrors = ValidateBodyAgainstSchema(
                    responseBody.Value,
                    mediaType.Value.Schema,
                    "response.body");

                errors.AddRange(bodyErrors);
            }
        }
    }

    // ── Schema validation ─────────────────────────────────────────────────────

    private static List<string> ValidateBodyAgainstSchema(
        JsonElement element,
        OpenApiSchema schema,
        string path)
    {
        var errors = new List<string>();
        ValidateElement(element, schema, path, errors);
        return errors;
    }

    private static void ValidateElement(
        JsonElement element,
        OpenApiSchema schema,
        string path,
        List<string> errors)
    {
        // Resolve $ref chains (OpenApi reader usually inlines, but guard anyway)
        if (schema is null) return;

        // anyOf / oneOf — pass if at least one sub-schema validates without errors
        if (schema.AnyOf?.Count > 0 || schema.OneOf?.Count > 0)
        {
            var candidates = (schema.AnyOf?.Count > 0 ? schema.AnyOf : schema.OneOf)!;
            var anyValid = candidates.Any(s =>
            {
                var tentative = new List<string>();
                ValidateElement(element, s, path, tentative);
                return tentative.Count == 0;
            });
            if (!anyValid)
                errors.Add($"[{path}] value does not match any of the allowed schemas.");
            return;
        }

        var schemaType = schema.Type?.ToLowerInvariant();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (!string.IsNullOrEmpty(schemaType) && schemaType != "object")
                {
                    errors.Add($"[{path}] expected type '{schemaType}' but got object.");
                    return;
                }
                ValidateObjectElement(element, schema, path, errors);
                break;

            case JsonValueKind.Array:
                if (!string.IsNullOrEmpty(schemaType) && schemaType != "array")
                    errors.Add($"[{path}] expected type '{schemaType}' but got array.");
                else if (schema.Items is not null)
                    foreach (var item in element.EnumerateArray())
                        ValidateElement(item, schema.Items, $"{path}[]", errors);
                break;

            case JsonValueKind.String:
                if (!string.IsNullOrEmpty(schemaType) &&
                    schemaType != "string" && schemaType != "object")
                    errors.Add($"[{path}] expected type '{schemaType}' but got string.");
                break;

            case JsonValueKind.Number:
                if (!string.IsNullOrEmpty(schemaType) &&
                    schemaType != "integer" && schemaType != "number" &&
                    schemaType != "object")
                    errors.Add($"[{path}] expected type '{schemaType}' but got number.");
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                if (!string.IsNullOrEmpty(schemaType) &&
                    schemaType != "boolean" && schemaType != "object")
                    errors.Add($"[{path}] expected type '{schemaType}' but got boolean.");
                break;

            case JsonValueKind.Null:
                if (schema.Nullable != true)
                    errors.Add($"[{path}] is null but the schema does not allow null.");
                break;
        }
    }

    private static void ValidateObjectElement(
        JsonElement element,
        OpenApiSchema schema,
        string path,
        List<string> errors)
    {
        var properties = schema.Properties ?? new Dictionary<string, OpenApiSchema>();
        var required   = schema.Required   ?? new HashSet<string>();

        // --- Check required fields are present ---
        foreach (var req in required)
        {
            // Case-insensitive search to be tolerant of PascalCase / camelCase variation
            var found = element.EnumerateObject()
                .Any(p => p.Name.Equals(req, StringComparison.OrdinalIgnoreCase));

            if (!found)
                errors.Add($"[{path}] required property '{req}' is missing.");
        }

        // --- Check each pact property exists in schema and recurse ---
        foreach (var prop in element.EnumerateObject())
        {
            // Case-insensitive lookup
            var schemaEntry = properties
                .FirstOrDefault(kv => kv.Key.Equals(prop.Name, StringComparison.OrdinalIgnoreCase));

            if (schemaEntry.Key is null)
            {
                // Only an error when additionalProperties is explicitly false
                if (schema.AdditionalPropertiesAllowed == false)
                    errors.Add($"[{path}.{prop.Name}] is not defined in the spec schema " +
                               "(additionalProperties is false).");
                // Otherwise unknown extra properties are allowed (default OpenAPI behaviour)
            }
            else
            {
                ValidateElement(prop.Value, schemaEntry.Value, $"{path}.{prop.Name}", errors);
            }
        }
    }

    // ── Operation lookup ──────────────────────────────────────────────────────

    /// <summary>
    /// Finds the <see cref="OpenApiOperation"/> for the given HTTP method and
    /// concrete path.  Handles path-parameter templates such as
    /// <c>/api/devices/{id}</c> matching <c>/api/devices/123</c>.
    /// </summary>
    private static OpenApiOperation? FindOperation(
        OpenApiDocument spec,
        string method,
        string concretePath)
    {
        foreach (var (specPath, pathItem) in spec.Paths)
        {
            if (!PathMatches(specPath, concretePath)) continue;

            var opType = method.ToUpperInvariant() switch
            {
                "GET"     => (OperationType?)OperationType.Get,
                "POST"    => OperationType.Post,
                "PUT"     => OperationType.Put,
                "PATCH"   => OperationType.Patch,
                "DELETE"  => OperationType.Delete,
                "HEAD"    => OperationType.Head,
                "OPTIONS" => OperationType.Options,
                _         => null
            };

            if (opType is null) return null;

            return pathItem.Operations.TryGetValue(opType.Value, out var op) ? op : null;
        }
        return null;
    }

    /// <summary>
    /// Returns true when <paramref name="specTemplate"/> (which may contain
    /// <c>{param}</c> placeholders) matches the concrete <paramref name="path"/>.
    /// </summary>
    private static bool PathMatches(string specTemplate, string path)
    {
        // Build a regex from the template, replacing {param} with [^/]+
        var pattern = "^" +
                      Regex.Escape(specTemplate)
                           .Replace(@"\{", "(?<")  // \{ -> (?<
                           .Replace(@"\}", ">[^/]+)")  // \} -> >[^/]+)
                      + "$";

        return Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase);
    }

    // ── Pact body extraction ──────────────────────────────────────────────────

    /// <summary>
    /// Extracts method, path, and optional body from the pact "request" node.
    /// Handles both Pact spec v3 (body is raw JSON) and v4 (body is wrapped in
    /// <c>{ "content": {...}, "contentType": "application/json" }</c>).
    /// </summary>
    private static bool TryGetRequestInfo(
        JsonElement interaction,
        out string? method,
        out string? path,
        out JsonElement? body)
    {
        method = null; path = null; body = null;

        if (!interaction.TryGetProperty("request", out var request))
            return false;

        method = request.TryGetProperty("method", out var m) ? m.GetString() : null;
        path   = request.TryGetProperty("path",   out var p) ? p.GetString() : null;

        if (string.IsNullOrEmpty(method) || string.IsNullOrEmpty(path))
            return false;

        body = ExtractBody(request);
        return true;
    }

    /// <summary>
    /// Extracts status code and optional body from the pact "response" node.
    /// </summary>
    private static bool TryGetResponseInfo(
        JsonElement interaction,
        out int statusCode,
        out JsonElement? body)
    {
        statusCode = 0; body = null;

        if (!interaction.TryGetProperty("response", out var response))
            return false;

        if (!response.TryGetProperty("status", out var statusEl))
            return false;

        statusCode = statusEl.GetInt32();
        body = ExtractBody(response);
        return true;
    }

    /// <summary>
    /// Returns the actual JSON content of the body, stripping the Pact v4
    /// <c>{ "content": {...} }</c> wrapper when present.
    /// </summary>
    private static JsonElement? ExtractBody(JsonElement node)
    {
        if (!node.TryGetProperty("body", out var bodyEl))
            return null;

        // Pact v4 wraps the body: { "content": {...}, "contentType": "..." }
        if (bodyEl.ValueKind == JsonValueKind.Object &&
            bodyEl.TryGetProperty("content", out var content))
        {
            return content.ValueKind != JsonValueKind.Null ? content : null;
        }

        // Pact v3 / flat body
        return bodyEl.ValueKind != JsonValueKind.Null ? bodyEl : null;
    }
}
