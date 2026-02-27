// =======================================================
// Data/Database/RssReaderDbContext.Relationships.cs
// =======================================================

using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Data.Database
{
    internal partial class RSSReaderDbContext
    {
        private static void ConfigureRelationships(ModelBuilder modelBuilder)
        {
            // Feed → Article
            modelBuilder.Entity<Feed>()
                .HasMany(f => f.Articles)
                .WithOne(a => a.Feed)
                .HasForeignKey(a => a.FeedId)
                .OnDelete(DeleteBehavior.Cascade);

            // Category → Feed
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Feeds)
                .WithOne(f => f.Category)
                .HasForeignKey(f => f.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Category self-reference
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Subcategories)
                .WithOne(c => c.ParentCategory)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Article ↔ Tag many-to-many
            modelBuilder.Entity<ArticleTag>()
                .HasKey(at => new { at.ArticleId, at.TagId });

            modelBuilder.Entity<ArticleTag>()
                .HasOne(at => at.Article)
                .WithMany(a => a.ArticleTags)
                .HasForeignKey(at => at.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ArticleTag>()
                .HasOne(at => at.Tag)
                .WithMany(t => t.ArticleTags)
                .HasForeignKey(at => at.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            // Rule → RuleCondition
            modelBuilder.Entity<Rule>()
                .HasMany(r => r.Conditions)
                .WithOne(rc => rc.Rule)
                .HasForeignKey(rc => rc.RuleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Article → NotificationLog
            modelBuilder.Entity<Article>()
                .HasMany(a => a.NotificationLogs)
                .WithOne(n => n.Article)
                .HasForeignKey(n => n.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}