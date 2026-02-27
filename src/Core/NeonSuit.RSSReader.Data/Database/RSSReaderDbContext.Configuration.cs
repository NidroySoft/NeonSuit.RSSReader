// =======================================================
// Data/Database/RssReaderDbContext.Configuration.cs
// =======================================================

using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Data.Database
{
    internal partial class RSSReaderDbContext
    {
        private static void ConfigureTableNames(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ArticleTag>().ToTable("ArticleTags");
            modelBuilder.Entity<RuleCondition>().ToTable("RuleConditions");
            modelBuilder.Entity<NotificationLog>().ToTable("NotificationLogs");
            modelBuilder.Entity<UserPreferences>().ToTable("UserPreferences");
        }

        private static void ConfigureEntities(ModelBuilder modelBuilder)
        {
            // RuleCondition
            modelBuilder.Entity<RuleCondition>()
                .Property(rc => rc.Order)
                .IsRequired();

            // Article
            modelBuilder.Entity<Article>()
                .Property(a => a.Title)
                .HasMaxLength(1200)
                .IsRequired();

            modelBuilder.Entity<Article>()
                .Property(a => a.Link)
                .HasMaxLength(2048)
                .IsRequired();

            modelBuilder.Entity<Article>()
                .Property(a => a.Guid)
                .HasMaxLength(512)
                .IsRequired();

            // Feed
            modelBuilder.Entity<Feed>()
                .Property(f => f.Url)
                .HasMaxLength(2048)
                .IsRequired();

            modelBuilder.Entity<Feed>()
                .Property(f => f.Title)
                .HasMaxLength(512)
                .IsRequired();

            // Category
            modelBuilder.Entity<Category>()
                .Property(c => c.Name)
                .HasMaxLength(200)
                .IsRequired();

            // Tag
            modelBuilder.Entity<Tag>(entity =>
            {
                entity.HasIndex(t => t.Name).IsUnique();
                entity.Property(t => t.Name).HasMaxLength(50).IsRequired();
                entity.Property(t => t.Color).HasMaxLength(9).IsRequired();
            });

            // Rule
            modelBuilder.Entity<Rule>()
                .Property(r => r.Name)
                .HasMaxLength(200)
                .IsRequired();

            // NotificationLog
            modelBuilder.Entity<NotificationLog>()
                .Property(n => n.Title)
                .HasMaxLength(200)
                .IsRequired();

            // UserPreferences
            modelBuilder.Entity<UserPreferences>()
                .Property(p => p.Key)
                .HasMaxLength(255)
                .IsRequired();
        }

        private static void ConfigureValueConversions(ModelBuilder modelBuilder)
        {
            // Article enums
            modelBuilder.Entity<Article>()
                .Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Feed enums
            modelBuilder.Entity<Feed>()
                .Property(f => f.UpdateFrequency)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Rule enums
            modelBuilder.Entity<Rule>()
                .Property(r => r.Target)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<Rule>()
                .Property(r => r.Operator)
                .HasConversion<string>()
                .HasMaxLength(20);

            // RuleCondition enums
            modelBuilder.Entity<RuleCondition>()
                .Property(rc => rc.Field)
                .HasConversion<string>()
                .HasMaxLength(20);

            // NotificationLog enums
            modelBuilder.Entity<NotificationLog>()
                .Property(n => n.NotificationType)
                .HasConversion<string>()
                .HasMaxLength(20);
        }

        private static void ConfigureQueryFilters(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Feed>()
                .HasQueryFilter(f => f.IsActive);
        }
    }
}