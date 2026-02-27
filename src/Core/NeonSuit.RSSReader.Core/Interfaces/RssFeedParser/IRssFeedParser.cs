using NeonSuit.RSSReader.Core.Models;
using System.Xml;

namespace NeonSuit.RSSReader.Core.Interfaces.RssFeedParser;

/// <summary>
/// Defines the operations required to download, clean, and parse RSS/Atom feeds.
/// Provides methods for initial feed discovery and incremental article retrieval.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the RSS/Atom parsing logic from the rest of the application,
/// allowing for pluggable implementations (e.g., different feed parsers, syndication libraries,
/// or future enhancements like OPML support or full-text extraction).
/// </para>
/// <para>
/// Implementations must ensure:
/// - Robust HTTP handling (timeouts, redirects, compression, user-agent).
/// - Proper feed detection (RSS 1.0/2.0, Atom, RDF, JSON Feed fallback if possible).
/// - Sanitization of HTML content (XSS prevention, relative URLs to absolute).
/// - Duplicate detection (GUID, content hash, link).
/// - Efficient memory usage (streaming parsing, avoid loading entire feed in RAM).
/// - Cancellation support for long-running operations.
/// - Proper exception handling and logging of feed-specific errors.
/// </para>
/// </remarks>
public interface IRssFeedParser
{
    #region Full Feed Parsing

    /// <summary>
    /// Parses a complete RSS/Atom feed from the given URL, extracting both feed metadata and initial articles.
    /// </summary>
    /// <param name="url">The absolute URL of the RSS/Atom feed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A tuple containing the parsed <see cref="Feed"/> metadata and the list of initial <see cref="Article"/> items.</returns>
    /// <exception cref="ArgumentNullException">Thrown if url is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown if url is not a valid absolute URI.</exception>
    /// <exception cref="HttpRequestException">Thrown on network or HTTP errors.</exception>
    /// <exception cref="XmlException">Thrown on XML parsing failures.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    Task<(Feed feed, List<Article> articles)> ParseFeedAsync(string url, CancellationToken cancellationToken = default);

    #endregion

    #region Incremental Article Parsing

    /// <summary>
    /// Parses only new or updated articles from an existing feed URL.
    /// </summary>
    /// <param name="url">The absolute URL of the RSS/Atom feed.</param>
    /// <param name="feedId">The database ID of the existing feed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of new or updated <see cref="Article"/> items ready for insertion/update.</returns>
    /// <exception cref="ArgumentNullException">Thrown if url is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if feedId is less than or equal to 0.</exception>
    /// <exception cref="HttpRequestException">Thrown on network or HTTP errors.</exception>
    /// <exception cref="XmlException">Thrown on parsing failures.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    Task<List<Article>> ParseArticlesAsync(string url, int feedId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a clean, ad-free HTML "Reader View" from an article's content or summary.
    /// </summary>
    /// <param name="article">The article to generate reader view for.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A standalone HTML document suitable for loading in WebView2.</returns>
    /// <exception cref="ArgumentNullException">Thrown if article is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <remarks>
    /// <para>
    /// This method produces a standalone HTML document that:
    /// - Removes scripts, styles, ads, trackers, and unnecessary markup
    /// - Preserves readable structure (headings, paragraphs, images, links)
    /// - Uses simple, responsive CSS for clean presentation
    /// - Is fully offline-capable (no external resources)
    /// </para>
    /// </remarks>
    Task<string> GenerateReaderViewHtmlAsync(Article article, CancellationToken cancellationToken = default);

    #endregion
}