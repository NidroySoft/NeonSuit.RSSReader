using SQLite;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a user-defined tag for categorizing articles.
    /// Tags are many-to-many with Articles via ArticleTag join table.
    /// </summary>
    [Table("Tags")]
    public class Tag
    {
        
        public virtual ICollection<ArticleTag> ArticleTags { get; set; } = new List<ArticleTag>();

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Display name of the tag
        /// </summary>
        [NotNull, Indexed]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description
        /// </summary>
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Color in hexadecimal format (#RRGGBB or #RRGGBBAA)
        /// </summary>
        [NotNull]
        [MaxLength(9)]
        public string Color { get; set; } = "#3498db"; // Azul por defecto

        /// <summary>
        /// Optional icon identifier (FontAwesome/Material icon name)
        /// </summary>
        [MaxLength(30)]
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// Whether this tag is pinned/featured in the UI
        /// </summary>
        public bool IsPinned { get; set; } = false;

        /// <summary>
        /// Whether this tag is visible in the tag cloud
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Created timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last time this tag was used/assigned
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Usage count (number of articles tagged)
        /// </summary>
        public int UsageCount { get; set; } = 0;

        /// <summary>
        /// Articles associated with this tag
        /// </summary>
        [Ignore]
        public List<Article> Articles { get; set; } = new List<Article>();

        /// <summary>
        /// Returns a darker shade for border/text (calculated)
        /// </summary>
        [Ignore]
        public string DarkColor => CalculateDarkColor(Color);

        private static string CalculateDarkColor(string hexColor)
        {
            // Lógica simple para oscurecer color (puedes mejorar esto)
            if (hexColor.Length == 7 && hexColor.StartsWith("#"))
            {
                try
                {
                    var r = Convert.ToInt32(hexColor.Substring(1, 2), 16);
                    var g = Convert.ToInt32(hexColor.Substring(3, 2), 16);
                    var b = Convert.ToInt32(hexColor.Substring(5, 2), 16);

                    r = (int)(r * 0.7);
                    g = (int)(g * 0.7);
                    b = (int)(b * 0.7);

                    return $"#{r:X2}{g:X2}{b:X2}";
                }
                catch
                {
                    return "#2c3e50"; // Fallback dark
                }
            }
            return "#2c3e50";
        }
    }
}