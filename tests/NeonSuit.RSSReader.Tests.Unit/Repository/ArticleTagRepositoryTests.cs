using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Repository
{
    [CollectionDefinition("Database_ArticleTags")]
    public class DatabaseCollectionArticleTags : ICollectionFixture<DatabaseFixture> { }

    [Collection("Database_ArticleTags")]
    public class ArticleTagRepositoryTests : IDisposable
    {
        private readonly RssReaderDbContext _dbContext;
        private readonly ArticleTagRepository _repository;
        private readonly Mock<ILogger> _mockLogger;
        private bool _disposed;

        private const string DEFAULT_APPLIED_BY = "user";
        private const int DEFAULT_RULE_ID = 1;
        private const double DEFAULT_CONFIDENCE = 0.95;

        public ArticleTagRepositoryTests(DatabaseFixture fixture)
        {
            _mockLogger = new Mock<ILogger>();
            SetupMockLogger();
            _dbContext = fixture.Context;
            ClearTestData();
            _repository = new ArticleTagRepository(_dbContext, _mockLogger.Object);
        }

        private void SetupMockLogger()
        {
            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
                .Returns(_mockLogger.Object);
            _mockLogger.Setup(x => x.ForContext<ArticleTagRepository>())
                .Returns(_mockLogger.Object);
        }

        #region Test Data Helpers

        private void ClearTestData()
        {
            _dbContext.Database.ExecuteSqlRaw("DELETE FROM ArticleTags");
            _dbContext.Database.ExecuteSqlRaw("DELETE FROM Tags");
            _dbContext.Database.ExecuteSqlRaw("DELETE FROM Articles");
            _dbContext.ChangeTracker.Clear();
        }

        private async Task<Article> CreateTestArticle()
        {
            // Asegurar que existe un feed
            var feed = await _dbContext.Feeds.FirstOrDefaultAsync();
            if (feed == null)
            {
                feed = new Feed
                {
                    Title = "Test Feed",
                    Url = $"https://test{Guid.NewGuid():N}.com/feed",
                    WebsiteUrl = "https://test.com",
                    IsActive = true
                };
                _dbContext.Feeds.Add(feed);
                await _dbContext.SaveChangesAsync();
            }

            var article = new Article
            {
                Title = $"Test Article {Guid.NewGuid():N}",
                Content = "Test content",
                FeedId = feed.Id,
                Guid = Guid.NewGuid().ToString(),
                PublishedDate = DateTime.UtcNow.AddDays(-1),
                AddedDate = DateTime.UtcNow
            };

            _dbContext.Articles.Add(article);
            await _dbContext.SaveChangesAsync();
            return article;
        }

        private async Task<Tag> CreateTestTag()
        {
            var tag = new Tag
            {
                Name = $"Test Tag {Guid.NewGuid():N}",
                Description = "Test description",
                Color = "#FF5733",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Tags.Add(tag);
            await _dbContext.SaveChangesAsync();
            return tag;
        }

        private async Task<ArticleTag> CreateTestArticleTag(Article? article = null, Tag? tag = null)
        {
            article ??= await CreateTestArticle();
            tag ??= await CreateTestTag();

            var articleTag = new ArticleTag
            {
                ArticleId = article.Id,
                TagId = tag.Id,
                AppliedBy = DEFAULT_APPLIED_BY,
                RuleId = DEFAULT_RULE_ID,
                Confidence = DEFAULT_CONFIDENCE,
                AppliedAt = DateTime.UtcNow
            };

            _dbContext.ArticleTags.Add(articleTag);
            await _dbContext.SaveChangesAsync();
            return articleTag;
        }

        private async Task<List<ArticleTag>> SeedTestArticleTagsAsync(int count)
        {
            var articleTags = new List<ArticleTag>();

            for (int i = 0; i < count; i++)
            {
                var article = await CreateTestArticle();
                var tag = await CreateTestTag();

                var articleTag = new ArticleTag
                {
                    ArticleId = article.Id,
                    TagId = tag.Id,
                    AppliedBy = i % 2 == 0 ? "user" : "rule",
                    RuleId = i % 2 == 0 ? null : DEFAULT_RULE_ID + i,
                    Confidence = DEFAULT_CONFIDENCE - (i * 0.1),
                    AppliedAt = DateTime.UtcNow.AddHours(-i)
                };

                _dbContext.ArticleTags.Add(articleTag);
                articleTags.Add(articleTag);
            }

            await _dbContext.SaveChangesAsync();
            return articleTags;
        }

        private void ClearEntityTracking() => _dbContext.ChangeTracker.Clear();

        #endregion

        #region Basic CRUD Tests

        [Fact]
        public async Task InsertAsync_WithValidArticleTag_ShouldAddToDatabase()
        {
            var article = await CreateTestArticle();
            var tag = await CreateTestTag();

            var articleTag = new ArticleTag
            {
                ArticleId = article.Id,
                TagId = tag.Id,
                AppliedBy = DEFAULT_APPLIED_BY,
                AppliedAt = DateTime.UtcNow
            };

            var result = await _repository.InsertAsync(articleTag);

            result.Should().Be(1);
            ClearEntityTracking();

            var exists = await _repository.ExistsAsync(article.Id, tag.Id);
            exists.Should().BeTrue();
        }

        [Fact]
        public async Task GetByIdAsync_WithExistingArticleTag_ShouldReturnArticleTag()
        {
            var articleTag = await CreateTestArticleTag();
            ClearEntityTracking();

            var result = await _repository.GetByArticleAndTagAsync(
                articleTag.ArticleId,
                articleTag.TagId);

            result.Should().NotBeNull();
            result?.ArticleId.Should().Be(articleTag.ArticleId);
            result?.TagId.Should().Be(articleTag.TagId);
        }

        [Fact]
        public async Task UpdateAsync_WithValidArticleTag_ShouldUpdateInDatabase()
        {
            var articleTag = await CreateTestArticleTag();
            var updatedAppliedBy = "system";

            articleTag.AppliedBy = updatedAppliedBy;
            var result = await _repository.UpdateAsync(articleTag);

            result.Should().Be(1);
            ClearEntityTracking();

            var retrieved = await _repository.GetByArticleAndTagAsync(
                articleTag.ArticleId,
                articleTag.TagId);

            retrieved?.AppliedBy.Should().Be(updatedAppliedBy);
        }

        [Fact]
        public async Task DeleteAsync_WithExistingArticleTag_ShouldRemoveFromDatabase()
        {
            var articleTag = await CreateTestArticleTag();
            ClearEntityTracking();

            var existingArticleTag = await _repository.GetByArticleAndTagAsync(
                articleTag.ArticleId,
                articleTag.TagId);

            existingArticleTag.Should().NotBeNull();

            var result = await _repository.DeleteAsync(existingArticleTag!);
            result.Should().Be(1);

            var exists = await _repository.ExistsAsync(articleTag.ArticleId, articleTag.TagId);
            exists.Should().BeFalse();
        }

        #endregion

        #region Article-Specific Tests

        [Fact]
        public async Task GetByArticleIdAsync_WithValidArticleId_ShouldReturnArticleTags()
        {
            var article = await CreateTestArticle();
            var tag1 = await CreateTestTag();
            var tag2 = await CreateTestTag();
            var tag3 = await CreateTestTag();

            var articleTags = new List<ArticleTag>
            {
                new ArticleTag { ArticleId = article.Id, TagId = tag1.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = article.Id, TagId = tag2.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-1) },
                new ArticleTag { ArticleId = article.Id, TagId = tag3.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-2) }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetByArticleIdAsync(article.Id);

            result.Should().HaveCount(3);
            result.Should().OnlyContain(at => at.ArticleId == article.Id);
        }

        [Fact]
        public async Task GetByArticleIdAsync_WithNonExistentArticle_ShouldReturnEmptyList()
        {
            var result = await _repository.GetByArticleIdAsync(99999);
            result.Should().BeEmpty();
        }

        #endregion

        #region Tag-Specific Tests

        [Fact]
        public async Task GetByTagIdAsync_WithValidTagId_ShouldReturnArticleTags()
        {
            var tag = await CreateTestTag();
            var article1 = await CreateTestArticle();
            var article2 = await CreateTestArticle();
            var article3 = await CreateTestArticle();

            var articleTags = new List<ArticleTag>
            {
                new ArticleTag { ArticleId = article1.Id, TagId = tag.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = article2.Id, TagId = tag.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-1) },
                new ArticleTag { ArticleId = article3.Id, TagId = tag.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-2) }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetByTagIdAsync(tag.Id);

            result.Should().HaveCount(3);
            result.Should().OnlyContain(at => at.TagId == tag.Id);
        }

        [Fact]
        public async Task GetByTagIdAsync_WithNonExistentTag_ShouldReturnEmptyList()
        {
            var result = await _repository.GetByTagIdAsync(99999);
            result.Should().BeEmpty();
        }

        #endregion

        #region Existence and Retrieval Tests

        [Fact]
        public async Task ExistsAsync_WithExistingAssociation_ShouldReturnTrue()
        {
            var articleTag = await CreateTestArticleTag();
            ClearEntityTracking();

            var result = await _repository.ExistsAsync(articleTag.ArticleId, articleTag.TagId);
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_WithNonExistentAssociation_ShouldReturnFalse()
        {
            var result = await _repository.ExistsAsync(99999, 99999);
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetByArticleAndTagAsync_WithExistingAssociation_ShouldReturnArticleTag()
        {
            var expected = await CreateTestArticleTag();
            ClearEntityTracking();

            var result = await _repository.GetByArticleAndTagAsync(expected.ArticleId, expected.TagId);

            result.Should().NotBeNull();
            result?.ArticleId.Should().Be(expected.ArticleId);
            result?.TagId.Should().Be(expected.TagId);
        }

        [Fact]
        public async Task GetByArticleAndTagAsync_WithNonExistentAssociation_ShouldReturnNull()
        {
            var result = await _repository.GetByArticleAndTagAsync(99999, 99999);
            result.Should().BeNull();
        }

        #endregion

        #region Association Management Tests

        [Fact]
        public async Task AssociateTagWithArticleAsync_WithNewAssociation_ShouldCreateAssociation()
        {
            var article = await CreateTestArticle();
            var tag = await CreateTestTag();
            ClearEntityTracking();

            var result = await _repository.AssociateTagWithArticleAsync(
                article.Id,
                tag.Id,
                DEFAULT_APPLIED_BY,
                DEFAULT_RULE_ID,
                DEFAULT_CONFIDENCE);

            result.Should().BeTrue();
            ClearEntityTracking();

            var exists = await _repository.ExistsAsync(article.Id, tag.Id);
            exists.Should().BeTrue();
        }

        [Fact]
        public async Task AssociateTagWithArticleAsync_WithExistingAssociation_ShouldReturnFalse()
        {
            var articleTag = await CreateTestArticleTag();
            ClearEntityTracking();

            var result = await _repository.AssociateTagWithArticleAsync(
                articleTag.ArticleId,
                articleTag.TagId,
                DEFAULT_APPLIED_BY);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task RemoveTagFromArticleAsync_WithExistingAssociation_ShouldRemoveAssociation()
        {
            var articleTag = await CreateTestArticleTag();
            ClearEntityTracking();

            var result = await _repository.RemoveTagFromArticleAsync(articleTag.ArticleId, articleTag.TagId);

            result.Should().BeTrue();
            ClearEntityTracking();

            var exists = await _repository.ExistsAsync(articleTag.ArticleId, articleTag.TagId);
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task RemoveTagFromArticleAsync_WithNonExistentAssociation_ShouldReturnFalse()
        {
            var result = await _repository.RemoveTagFromArticleAsync(99999, 99999);
            result.Should().BeFalse();
        }

        #endregion

        #region Bulk Association Management Tests

        [Fact]
        public async Task RemoveAllTagsFromArticleAsync_WithMultipleAssociations_ShouldRemoveAll()
        {
            var article = await CreateTestArticle();
            var tag1 = await CreateTestTag();
            var tag2 = await CreateTestTag();
            var tag3 = await CreateTestTag();

            var articleTags = new[]
            {
                new ArticleTag { ArticleId = article.Id, TagId = tag1.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = article.Id, TagId = tag2.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-1) },
                new ArticleTag { ArticleId = article.Id, TagId = tag3.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-2) }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.RemoveAllTagsFromArticleAsync(article.Id);

            result.Should().Be(3);
            ClearEntityTracking();

            var remaining = await _repository.GetByArticleIdAsync(article.Id);
            remaining.Should().BeEmpty();
        }

        [Fact]
        public async Task RemoveAllTagsFromArticleAsync_WithNoAssociations_ShouldReturnZero()
        {
            var result = await _repository.RemoveAllTagsFromArticleAsync(99999);
            result.Should().Be(0);
        }

        [Fact]
        public async Task RemoveTagFromAllArticlesAsync_WithMultipleAssociations_ShouldRemoveAll()
        {
            var tag = await CreateTestTag();
            var article1 = await CreateTestArticle();
            var article2 = await CreateTestArticle();
            var article3 = await CreateTestArticle();

            var articleTags = new[]
            {
                new ArticleTag { ArticleId = article1.Id, TagId = tag.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = article2.Id, TagId = tag.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-1) },
                new ArticleTag { ArticleId = article3.Id, TagId = tag.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-2) }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.RemoveTagFromAllArticlesAsync(tag.Id);

            result.Should().Be(3);
            ClearEntityTracking();

            var remaining = await _repository.GetByTagIdAsync(tag.Id);
            remaining.Should().BeEmpty();
        }

        [Fact]
        public async Task AssociateTagsWithArticleAsync_WithMultipleTags_ShouldAssociateAll()
        {
            var article = await CreateTestArticle();
            var tag1 = await CreateTestTag();
            var tag2 = await CreateTestTag();
            var tag3 = await CreateTestTag();

            var tagIds = new[] { tag1.Id, tag2.Id, tag3.Id };
            ClearEntityTracking();

            var result = await _repository.AssociateTagsWithArticleAsync(
                article.Id,
                tagIds,
                DEFAULT_APPLIED_BY,
                DEFAULT_RULE_ID);

            result.Should().Be(3);
            ClearEntityTracking();

            var articleTags = await _repository.GetByArticleIdAsync(article.Id);
            articleTags.Should().HaveCount(3);
        }

        [Fact]
        public async Task AssociateTagsWithArticleAsync_WithDuplicateTags_ShouldOnlyAssociateOncePerTag()
        {
            var article = await CreateTestArticle();
            var tag = await CreateTestTag();

            var duplicateTagIds = new[] { tag.Id, tag.Id, tag.Id };
            ClearEntityTracking();

            var result = await _repository.AssociateTagsWithArticleAsync(
                article.Id,
                duplicateTagIds,
                DEFAULT_APPLIED_BY);

            result.Should().Be(1);
            ClearEntityTracking();

            var articleTags = await _repository.GetByArticleIdAsync(article.Id);
            articleTags.Should().HaveCount(1);
        }

        [Fact]
        public async Task RemoveTagsFromArticleAsync_WithMultipleTags_ShouldRemoveAll()
        {
            var article = await CreateTestArticle();
            var tag1 = await CreateTestTag();
            var tag2 = await CreateTestTag();
            var tag3 = await CreateTestTag();

            var articleTags = new[]
            {
                new ArticleTag { ArticleId = article.Id, TagId = tag1.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = article.Id, TagId = tag2.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-1) },
                new ArticleTag { ArticleId = article.Id, TagId = tag3.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-2) }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var tagIds = new[] { tag1.Id, tag2.Id, tag3.Id };
            var result = await _repository.RemoveTagsFromArticleAsync(article.Id, tagIds);

            result.Should().Be(3);
            ClearEntityTracking();

            var remaining = await _repository.GetByArticleIdAsync(article.Id);
            remaining.Should().BeEmpty();
        }

        [Fact]
        public async Task RemoveTagsFromArticleAsync_WithNonExistentTags_ShouldHandleGracefully()
        {
            var articleTag = await CreateTestArticleTag();
            ClearEntityTracking();

            var mixedTagIds = new[] { articleTag.TagId, 99999, 99998 };

            var result = await _repository.RemoveTagsFromArticleAsync(articleTag.ArticleId, mixedTagIds);

            result.Should().Be(1);
            ClearEntityTracking();

            var exists = await _repository.ExistsAsync(articleTag.ArticleId, articleTag.TagId);
            exists.Should().BeFalse();
        }

        #endregion

        #region Advanced Query Tests

        [Fact]
        public async Task GetArticleIdsByTagNameAsync_WithValidTagName_ShouldReturnArticleIds()
        {
            var tag = await CreateTestTag();
            var article1 = await CreateTestArticle();
            var article2 = await CreateTestArticle();

            var articleTags = new List<ArticleTag>
            {
                new ArticleTag { ArticleId = article1.Id, TagId = tag.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = article2.Id, TagId = tag.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetArticleIdsByTagNameAsync(tag.Name);

            result.Should().HaveCount(2);
            result.Should().Contain(article1.Id).And.Contain(article2.Id);
        }

        [Fact]
        public async Task GetTagsForArticleWithDetailsAsync_WithAssociatedTags_ShouldReturnTagsWithDetails()
        {
            var article = await CreateTestArticle();
            var tag = await CreateTestTag();

            var articleTag = new ArticleTag
            {
                ArticleId = article.Id,
                TagId = tag.Id,
                AppliedBy = "user",
                AppliedAt = DateTime.UtcNow
            };

            _dbContext.ArticleTags.Add(articleTag);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetTagsForArticleWithDetailsAsync(article.Id);

            result.Should().HaveCount(1);
            result.First().Id.Should().Be(tag.Id);
            result.First().Name.Should().Be(tag.Name);
        }

        [Fact]
        public async Task GetTagStatisticsForArticleAsync_WithMultipleAssociations_ShouldReturnStatistics()
        {
            var article = await CreateTestArticle();
            var tag1 = await CreateTestTag();
            var tag2 = await CreateTestTag();

            var articleTags = new[]
            {
                new ArticleTag { ArticleId = article.Id, TagId = tag1.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = article.Id, TagId = tag2.Id, AppliedBy = "rule", AppliedAt = DateTime.UtcNow.AddMinutes(-1) }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetTagStatisticsForArticleAsync(article.Id);

            result.Should().HaveCount(2);
            result.Should().ContainKey(tag1.Name).WhoseValue.Should().Be(1);
            result.Should().ContainKey(tag2.Name).WhoseValue.Should().Be(1);
        }

        [Fact]
        public async Task GetRecentlyAppliedTagsAsync_WithMultipleAssociations_ShouldReturnOrderedResults()
        {
            await SeedTestArticleTagsAsync(5);
            ClearEntityTracking();

            var result = await _repository.GetRecentlyAppliedTagsAsync(3);

            result.Should().HaveCount(3);
            result.Should().BeInDescendingOrder(at => at.AppliedAt);
        }

        [Fact]
        public async Task GetTagsAppliedByRuleAsync_WithRuleAppliedTags_ShouldReturnFilteredResults()
        {
            var article = await CreateTestArticle();
            var tag1 = await CreateTestTag();
            var tag2 = await CreateTestTag();

            var articleTags = new[]
            {
                new ArticleTag { ArticleId = article.Id, TagId = tag1.Id, AppliedBy = "rule", RuleId = DEFAULT_RULE_ID, AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = article.Id, TagId = tag2.Id, AppliedBy = "user", AppliedAt = DateTime.UtcNow }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetTagsAppliedByRuleAsync(DEFAULT_RULE_ID);

            result.Should().HaveCount(1);
            result[0].TagId.Should().Be(tag1.Id);
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task GetAllAsync_WithLargeDataset_ShouldReturnAllAssociations()
        {
            await SeedTestArticleTagsAsync(25);
            ClearEntityTracking();

            var result = await _repository.GetAllAsync();

            result.Should().HaveCount(25);
        }

        #endregion

        #region Insert Multiple Entities Test

        [Fact]
        public async Task InsertAsync_MultipleEntities_ShouldSetIdCorrectly()
        {
            ClearTestData();

            var article1 = await CreateTestArticle();
            var article2 = await CreateTestArticle();
            var tag1 = await CreateTestTag();
            var tag2 = await CreateTestTag();

            var articleTag1 = new ArticleTag
            {
                ArticleId = article1.Id,
                TagId = tag1.Id,
                AppliedBy = "user",
                AppliedAt = DateTime.UtcNow
            };

            var articleTag2 = new ArticleTag
            {
                ArticleId = article2.Id,
                TagId = tag2.Id,
                AppliedBy = "user",
                AppliedAt = DateTime.UtcNow
            };

            var result1 = await _repository.InsertAsync(articleTag1);
            var result2 = await _repository.InsertAsync(articleTag2);

            result1.Should().Be(1);
            result2.Should().Be(1);

            articleTag1.Id.Should().Be(0);
            articleTag2.Id.Should().Be(0);

            ClearEntityTracking();

            var exists1 = await _repository.ExistsAsync(article1.Id, tag1.Id);
            var exists2 = await _repository.ExistsAsync(article2.Id, tag2.Id);

            exists1.Should().BeTrue();
            exists2.Should().BeTrue();
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _mockLogger?.Reset();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}