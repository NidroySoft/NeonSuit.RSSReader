namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Defines the comparison operators available for evaluating conditions in the RSS reader's rule/filter engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration specifies how a rule condition compares the value of a target field 
    /// (title, content, author, etc.) against the user-provided value or pattern.
    /// The operator determines the matching logic applied during article evaluation.
    /// </para>
    ///
    /// <para>
    /// Key considerations for operator selection and behavior:
    /// </para>
    /// <list type="bullet">
    ///     <item>String-based operators (<see cref="Contains"/>, <see cref="Equals"/>, etc.) are typically case-insensitive 
    ///           by default (configurable per rule or globally).</item>
    ///     <item>Numeric/date operators (<see cref="GreaterThan"/>, <see cref="LessThan"/>) are only valid when the target 
    ///           field is a date, numeric value, or can be parsed as such (e.g., publication date).</item>
    ///     <item><see cref="Regex"/> supports full regular expression patterns (PCRE or .NET flavor, depending on implementation).</item>
    ///     <item><see cref="IsEmpty"/> and <see cref="IsNotEmpty"/> ignore the comparison value and check field presence/content length.</item>
    ///     <item>Operators are evaluated per condition; multiple conditions in a rule use logical AND/OR grouping.</item>
    /// </list>
    ///
    /// <para>
    /// Business rules and usage recommendations:
    /// </para>
    /// <list type="bullet">
    ///     <item>Most common operators for keyword rules: <see cref="Contains"/> (broad) and <see cref="Regex"/> (precise).</item>
    ///     <item>Use <see cref="Equals"/> for exact matches (e.g., author name, specific category/tag).</item>
    ///     <item><see cref="StartsWith"/> and <see cref="EndsWith"/> are useful for domain-specific patterns 
    ///           (e.g., titles starting with "[Sponsored]", URLs ending in ".pdf").</item>
    ///     <item>Date-based operators (<see cref="GreaterThan"/>, <see cref="LessThan"/>) require the field to be parsed 
    ///           as DateTimeOffset or similar; invalid parsing should fail the condition gracefully.</item>
    ///     <item><see cref="NotContains"/> and <see cref="NotEquals"/> are useful for exclusion rules 
    ///           (e.g., "do not star articles containing 'ad' or 'promo'").</item>
    ///     <item><see cref="Between"/> and <see cref="NotBetween"/> are ideal for range-based filtering
    ///           (e.g., word count ranges, date ranges).</item>
    ///     <item>UI validation: Disable invalid operators for certain field types 
    ///           (e.g., hide date operators when target is Title/Content).</item>
    ///     <item>Performance note: <see cref="Regex"/> and full-text searches can be expensive on large datasets; 
    ///           use judiciously and prefer simpler operators when possible.</item>
    /// </list>
    /// </remarks>
    public enum RuleOperator
    {
        /// <summary>
        /// The target field contains the specified substring (case-insensitive by default).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Partial text match anywhere in the field.</item>
        ///     <item>Most frequently used operator for keyword-based rules.</item>
        ///     <item>Example: Title Contains "breaking" matches "Breaking News Today".</item>
        /// </list>
        /// </remarks>
        Contains = 0,

        /// <summary>
        /// The target field exactly matches the specified value (case-insensitive by default).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Full exact match required (no wildcards).</item>
        ///     <item>Ideal for precise filters (author name, exact tag, specific title).</item>
        /// </list>
        /// </remarks>
        Equals = 1,

        /// <summary>
        /// The target field begins with the specified prefix.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Checks only the start of the string.</item>
        ///     <item>Useful for patterns like titles starting with "[Update]", "[Video]", etc.</item>
        /// </list>
        /// </remarks>
        StartsWith = 2,

        /// <summary>
        /// The target field ends with the specified suffix.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Checks only the end of the string.</item>
        ///     <item>Common for file extensions, URL patterns, or title endings.</item>
        /// </list>
        /// </remarks>
        EndsWith = 3,

        /// <summary>
        /// The target field does NOT contain the specified substring.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Negation of <see cref="Contains"/>.</item>
        ///     <item>Useful for exclusion rules (e.g., ignore promotional content).</item>
        /// </list>
        /// </remarks>
        NotContains = 4,

        /// <summary>
        /// The target field is NOT exactly equal to the specified value.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Negation of <see cref="Equals"/>.</item>
        ///     <item>Helps exclude specific known values.</item>
        /// </list>
        /// </remarks>
        NotEquals = 5,

        /// <summary>
        /// The target field matches the provided regular expression pattern.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Full regex matching (case sensitivity depends on implementation/flags).</item>
        ///     <item>Most powerful but slowest and most error-prone operator.</item>
        ///     <item>Requires validation of regex syntax in UI.</item>
        ///     <item>Example: \b[A-Z]{3}-\d{4}\b for codes like ABC-1234.</item>
        /// </list>
        /// </remarks>
        Regex = 6,

        /// <summary>
        /// The target field (parsed as date or number) is greater than the specified value.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Used primarily for publication dates (e.g., newer than X days ago).</item>
        ///     <item>Requires field to be convertible to DateTime or numeric type.</item>
        ///     <item>Example: Published > "2025-01-01"</item>
        /// </list>
        /// </remarks>
        GreaterThan = 7,

        /// <summary>
        /// The target field (parsed as date or number) is less than the specified value.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Used for older content (e.g., older than 7 days).</item>
        ///     <item>Same parsing requirements as <see cref="GreaterThan"/>.</item>
        /// </list>
        /// </remarks>
        LessThan = 8,

        /// <summary>
        /// The target field is empty, null, or contains only whitespace.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Does not use comparison value.</item>
        ///     <item>Useful for detecting missing metadata (e.g., no author, empty content).</item>
        /// </list>
        /// </remarks>
        IsEmpty = 9,

        /// <summary>
        /// The target field is NOT empty (has content beyond whitespace).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Negation of <see cref="IsEmpty"/>.</item>
        ///     <item>Common for ensuring field presence before other conditions.</item>
        /// </list>
        /// </remarks>
        IsNotEmpty = 10,

        /// <summary>
        /// The target field value falls within the specified inclusive range (minimum to maximum).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Requires two values: minimum and maximum (inclusive).</item>
        ///     <item>Works with numeric values (article length, word count, etc.).</item>
        ///     <item>Works with date/time values (publication date, last updated, etc.).</item>
        ///     <item>For strings, uses lexical/alphabetical comparison (rarely used).</item>
        ///     <item>Example: WordCount Between 500 and 1000 matches articles with 500-1000 words.</item>
        ///     <item>Example: Published Between "2025-01-01" and "2025-01-31" matches January 2025 articles.</item>
        /// </list>
        /// <para>Implementation notes:</para>
        /// <list type="bullet">
        ///     <item>Both values are required; UI should enforce min ≤ max.</item>
        ///     <item>Parsing failures should fail the condition gracefully (returns false).</item>
        ///     <item>For dates, consider timezone handling (typically UTC).</item>
        ///     <item>Inclusive boundaries: value ≥ min AND value ≤ max.</item>
        /// </list>
        /// </remarks>
        Between = 11,

        /// <summary>
        /// The target field value falls outside the specified inclusive range (less than minimum OR greater than maximum).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Negation of <see cref="Between"/>.</item>
        ///     <item>Requires two values: minimum and maximum (inclusive).</item>
        ///     <item>Matches if value is less than minimum OR greater than maximum.</item>
        ///     <item>Useful for outliers, old articles, or extremely long content.</item>
        ///     <item>Example: WordCount NotBetween 100 and 1000 matches very short or very long articles.</item>
        /// </list>
        /// <para>Implementation notes:</para>
        /// <list type="bullet">
        ///     <item>Both values are required; UI should enforce min ≤ max.</item>
        ///     <item>Same parsing requirements as <see cref="Between"/>.</item>
        ///     <item>Edge case: If min == max, this matches everything except that exact value.</item>
        /// </list>
        /// </remarks>
        NotBetween = 12
    }
}