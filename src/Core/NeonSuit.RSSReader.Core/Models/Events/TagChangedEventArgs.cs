using System;

namespace NeonSuit.RSSReader.Core.Models.Events
{
    /// <summary>
    /// Event arguments for tag change notifications.
    /// </summary>
    public class TagChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the ID of the changed tag.
        /// </summary>
        public int TagId { get; }

        /// <summary>
        /// Gets the name of the changed tag.
        /// </summary>
        public string TagName { get; }

        /// <summary>
        /// Gets the type of change that occurred.
        /// </summary>
        public TagChangeType ChangeType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TagChangedEventArgs"/> class.
        /// </summary>
        /// <param name="tagId">The ID of the changed tag.</param>
        /// <param name="tagName">The name of the changed tag.</param>
        /// <param name="changeType">The type of change.</param>
        public TagChangedEventArgs(int tagId, string tagName, TagChangeType changeType)
        {
            TagId = tagId;
            TagName = tagName;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// Type of change performed on a tag.
    /// </summary>
    public enum TagChangeType
    {
        /// <summary>Tag was created.</summary>
        Created,

        /// <summary>Tag was updated (name, color, etc.).</summary>
        Updated,

        /// <summary>Tag was deleted.</summary>
        Deleted,

        /// <summary>Tag was pinned.</summary>
        Pinned,

        /// <summary>Tag was unpinned.</summary>
        Unpinned,

        /// <summary>Tag visibility was changed (shown/hidden).</summary>
        VisibilityChanged
    }
}