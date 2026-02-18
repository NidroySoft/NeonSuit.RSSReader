namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Defines the possible automated actions that can be executed when an article matches 
    /// the conditions of a user-defined rule or filter in the RSS reader application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration represents the actions available in the rule engine / smart filter system.
    /// Rules allow users to automate article processing based on criteria such as keywords, authors,
    /// publication date, feed source, content patterns, or other metadata.
    /// </para>
    ///
    /// <para>
    /// Each rule can trigger zero or more actions when an article matches. Actions are applied 
    /// immediately after ingestion/sync (or on first evaluation for existing articles).
    /// </para>
    ///
    /// <para>
    /// Key behavioral impacts and system considerations:
    /// </para>
    /// <list type="bullet">
    ///     <item>Actions modify article state (status, tags, category) or trigger side effects (notifications, sounds, deletion).</item>
    ///     <item>Multiple actions can be combined in one rule (e.g., MarkAsStarred + ApplyTags + SendNotification).</item>
    ///     <item>Order of execution is typically the order defined in the rule configuration.</item>
    ///     <item>Some actions are mutually exclusive or have precedence (e.g., DeleteArticle usually overrides others).</item>
    ///     <item>Actions are executed server-side (if cloud sync) or client-side, depending on architecture.</item>
    ///     <item>User rules should be evaluated efficiently to avoid performance impact during bulk syncs.</item>
    /// </list>
    ///
    /// <para>
    /// Business rules and recommendations:
    /// </para>
    /// <list type="bullet">
    ///     <item>Irreversible actions (<see cref="DeleteArticle"/>, <see cref="ArchiveArticle"/>) should require explicit user confirmation when creating/editing rules.</item>
    ///     <item><see cref="MarkAsFavorite"/> is included for backward compatibility or alternative naming; prefer <see cref="MarkAsStarred"/> in new implementations if they represent the same concept.</item>
    ///     <item><see cref="HighlightArticle"/> requires an associated color or style parameter (not stored in the enum value itself).</item>
    ///     <item>Actions like <see cref="PlaySound"/> and <see cref="SendNotification"/> should respect global notification settings and device state.</item>
    ///     <item>Default behavior: new articles start as Unread unless a rule changes their status.</item>
    /// </list>
    /// </remarks>
    public enum RuleActionType
    {
        /// <summary>
        /// Automatically marks the matching article as read.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Sets article status to <see cref="ArticleStatus.Read"/>.</item>
        ///     <item>Removes from unread counters and default "unread" views.</item>
        ///     <item>Common for low-priority or already-seen content (e.g., newsletters user skims).</item>
        /// </list>
        /// </remarks>
        MarkAsRead = 0,

        /// <summary>
        /// Forces the article to remain (or become) unread, even if previously viewed.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Sets or resets article status to <see cref="ArticleStatus.Unread"/>.</item>
        ///     <item>Useful for high-priority matches that should always demand attention.</item>
        /// </list>
        /// </remarks>
        MarkAsUnread = 1,

        /// <summary>
        /// Marks the article as starred/favorited for later reference.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Sets article status to <see cref="ArticleStatus.Starred"/> (or adds starred flag if compound states are supported).</item>
        ///     <item>Makes article appear in "Starred" / "Favorites" views.</item>
        ///     <item>Often exempt from auto-archive/cleanup policies.</item>
        /// </list>
        /// </remarks>
        MarkAsStarred = 2,

        /// <summary>
        /// Marks the article as a favorite (alternative naming for starred).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Functionally identical to <see cref="MarkAsStarred"/> in most implementations.</item>
        ///     <item>Retained for compatibility or user preference in naming.</item>
        ///     <item>Avoid using both in the same rule to prevent confusion.</item>
        /// </list>
        /// </remarks>
        MarkAsFavorite = 3,

        /// <summary>
        /// Applies one or more user-defined tags/labels to the article.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Adds specified tags to the article's tag collection.</item>
        ///     <item>Enables filtering, searching, and organization by tag.</item>
        ///     <item>Requires associated tag IDs/names as rule parameters.</item>
        /// </list>
        /// </remarks>
        ApplyTags = 4,

        /// <summary>
        /// Moves the article (or assigns it) to a specific user-defined category/folder.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Changes the article's category assignment.</item>
        ///     <item>Feeds may support per-article categories independent of feed-level folders.</item>
        ///     <item>Requires category ID as parameter.</item>
        /// </list>
        /// </remarks>
        MoveToCategory = 5,

        /// <summary>
        /// Triggers a user notification for the matching article.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Generates a notification (toast, banner, push) even if global article notifications are off.</item>
        ///     <item>Priority and type configurable per rule or globally.</item>
        ///     <item>Common for keyword alerts (e.g., "job opening", "breaking news").</item>
        /// </list>
        /// </remarks>
        SendNotification = 6,

        /// <summary>
        /// Plays a custom or predefined sound alert when the article matches.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Plays an audible alert (device-dependent).</item>
        ///     <item>Should respect mute/vibration settings.</item>
        ///     <item>Use sparingly to avoid annoyance.</item>
        /// </list>
        /// </remarks>
        PlaySound = 7,

        /// <summary>
        /// Permanently deletes the matching article from the user's library.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Removes article completely (usually irreversible without backup).</item>
        ///     <item>Should include user confirmation on rule creation.</item>
        ///     <item>Useful for spam, duplicates, or outdated content filters.</item>
        /// </list>
        /// </remarks>
        DeleteArticle = 8,

        /// <summary>
        /// Archives the matching article (moves to archive, hides from main views).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Sets article status to <see cref="ArticleStatus.Archived"/>.</item>
        ///     <item>Hides from active lists but keeps for search/restore.</item>
        ///     <item>Common for "processed" or low-value articles.</item>
        /// </list>
        /// </remarks>
        ArchiveArticle = 9,

        /// <summary>
        /// Applies a visual highlight (e.g., background color) to the article in lists/views.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Associates a specific color/style with the article.</item>
        ///     <item>Requires color parameter (e.g., hex code or predefined theme color ID).</item>
        ///     <item>Helps visually prioritize important matches (e.g., red for urgent, green for approved).</item>
        /// </list>
        /// </remarks>
        HighlightArticle = 10
    }
}