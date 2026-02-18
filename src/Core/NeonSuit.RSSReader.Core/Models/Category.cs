using SQLite;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a category/folder for organizing feeds.
    /// Supports nesting via ParentCategoryId.
    /// </summary>
    public class Category
    {
        public Category()
        {
            SortOrder = 0;
            CreatedAt = DateTime.UtcNow;
            IsExpanded = true;
        }

        public virtual Category? ParentCategory { get; set; }
        public virtual ICollection<Category> Subcategories { get; set; } = new List<Category>();

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Name of the category.
        /// </summary>
        [Unique(Name = "IX_Category_Name_Parent"), NotNull]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Parent category ID for nesting (null = root category).
        /// </summary>
        [Indexed(Name = "IX_Category_Name_Parent")]
        public int? ParentCategoryId { get; set; }

        /// <summary>
        /// Color in hexadecimal format (#RRGGBB) for visual identification.
        /// </summary>
        [NotNull]
        public string Color { get; set; } = "#3498db";

        /// <summary>
        /// Display order within parent category.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Creation date.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Whether this category is expanded in the UI tree view.
        /// </summary>
        [Ignore]
        public bool IsExpanded { get; set; }

        /// <summary>
        /// Number of feeds in this category (calculated).
        /// </summary>
        [Ignore]
        public int FeedCount { get; set; }

        /// <summary>
        /// Number of unread articles in this category (calculated).
        /// </summary>
        [Ignore]
        public int UnreadCount { get; set; }

        /// <summary>
        /// Child categories (for tree navigation).
        /// </summary>
        [Ignore]
        public List<Category> Children { get; set; } = new List<Category>();

        /// <summary>
        /// Parent category navigation property.
        /// </summary>
        [Ignore]
        public Category? Parent { get; set; }

        /// <summary>
        /// Feeds belonging to this category.
        /// </summary>
        [Ignore]
        public List<Feed> Feeds { get; set; } = new List<Feed>();

        /// <summary>
        /// Full path including parent categories (e.g., "News/Tech/AI").
        /// </summary>
        [Ignore]
        public string FullPath
        {
            get
            {
                var path = Name;
                var current = Parent;
                while (current != null)
                {
                    path = current.Name + "/" + path;
                    current = current.Parent;
                }
                return path;
            }
        }

        /// <summary>
        /// Depth in the category tree (0 = root).
        /// </summary>
        [Ignore]
        public int Depth
        {
            get
            {
                int depth = 0;
                var current = Parent;
                while (current != null)
                {
                    depth++;
                    current = current.Parent;
                }
                return depth;
            }
        }

        /// <summary>
        /// Indicates if this is a root category (no parent).
        /// </summary>
        [Ignore]
        public bool IsRoot => ParentCategoryId == null;
    }
}