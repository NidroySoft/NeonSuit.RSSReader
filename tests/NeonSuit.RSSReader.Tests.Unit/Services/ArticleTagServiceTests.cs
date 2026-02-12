using Moq;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    /// <summary>
    /// Professional test suite for the ArticleTagService class.
    /// Tests all public methods with various scenarios including edge cases.
    /// Verifies tag-article association logic and event handling.
    /// </summary>
    public class ArticleTagServiceTests
    {
        private readonly Mock<IArticleTagRepository> _mockArticleTagRepository;
        private readonly Mock<ITagService> _mockTagService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly ArticleTagService _articleTagService;

        private ArticleTaggedEventArgs? _capturedTaggedEventArgs;
        private ArticleUntaggedEventArgs? _capturedUntaggedEventArgs;

        /// <summary>
        /// Initializes test dependencies before each test.
        /// Creates mock repositories and instantiates the ArticleTagService.
        /// </summary>
        public ArticleTagServiceTests()
        {
            _mockArticleTagRepository = new Mock<IArticleTagRepository>();
            _mockTagService = new Mock<ITagService>();
            _mockLogger = new Mock<ILogger>();
            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
               .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.ForContext(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                       .Returns(_mockLogger.Object);

            _articleTagService = new ArticleTagService(
                _mockArticleTagRepository.Object,
                _mockTagService.Object,
                _mockLogger.Object);



            // Capture events for verification
            _articleTagService.OnArticleTagged += (sender, args) => _capturedTaggedEventArgs = args;
            _articleTagService.OnArticleUntagged += (sender, args) => _capturedUntaggedEventArgs = args;
        }

        #region Test Data Setup

        /// <summary>
        /// Creates a test Tag instance with realistic data.
        /// </summary>
        /// <param name="id">The tag identifier.</param>
        /// <param name="usageCount">The tag usage count.</param>
        /// <returns>A configured Tag instance for testing.</returns>
        private Tag CreateTestTag(int id = 1, int usageCount = 0)
        {
            return new Tag
            {
                Id = id,
                Name = $"Test Tag {id}",
                Color = id % 2 == 0 ? "#FF0000" : "#00FF00",
                UsageCount = usageCount,
                CreatedAt = DateTime.UtcNow.AddDays(-id)
            };
        }

        /// <summary>
        /// Creates a test ArticleTag instance with realistic data.
        /// </summary>
        /// <param name="articleId">The article identifier.</param>
        /// <param name="tagId">The tag identifier.</param>
        /// <param name="appliedBy">Who applied the tag.</param>
        /// <param name="ruleId">Optional rule identifier.</param>
        /// <param name="confidence">Optional confidence score.</param>
        /// <returns>A configured ArticleTag instance for testing.</returns>
        private ArticleTag CreateTestArticleTag(
            int articleId = 1,
            int tagId = 1,
            string appliedBy = "user",
            int? ruleId = null,
            double? confidence = null)
        {
            return new ArticleTag
            {
                ArticleId = articleId,
                TagId = tagId,
                AppliedBy = appliedBy,
                RuleId = ruleId,
                Confidence = confidence,
                AppliedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates a list of test tags.
        /// </summary>
        /// <param name="count">Number of tags to create.</param>
        /// <returns>A list of test tags.</returns>
        private List<Tag> CreateTestTags(int count)
        {
            return Enumerable.Range(1, count)
                .Select(i => CreateTestTag(i, i * 10))
                .ToList();
        }

        /// <summary>
        /// Creates a list of test article-tag associations.
        /// </summary>
        /// <param name="count">Number of associations to create.</param>
        /// <param name="articleId">The article identifier.</param>
        /// <returns>A list of test ArticleTag instances.</returns>
        private List<ArticleTag> CreateTestArticleTags(int count, int articleId = 1)
        {
            return Enumerable.Range(1, count)
                .Select(i => CreateTestArticleTag(articleId, i))
                .ToList();
        }

        #endregion

        #region TagArticleAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "AssociationOperations")]
        [Trait("Type", "Unit")]
        public async Task TagArticleAsync_WithValidParameters_ReturnsTrueAndRaisesEvent()
        {
            // Arrange
            var articleId = 1;
            var tagId = 2;
            var appliedBy = "user";
            var ruleId = 5;
            var confidence = 0.9;
            var testTag = CreateTestTag(tagId);

            _mockTagService
                .Setup(service => service.GetTagAsync(tagId))
                .ReturnsAsync(testTag);

            _mockArticleTagRepository
                .Setup(repo => repo.AssociateTagWithArticleAsync(articleId, tagId, appliedBy, ruleId, confidence))
                .ReturnsAsync(true);

            _mockTagService
                .Setup(service => service.UpdateTagUsageAsync(tagId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _articleTagService.TagArticleAsync(articleId, tagId, appliedBy, ruleId, confidence);

            // Assert
            Assert.True(result);
            Assert.NotNull(_capturedTaggedEventArgs);
            Assert.Equal(articleId, _capturedTaggedEventArgs!.ArticleId);
            Assert.Equal(tagId, _capturedTaggedEventArgs.TagId);
            Assert.Equal(testTag.Name, _capturedTaggedEventArgs.TagName);
            Assert.Equal(appliedBy, _capturedTaggedEventArgs.AppliedBy);
            Assert.Equal(ruleId, _capturedTaggedEventArgs.RuleId);

            _mockTagService.Verify(service => service.GetTagAsync(tagId), Times.Once);
            _mockArticleTagRepository.Verify(repo =>
                repo.AssociateTagWithArticleAsync(articleId, tagId, appliedBy, ruleId, confidence), Times.Once);
            _mockTagService.Verify(service => service.UpdateTagUsageAsync(tagId), Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "AssociationOperations")]
        [Trait("Type", "Unit")]
        public async Task TagArticleAsync_WhenAssociationFails_ReturnsFalseAndNoEvent()
        {
            // Arrange
            var articleId = 1;
            var tagId = 2;
            var testTag = CreateTestTag(tagId);

            _mockTagService
                .Setup(service => service.GetTagAsync(tagId))
                .ReturnsAsync(testTag);

            _mockArticleTagRepository
                .Setup(repo => repo.AssociateTagWithArticleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null, null))
                .ReturnsAsync(false);

            // Reset captured event
            _capturedTaggedEventArgs = null;

            // Act
            var result = await _articleTagService.TagArticleAsync(articleId, tagId);

            // Assert
            Assert.False(result);
            Assert.Null(_capturedTaggedEventArgs);
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "ErrorHandling")]
        [Trait("Type", "Exception")]
        public async Task TagArticleAsync_WhenTagServiceThrowsException_PropagatesException()
        {
            // Arrange
            var articleId = 1;
            var tagId = 2;
            var expectedException = new InvalidOperationException("Tag not found");

            _mockTagService
                .Setup(service => service.GetTagAsync(tagId))
                .ThrowsAsync(expectedException);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _articleTagService.TagArticleAsync(articleId, tagId));

            _mockArticleTagRepository.Verify(repo =>
                repo.AssociateTagWithArticleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null, null), Times.Never);
        }

        #endregion

        #region UntagArticleAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "AssociationOperations")]
        [Trait("Type", "Unit")]
        public async Task UntagArticleAsync_WithValidParameters_ReturnsTrueAndRaisesEvent()
        {
            // Arrange
            var articleId = 1;
            var tagId = 2;
            var testTag = CreateTestTag(tagId);

            _mockTagService
                .Setup(service => service.GetTagAsync(tagId))
                .ReturnsAsync(testTag);

            _mockArticleTagRepository
                .Setup(repo => repo.RemoveTagFromArticleAsync(articleId, tagId))
                .ReturnsAsync(true);

            // Act
            var result = await _articleTagService.UntagArticleAsync(articleId, tagId);

            // Assert
            Assert.True(result);
            Assert.NotNull(_capturedUntaggedEventArgs);
            Assert.Equal(articleId, _capturedUntaggedEventArgs!.ArticleId);
            Assert.Equal(tagId, _capturedUntaggedEventArgs.TagId);
            Assert.Equal(testTag.Name, _capturedUntaggedEventArgs.TagName);

            _mockTagService.Verify(service => service.GetTagAsync(tagId), Times.Once);
            _mockArticleTagRepository.Verify(repo => repo.RemoveTagFromArticleAsync(articleId, tagId), Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "AssociationOperations")]
        [Trait("Type", "Unit")]
        public async Task UntagArticleAsync_WhenRemovalFails_ReturnsFalseAndNoEvent()
        {
            // Arrange
            var articleId = 1;
            var tagId = 2;
            var testTag = CreateTestTag(tagId);

            _mockTagService
                .Setup(service => service.GetTagAsync(tagId))
                .ReturnsAsync(testTag);

            _mockArticleTagRepository
                .Setup(repo => repo.RemoveTagFromArticleAsync(articleId, tagId))
                .ReturnsAsync(false);

            // Reset captured event
            _capturedUntaggedEventArgs = null;

            // Act
            var result = await _articleTagService.UntagArticleAsync(articleId, tagId);

            // Assert
            Assert.False(result);
            Assert.Null(_capturedUntaggedEventArgs);
        }

        #endregion

        #region IsArticleTaggedAsync Tests

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "AssociationOperations")]
        [Trait("Type", "Unit")]
        public async Task IsArticleTaggedAsync_WhenCalled_ReturnsRepositoryResult(bool exists)
        {
            // Arrange
            var articleId = 1;
            var tagId = 2;

            _mockArticleTagRepository
                .Setup(repo => repo.ExistsAsync(articleId, tagId))
                .ReturnsAsync(exists);

            // Act
            var result = await _articleTagService.IsArticleTaggedAsync(articleId, tagId);

            // Assert
            Assert.Equal(exists, result);
            _mockArticleTagRepository.Verify(repo => repo.ExistsAsync(articleId, tagId), Times.Once);
        }

        #endregion

        #region TagArticleWithMultipleAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "BulkOperations")]
        [Trait("Type", "Unit")]
        public async Task TagArticleWithMultipleAsync_WithMultipleTags_ReturnsSuccessCount()
        {
            // Arrange
            var articleId = 1;
            var tagIds = new List<int> { 1, 2, 3, 4 };
            var appliedBy = "user";
            var ruleId = 5;

            // Setup TagArticleAsync to succeed for tags 1, 2, 3 and fail for 4
            var setupSequence = _mockArticleTagRepository.SetupSequence(
                repo => repo.AssociateTagWithArticleAsync(
                    articleId, It.IsAny<int>(), appliedBy, ruleId, null));

            setupSequence.ReturnsAsync(true); // Tag 1
            setupSequence.ReturnsAsync(true); // Tag 2
            setupSequence.ReturnsAsync(true); // Tag 3
            setupSequence.ReturnsAsync(false); // Tag 4

            foreach (var tagId in tagIds)
            {
                _mockTagService
                    .Setup(service => service.GetTagAsync(tagId))
                    .ReturnsAsync(CreateTestTag(tagId));
            }

            _mockTagService
                .Setup(service => service.UpdateTagUsageAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _articleTagService.TagArticleWithMultipleAsync(articleId, tagIds, appliedBy, ruleId);

            // Assert
            Assert.Equal(3, result); // 3 successful, 1 failed
            _mockArticleTagRepository.Verify(repo =>
                repo.AssociateTagWithArticleAsync(articleId, It.IsAny<int>(), appliedBy, ruleId, null),
                Times.Exactly(4));
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "BulkOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task TagArticleWithMultipleAsync_WithEmptyTagList_ReturnsZero()
        {
            // Arrange
            var articleId = 1;
            var tagIds = Enumerable.Empty<int>();

            // Act
            var result = await _articleTagService.TagArticleWithMultipleAsync(articleId, tagIds);

            // Assert
            Assert.Equal(0, result);
            _mockArticleTagRepository.Verify(repo =>
                repo.AssociateTagWithArticleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null, null),
                Times.Never);
        }

        #endregion

        #region UntagArticleMultipleAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "BulkOperations")]
        [Trait("Type", "Unit")]
        public async Task UntagArticleMultipleAsync_WithMultipleTags_ReturnsSuccessCount()
        {
            // Arrange
            var articleId = 1;
            var tagIds = new List<int> { 1, 2, 3, 4 };

            // Setup UntagArticleAsync to succeed for tags 1, 3 and fail for 2, 4
            var setupSequence = _mockArticleTagRepository.SetupSequence(
                repo => repo.RemoveTagFromArticleAsync(articleId, It.IsAny<int>()));

            setupSequence.ReturnsAsync(true); // Tag 1
            setupSequence.ReturnsAsync(false); // Tag 2
            setupSequence.ReturnsAsync(true); // Tag 3
            setupSequence.ReturnsAsync(false); // Tag 4

            foreach (var tagId in tagIds)
            {
                _mockTagService
                    .Setup(service => service.GetTagAsync(tagId))
                    .ReturnsAsync(CreateTestTag(tagId));
            }

            // Act
            var result = await _articleTagService.UntagArticleMultipleAsync(articleId, tagIds);

            // Assert
            Assert.Equal(2, result); // 2 successful, 2 failed
            _mockArticleTagRepository.Verify(repo => repo.RemoveTagFromArticleAsync(articleId, It.IsAny<int>()),
                Times.Exactly(4));
        }

        #endregion

        #region ReplaceArticleTagsAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "BulkOperations")]
        [Trait("Type", "Unit")]
        public async Task ReplaceArticleTagsAsync_WithDifferentTagSets_ReplacesTagsCorrectly()
        {
            // Arrange
            var articleId = 1;
            var currentTags = CreateTestTags(3);
            var currentTagIds = currentTags.Select(t => t.Id).ToList();
            var newTagIds = new List<int> { 2, 4, 5 }; // Keep 2, remove 1,3, add 4,5

            _mockArticleTagRepository
                .Setup(repo => repo.GetTagsForArticleWithDetailsAsync(articleId))
                .ReturnsAsync(currentTags);

            // Setup untagging for tags 1 and 3
            _mockArticleTagRepository
                .Setup(repo => repo.RemoveTagFromArticleAsync(articleId, 1))
                .ReturnsAsync(true);
            _mockArticleTagRepository
                .Setup(repo => repo.RemoveTagFromArticleAsync(articleId, 3))
                .ReturnsAsync(true);

            // Setup tagging for tags 4 and 5
            var setupSequence = _mockArticleTagRepository.SetupSequence(
                repo => repo.AssociateTagWithArticleAsync(articleId, It.IsAny<int>(), "user", null, null));

            setupSequence.ReturnsAsync(true); // Tag 4
            setupSequence.ReturnsAsync(true); // Tag 5

            // Setup tag service for all tag lookups
            foreach (var tagId in currentTagIds.Concat(newTagIds).Distinct())
            {
                _mockTagService
                    .Setup(service => service.GetTagAsync(tagId))
                    .ReturnsAsync(CreateTestTag(tagId));
            }

            _mockTagService
                .Setup(service => service.UpdateTagUsageAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _articleTagService.ReplaceArticleTagsAsync(articleId, newTagIds, "user");

            // Assert
            Assert.Equal(2, result); // Added 2 new tags
            _mockArticleTagRepository.Verify(repo => repo.GetTagsForArticleWithDetailsAsync(articleId), Times.Once);
            _mockArticleTagRepository.Verify(repo => repo.RemoveTagFromArticleAsync(articleId, 1), Times.Once);
            _mockArticleTagRepository.Verify(repo => repo.RemoveTagFromArticleAsync(articleId, 3), Times.Once);
            _mockArticleTagRepository.Verify(repo =>
                repo.AssociateTagWithArticleAsync(articleId, 4, "user", null, null), Times.Once);
            _mockArticleTagRepository.Verify(repo =>
                repo.AssociateTagWithArticleAsync(articleId, 5, "user", null, null), Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "BulkOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task ReplaceArticleTagsAsync_WithSameTagSets_ReturnsZero()
        {
            // Arrange
            var articleId = 1;
            var currentTags = CreateTestTags(3);
            var currentTagIds = currentTags.Select(t => t.Id).ToList();
            var newTagIds = currentTagIds; // Same tags

            _mockArticleTagRepository
                .Setup(repo => repo.GetTagsForArticleWithDetailsAsync(articleId))
                .ReturnsAsync(currentTags);

            // Act
            var result = await _articleTagService.ReplaceArticleTagsAsync(articleId, newTagIds, "user");

            // Assert
            Assert.Equal(0, result); // No changes
            _mockArticleTagRepository.Verify(repo => repo.RemoveTagFromArticleAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _mockArticleTagRepository.Verify(repo =>
                repo.AssociateTagWithArticleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null, null), Times.Never);
        }

        #endregion

        #region GetTagsForArticleAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "Unit")]
        public async Task GetTagsForArticleAsync_WhenCalled_ReturnsTagsFromRepository()
        {
            // Arrange
            var articleId = 1;
            var expectedTags = CreateTestTags(3);

            _mockArticleTagRepository
                .Setup(repo => repo.GetTagsForArticleWithDetailsAsync(articleId))
                .ReturnsAsync(expectedTags);

            // Act
            var result = await _articleTagService.GetTagsForArticleAsync(articleId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedTags.Count, result.Count);
            Assert.Equal(expectedTags[0].Id, result[0].Id);
            Assert.Equal(expectedTags[0].Name, result[0].Name);
            _mockArticleTagRepository.Verify(repo => repo.GetTagsForArticleWithDetailsAsync(articleId), Times.Once);
        }

        #endregion

        #region GetArticleTagAssociationsAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "Unit")]
        public async Task GetArticleTagAssociationsAsync_WhenCalled_ReturnsAssociationsFromRepository()
        {
            // Arrange
            var articleId = 1;
            var expectedAssociations = CreateTestArticleTags(3, articleId);

            _mockArticleTagRepository
                .Setup(repo => repo.GetByArticleIdAsync(articleId))
                .ReturnsAsync(expectedAssociations);

            // Act
            var result = await _articleTagService.GetArticleTagAssociationsAsync(articleId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedAssociations.Count, result.Count);
            Assert.All(result, association => Assert.Equal(articleId, association.ArticleId));
            _mockArticleTagRepository.Verify(repo => repo.GetByArticleIdAsync(articleId), Times.Once);
        }

        #endregion

        #region GetTagUsageCountsAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "Statistics")]
        [Trait("Type", "Unit")]
        public async Task GetTagUsageCountsAsync_WhenCalled_ReturnsUsageCountsFromTagService()
        {
            // Arrange
            var popularTags = CreateTestTags(3);
            popularTags[0].UsageCount = 10;
            popularTags[1].UsageCount = 20;
            popularTags[2].UsageCount = 30;

            var expectedDictionary = popularTags.ToDictionary(t => t.Id, t => t.UsageCount);

            _mockTagService
                .Setup(service => service.GetPopularTagsAsync(int.MaxValue))
                .ReturnsAsync(popularTags);

            // Act
            var result = await _articleTagService.GetTagUsageCountsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedDictionary.Count, result.Count);
            Assert.Equal(expectedDictionary[1], result[1]);
            Assert.Equal(expectedDictionary[2], result[2]);
            Assert.Equal(expectedDictionary[3], result[3]);
            _mockTagService.Verify(service => service.GetPopularTagsAsync(int.MaxValue), Times.Once);
        }

        #endregion

        #region GetMostUsedTagsAsync Tests

        [Theory]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(20)]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "Statistics")]
        [Trait("Type", "Unit")]
        public async Task GetMostUsedTagsAsync_WithDifferentLimits_ReturnsTagsFromTagService(int limit)
        {
            // Arrange
            var expectedTags = CreateTestTags(limit);

            _mockTagService
                .Setup(service => service.GetPopularTagsAsync(limit))
                .ReturnsAsync(expectedTags);

            // Act
            var result = await _articleTagService.GetMostUsedTagsAsync(limit);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(limit, result.Count);
            _mockTagService.Verify(service => service.GetPopularTagsAsync(limit), Times.Once);
        }

        #endregion

        #region GetTaggingStatisticsAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "Statistics")]
        [Trait("Type", "Unit")]
        public async Task GetTaggingStatisticsAsync_WithoutDateFilter_ReturnsAllStatistics()
        {
            // Arrange
            var associations = new List<ArticleTag>
            {
                CreateTestArticleTag(1, 1, "user"),
                CreateTestArticleTag(1, 2, "rule"),
                CreateTestArticleTag(2, 1, "system"),
                CreateTestArticleTag(2, 3, "user"),
                CreateTestArticleTag(3, 2, "rule")
            };

            _mockArticleTagRepository
                .Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(associations);

            // Act
            var result = await _articleTagService.GetTaggingStatisticsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(4, result.Count);
            Assert.Equal(5, result["total_associations"]);
            Assert.Equal(2, result["user_applied"]);
            Assert.Equal(2, result["rule_applied"]);
            Assert.Equal(1, result["system_applied"]);
            _mockArticleTagRepository.Verify(repo => repo.GetAllAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "Statistics")]
        [Trait("Type", "Unit")]
        public async Task GetTaggingStatisticsAsync_WithDateFilter_ReturnsFilteredStatistics()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            var associations = new List<ArticleTag>
            {
                new ArticleTag { ArticleId = 1, TagId = 1, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddDays(-10) },
                new ArticleTag { ArticleId = 1, TagId = 2, AppliedBy = "user", AppliedAt = DateTime.UtcNow.AddDays(-3) },
                new ArticleTag { ArticleId = 2, TagId = 1, AppliedBy = "rule", AppliedAt = DateTime.UtcNow.AddDays(-1) }
            };

            _mockArticleTagRepository
                .Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(associations);

            // Act
            var result = await _articleTagService.GetTaggingStatisticsAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result["total_associations"]); // Only 2 within date range
            Assert.Equal(1, result["user_applied"]);
            Assert.Equal(1, result["rule_applied"]);
        }

        #endregion

        #region ApplyRuleTaggingAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "RuleOperations")]
        [Trait("Type", "Unit")]
        public async Task ApplyRuleTaggingAsync_WithMultipleArticlesAndTags_ReturnsTotalApplied()
        {
            // Arrange
            var ruleId = 1;
            var articleIds = new List<int> { 1, 2, 3 };
            var tagIds = new List<int> { 10, 20 };
            var confidence = 0.85;

            // Setup TagArticleAsync to succeed for all combinations
            _mockArticleTagRepository
                .Setup(repo => repo.AssociateTagWithArticleAsync(
                    It.IsAny<int>(), It.IsAny<int>(), "rule", ruleId, confidence))
                .ReturnsAsync(true);

            foreach (var tagId in tagIds)
            {
                _mockTagService
                    .Setup(service => service.GetTagAsync(tagId))
                    .ReturnsAsync(CreateTestTag(tagId));
            }

            _mockTagService
                .Setup(service => service.UpdateTagUsageAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _articleTagService.ApplyRuleTaggingAsync(ruleId, articleIds, tagIds, confidence);

            // Assert
            var expectedTotal = articleIds.Count * tagIds.Count; // 3 * 2 = 6
            Assert.Equal(expectedTotal, result);
            _mockArticleTagRepository.Verify(repo =>
                repo.AssociateTagWithArticleAsync(It.IsAny<int>(), It.IsAny<int>(), "rule", ruleId, confidence),
                Times.Exactly(expectedTotal));
        }

        #endregion

        #region RemoveRuleTagsAsync Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "RuleOperations")]
        [Trait("Type", "Unit")]
        public async Task RemoveRuleTagsAsync_WithRuleTags_RemovesAllAndReturnsCount()
        {
            // Arrange
            var ruleId = 1;
            var ruleTags = new List<ArticleTag>
            {
                CreateTestArticleTag(1, 1, "rule", ruleId),
                CreateTestArticleTag(1, 2, "rule", ruleId),
                CreateTestArticleTag(2, 1, "rule", ruleId)
            };

            _mockArticleTagRepository
                .Setup(repo => repo.GetTagsAppliedByRuleAsync(ruleId))
                .ReturnsAsync(ruleTags);

            _mockArticleTagRepository
                .Setup(repo => repo.RemoveTagFromArticleAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(true);

            foreach (var tagId in ruleTags.Select(rt => rt.TagId).Distinct())
            {
                _mockTagService
                    .Setup(service => service.GetTagAsync(tagId))
                    .ReturnsAsync(CreateTestTag(tagId));
            }

            // Act
            var result = await _articleTagService.RemoveRuleTagsAsync(ruleId);

            // Assert
            Assert.Equal(ruleTags.Count, result);
            _mockArticleTagRepository.Verify(repo => repo.GetTagsAppliedByRuleAsync(ruleId), Times.Once);
            _mockArticleTagRepository.Verify(repo => repo.RemoveTagFromArticleAsync(It.IsAny<int>(), It.IsAny<int>()),
                Times.Exactly(ruleTags.Count));
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "RuleOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task RemoveRuleTagsAsync_WhenNoRuleTagsExist_ReturnsZero()
        {
            // Arrange
            var ruleId = 99;

            _mockArticleTagRepository
                .Setup(repo => repo.GetTagsAppliedByRuleAsync(ruleId))
                .ReturnsAsync(new List<ArticleTag>());

            // Act
            var result = await _articleTagService.RemoveRuleTagsAsync(ruleId);

            // Assert
            Assert.Equal(0, result);
            _mockArticleTagRepository.Verify(repo => repo.GetTagsAppliedByRuleAsync(ruleId), Times.Once);
            _mockArticleTagRepository.Verify(repo => repo.RemoveTagFromArticleAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        #endregion

        #region Unimplemented Method Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "Placeholder")]
        public async Task GetArticlesWithTagAsync_ReturnsEmptyList_AsNotImplemented()
        {
            // Act
            var result = await _articleTagService.GetArticlesWithTagAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _mockLogger.Verify(log => log.Debug(
                "GetArticlesWithTagAsync requires ArticleRepository implementation"),
                Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "Placeholder")]
        public async Task GetArticlesWithTagNameAsync_ReturnsEmptyList_AsNotImplemented()
        {
            // Act
            var result = await _articleTagService.GetArticlesWithTagNameAsync("test");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _mockLogger.Verify(log => log.Debug(
                "GetArticlesWithTagNameAsync requires ArticleRepository implementation"),
                Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "Placeholder")]
        public async Task GetRecentlyTaggedArticlesAsync_ReturnsEmptyList_AsNotImplemented()
        {
            // Act
            var result = await _articleTagService.GetRecentlyTaggedArticlesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _mockLogger.Verify(log => log.Debug(
                "GetRecentlyTaggedArticlesAsync requires ArticleRepository implementation"),
                Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "MaintenanceOperations")]
        [Trait("Type", "Placeholder")]
        public async Task CleanupOrphanedAssociationsAsync_ReturnsZero_AsNotImplemented()
        {
            // Act
            var result = await _articleTagService.CleanupOrphanedAssociationsAsync();

            // Assert
            Assert.Equal(0, result);
            _mockLogger.Verify(log => log.Debug(
                "CleanupOrphanedAssociationsAsync requires ArticleRepository implementation"),
                Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "MaintenanceOperations")]
        [Trait("Type", "Placeholder")]
        public async Task RecalculateTagUsageCountsAsync_ReturnsZero_AsNotImplemented()
        {
            // Act
            var result = await _articleTagService.RecalculateTagUsageCountsAsync();

            // Assert
            Assert.Equal(0, result);
            _mockLogger.Verify(log => log.Debug(
                "RecalculateTagUsageCountsAsync requires bulk update implementation"),
                Times.Once);
        }

        #endregion

        #region Integration-Style Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "Integration")]
        [Trait("Type", "Integration")]
        public async Task CompleteTagWorkflow_TagReplaceAndUntag_ExecutesSuccessfully()
        {
            // Arrange
            var articleId = 1;
            var initialTags = CreateTestTags(2);
            var newTagIds = new List<int> { 2, 3, 4 };
            var ruleId = 5;

            // Setup initial tags for article
            _mockArticleTagRepository
                .Setup(repo => repo.GetTagsForArticleWithDetailsAsync(articleId))
                .ReturnsAsync(initialTags);

            // Setup untagging for tag 1
            _mockArticleTagRepository
                .Setup(repo => repo.RemoveTagFromArticleAsync(articleId, 1))
                .ReturnsAsync(true);

            // Setup tagging for tags 3 and 4
            _mockArticleTagRepository
                .Setup(repo => repo.AssociateTagWithArticleAsync(articleId, 3, "user", null, null))
                .ReturnsAsync(true);
            _mockArticleTagRepository
                .Setup(repo => repo.AssociateTagWithArticleAsync(articleId, 4, "user", null, null))
                .ReturnsAsync(true);

            // Setup rule tagging
            _mockArticleTagRepository
                .Setup(repo => repo.AssociateTagWithArticleAsync(articleId, 5, "rule", ruleId, 0.8))
                .ReturnsAsync(true);

            // Setup untagging by rule
            _mockArticleTagRepository
                .Setup(repo => repo.GetTagsAppliedByRuleAsync(ruleId))
                .ReturnsAsync(new List<ArticleTag>
                {
                    new ArticleTag { ArticleId = articleId, TagId = 5, AppliedBy = "rule", RuleId = ruleId }
                });
            _mockArticleTagRepository
                .Setup(repo => repo.RemoveTagFromArticleAsync(articleId, 5))
                .ReturnsAsync(true);

            // Setup tag service for all tag lookups
            foreach (var tagId in Enumerable.Range(1, 5))
            {
                _mockTagService
                    .Setup(service => service.GetTagAsync(tagId))
                    .ReturnsAsync(CreateTestTag(tagId));
            }

            _mockTagService
                .Setup(service => service.UpdateTagUsageAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            // Act - Simulate complete tag workflow
            var initialTagsResult = await _articleTagService.GetTagsForArticleAsync(articleId);
            var replaceResult = await _articleTagService.ReplaceArticleTagsAsync(articleId, newTagIds, "user");
            var ruleTagResult = await _articleTagService.TagArticleAsync(articleId, 5, "rule", ruleId, 0.8);
            var removeRuleResult = await _articleTagService.RemoveRuleTagsAsync(ruleId);

            // Assert
            Assert.Equal(2, initialTagsResult.Count);
            Assert.Equal(2, replaceResult); // Added 2 new tags (3 and 4)
            Assert.True(ruleTagResult);
            Assert.Equal(1, removeRuleResult);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        [Trait("Category", "ArticleTagService")]
        [Trait("Scope", "ErrorHandling")]
        [Trait("Type", "Exception")]
        public async Task AnyServiceMethod_WhenRepositoryThrowsException_PropagatesException()
        {
            // Arrange
            var articleId = 1;
            var expectedException = new InvalidOperationException("Database connection failed");

            _mockArticleTagRepository
                .Setup(repo => repo.GetTagsForArticleWithDetailsAsync(articleId))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _articleTagService.GetTagsForArticleAsync(articleId));

            Assert.Equal(expectedException.Message, exception.Message);
        }

        #endregion
    }
}