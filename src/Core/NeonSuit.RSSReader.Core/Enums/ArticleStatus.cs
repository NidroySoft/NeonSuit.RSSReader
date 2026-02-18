namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Represents the user-facing status of an individual article within the RSS/Atom reader application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration tracks the primary lifecycle and user interaction states for each article.
    /// It is used across the application to control visibility, filtering, sorting, notification behavior,
    /// and UI presentation of articles in lists, reading views, and dashboards.
    /// </para>
    ///
    /// <para>
    /// ArticleStatus directly influences:
    /// </para>
    /// <list type="bullet">
    ///     <item>Which articles appear in the main "unread" or "all" feeds/timelines</item>
    ///     <item>Badge counters (e.g., number of unread articles per feed or globally)</item>
    ///     <item>Filtering options (e.g., "show only unread", "show starred", "show archived")</item>
    ///     <item>Sync priorities and cleanup policies (e.g., archived articles may be eligible for deletion after a retention period)</item>
    ///     <item>User actions like marking all as read, starring for later, or archiving to declutter</item>
    /// </list>
    ///
    /// <para>
    /// Status changes are almost always triggered by explicit user actions (via UI or API) rather than
    /// automatically by the system, except in rare cases such as bulk "mark all as read" operations.
    /// The status is persisted per-user per-article (typically in a database table linking user ID,
    /// article GUID, and status value).
    /// </para>
    ///
    /// <para>
    /// Important business rules and design notes:
    /// </para>
    /// <list type="bullet">
    ///     <item>Statuses are mutually exclusive — an article has exactly one status at any time.</item>
    ///     <item><see cref="Starred"/> is an orthogonal flag in many readers, but here it is modeled as a distinct primary status
    ///           (meaning a starred article is no longer considered Unread/Read in the default view unless explicitly shown).</item>
    ///     <item>Archiving is a user-initiated "cleanup" action; archived articles are hidden from main views by default
    ///           but remain searchable and restorable.</item>
    ///     <item>No automatic transitions occur (e.g., articles do not become Read just because time passed).</item>
    ///     <item>When a new article is first ingested from a feed, it always starts as <see cref="Unread"/>.</item>
    /// </list>
    /// </remarks>
    public enum ArticleStatus
    {
        /// <summary>
        /// The article is new and has not yet been opened or marked as read by the user.
        /// </summary>
        /// <remarks>
        /// <para>Behavior impact:</para>
        /// <list type="bullet">
        ///     <item>Appears prominently in "unread" views, timelines, and feed lists.</item>
        ///     <item>Contributes to unread counters/badges.</item>
        ///     <item>May trigger notifications (push/email) depending on user settings.</item>
        ///     <item>Default sorting often places Unread articles first (chronological descending within unread).</item>
        /// </list>
        ///
        /// <para>When assigned:</para>
        /// <list type="bullet">
        ///     <item>Immediately upon successful ingestion/sync of a new article from the feed.</item>
        ///     <item>Never assigned automatically after initial creation.</item>
        /// </list>
        ///
        /// <para>Typical transition: User opens/scrolls past the article → changes to <see cref="Read"/>.</para>
        /// </remarks>
        Unread = 0,

        /// <summary>
        /// The user has opened, viewed, or explicitly marked the article as having been read.
        /// </summary>
        /// <remarks>
        /// <para>Behavior impact:</para>
        /// <list type="bullet">
        ///     <item>Removed from default "unread" views and counters (unless user filters to show read items).</item>
        ///     <item>Still visible in "all articles" or chronological views.</item>
        ///     <item>No longer triggers new notifications.</item>
        ///     <item>Often grayed out or styled differently in lists to indicate completion.</item>
        /// </list>
        ///
        /// <para>When assigned:</para>
        /// <list type="bullet">
        ///     <item>User opens the article in detail view.</item>
        ///     <item>User uses bulk "mark as read" (up to current, all in feed, etc.).</item>
        ///     <item>Some clients auto-mark as read when article is visible on screen for X seconds (configurable).</item>
        /// </list>
        ///
        /// <para>Typical transitions: Back to <see cref="Unread"/> if user manually marks unread; to <see cref="Starred"/> or <see cref="Archived"/>.</para>
        /// </remarks>
        Read = 1,

        /// <summary>
        /// The user has explicitly marked the article as important, favorite, or "starred" for later reference.
        /// </summary>
        /// <remarks>
        /// <para>Behavior impact:</para>
        /// <list type="bullet">
        ///     <item>Appears in a dedicated "Starred"/"Favorites"/"Saved" section or filter.</item>
        ///     <item>Often exempt from auto-archiving or cleanup policies.</item>
        ///     <item>Highlighted visually (star icon, different background/color).</item>
        ///     <item>May persist longer in local storage or sync even if original feed removes it.</item>
        /// </list>
        ///
        /// <para>When assigned:</para>
        /// <list type="bullet">
        ///     <item>User clicks star/favorite button on an article (from any prior status: Unread, Read, or even Archived in some flows).</item>
        /// </list>
        ///
        /// <para>Business note: In this model, Starred replaces the previous status (e.g., a Read article becomes Starred, not Read+Starred).
        ///     If compound states are needed (e.g., read AND starred), consider a separate flags table instead of a single enum.</para>
        /// </remarks>
        Starred = 2,

        /// <summary>
        /// The user has intentionally removed the article from active/inbox views by archiving it.
        /// </summary>
        /// <remarks>
        /// <para>Behavior impact:</para>
        /// <list type="bullet">
        ///     <item>Hidden from main timelines, unread views, and per-feed lists by default.</item>
        ///     <item>Accessible only via "Archived" filter, search, or dedicated archive view.</item>
        ///     <item>Eligible for eventual deletion after a configurable retention period (to save storage).</item>
        ///     <item>Does not contribute to unread counters or clutter active feeds.</item>
        /// </list>
        ///
        /// <para>When assigned:</para>
        /// <list type="bullet">
        ///     <item>User clicks "Archive", "Done", or equivalent action.</item>
        ///     <item>Bulk archive operations (e.g., archive all read in a feed).</item>
        /// </list>
        ///
        /// <para>Typical transition: User can "unarchive"/"restore" back to Read or Unread.</para>
        ///
        /// <para>Recommendation: Treat Archived as a soft-delete — keep metadata and content for search/restore,
        ///     but exclude from most queries by default for performance and UX.</para>
        /// </remarks>
        Archived = 3
    }
}