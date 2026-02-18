using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeonSuit.RSSReader.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for validating JSON strings during rule processing, configuration loading,
    /// and data deserialization in the RSS reader application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These validation helpers are used primarily when:
    /// </para>
    /// <list type="bullet">
    ///     <item>Storing or loading complex rule parameters (e.g., list of feed IDs, tag arrays)</item>
    ///     <item>Validating user-provided JSON in custom filter conditions or action payloads</item>
    ///     <item>Ensuring configuration fields that store serialized data remain parseable</item>
    /// </list>
    ///
    /// <para>
    /// Key behavioral notes:
    /// </para>
    /// <list type="bullet">
    ///     <item>Null or whitespace-only input is treated as valid (represents "no value" or empty collection).</item>
    ///     <item>Validation is strict: even valid JSON that deserializes to null is rejected when expecting a value.</item>
    ///     <item>Exceptions thrown are <see cref="ArgumentException"/> with clear field context for easier debugging and user feedback.</item>
    ///     <item>Inner <see cref="JsonException"/> is preserved as inner exception for detailed diagnostics.</item>
    ///     <item>No logging is performed inside these methods — logging should be handled by the calling layer if needed.</item>
    /// </list>
    ///
    /// <para>
    /// Usage recommendations:
    /// </para>
    /// <list type="bullet">
    ///     <item>Call these methods early in rule/command validation pipelines to fail fast on malformed data.</item>
    ///     <item>When validating arrays/lists, prefer the <c>expectIntArray</c> overload for common cases (feed IDs, tag IDs).</item>
    ///     <item>For complex types, use the generic <see cref="EnsureValidJson{T}"/> overload.</item>
    ///     <item>Avoid using these helpers for very large JSON payloads — consider streaming validation instead.</item>
    /// </list>
    /// </remarks>
    public static class JsonValidationHelper
    {
        /// <summary>
        /// Validates that the provided string is either null/empty/whitespace or contains valid JSON.
        /// When <paramref name="expectIntArray"/> is true, also ensures it deserializes to a non-null integer array.
        /// </summary>
        /// <param name="json">The JSON string to validate (can be null or empty).</param>
        /// <param name="fieldName">
        /// The name of the field or property being validated — used in exception messages for traceability.
        /// </param>
        /// <param name="expectIntArray">
        /// If <c>true</c>, validates that the JSON is a non-null array of integers (common for ID lists).
        /// If <c>false</c>, only checks for syntactically valid JSON.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when the JSON is invalid, or when expecting an int array but the result is null or not an array.
        /// </exception>
        public static void EnsureValidJson(string? json, string fieldName, bool expectIntArray = false)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return; // Considered valid (represents empty/no value)
            }

            try
            {
                if (expectIntArray)
                {
                    // Use typed deserialization for stricter validation
                    var array = JsonSerializer.Deserialize<int[]>(json);
                    if (array == null)
                    {
                        throw new ArgumentException($"{fieldName} must be a non-null integer array", fieldName);
                    }
                }
                else
                {
                    // Lightweight parse – only checks syntax
                    using var document = JsonDocument.Parse(json);
                }
            }
            catch (JsonException ex)
            {
                throw new ArgumentException(
                    $"{fieldName} contains invalid JSON{(expectIntArray ? " (expected integer array)" : "")}",
                    fieldName,
                    ex);
            }
        }

        /// <summary>
        /// Validates that the provided string is either null/empty/whitespace or contains valid JSON
        /// that successfully deserializes to the expected type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The expected target type after deserialization (e.g., List&lt;string&gt;, CustomRulePayload).</typeparam>
        /// <param name="json">The JSON string to validate (can be null or empty).</param>
        /// <param name="fieldName">
        /// The name of the field or property being validated — used in exception messages.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when the JSON is invalid or deserializes to null when a value is expected.
        /// </exception>
        public static void EnsureValidJson<T>(string? json, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                var result = JsonSerializer.Deserialize<T>(json);
                if (result == null)
                {
                    throw new ArgumentException(
                        $"{fieldName} deserializes to null (expected non-null {typeof(T).Name})",
                        fieldName);
                }
            }
            catch (JsonException ex)
            {
                throw new ArgumentException(
                    $"{fieldName} contains invalid JSON for type {typeof(T).Name}",
                    fieldName,
                    ex);
            }
        }
    }
}