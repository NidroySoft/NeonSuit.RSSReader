using NeonSuit.RSSReader.Core.DTOs.Notifications;
using NeonSuit.RSSReader.Core.Models.Events;

namespace NeonSuit.RSSReader.Core.Interfaces.Services;

/// <summary>Application-wide events emitted by short-lived background service scopes.</summary>
public interface IBackendEvents
{
    /// <summary>A notification is ready for the presentation layer.</summary>
    event EventHandler<NotificationDto>? NotificationCreated;
    /// <summary>The host is asked to handle an action such as playing a sound.</summary>
    event EventHandler<RuleActionRequestedEventArgs>? RuleActionRequested;
}
