namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Specifies the target field(s) of an article where a rule condition (e.g., keyword match, 
    /// pattern search, or value comparison) is evaluated in the RSS reader's rule/filter engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration determines which part(s) of an article are inspected when applying 
    /// a rule condition. It is used in combination with operators (contains, equals, starts with, 
    /// regex match, etc.) and values to define precise matching criteria.
    /// </para>
    ///
    /// <para>
    /// Typical usage in rule definitions:
    /// </para>
    /// <list type="bullet">
    ///     <item>RuleCondition { Field = RuleFieldTarget.Title, Operator = Contains, Value = "urgent" }</item>
    ///     <item>RuleCondition { Field = RuleFieldTarget.AllFields, Operator = MatchesRegex, Value = @"\bimportant\b" }</item>
    /// </list>
    ///
    /// <para>
    /// Behavioral impact and system considerations:
    /// </para>
    /// <list type="bullet">
    ///     <item>Choosing a narrow target (<see cref="Title"/>, <see cref="Author"/>) improves performance 
    ///           and reduces false positives compared to broader targets (<see cref="AllFields"/>).</item>
    ///     <item><see cref="Categories"/> typically refers to the article's tags/labels or feed categories 
    ///           (exact semantics depend on whether tags are per-article or inherited from feed).</item>
    ///     <item><see cref="AllFields"/> and <see cref="AnyField"/> perform searches across multiple parts 
    ///           of the article, usually title + content (and sometimes author/description if indexed).</item>
    ///     <item>Full-text search targets may be case-insensitive and support stemming/tokenization 
    ///           depending on the underlying search implementation.</item>
    ///     <item>When using <see cref="AllFields"/> or <see cref="AnyField"/>, the rule engine may apply 
    ///           different logical operators internally (AND for AllFields, OR for AnyField).</item>
    /// </list>
    ///
    /// <para>
    /// Business rules and recommendations:
    /// </para>
    /// <list type="bullet">
    ///     <item>Default recommended target for keyword rules: <see cref="Title"/> (fastest, most relevant for headlines).</item>
    ///     <item>Use <see cref="Content"/> for deeper semantic matches (e.g., body text contains specific phrases).</item>
    ///     <item><see cref="AllFields"/> is convenient for broad keyword alerts but may increase false positives 
    ///           and slightly slow evaluation during bulk syncs.</item>
    ///     <item><see cref="AnyField"/> is useful when users want to catch a term anywhere prominent 
    ///           without requiring it in every field.</item>
    ///     <item>For performance-critical environments, discourage widespread use of <see cref="AllFields"/> 
    ///           and <see cref="AnyField"/> on very large article sets unless indexed properly.</item>
    ///     <item>When exposing in UI, label clearly: "Title only", "Article body", "Author", "Tags/Categories", 
    ///           "Title and content (all must match)", "Title or content (any match)".</item>
    /// </list>
    /// </remarks>
    public enum RuleFieldTarget
    {
        /// <summary>
        /// The condition is evaluated only against the article's title/headline.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Fastest evaluation — title is usually a short indexed field.</item>
        ///     <item>Most relevant for catching important topics from headlines.</item>
        ///     <item>Recommended default for keyword-based rules.</item>
        /// </list>
        /// </remarks>
        Title = 0,

        /// <summary>
        /// The condition is evaluated only against the article's main content/body text.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Checks full article text (or summary/excerpt if full content not available).</item>
        ///     <item>Slower than title-only but catches deeper context.</item>
        ///     <item>Useful for rules like "contains technical term" or "mentions specific company".</item>
        /// </list>
        /// </remarks>
        Content = 1,

        /// <summary>
        /// The condition is evaluated against the article's author name(s) or byline.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Matches single or multiple authors (comma-separated or array).</item>
        ///     <item>Common for author-specific rules (e.g., "from trusted journalist X").</item>
        ///     <item>Usually case-insensitive exact or partial match.</item>
        /// </list>
        /// </remarks>
        Author = 2,

        /// <summary>
        /// The condition is evaluated against the article's assigned categories, tags, or labels.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Checks per-article tags or inherited feed categories.</item>
        ///     <item>Typically supports "contains tag X" or "has any of [X, Y]".</item>
        ///     <item>Fast if tags are stored/indexed as arrays or sets.</item>
        /// </list>
        /// </remarks>
        Categories = 3,

        /// <summary>
        /// The condition must be true in **all** searched fields (usually title AND content).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Restrictive: term must appear in both title and content (or all targeted fields).</item>
        ///     <item>Reduces false positives significantly.</item>
        ///     <item>Internally applies logical AND across fields.</item>
        ///     <item>Higher performance cost than single-field targets.</item>
        /// </list>
        /// </remarks>
        AllFields = 4,

        /// <summary>
        /// The condition is satisfied if true in **any** of the searched fields (usually title OR content).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Inclusive: term found in title, content, or both.</item>
        ///     <item>Broadens matches — useful for general keyword discovery.</item>
        ///     <item>Internally applies logical OR across fields.</item>
        ///     <item>May increase false positives compared to narrower targets.</item>
        /// </list>
        /// </remarks>
        AnyField = 5
    }
}