namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Specifies the delivery/presentation method for notifications in the RSS reader application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration defines how a notification is delivered to the user — whether visually, 
    /// audibly, both, or silently in the background — independent of its priority or content.
    /// It controls the immediate user experience at the moment of delivery.
    /// </para>
    ///
    /// <para>
    /// NotificationType affects:
    /// </para>
    /// <list type="bullet">
    ///     <item>Whether a visual element (toast, banner, popup) is shown on screen</item>
    ///     <item>Whether a sound alert is played (regardless of device volume/mute state)</item>
    ///     <item>Whether the notification is completely silent and only logged internally</item>
    ///     <item>How intrusive the notification feels to the user at the time it is triggered</item>
    /// </list>
    ///
    /// <para>
    /// Important business rules and usage guidelines:
    /// </para>
    /// <list type="bullet">
    ///     <item>The type is typically chosen based on the event's nature and user-configured preferences 
    ///           for specific notification categories (new articles, feed errors, sync status, etc.).</item>
    ///     <item><see cref="Silent"/> should be used for background/logging-only events that the user 
    ///           should never be interrupted for (e.g., successful background sync, minor housekeeping).</item>
    ///     <item><see cref="Sound"/> alone (without visual) is rare and usually discouraged — most platforms 
    ///           require a visual component when playing sound for accessibility and UX reasons.</item>
    ///     <item><see cref="Banner"/> is intended for more attention-grabbing visual presentation 
    ///           (larger, persistent, or heads-up style) compared to a standard <see cref="Toast"/>.</item>
    ///     <item>Final delivery may still be modulated by device settings (Do Not Disturb, notification 
    ///           channels, app-level mute, screen state), but <see cref="NotificationType"/> defines 
    ///           the application's intended delivery intent.</item>
    ///     <item>Default for most article-related notifications: <see cref="Both"/> or <see cref="Toast"/>
    ///           (depending on user preference for sound).</item>
    /// </list>
    /// </remarks>
    public enum NotificationType
    {
        /// <summary>
        /// Visual notification only (toast-style popup or entry in notification center).
        /// No sound is played.
        /// </summary>
        /// <remarks>
        /// <para>Typical use cases:</para>
        /// <list type="bullet">
        ///     <item>Low-to-normal priority new article notifications when user has disabled sounds</item>
        ///     <item>Informational messages that do not require immediate attention</item>
        /// </list>
        ///
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Shows a brief toast or persistent notification entry</item>
        ///     <item>No audible alert</item>
        ///     <item>Respects mute/quiet modes fully</item>
        /// </list>
        /// </remarks>
        Toast = 0,

        /// <summary>
        /// Audible alert only (sound/vibration), without any visual element on screen.
        /// </summary>
        /// <remarks>
        /// <para>Typical use cases:</para>
        /// <list type="bullet">
        ///     <item>Rare — mostly for very specific accessibility scenarios or legacy behaviors</item>
        ///     <item>Subtle reminders when user is already looking at the app</item>
        /// </list>
        ///
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Plays sound (and possibly vibration) but shows nothing on screen</item>
        ///     <item>May not be supported or visible on all platforms (many require visual pairing with sound)</item>
        ///     <item>Use cautiously to avoid confusing or annoying users</item>
        /// </list>
        ///
        /// <para>Recommendation: Prefer <see cref="Both"/> or <see cref="Toast"/> in most cases.</para>
        /// </remarks>
        Sound = 1,

        /// <summary>
        /// Both visual notification (toast/banner) and audible alert (sound/vibration).
        /// </summary>
        /// <remarks>
        /// <para>Typical use cases:</para>
        /// <list type="bullet">
        ///     <item>Standard new article notifications when sound is enabled</item>
        ///     <item>Medium-to-high importance events that should catch attention</item>
        ///     <item>User's default preference for important feeds</item>
        /// </list>
        ///
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Shows visual element + plays notification sound once</item>
        ///     <item>Most common and expected delivery method for alerting notifications</item>
        ///     <item>Combines visibility with immediate awareness</item>
        /// </list>
        /// </remarks>
        Both = 2,

        /// <summary>
        /// No visual or audible interruption — notification is logged internally only.
        /// </summary>
        /// <remarks>
        /// <para>Typical use cases:</para>
        /// <list type="bullet">
        ///     <item>Successful background operations (sync completed, feed refreshed silently)</item>
        ///     <item>Debug/telemetry events visible only in logs or app diagnostics</item>
        ///     <item>Events explicitly muted by user per category</item>
        /// </list>
        ///
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>No user-visible popup, banner, sound, or vibration</item>
        ///     <item>May still be recorded in an in-app notification history or activity log</item>
        ///     <item>Zero interruption to user experience</item>
        /// </list>
        /// </remarks>
        Silent = 3,

        /// <summary>
        /// Prominent visual banner (heads-up, persistent, or expanded style), typically without sound unless combined with priority.
        /// </summary>
        /// <remarks>
        /// <para>Typical use cases:</para>
        /// <list type="bullet">
        ///     <item>High-priority events requiring stronger visual emphasis</item>
        ///     <item>Notifications that should remain visible longer than a standard toast</item>
        ///     <item>Critical alerts on platforms that distinguish banner styles</item>
        /// </list>
        ///
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Displays as a larger, more noticeable banner/pop-up (may cover more screen area or persist longer)</item>
        ///     <item>No automatic sound (sound controlled separately via <see cref="NotificationPriority"/>)</item>
        ///     <item>Often used for elevated-attention but non-audible alerts</item>
        /// </list>
        /// </remarks>
        Banner = 4
    }
}