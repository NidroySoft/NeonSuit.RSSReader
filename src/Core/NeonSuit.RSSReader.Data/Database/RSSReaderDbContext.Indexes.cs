// =======================================================
// Data/Database/RssReaderDbContext.Indexes.cs
// =======================================================

using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Data.Database
{
    internal partial class RSSReaderDbContext
    {
        private static void ConfigureIndexes(ModelBuilder modelBuilder)
        {
            // Article indexes
            modelBuilder.Entity<Article>()
                .HasIndex(a => new { a.FeedId, a.PublishedDate })
                .HasDatabaseName("IX_Article_FeedId_PublishedDate")
                .IsDescending(false, true);

            modelBuilder.Entity<Article>()
                .HasIndex(a => a.PublishedDate)
                .HasDatabaseName("IX_Article_PublishedDate")
                .IsDescending();

            modelBuilder.Entity<Article>()
                .HasIndex(a => a.ContentHash)
                .HasDatabaseName("IX_Article_ContentHash")
                .HasFilter("[ContentHash] IS NOT NULL AND [ContentHash] != ''");

            modelBuilder.Entity<Article>()
                .HasIndex(a => a.IsNotified)
                .HasDatabaseName("IX_Article_IsNotified")
                .HasFilter("[IsNotified] = 0");

            // Feed indexes
            modelBuilder.Entity<Feed>()
                .HasIndex(f => f.Url)
                .IsUnique()
                .HasDatabaseName("IX_Feed_Url");

            modelBuilder.Entity<Feed>()
                .HasIndex(f => f.CategoryId)
                .HasDatabaseName("IX_Feed_CategoryId");

            modelBuilder.Entity<Feed>()
                .HasIndex(f => new { f.IsActive, f.NextUpdateSchedule })
                .HasDatabaseName("IX_Feed_IsActive_NextUpdateSchedule")
                .HasFilter("[IsActive] = 1");

            // Category indexes
            modelBuilder.Entity<Category>()
                .HasIndex(c => new { c.ParentCategoryId, c.Name })
                .HasDatabaseName("IX_Category_ParentId_Name")
                .IsUnique();

            // Rule indexes
            modelBuilder.Entity<Rule>()
                .HasIndex(r => new { r.IsEnabled, r.Priority })
                .HasDatabaseName("IX_Rule_IsEnabled_Priority");

            // NotificationLog indexes
            modelBuilder.Entity<NotificationLog>()
                .HasIndex(n => n.ArticleId)
                .HasDatabaseName("IX_NotificationLog_ArticleId");

            modelBuilder.Entity<NotificationLog>()
                .HasIndex(n => n.SentAt)
                .HasDatabaseName("IX_NotificationLog_SentAt")
                .IsDescending();

            // ArticleTag indexes
            modelBuilder.Entity<ArticleTag>()
                .HasIndex(at => new { at.ArticleId, at.TagId })
                .IsUnique()
                .HasDatabaseName("IX_ArticleTag_ArticleId_TagId");

            // RuleCondition indexes
            modelBuilder.Entity<RuleCondition>()
                .HasIndex(rc => rc.RuleId)
                .HasDatabaseName("IX_RuleCondition_RuleId");

            // UserPreferences indexes
            modelBuilder.Entity<UserPreferences>()
                .HasIndex(p => p.Key)
                .IsUnique()
                .HasDatabaseName("IX_UserPreferences_Key");
        }
    }
}