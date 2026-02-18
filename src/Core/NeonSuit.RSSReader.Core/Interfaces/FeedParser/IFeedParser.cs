using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.FeedParser
{
    /// <summary>
    /// Defines the contract for parsing RSS, Atom, and compatible syndication feeds.
    /// Responsible for fetching, parsing, and mapping feed metadata and articles 
    /// into domain models usable by the application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations of this interface handle:
    /// </para>
    /// <list type="bullet">
    ///     <item>HTTP fetching of the feed content (with proper headers, timeouts, and redirects)</item>
    ///     <item>Format detection and parsing (RSS 0.9x/1.0/2.0, Atom 1.0, JSON Feed, RDF, etc.)</item>
    ///     <item>Normalization of common fields (title, description, publication dates, enclosures, authors)</item>
    ///     <item>Duplicate detection avoidance (via GUID/link hashing)</item>
    ///     <item>Basic content sanitization (HTML stripping or safe rendering)</item>
    /// </list>
    ///
    /// <para>
    /// Key behavioral expectations:
    /// </para>
    /// <list type="bullet">
    ///     <item>Methods are async and cancellation-aware — support timeouts and user cancellation during fetch/parse</item>
    ///     <item>Throw meaningful exceptions (HttpRequestException, FeedFormatException, etc.) on failure</item>
    ///     <item>Do **not** persist data — persistence is handled by higher layers (services/repositories)</item>
    ///     <item>Return normalized domain models (<see cref="Feed"/> and <see cref="Article"/>) ready for storage</item>
    ///     <item>Handle relative URLs correctly (resolve against feed base URI)</item>
    /// </list>
    ///
    /// <para>
    /// Usage patterns:
    /// </para>
    /// <list type="bullet">
    ///     <item><see cref="ParseFeedAsync"/> — used on first subscription or full refresh (metadata + articles)</item>
    ///     <item><see cref="ParseArticlesAsync"/> — used on subsequent incremental refreshes (only new/changed articles)</item>
    /// </list>
    ///
    /// <para>
    /// Recommendations for implementers:
    /// </para>
    /// <list type="bullet">
    ///     <item>Use libraries like SyndicationFeed (System.ServiceModel.Syndication) or CodeHollow.FeedReader for robust parsing</item>
    ///     <item>Support common extensions (Media RSS, iTunes podcast, Dublin Core, Content:encoded)</item>
    ///     <item>Implement fallback strategies for malformed feeds (best-effort parsing)</item>
    ///     <item>Log parsing warnings/errors (e.g., missing GUIDs, invalid dates) at the caller level</item>
    ///     <item>Consider adding overloads with last-modified/ETag support for conditional fetching in future</item>
    /// </list>
    /// </remarks>
    public interface IFeedParser
    {
        /// <summary>
        /// Fetches and parses the complete feed from the given URL, returning both the feed metadata 
        /// and all currently available articles in a single operation.
        /// </summary>
        /// <param name="url">The absolute URL of the feed (RSS, Atom, JSON Feed, etc.).</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><see cref="Feed"/> — normalized feed metadata (title, description, link, icon, last updated, etc.)</item>
        ///     <item><see cref="List{Article}"/> — list of parsed articles (with assigned temporary IDs or GUIDs)</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>Use this method when:</para>
        /// <list type="bullet">
        ///     <item>Subscribing to a new feed (initial import)</item>
        ///     <item>Performing a full manual refresh</item>
        ///     <item>Validating a feed URL before saving</item>
        /// </list>
        ///
        /// <para>Behavior notes:</para>
        /// <list type="bullet">
        ///     <item>Articles are returned in reverse-chronological order (newest first) if possible</item>
        ///     <item>Feed.LastUpdated should reflect the most recent article date or feed-level pubDate/updated</item>
        ///     <item>May throw <see cref="HttpRequestException"/>, <see cref="TaskCanceledException"/>, or custom feed parsing exceptions</item>
        /// </list>
        /// </remarks>
        Task<(Feed feed, List<Article> articles)> ParseFeedAsync(string url);

        /// <summary>
        /// Fetches and parses only the articles from the given feed URL, assigning them to the specified feed ID.
        /// Intended for incremental/delta updates after initial subscription.
        /// </summary>
        /// <param name="url">The absolute URL of the feed to fetch articles from.</param>
        /// <param name="feedId">The persistent ID of the feed in the database — assigned to each returned article.</param>
        /// <returns>
        /// A list of newly parsed or updated <see cref="Article"/> entities, ready for insertion or upsert.
        /// </returns>
        /// <remarks>
        /// <para>Use this method when:</para>
        /// <list type="bullet">
        ///     <item>Performing periodic background refresh of an existing feed</item>
        ///     <item>Only needing article delta (without re-parsing feed metadata)</item>
        /// </list>
        ///
        /// <para>Behavior notes:</para>
        /// <list type="bullet">
        ///     <item>Every returned article must have <c>Article.FeedId = feedId</c></item>
        ///     <item>Should attempt to detect unchanged/old articles (via GUID, link, or hash) — implementations may return only new ones</item>
        ///     <item>Publication dates should be normalized (UTC, fallback to fetch time if missing)</item>
        ///     <item>Empty list return value is valid (no new articles since last check)</item>
        ///     <item>Exceptions mirror those of <see cref="ParseFeedAsync"/></item>
        /// </list>
        ///
        /// <para>Recommendation: Higher layers should compare returned articles against stored ones using GUID or link.</para>
        /// </remarks>
        Task<List<Article>> ParseArticlesAsync(string url, int feedId);
    }
}