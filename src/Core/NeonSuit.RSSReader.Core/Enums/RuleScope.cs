namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Defines the scope of application for a user-defined rule or filter in the RSS reader.
    /// The scope determines which feeds or articles the rule conditions and actions are evaluated against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration controls the breadth of a rule's influence — from applying globally to all subscribed feeds 
    /// down to only the currently visible articles in the user's active view.
    /// It helps users organize rules efficiently and prevent unintended side effects on unrelated content.
    /// </para>
    ///
    /// <para>
    /// Behavioral impact and system usage:
    /// </para>
    /// <list type="bullet">
    ///     <item>The scope is evaluated at rule execution time (usually during article ingestion or on-demand re-evaluation).</item>
    ///     <item>Rules with narrower scopes (<see cref="CurrentView"/>) are typically faster and more targeted.</item>
    ///     <item>Broader scopes (<see cref="AllFeeds"/>) affect more articles but may increase processing time during bulk syncs.</item>
    ///     <item>Scope is independent of conditions — even if conditions match, the article must fall within the scope to apply actions.</item>
    ///     <item>UI should clearly indicate scope when creating/editing rules (e.g., dropdown: "Apply to all feeds", "Selected feeds only", etc.).</item>
    /// </list>
    ///
    /// <para>
    /// Business rules and recommendations:
    /// </para>
    /// <list type="bullet">
    ///     <item>Default scope for new rules: <see cref="AllFeeds"/> (broadest, most common for global behaviors like "star articles with keyword X").</item>
    ///     <item>Use <see cref="SpecificFeeds"/> or <see cref="SpecificCategories"/> for feed-specific automation 
    ///           (e.g., archive everything from "Promotions" feed, highlight posts from "Tech News" category).</item>
    ///     <item><see cref="CurrentView"/> is intended for temporary, context-aware rules 
    ///           (e.g., bulk mark-as-read in the current search/filter view, or apply tags to visible selection).</item>
    ///     <item>When scope is <see cref="SpecificFeeds"/> or <see cref="SpecificCategories"/>, 
    ///           the rule configuration must include a list of feed IDs or category IDs; otherwise, the rule is inactive.</item>
    ///     <item>Rules scoped to <see cref="CurrentView"/> should only be triggered via explicit user actions 
    ///           (e.g., "Apply rules to current view" button), not automatically on sync.</item>
    ///     <item>Performance note: Avoid combining very broad scopes with expensive conditions (e.g., regex on AllFields) 
    ///           unless the user base and dataset size justify it.</item>
    /// </list>
    /// </remarks>
    public enum RuleScope
    {
        /// <summary>
        /// The rule applies to articles from all subscribed feeds in the user's account.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Evaluated against every incoming or existing article, regardless of feed or category.</item>
        ///     <item>Most common scope for global behaviors (e.g., "star anything mentioning 'AI'").</item>
        ///     <item>Highest potential performance impact during large syncs.</item>
        /// </list>
        /// </remarks>
        AllFeeds = 0,

        /// <summary>
        /// The rule applies only to articles from a specific list of selected feeds.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Requires associated feed IDs in the rule definition.</item>
        ///     <item>Ideal for feed-specific automation (e.g., auto-archive newsletters, notify on job board updates).</item>
        ///     <item>More efficient than <see cref="AllFeeds"/> when targeting few feeds.</item>
        /// </list>
        /// </remarks>
        SpecificFeeds = 1,

        /// <summary>
        /// The rule applies only to articles belonging to one or more selected categories/folders.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Requires associated category IDs in the rule definition.</item>
        ///     <item>Useful when feeds are grouped logically (e.g., "Work", "Hobbies", "News") and rules should respect that grouping.</item>
        ///     <item>Allows inheritance: articles inherit category from their parent feed if per-article categories are not used.</item>
        /// </list>
        /// </remarks>
        SpecificCategories = 2,

        /// <summary>
        /// The rule applies only to articles that are currently visible in the user's active view 
        /// (based on current filters, search, selected feed/category, unread status, etc.).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Evaluated against the current filtered/selected set of articles at the moment of rule execution.</item>
        ///     <item>Not applied automatically during background sync — typically triggered by explicit user action 
        ///           (e.g., "Apply rules to this view", bulk operations).</item>
        ///     <item>Fastest scope — only a small subset of articles is considered.</item>
        ///     <item>Common for temporary/ad-hoc actions (bulk tagging visible results, mark-as-read current page).</item>
        ///     <item>May behave inconsistently if view changes rapidly; UI should warn or lock scope during application.</item>
        /// </list>
        /// </remarks>
        CurrentView = 3
    }
}