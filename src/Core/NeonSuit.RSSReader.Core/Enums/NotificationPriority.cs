namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Defines the urgency level and presentation behavior of notifications in the RSS reader application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration determines how a notification is delivered to the user, including sound, 
    /// vibration, persistence, screen wake behavior, and whether user interaction is mandatory.
    /// The priority level is assigned when the notification is created, based on the nature 
    /// and importance of the triggering event.
    /// </para>
    ///
    /// <para>
    /// Priority influences the following aspects:
    /// </para>
    /// <list type="bullet">
    ///     <item>Sound and vibration patterns (silent, single alert, repeating, or escalating)</item>
    ///     <item>Notification display style (transient toast vs persistent banner/heads-up)</item>
    ///     <item>Ability to bypass Do Not Disturb / Focus modes (platform-dependent)</item>
    ///     <item>Positioning in the notification shade/center (higher priorities appear first)</item>
    ///     <item>Requirement for explicit user acknowledgment or action</item>
    /// </list>
    ///
    /// <para>
    /// Important business rules and guidelines:
    /// </para>
    /// <list type="bullet">
    ///     <item>Use <see cref="Low"/> and <see cref="Normal"/> for the vast majority of events to prevent notification fatigue.</item>
    ///     <item><see cref="High"/> and especially <see cref="Critical"/> should be reserved for genuinely urgent situations 
    ///           that demand immediate user attention.</item>
    ///     <item>User preferences may override or cap notification priority per category or globally.</item>
    ///     <item>On Android, each priority typically maps to a distinct notification channel with its own sound/vibration settings.</item>
    ///     <item>On iOS/macOS, priority affects interruption level and banner style (temporary vs persistent).</item>
    ///     <item>Default priority for new article notifications: <see cref="Normal"/></item>
    /// </list>
    /// </remarks>
    public enum NotificationPriority
    {
        /// <summary>
        /// Lowest urgency: completely silent and non-intrusive.
        /// </summary>
        /// <remarks>
        /// <para>Typical scenarios:</para>
        /// <list type="bullet">
        ///     <item>Background sync completed without issues</item>
        ///     <item>Minor status updates or batch summaries</item>
        ///     <item>Low-priority informational messages</item>
        /// </list>
        ///
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>No sound, no vibration, no screen wake</item>
        ///     <item>Appears as a short-lived toast or collapsed notification</item>
        ///     <item>Stays in notification history but does not demand attention</item>
        ///     <item>Fully respects quiet hours and focus modes</item>
        /// </list>
        /// </remarks>
        Low = 0,

        /// <summary>
        /// Standard urgency: uses a single, non-disruptive alert.
        /// </summary>
        /// <remarks>
        /// <para>Typical scenarios:</para>
        /// <list type="bullet">
        ///     <item>New articles from regular feeds (when notifications enabled)</item>
        ///     <item>Feed successfully recovered from warning/error state</item>
        ///     <item>Daily/weekly unread digest summaries</item>
        /// </list>
        ///
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Plays default notification sound once</item>
        ///     <item>Shows as a standard toast/banner; persists in notification center</item>
        ///     <item>May briefly wake the screen (platform-dependent)</item>
        ///     <item>Most common priority for routine application events</item>
        /// </list>
        /// </remarks>
        Normal = 1,

        /// <summary>
        /// Elevated urgency: uses persistent or repeating alerts to gain attention.
        /// </summary>
        /// <remarks>
        /// <para>Typical scenarios:</para>
        /// <list type="bullet">
        ///     <item>New articles from high-priority/starred feeds or matching critical filters</item>
        ///     <item>Prolonged feed outage on important sources</item>
        ///     <item>Significant application events (e.g., major update available)</item>
        /// </list>
        ///
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Plays persistent/repeating sound or vibration pattern</item>
        ///     <item>Shows as a prominent, longer-lasting banner or heads-up notification</item>
        ///     <item>Higher likelihood of bypassing quiet hours (with user permission)</item>
        ///     <item>Appears near the top of notification lists</item>
        /// </list>
        /// </remarks>
        High = 2,

        /// <summary>
        /// Highest urgency: demands immediate user attention and action.
        /// </summary>
        /// <remarks>
        /// <para>Typical scenarios:</para>
        /// <list type="bullet">
        ///     <item>Authentication expired — login required to continue syncing</item>
        ///     <item>Critical storage or database capacity warning (risk of data loss)</item>
        ///     <item>Security/privacy alert (e.g., potential malicious feed content detected)</item>
        ///     <item>Complete sync failure preventing any updates for extended period</item>
        /// </list>
        ///
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Strong, repeating alert sound/vibration</item>
        ///     <item>Persistent full-screen or heads-up display requiring interaction</item>
        ///     <item>Usually bypasses Do Not Disturb / Focus modes (subject to OS rules)</item>
        ///     <item>Often includes mandatory action buttons (e.g., "Fix Now", "Retry", "Login")</item>
        ///     <item>Use extremely sparingly to preserve user trust in notifications</item>
        /// </list>
        /// </remarks>
        Critical = 3
    }
}