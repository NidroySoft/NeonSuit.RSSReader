using System.Text.Json;

namespace NeonSuit.RSSReader.Core.Helpers;

/// <summary>
/// Provides helper methods for JSON validation in rule and configuration processing.
/// </summary>
public static class JsonValidationHelper
{
    /// <summary>
    /// Validates that a string contains valid JSON, optionally expecting an integer array.
    /// Throws ArgumentException if validation fails.
    /// </summary>
    /// <param name="json">The JSON string to validate.</param>
    /// <param name="fieldName">The name of the field being validated (used in exception).</param>
    /// <param name="expectIntArray">If true, validates that the JSON deserializes to an integer array.</param>
    /// <exception cref="ArgumentException">Thrown when JSON is invalid or doesn't meet expectations.</exception>
    public static void EnsureValidJson(string? json, string fieldName, bool expectIntArray = false)
    {
        if (string.IsNullOrWhiteSpace(json))
            return; // null or empty is considered valid (empty list)

        try
        {
            if (expectIntArray)
            {
                var array = JsonSerializer.Deserialize<int[]>(json);
                if (array == null)
                    throw new ArgumentException($"{fieldName} cannot be null array", fieldName);
            }
            else
            {
                // Only validate that it's valid JSON, regardless of type
                using var doc = JsonDocument.Parse(json);
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"{fieldName} contains invalid JSON", fieldName, ex);
        }
    }

    /// <summary>
    /// Generic version: validates that the JSON deserializes to the expected type.
    /// </summary>
    /// <typeparam name="T">The expected type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to validate.</param>
    /// <param name="fieldName">The name of the field being validated (used in exception).</param>
    /// <exception cref="ArgumentException">Thrown when JSON is invalid or deserializes to null.</exception>
    public static void EnsureValidJson<T>(string? json, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            var result = JsonSerializer.Deserialize<T>(json);
            if (result == null)
                throw new ArgumentException($"{fieldName} cannot be null", fieldName);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"{fieldName} contains invalid JSON for type {typeof(T).Name}", fieldName, ex);
        }
    }
}