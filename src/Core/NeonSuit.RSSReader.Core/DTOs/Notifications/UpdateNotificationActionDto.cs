// =======================================================
// Core/DTOs/Notifications/UpdateNotificationActionDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Notifications
{
    /// <summary>
    /// Data Transfer Object for updating notification action (user interaction).
    /// Used when user clicks, dismisses, or snoozes a notification.
    /// </summary>
    public class UpdateNotificationActionDto
    {
        /// <summary>
        /// ID of the notification being acted upon.
        /// </summary>
        [Required]
        public int NotificationId { get; set; }

        /// <summary>
        /// Action performed by the user.
        /// </summary>
        [Required]
        public NotificationAction Action { get; set; }
    }
}