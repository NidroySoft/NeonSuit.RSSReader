// =======================================================
// Core/DTOs/Notifications/NotificationStatsDto.cs
// =======================================================

using System;

namespace NeonSuit.RSSReader.Core.DTOs.Notifications
{
    /// <summary>
    /// Data Transfer Object for notification statistics and analytics.
    /// Used in dashboards and reporting views.
    /// </summary>
    public class NotificationStatsDto
    {
        /// <summary>
        /// Total number of notifications sent.
        /// </summary>
        public int TotalSent { get; set; }

        /// <summary>
        /// Number of notifications delivered successfully.
        /// </summary>
        public int TotalDelivered { get; set; }

        /// <summary>
        /// Number of failed notifications.
        /// </summary>
        public int TotalFailed { get; set; }

        /// <summary>
        /// Number of notifications clicked by user.
        /// </summary>
        public int TotalClicked { get; set; }

        /// <summary>
        /// Number of notifications dismissed.
        /// </summary>
        public int TotalDismissed { get; set; }

        /// <summary>
        /// Number of notifications snoozed.
        /// </summary>
        public int TotalSnoozed { get; set; }

        /// <summary>
        /// Click-through rate (clicked / delivered).
        /// </summary>
        public double ClickThroughRate { get; set; }

        /// <summary>
        /// Average response time in seconds (sent to click).
        /// </summary>
        public double? AverageResponseTimeSeconds { get; set; }

        /// <summary>
        /// Number of notifications in the last 24 hours.
        /// </summary>
        public int Last24Hours { get; set; }

        /// <summary>
        /// Number of notifications in the last 7 days.
        /// </summary>
        public int Last7Days { get; set; }

        /// <summary>
        /// Number of notifications in the last 30 days.
        /// </summary>
        public int Last30Days { get; set; }

        /// <summary>
        /// Date of the last notification sent.
        /// </summary>
        public DateTime? LastNotificationDate { get; set; }

        /// <summary>
        /// Top rules by notification count.
        /// </summary>
        public Dictionary<string, int> TopRules { get; set; } = new();
    }
}