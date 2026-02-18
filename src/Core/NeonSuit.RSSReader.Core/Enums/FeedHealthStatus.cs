namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Represents the health and operational status of an RSS/Atom feed, determined by the outcome
    /// of recent update/fetch attempts and overall accessibility.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration drives multiple critical behaviors across the application:
    /// </para>
    /// <list type="bullet">
    ///     <item>Whether the feed remains eligible for automatic background polling</item>
    ///     <item>Visual indicators in the user interface (colors, icons, status badges)</item>
    ///     <item>Retry frequency, exponential backoff, or circuit-breaker strategies during failures</item>
    ///     <item>Generation (or suppression) of user notifications and alerts</item>
    ///     <item>Filtering and sorting logic in feed lists, dashboards, and management views (e.g. "show only healthy feeds")</item>
    /// </list>
    ///
    /// <para>
    /// The status is automatically evaluated and updated by the feed synchronization service
    /// after each refresh attempt. It is not based solely on the most recent fetch, but rather
    /// considers a sliding window of recent attempts — typically the last 3 to 10 fetches
    /// (the exact window size and weighting logic are configurable in the refresh policy settings).
    /// </para>
    ///
    /// <para>
    /// Key business rules and invariants:
    /// </para>
    /// <list type="bullet">
    ///     <item>Only feeds in <see cref="Healthy"/> or <see cref="Warning"/> states are eligible
    ///           for regular automatic polling under normal conditions.</item>
    ///     <item>Feeds in <see cref="Error"/> or <see cref="Invalid"/> states trigger progressive
    ///           restrictions: longer intervals, exponential backoff, or complete temporary exclusion
    ///           from refresh queues.</item>
    ///     <item><see cref="Paused"/> is a user-controlled state that unconditionally blocks all
    ///           automatic and background refresh activity — the system must never transition
    ///           a Paused feed to another state without explicit user intervention via the UI.</item>
    ///     <item>Most state transitions are driven by observed fetch results, except for <see cref="Paused"/>,
    ///           which is exclusively set or cleared by direct user action.</item>
    /// </list>
    ///
    /// <para>
    /// These rules help balance reliability, resource usage, and user experience while preventing
    /// unnecessary network requests to broken or intentionally disabled feeds.
    /// </para>
    /// </remarks>
    public enum FeedHealthStatus
    {
        /// <summary>
        /// The feed is updating successfully with no significant issues detected in recent attempts.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Standard polling interval is used (default or user-defined).</item>
        ///     <item>No warning indicators are displayed in the UI.</item>
        ///     <item>Full eligibility for background refresh, article parsing, and notifications.</item>
        /// </list>
        ///
        /// <para>When assigned:</para>
        /// <list type="bullet">
        ///     <item>After multiple consecutive successful fetches.</item>
        ///     <item>Error rate in the recent window is zero or negligible.</item>
        /// </list>
        /// </remarks>
        Healthy = 0,

        /// <summary>
        /// The feed remains operational but exhibits intermittent problems (occasional failures,
        /// slow responses, partial content, rate limiting, transient HTTP errors, etc.).
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Polling frequency may be moderately reduced (configurable).</item>
        ///     <item>Subtle warning indicators appear in the UI (yellow/orange badge or icon).</item>
        ///     <item>Mild retry backoff may begin on individual failures.</item>
        ///     <item>Still fully eligible for notifications and content processing.</item>
        /// </list>
        ///
        /// <para>When assigned:</para>
        /// <list type="bullet">
        ///     <item>Recent error rate falls in the approximate 10–40% range (thresholds configurable).</item>
        ///     <item>Temporary issues at the feed provider (rate limits, unstable hosting, etc.).</item>
        /// </list>
        ///
        /// <para>Recommendation: Prolonged <see cref="Warning"/> states often precede degradation to <see cref="Error"/>.</para>
        /// </remarks>
        Warning = 1,

        /// <summary>
        /// The feed has encountered multiple consecutive failures or exhibits a high error rate in recent attempts.
        /// Automatic updates are significantly restricted.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Polling interval is greatly increased or switched to manual-only (configurable).</item>
        ///     <item>Prominent error indicators in UI (red icon, "Error" label).</item>
        ///     <item>Exponential backoff or circuit-breaker logic is active.</item>
        ///     <item>Notifications are usually suppressed until recovery is detected.</item>
        /// </list>
        ///
        /// <para>When assigned:</para>
        /// <list type="bullet">
        ///     <item>After 3–8 consecutive failures (exact threshold configurable).</item>
        ///     <item>Persistent issues: 4xx/5xx errors, timeouts, SSL problems, malformed XML, etc.</item>
        /// </list>
        /// </remarks>
        Error = 2,

        /// <summary>
        /// The feed has been manually paused or disabled by the user. No automatic refresh activity occurs.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Completely excluded from all refresh queues and background tasks.</item>
        ///     <item>No network requests are made until the user explicitly resumes the feed.</item>
        ///     <item>UI displays paused state (grayed out, pause icon).</item>
        ///     <item>Existing articles and metadata remain accessible in read-only mode.</item>
        /// </list>
        ///
        /// <para>When assigned:</para>
        /// <list type="bullet">
        ///     <item>User explicitly pauses the feed (e.g., too noisy, seasonal, temporary irrelevance).</item>
        ///     <item>Some import flows (e.g. OPML) may initialize feeds as Paused.</item>
        /// </list>
        ///
        /// <para>Important: System logic must never automatically resume a Paused feed.</para>
        /// </remarks>
        Paused = 3,

        /// <summary>
        /// The feed URL is invalid, permanently inaccessible, or points to content that cannot be processed as a valid syndication feed.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>No further automatic refresh attempts are scheduled.</item>
        ///     <item>Even manual refresh may be blocked or heavily warned against.</item>
        ///     <item>Strong error UI state ("Invalid", "Gone", red indicators).</item>
        ///     <item>Feed is typically hidden from active views by default (configurable).</item>
        /// </list>
        ///
        /// <para>When assigned:</para>
        /// <list type="bullet">
        ///     <item>Permanent HTTP errors (410 Gone, consistent 404).</item>
        ///     <item>DNS failures, invalid SSL certificates, wrong content type (HTML instead of XML).</item>
        ///     <item>Valid 200 response but content is not parseable as RSS/Atom/JSON Feed after retries.</item>
        /// </list>
        ///
        /// <para>Business note: <see cref="Invalid"/> is generally a terminal state — recovery requires user correction or deletion.</para>
        /// </remarks>
        Invalid = 4
    }
}