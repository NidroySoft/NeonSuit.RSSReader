using System.Text.RegularExpressions;

namespace NeonSuit.RSSReader.Core.Extensions;

/// <summary>
/// Provides extension methods for string manipulation, particularly useful for cleaning and processing RSS/Atom feed content.
/// </summary>
/// <remarks>
/// <para>
/// This static class contains utility extensions for common string operations in the RSS reader domain,
/// such as HTML tag removal for safe summary generation and content preview.
/// </para>
/// <para>
/// All methods are designed to be null-safe, performant, and suitable for processing potentially malformed
/// feed data from external sources.
/// </para>
/// <para>
/// Performance note: Methods use compiled regular expressions where applicable to minimize overhead
/// on repeated calls (e.g., during feed parsing).
/// </para>
/// </remarks>
public static class StringExtensions
{
    private static readonly Regex HtmlTagRegex = new Regex(
        @"<[^>]*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Removes all HTML tags from the input string and trims whitespace, producing a plain-text version.
    /// </summary>
    /// <param name="input">The input string potentially containing HTML markup (e.g., from RSS description or content).</param>
    /// <returns>A clean plain-text string with all HTML tags removed and normalized whitespace; never null.</returns>
    /// <remarks>
    /// <para>
    /// This method is optimized for RSS/Atom feed summaries and descriptions:
    /// - Strips all HTML tags using a compiled regex
    /// - Replaces common non-breaking spaces (&amp;nbsp;) with regular spaces
    /// - Normalizes line breaks to single spaces
    /// - Trims leading/trailing whitespace
    /// </para>
    /// <para>
    /// Safe for large inputs: does not load full DOM or use heavy HTML parsers.
    /// Sufficient for most feed use cases; does not attempt to preserve formatting or handle malformed HTML perfectly.
    /// </para>
    /// <para>
    /// Performance: Regex is compiled once at startup for fast repeated execution during feed sync.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// string dirty = "<p>Hello &amp;nbsp; world!<br /> &lt;script&gt;alert(1)&lt;/script&gt;</p>";
    /// string clean = dirty.StripHtml(); // Returns: "Hello  world! alert(1)"
    /// </code>
    /// </example>
    public static string StripHtml(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Remove all HTML tags
        var withoutTags = HtmlTagRegex.Replace(input, string.Empty);

        // Replace common HTML entities and normalize whitespace
        return withoutTags
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Trim();
    }
}