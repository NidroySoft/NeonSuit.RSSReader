using NeonSuit.RSSReader.Core.Enums;

namespace NeonSuit.RSSReader.Core.Models.Events;

/// <summary>A presentation action requested by a matching automation rule.</summary>
public sealed class RuleActionRequestedEventArgs : EventArgs
{
    /// <summary>Matching rule identifier.</summary>
    public int RuleId { get; init; }
    /// <summary>Article identifier.</summary>
    public int ArticleId { get; init; }
    /// <summary>Requested presentation action.</summary>
    public RuleActionType ActionType { get; init; }
    /// <summary>Action payload, such as the sound path.</summary>
    public string? Value { get; init; }
}
