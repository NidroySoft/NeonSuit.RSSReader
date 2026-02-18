namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Defines the refresh interval options for automatic background polling of RSS/Atom feeds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration controls how frequently the system attempts to fetch new content from each subscribed feed
    /// when automatic updates are enabled (i.e., the feed is not in Paused, Error, or Invalid state).
    /// </para>
    ///
    /// <para>
    /// Key behaviors and system impacts:
    /// </para>
    /// <list type="bullet">
    ///     <item>Determines the minimum time (in minutes) between two consecutive refresh attempts for a feed.</item>
    ///     <item>Directly affects battery usage, data consumption, server load, and perceived freshness of content.</item>
    ///     <item>Used by the background scheduler/refresh queue to calculate next poll time.</item>
    ///     <item>Can be overridden per-feed or globally via user settings.</item>
    ///     <item>More frequent intervals (15–60 min) are suitable for news-heavy or real-time feeds; longer intervals reduce resource usage.</item>
    /// </list>
    ///
    /// <para>
    /// Business rules and recommendations:
    /// </para>
    /// <list type="bullet">
    ///     <item><see cref="Manual"/> disables all automatic polling — refreshes occur only when the user explicitly triggers them.</item>
    ///     <item>Values represent minutes between refreshes (except <see cref="Manual"/> which is a special case).</item>
    ///     <item>The actual polling time may be slightly delayed due to device sleep, network conditions, or queue prioritization.</item>
    ///     <item>Very frequent polling (Every15Minutes) should be discouraged or limited to a small number of feeds to avoid rate-limiting by feed hosts and excessive battery drain.</item>
    ///     <item>Default recommendation for most users: EveryHour or Every3Hours balances freshness and efficiency.</item>
    ///     <item>Feeds in Warning or Error states may have their effective frequency temporarily reduced regardless of this setting.</item>
    /// </list>
    /// </remarks>
    public enum FeedUpdateFrequency
    {
        /// <summary>
        /// No automatic polling is performed. Updates occur only when manually triggered by the user.
        /// </summary>
        /// <remarks>
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Feed is excluded from all background refresh schedules.</item>
        ///     <item>Conserves maximum resources (battery, data, network).</item>
        ///     <item>Useful for rarely updated feeds, metered connections, or power-saving mode.</item>
        /// </list>
        /// </remarks>
        Manual = 0,

        /// <summary>
        /// Attempt refresh approximately every 15 minutes.
        /// </summary>
        /// <remarks>
        /// High-frequency option. Suitable only for critical real-time feeds.
        /// May trigger rate limits or increased battery/data usage.
        /// </remarks>
        Every15Minutes = 15,

        /// <summary>
        /// Attempt refresh approximately every 30 minutes.
        /// </summary>
        /// <remarks>
        /// Balanced option for feeds that update several times per day (news, blogs with frequent posts).
        /// </remarks>
        Every30Minutes = 30,

        /// <summary>
        /// Attempt refresh approximately every 60 minutes (hourly).
        /// </summary>
        /// <remarks>
        /// Common default for general news and blog feeds.
        /// Good balance between freshness and resource consumption.
        /// </remarks>
        EveryHour = 60,

        /// <summary>
        /// Attempt refresh approximately every 3 hours.
        /// </summary>
        /// <remarks>
        /// Recommended for most users and moderately active feeds.
        /// Significantly reduces background activity.
        /// </remarks>
        Every3Hours = 180,

        /// <summary>
        /// Attempt refresh approximately every 6 hours.
        /// </summary>
        /// <remarks>
        /// Suitable for feeds that update infrequently (daily digests, weekly publications).
        /// </remarks>
        Every6Hours = 360,

        /// <summary>
        /// Attempt refresh approximately every 12 hours.
        /// </summary>
        /// <remarks>
        /// Very conservative polling. Ideal for low-update-frequency sources or strict power-saving scenarios.
        /// </remarks>
        Every12Hours = 720,

        /// <summary>
        /// Attempt refresh approximately once per day (every 1440 minutes).
        /// </summary>
        /// <remarks>
        /// Minimal polling frequency. Best for archival feeds, podcasts, or extremely low-activity sources.
        /// </remarks>
        Daily = 1440
    }
}