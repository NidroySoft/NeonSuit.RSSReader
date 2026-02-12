namespace NeonSuit.RSSReader.Core.Enums
{
    public enum RuleActionType
    {
        MarkAsRead = 0,
        MarkAsUnread = 1,
        MarkAsStarred = 2,
        MarkAsFavorite = 3,
        ApplyTags = 4,
        MoveToCategory = 5,
        SendNotification = 6,
        PlaySound = 7,
        DeleteArticle = 8,
        ArchiveArticle = 9,
        HighlightArticle = 10   // Con color específico
    }
}