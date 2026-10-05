using NeonSuit.RSSReader.Core.DTOs.Notifications;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models.Events;

namespace NeonSuit.RSSReader.Services;

internal sealed class BackendEvents : IBackendEvents
{
    public event EventHandler<NotificationDto>? NotificationCreated;
    public event EventHandler<RuleActionRequestedEventArgs>? RuleActionRequested;
    internal void PublishNotification(NotificationDto notification) => NotificationCreated?.Invoke(this, notification);
    internal bool HasActionHandler => RuleActionRequested != null;
    internal void PublishAction(RuleActionRequestedEventArgs request) => RuleActionRequested?.Invoke(this, request);
}
