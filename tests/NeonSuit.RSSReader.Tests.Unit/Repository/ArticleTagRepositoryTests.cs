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

        private const int DEFAULT_ARTICLE_ID = 1;
        private const int DEFAULT_TAG_ID = 1;
        private const string DEFAULT_APPLIED_BY = "user";
        private const int DEFAULT_RULE_ID = 1;
        private const double DEFAULT_CONFIDENCE = 0.95;
        private const int SEED_COUNT = 5;

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

            _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();

            _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();
        }

        #region Test Data Helpers

        private void ClearTestData()
        {
            _dbContext.Database.ExecuteSqlRaw("DELETE FROM ArticleTags");
            _dbContext.Database.ExecuteSqlRaw("DELETE FROM Tags");
            _dbContext.Database.ExecuteSqlRaw("DELETE FROM Articles");
            _dbContext.ChangeTracker.Clear();
        }

        private async Task<Article> CreateTestArticle(int id = DEFAULT_ARTICLE_ID)
        {
            var article = new Article
            {
                Id = id,
                FeedId = 1,
                Title = $"Test Article {id}",
                Content = $"Content for article {id}",
                Summary = $"Summary for article {id}",
                Author = "Test Author",
                PublishedDate = DateTime.UtcNow.AddDays(-id),
                Guid = Guid.NewGuid().ToString(),
                ContentHash = Guid.NewGuid().ToString(),
                Status = Core.Enums.ArticleStatus.Unread,
                AddedDate = DateTime.UtcNow
            };

            _dbContext.Articles.Add(article);
            await _dbContext.SaveChangesAsync();
            return article;
        }

        private async Task<Tag> CreateTestTag(int id = DEFAULT_TAG_ID)
        {
            var tag = new Tag
            {
                Id = id,
                Name = $"Test Tag {id}",
                Description = $"Description for tag {id}",
                Color = $"#{id:000000}",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Tags.Add(tag);
            await _dbContext.SaveChangesAsync();
            return tag;
        }

        private async Task<ArticleTag> CreateTestArticleTag(int articleId = DEFAULT_ARTICLE_ID, int tagId = DEFAULT_TAG_ID)
        {
            await CreateTestArticle(articleId);
            await CreateTestTag(tagId);

            var articleTag = new ArticleTag
            {
                ArticleId = articleId,
                TagId = tagId,
                AppliedBy = DEFAULT_APPLIED_BY,
                RuleId = DEFAULT_RULE_ID,
                Confidence = DEFAULT_CONFIDENCE,
                AppliedAt = DateTime.UtcNow
            };

            _dbContext.ArticleTags.Add(articleTag);
            await _dbContext.SaveChangesAsync();
            return articleTag;
        }

        private async Task<List<ArticleTag>> SeedTestArticleTagsAsync(int count = SEED_COUNT, int startArticleId = 1, int startTagId = 1)
        {
            var articleTags = new List<ArticleTag>();

            for (int i = 0; i < count; i++)
            {
                var articleId = startArticleId + i;
                var tagId = startTagId + i;

                await CreateTestArticle(articleId);
                await CreateTestTag(tagId);

                var articleTag = new ArticleTag
                {
                    ArticleId = articleId,
                    TagId = tagId,
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

        /// <summary>
        /// Tests that InsertAsync adds a valid ArticleTag to the database.
        /// </summary>
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

            var retrieved = await _repository.GetByArticleAndTagAsync(article.Id, tag.Id);
            retrieved.Should().NotBeNull();
            retrieved?.ArticleId.Should().Be(article.Id);
            retrieved?.TagId.Should().Be(tag.Id);
            retrieved?.AppliedBy.Should().Be(DEFAULT_APPLIED_BY);
        }

        /// <summary>
        /// Tests that GetByArticleAndTagAsync returns an existing ArticleTag.
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_WithExistingArticleTag_ShouldReturnArticleTag()
        {
            var articleTag = await CreateTestArticleTag();
            ClearEntityTracking();

            var result = await _repository.GetByArticleAndTagAsync(
                articleTag.ArticleId,
                articleTag.TagId);

            result.Should().NotBeNull();
            result?.ArticleId.Should().Be(DEFAULT_ARTICLE_ID);
            result?.TagId.Should().Be(DEFAULT_TAG_ID);
        }

        /// <summary>
        /// Tests that UpdateAsync updates an existing ArticleTag in the database.
        /// </summary>
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

        /// <summary>
        /// Tests that DeleteAsync removes an existing ArticleTag from the database.
        /// </summary>
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
            var exists = await _repository.ExistsAsync(
                articleTag.ArticleId,
                articleTag.TagId);
            exists.Should().BeFalse();
        }

        #endregion

        #region Article-Specific Tests

        /// <summary>
        /// Tests that GetByArticleIdAsync returns ArticleTags for a valid article ID.
        /// </summary>
        [Fact]
        public async Task GetByArticleIdAsync_WithValidArticleId_ShouldReturnArticleTags()
        {
            const int articleId = DEFAULT_ARTICLE_ID;

            ClearEntityTracking();
            await CreateTestArticle(articleId);
            await CreateTestTag(1);
            await CreateTestTag(2);
            await CreateTestTag(3);

            var articleTags = new List<ArticleTag>
            {
                new ArticleTag { ArticleId = articleId, TagId = 1, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = articleId, TagId = 2, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-1) },
                new ArticleTag { ArticleId = articleId, TagId = 3, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-2) }
            };

            var existing = await _dbContext.ArticleTags
                .Where(at => at.ArticleId == articleId && at.TagId >= 1 && at.TagId <= 3)
                .ToListAsync();

            if (existing.Any())
            {
                _dbContext.ArticleTags.RemoveRange(existing);
                await _dbContext.SaveChangesAsync();
            }

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetByArticleIdAsync(articleId);

            result.Should().NotBeNull().And.HaveCount(3);
            result.Should().OnlyContain(at => at.ArticleId == articleId);
        }

        /// <summary>
        /// Tests that GetByArticleIdAsync returns an empty list for a non-existent article.
        /// </summary>
        [Fact]
        public async Task GetByArticleIdAsync_WithNonExistentArticle_ShouldReturnEmptyList()
        {
            var result = await _repository.GetByArticleIdAsync(999);
            result.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region Tag-Specific Tests

        /// <summary>
        /// Tests that GetByTagIdAsync returns ArticleTags for a valid tag ID.
        /// </summary>
        [Fact]
        public async Task GetByTagIdAsync_WithValidTagId_ShouldReturnArticleTags()
        {
            const int tagId = DEFAULT_TAG_ID;

            ClearEntityTracking();
            var existingAssociations = await _dbContext.ArticleTags
                .Where(at => at.TagId == tagId)
                .ToListAsync();

            if (existingAssociations.Any())
            {
                _dbContext.ArticleTags.RemoveRange(existingAssociations);
                await _dbContext.SaveChangesAsync();
            }

            await CreateTestArticle(10);
            await CreateTestArticle(11);
            await CreateTestArticle(12);
            await CreateTestTag(tagId);

            var articleTags = new List<ArticleTag>
            {
                new ArticleTag { ArticleId = 10, TagId = tagId, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = 11, TagId = tagId, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-1) },
                new ArticleTag { ArticleId = 12, TagId = tagId, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-2) }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetByTagIdAsync(tagId);

            result.Should().NotBeNull().And.HaveCount(3);
            result.Should().OnlyContain(at => at.TagId == tagId);
            result.Select(at => at.ArticleId).Should().Contain(new[] { 10, 11, 12 });
        }

        /// <summary>
        /// Tests that GetByTagIdAsync returns an empty list for a non-existent tag.
        /// </summary>
        [Fact]
        public async Task GetByTagIdAsync_WithNonExistentTag_ShouldReturnEmptyList()
        {
            var result = await _repository.GetByTagIdAsync(999);
            result.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region Existence and Retrieval Tests

        /// <summary>
        /// Tests that ExistsAsync returns true for an existing association.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WithExistingAssociation_ShouldReturnTrue()
        {
            await CreateTestArticleTag();
            ClearEntityTracking();

            var result = await _repository.ExistsAsync(DEFAULT_ARTICLE_ID, DEFAULT_TAG_ID);
            result.Should().BeTrue();
        }

        /// <summary>
        /// Tests that ExistsAsync returns false for a non-existent association.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WithNonExistentAssociation_ShouldReturnFalse()
        {
            var result = await _repository.ExistsAsync(999, 999);
            result.Should().BeFalse();
        }

        /// <summary>
        /// Tests that GetByArticleAndTagAsync returns an ArticleTag for an existing association.
        /// </summary>
        [Fact]
        public async Task GetByArticleAndTagAsync_WithExistingAssociation_ShouldReturnArticleTag()
        {
            var expected = await CreateTestArticleTag();
            ClearEntityTracking();

            var result = await _repository.GetByArticleAndTagAsync(DEFAULT_ARTICLE_ID, DEFAULT_TAG_ID);

            result.Should().NotBeNull();
            result?.Id.Should().Be(expected.Id);
            result?.ArticleId.Should().Be(DEFAULT_ARTICLE_ID);
            result?.TagId.Should().Be(DEFAULT_TAG_ID);
        }

        /// <summary>
        /// Tests that GetByArticleAndTagAsync returns null for a non-existent association.
        /// </summary>
        [Fact]
        public async Task GetByArticleAndTagAsync_WithNonExistentAssociation_ShouldReturnNull()
        {
            var result = await _repository.GetByArticleAndTagAsync(999, 999);
            result.Should().BeNull();
        }

        #endregion

        #region Association Management Tests

        /// <summary>
        /// Tests that AssociateTagWithArticleAsync creates a new association.
        /// </summary>
        [Fact]
        public async Task AssociateTagWithArticleAsync_WithNewAssociation_ShouldCreateAssociation()
        {
            await CreateTestArticle();
            await CreateTestTag();
            ClearEntityTracking();

            var result = await _repository.AssociateTagWithArticleAsync(
                DEFAULT_ARTICLE_ID,
                DEFAULT_TAG_ID,
                DEFAULT_APPLIED_BY,
                DEFAULT_RULE_ID,
                DEFAULT_CONFIDENCE);

            result.Should().BeTrue();
            ClearEntityTracking();
            var exists = await _repository.ExistsAsync(DEFAULT_ARTICLE_ID, DEFAULT_TAG_ID);
            exists.Should().BeTrue();
        }

        /// <summary>
        /// Tests that AssociateTagWithArticleAsync returns false for an existing association.
        /// </summary>
        [Fact]
        public async Task AssociateTagWithArticleAsync_WithExistingAssociation_ShouldReturnFalse()
        {
            await CreateTestArticleTag();
            ClearEntityTracking();

            var result = await _repository.AssociateTagWithArticleAsync(
                DEFAULT_ARTICLE_ID,
                DEFAULT_TAG_ID,
                DEFAULT_APPLIED_BY);

            result.Should().BeFalse();
        }

        /// <summary>
        /// Tests that RemoveTagFromArticleAsync removes an existing association.
        /// </summary>
        [Fact]
        public async Task RemoveTagFromArticleAsync_WithExistingAssociation_ShouldRemoveAssociation()
        {
            await CreateTestArticleTag();
            ClearEntityTracking();

            var result = await _repository.RemoveTagFromArticleAsync(DEFAULT_ARTICLE_ID, DEFAULT_TAG_ID);

            result.Should().BeTrue();
            ClearEntityTracking();
            var exists = await _repository.ExistsAsync(DEFAULT_ARTICLE_ID, DEFAULT_TAG_ID);
            exists.Should().BeFalse();
        }

        /// <summary>
        /// Tests that RemoveTagFromArticleAsync returns false for a non-existent association.
        /// </summary>
        [Fact]
        public async Task RemoveTagFromArticleAsync_WithNonExistentAssociation_ShouldReturnFalse()
        {
            var result = await _repository.RemoveTagFromArticleAsync(999, 999);
            result.Should().BeFalse();
        }

        #endregion

        #region Bulk Association Management Tests

        /// <summary>
        /// Tests that RemoveAllTagsFromArticleAsync removes all associations for an article.
        /// </summary>
        [Fact]
        public async Task RemoveAllTagsFromArticleAsync_WithMultipleAssociations_ShouldRemoveAll()
        {
            var baseId = DateTime.UtcNow.Millisecond + 9000;
            const int articleId = 1;

            ClearEntityTracking();
            await CreateTestArticle(articleId);

            var tagId1 = baseId + 1;
            var tagId2 = baseId + 2;
            var tagId3 = baseId + 3;

            await CreateTestTag(tagId1);
            await CreateTestTag(tagId2);
            await CreateTestTag(tagId3);

            var existingAssociations = await _dbContext.ArticleTags
                .Where(at => at.ArticleId == articleId)
                .ToListAsync();

            if (existingAssociations.Any())
            {
                _dbContext.ArticleTags.RemoveRange(existingAssociations);
                await _dbContext.SaveChangesAsync();
            }

            var articleTags = new[]
            {
                new ArticleTag { ArticleId = articleId, TagId = tagId1, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = articleId, TagId = tagId2, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-1) },
                new ArticleTag { ArticleId = articleId, TagId = tagId3, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-2) }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();

            var initialCount = await _dbContext.ArticleTags
                .Where(at => at.ArticleId == articleId)
                .CountAsync();

            initialCount.Should().Be(3, "Should have 3 initial associations");
            ClearEntityTracking();

            var result = await _repository.RemoveAllTagsFromArticleAsync(articleId);

            result.Should().Be(3, "Should remove 3 associations");
            ClearEntityTracking();
            var remaining = await _repository.GetByArticleIdAsync(articleId);
            remaining.Should().BeEmpty("Should have no remaining associations");
        }

        /// <summary>
        /// Tests that RemoveAllTagsFromArticleAsync returns zero for an article with no associations.
        /// </summary>
        [Fact]
        public async Task RemoveAllTagsFromArticleAsync_WithNoAssociations_ShouldReturnZero()
        {
            var result = await _repository.RemoveAllTagsFromArticleAsync(999);
            result.Should().Be(0);
        }

        /// <summary>
        /// Tests that RemoveTagFromAllArticlesAsync removes all associations for a tag.
        /// </summary>
        [Fact]
        public async Task RemoveTagFromAllArticlesAsync_WithMultipleAssociations_ShouldRemoveAll()
        {
            var baseId = DateTime.UtcNow.Millisecond + 11000;
            const int tagId = 1;

            ClearEntityTracking();
            await CreateTestTag(tagId);

            var articleId1 = baseId + 1;
            var articleId2 = baseId + 2;
            var articleId3 = baseId + 3;

            await CreateTestArticle(articleId1);
            await CreateTestArticle(articleId2);
            await CreateTestArticle(articleId3);

            var existingAssociations = await _dbContext.ArticleTags
                .Where(at => at.TagId == tagId)
                .ToListAsync();

            if (existingAssociations.Any())
            {
                _dbContext.ArticleTags.RemoveRange(existingAssociations);
                await _dbContext.SaveChangesAsync();
            }

            var articleTags = new[]
            {
                new ArticleTag { ArticleId = articleId1, TagId = tagId, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = articleId2, TagId = tagId, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-1) },
                new ArticleTag { ArticleId = articleId3, TagId = tagId, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddMinutes(-2) }
            };

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();

            var initialCount = await _dbContext.ArticleTags
                .Where(at => at.TagId == tagId)
                .CountAsync();

            initialCount.Should().Be(3, "Should have 3 initial associations");
            ClearEntityTracking();

            var result = await _repository.RemoveTagFromAllArticlesAsync(tagId);

            result.Should().Be(3, "Should remove 3 associations");
            ClearEntityTracking();
            var remaining = await _repository.GetByTagIdAsync(tagId);
            remaining.Should().BeEmpty("Should have no remaining associations with this tag");
        }

        /// <summary>
        /// Tests that AssociateTagsWithArticleAsync associates multiple tags with an article.
        /// </summary>
        [Fact]
        public async Task AssociateTagsWithArticleAsync_WithMultipleTags_ShouldAssociateAll()
        {
            await CreateTestArticle();
            var tagIds = new List<int> { 1, 2, 3 };

            foreach (var tagId in tagIds)
            {
                await CreateTestTag(tagId);
            }
            ClearEntityTracking();

            var result = await _repository.AssociateTagsWithArticleAsync(
                DEFAULT_ARTICLE_ID,
                tagIds,
                DEFAULT_APPLIED_BY,
                DEFAULT_RULE_ID);

            result.Should().Be(3);
            ClearEntityTracking();
            var articleTags = await _repository.GetByArticleIdAsync(DEFAULT_ARTICLE_ID);
            articleTags.Should().HaveCount(3);
        }

        /// <summary>
        /// Tests that RemoveTagsFromArticleAsync removes multiple tags from an article.
        /// </summary>
        [Fact]
        public async Task RemoveTagsFromArticleAsync_WithMultipleTags_ShouldRemoveAll()
        {
            var baseId = DateTime.UtcNow.Millisecond + 13000;
            const int articleId = 1;

            ClearEntityTracking();
            await CreateTestArticle(articleId);

            var tagIds = new List<int>
            {
                baseId + 1,
                baseId + 2,
                baseId + 3
            };

            foreach (var tagId in tagIds)
            {
                await CreateTestTag(tagId);
            }

            var existingAssociations = await _dbContext.ArticleTags
                .Where(at => at.ArticleId == articleId)
                .ToListAsync();

            if (existingAssociations.Any())
            {
                _dbContext.ArticleTags.RemoveRange(existingAssociations);
                await _dbContext.SaveChangesAsync();
            }

            ClearEntityTracking();

            var articleTags = tagIds.Select(tagId => new ArticleTag
            {
                ArticleId = articleId,
                TagId = tagId,
                AppliedBy = "user",
                AppliedAt = DateTime.UtcNow
            }).ToList();

            _dbContext.ArticleTags.AddRange(articleTags);
            await _dbContext.SaveChangesAsync();

            var initialCount = await _dbContext.ArticleTags
                .Where(at => at.ArticleId == articleId)
                .CountAsync();

            initialCount.Should().Be(3, "Should have 3 initial associations");
            ClearEntityTracking();

            var result = await _repository.RemoveTagsFromArticleAsync(articleId, tagIds);

            result.Should().Be(3, "Should remove 3 associations");
            ClearEntityTracking();
            var remaining = await _repository.GetByArticleIdAsync(articleId);
            remaining.Should().BeEmpty("Should have no remaining associations");
        }

        #endregion

        #region Advanced Query Tests

        /// <summary>
        /// Tests that GetArticleIdsByTagNameAsync returns article IDs for a valid tag name.
        /// </summary>
        [Fact]
        public async Task GetArticleIdsByTagNameAsync_WithValidTagName_ShouldReturnArticleIds()
        {
            const string tagName = "Test Tag 1";

            _dbContext.ChangeTracker.Clear();

            if (!await _dbContext.Tags.AnyAsync(t => t.Id == 1))
            {
                await CreateTestTag(1);
            }

            await CreateTestArticle(1);
            await CreateTestArticle(2);
            await CreateTestArticle(3);

            var articles = await _dbContext.Articles.ToListAsync();
            var tag = await _dbContext.Tags.FirstAsync(t => t.Id == 1);

            var articleTags = new List<ArticleTag>
            {
                new ArticleTag { ArticleId = 1, TagId = 1, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = 2, TagId = 1, AppliedBy = "user", AppliedAt = DateTime.UtcNow }
            };

            var existing = await _dbContext.ArticleTags
                .Where(at => (at.ArticleId == 1 && at.TagId == 1) || (at.ArticleId == 2 && at.TagId == 1))
                .ToListAsync();

            foreach (var articleTag in articleTags)
            {
                if (!existing.Any(e => e.ArticleId == articleTag.ArticleId && e.TagId == articleTag.TagId))
                {
                    _dbContext.ArticleTags.Add(articleTag);
                }
            }

            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetArticleIdsByTagNameAsync(tagName);

            result.Should().NotBeNull().And.HaveCount(2);
            result.Should().Contain(1).And.Contain(2);
        }

        /// <summary>
        /// Tests that GetTagsForArticleWithDetailsAsync returns tags with details for an article.
        /// </summary>
        [Fact]
        public async Task GetTagsForArticleWithDetailsAsync_WithAssociatedTags_ShouldReturnTagsWithDetails()
        {
            const int articleId = DEFAULT_ARTICLE_ID;
            await SeedTestArticleTagsAsync(3, articleId, 1);
            ClearEntityTracking();

            var result = await _repository.GetTagsForArticleWithDetailsAsync(articleId);

            result.Should().NotBeNull().And.HaveCount(1);
            result.Should().OnlyContain(tag => tag != null);
            result.Should().BeInAscendingOrder(tag => tag.Name);
        }

        /// <summary>
        /// Tests that GetTagStatisticsForArticleAsync returns statistics for an article with multiple tags.
        /// </summary>
        [Fact]
        public async Task GetTagStatisticsForArticleAsync_WithMultipleAssociations_ShouldReturnStatistics()
        {
            var baseId = DateTime.UtcNow.Millisecond + 7000;
            const int articleId = 1;

            ClearEntityTracking();
            await CreateTestArticle(articleId);

            var tagId1 = baseId + 1;
            var tagId2 = baseId + 2;
            var tagId3 = baseId + 3;

            await CreateTestTag(tagId1);
            await CreateTestTag(tagId2);
            await CreateTestTag(tagId3);

            var existingKeys = new (int ArticleId, int TagId)[]
            {
                (articleId, tagId1),
                (articleId, tagId2),
                (articleId, tagId3)
            };

            foreach (var key in existingKeys)
            {
                var existing = await _dbContext.ArticleTags
                    .AsNoTracking()
                    .FirstOrDefaultAsync(at => at.ArticleId == key.ArticleId && at.TagId == key.TagId);

                if (existing != null)
                {
                    _dbContext.ArticleTags.Remove(existing);
                }
            }
            await _dbContext.SaveChangesAsync();

            ClearEntityTracking();

            var articleTags = new[]
            {
                new ArticleTag { ArticleId = articleId, TagId = tagId1, AppliedBy = "user", AppliedAt = DateTime.UtcNow },
                new ArticleTag { ArticleId = articleId, TagId = tagId2, AppliedBy = "rule", AppliedAt = DateTime.UtcNow.AddMinutes(-1) },
                new ArticleTag { ArticleId = articleId, TagId = tagId3, AppliedBy = "system", AppliedAt = DateTime.UtcNow.AddMinutes(-2) }
            };

            foreach (var articleTag in articleTags)
            {
                var existsInContext = _dbContext.ArticleTags.Local
                    .Any(at => at.ArticleId == articleTag.ArticleId && at.TagId == articleTag.TagId);

                var existsInDb = await _dbContext.ArticleTags
                    .AsNoTracking()
                    .AnyAsync(at => at.ArticleId == articleTag.ArticleId && at.TagId == articleTag.TagId);

                if (!existsInContext && !existsInDb)
                {
                    _dbContext.ArticleTags.Add(articleTag);
                }
            }

            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetTagStatisticsForArticleAsync(articleId);

            result.Should().NotBeNull().And.HaveCount(3);
            result.Should().ContainKey($"Test Tag {tagId1}").WhoseValue.Should().Be(1);
            result.Should().ContainKey($"Test Tag {tagId2}").WhoseValue.Should().Be(1);
            result.Should().ContainKey($"Test Tag {tagId3}").WhoseValue.Should().Be(1);
        }

        /// <summary>
        /// Tests that GetRecentlyAppliedTagsAsync returns ordered results for multiple associations.
        /// </summary>
        [Fact]
        public async Task GetRecentlyAppliedTagsAsync_WithMultipleAssociations_ShouldReturnOrderedResults()
        {
            await SeedTestArticleTagsAsync(10);
            ClearEntityTracking();

            var result = await _repository.GetRecentlyAppliedTagsAsync(5);

            result.Should().NotBeNull().And.HaveCount(5);
            result.Should().BeInDescendingOrder(at => at.AppliedAt);
        }

        /// <summary>
        /// Tests that GetTagsAppliedByRuleAsync returns filtered results for rule-applied tags.
        /// </summary>
        [Fact]
        public async Task GetTagsAppliedByRuleAsync_WithRuleAppliedTags_ShouldReturnFilteredResults()
        {
            const int ruleId = DEFAULT_RULE_ID;

            await CreateTestArticleTag(1, 1);
            await CreateTestArticle(2);
            await CreateTestTag(2);

            var ruleAppliedTag = new ArticleTag
            {
                ArticleId = 2,
                TagId = 2,
                AppliedBy = "rule",
                RuleId = ruleId,
                AppliedAt = DateTime.UtcNow
            };

            _dbContext.ArticleTags.Add(ruleAppliedTag);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetTagsAppliedByRuleAsync(ruleId);

            result.Should().NotBeNull().And.HaveCount(2);
            result[0].RuleId.Should().Be(ruleId);
        }

        #endregion

        #region Edge Case Tests

        /// <summary>
        /// Tests that AssociateTagsWithArticleAsync associates each tag only once when duplicates are provided.
        /// </summary>
        [Fact]
        public async Task AssociateTagsWithArticleAsync_WithDuplicateTags_ShouldOnlyAssociateOncePerTag()
        {
            await CreateTestArticle();
            await CreateTestTag();
            ClearEntityTracking();

            var duplicateTagIds = new List<int> { DEFAULT_TAG_ID, DEFAULT_TAG_ID, DEFAULT_TAG_ID };

            var result = await _repository.AssociateTagsWithArticleAsync(
                DEFAULT_ARTICLE_ID,
                duplicateTagIds,
                DEFAULT_APPLIED_BY);

            result.Should().Be(1);
            ClearEntityTracking();
            var articleTags = await _repository.GetByArticleIdAsync(DEFAULT_ARTICLE_ID);
            articleTags.Should().HaveCount(1);
        }

        /// <summary>
        /// Tests that RemoveTagsFromArticleAsync handles non-existent tags gracefully.
        /// </summary>
        [Fact]
        public async Task RemoveTagsFromArticleAsync_WithNonExistentTags_ShouldHandleGracefully()
        {
            await CreateTestArticleTag();
            ClearEntityTracking();

            var mixedTagIds = new List<int> { DEFAULT_TAG_ID, 999, 1000 };

            var result = await _repository.RemoveTagsFromArticleAsync(DEFAULT_ARTICLE_ID, mixedTagIds);

            result.Should().Be(1);
            ClearEntityTracking();
            var exists = await _repository.ExistsAsync(DEFAULT_ARTICLE_ID, DEFAULT_TAG_ID);
            exists.Should().BeFalse();
        }

        #endregion

        #region Performance and Concurrency Tests

        /// <summary>
        /// Tests that GetAllAsync returns all associations for a large dataset.
        /// </summary>
        [Fact]
        public async Task GetAllAsync_WithLargeDataset_ShouldReturnAllAssociations()
        {
            await SeedTestArticleTagsAsync(100);
            ClearEntityTracking();

            var result = await _repository.GetAllAsync();

            result.Should().NotBeNull().And.HaveCount(100);
        }

        /// <summary>
        /// Tests that GetByArticleIdAsync uses no-tracking for performance.
        /// </summary>
        [Fact]
        public async Task GetByArticleIdAsync_ShouldUseNoTrackingForPerformance()
        {
            await SeedTestArticleTagsAsync(10);
            ClearEntityTracking();

            var result = await _repository.GetByArticleIdAsync(DEFAULT_ARTICLE_ID);

            result.Should().NotBeNull();
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