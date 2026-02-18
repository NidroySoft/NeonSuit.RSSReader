namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Specifies the logical operator used to combine multiple conditions or filters in queries,
    /// rules, or search expressions within the RSS reader application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration defines how multiple criteria are evaluated when building complex filters
    /// for articles, feeds, or search operations.
    /// </para>
    ///
    /// <para>
    /// Typical usage scenarios include:
    /// </para>
    /// <list type="bullet">
    ///     <item>Combining keyword/tag/category filters in article search or smart feeds</item>
    ///     <item>Defining inclusion/exclusion rules in custom feed views or notification rules</item>
    ///     <item>Building advanced filtering logic for dashboards, saved searches, or automation</item>
    ///     <item>Combining conditions in user-defined rules (e.g., "star articles that match ALL of these tags" vs "ANY")</item>
    /// </list>
    ///
    /// <para>
    /// Business rules and behavioral impact:
    /// </para>
    /// <list type="bullet">
    ///     <item><see cref="AND"/> requires **all** specified conditions to be true for the item to match (restrictive/narrowing).</item>
    ///     <item><see cref="OR"/> requires **at least one** condition to be true for the item to match (inclusive/broadening).</item>
    ///     <item>The operator is usually applied between conditions of the same group/level; nested groups may use different operators.</item>
    ///     <item>Default behavior in UI (when not explicitly chosen) is typically <see cref="OR"/> for keyword/tag searches 
    ///           (broader discovery) and <see cref="AND"/> for strict filtering rules.</item>
    ///     <item>Changing the operator can dramatically alter result sets — users should be warned when switching between AND/OR in rule editors.</item>
    ///     <item>In most implementations, this value is stored per-filter-group or per-rule to allow mixed logic (e.g., (A AND B) OR C).</item>
    /// </list>
    ///
    /// <para>
    /// Recommendation: When exposing this in UI, label clearly as "All of these" (AND) vs "Any of these" (OR)
    /// to improve user understanding and reduce configuration errors.
    /// </para>
    /// </remarks>
    public enum LogicalOperator
    {
        /// <summary>
        /// All conditions must be satisfied (conjunction).
        /// </summary>
        /// <remarks>
        /// <para>Effect:</para>
        /// <list type="bullet">
        ///     <item>Narrows down results significantly.</item>
        ///     <item>Common in precise filtering: "has tag X AND published after date Y AND contains keyword Z".</item>
        ///     <item>Higher performance cost in large datasets due to fewer early exits in evaluation.</item>
        /// </list>
        /// </remarks>
        AND = 0,

        /// <summary>
        /// At least one condition must be satisfied (disjunction).
        /// </summary>
        /// <remarks>
        /// <para>Effect:</para>
        /// <list type="bullet">
        ///     <item>Expands result sets (union of matches).</item>
        ///     <item>Common in discovery/search: "contains keyword A OR keyword B OR keyword C".</item>
        ///     <item>Usually faster evaluation as any true condition short-circuits the check.</item>
        /// </list>
        /// </remarks>
        OR = 1
    }
}