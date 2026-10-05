using FluentAssertions;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;
using Serilog;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace NeonSuit.RSSReader.Tests.Integration.Services;

[Collection("Integration Tests")]
public class RuleEngineIntegrationTests : IAsyncLifetime
{
    private readonly ILogger _logger;
    private readonly IRuleEngine _ruleEngine;
    private readonly ITestOutputHelper _output;
    private readonly DatabaseFixture _dbFixture;
    private readonly ServiceFactory _factory;
    private readonly IFeedService _feedService;
    private readonly IArticleService _articleService;

    private Feed? _feed;
    private bool _feedReady = false;

    private const string ArsTechnicaFeed = "http://feeds.arstechnica.com/arstechnica/index";

    public RuleEngineIntegrationTests(DatabaseFixture dbFixture, ITestOutputHelper output)
    {
        _dbFixture = dbFixture;
        _logger = dbFixture.Logger;
        _ruleEngine = new RuleEngine(_logger);
        _output = output;

        _factory = new ServiceFactory(_dbFixture);
        _feedService = _factory.CreateFeedService();
        _articleService = _factory.CreateArticleService();
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("⏳ Creando feed para la prueba...");

        _feed = await _feedService.AddFeedAsync(ArsTechnicaFeed);

        var maxRetries = 10;
        for (int i = 0; i < maxRetries; i++)
        {
            var articles = await _articleService.GetArticlesByFeedAsync(_feed.Id);
            if (articles.Any())
            {
                _output.WriteLine($"✅ Feed listo con {articles.Count} artículos");
                _feedReady = true;
                return;
            }

            _output.WriteLine($"⏳ Esperando artículos... intento {i + 1}/{maxRetries}");
            await Task.Delay(1000);
        }

        throw new InvalidOperationException("El feed no tiene artículos después de 10 intentos");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(Article article, string keyword)> GetArticleWithKeywordAsync()
    {
        if (!_feedReady || _feed == null)
            await InitializeAsync();

        var articles = await _articleService.GetArticlesByFeedAsync(_feed!.Id);
        var article = articles.First();

        var words = article.Title.Split(new[] { ' ', '.', ',', '!', '?', ':', ';', '-', '_' },
            StringSplitOptions.RemoveEmptyEntries);

        var keyword = words.FirstOrDefault(w => w.Length > 3) ?? words.First();

        _output.WriteLine($"📌 Artículo: {article.Title}");
        _output.WriteLine($"🔑 Palabra: '{keyword}'");

        return (article, keyword);
    }

    #region Single Condition Evaluation with Real Articles

    [Fact]
    public async Task EvaluateCondition_WithTitleContainsKeyword_ShouldReturnTrue()
    {
        // Arrange
        var (article, keyword) = await GetArticleWithKeywordAsync();

        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = keyword,
            IsCaseSensitive = false
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, condition);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateCondition_WithCaseSensitiveMismatch_ShouldReturnFalse()
    {
        // Arrange
        var (article, keyword) = await GetArticleWithKeywordAsync();

        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = keyword.ToLower(),
            IsCaseSensitive = true
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, condition);

        // Assert
        if (keyword.Any(char.IsUpper))
            result.Should().BeFalse();
        else
            result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateCondition_WithNotContainsOperator_ShouldReturnFalseForMatchingText()
    {
        // Arrange
        var (article, keyword) = await GetArticleWithKeywordAsync();

        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.NotContains,
            Value = keyword,
            IsCaseSensitive = false
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, condition);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateCondition_WithRegexPattern_ShouldMatchPattern()
    {
        // Arrange
        var (article, keyword) = await GetArticleWithKeywordAsync();

        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Regex,
            RegexPattern = $@"\b{Regex.Escape(keyword)}\b",
            IsCaseSensitive = false
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, condition);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateCondition_WithIsEmptyOperator_ShouldReturnFalseForNonEmptyContent()
    {
        // Arrange
        var (article, _) = await GetArticleWithKeywordAsync();

        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Content,
            Operator = RuleOperator.IsEmpty
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, condition);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateCondition_WithNegateModifier_ShouldInvertResult()
    {
        // Arrange
        var (article, keyword) = await GetArticleWithKeywordAsync();

        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = keyword,
            IsCaseSensitive = false,
            Negate = true
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, condition);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Advanced Condition Groups

    [Fact]
    public async Task EvaluateConditionGroup_WithAndOperator_BothConditionsMustMatch()
    {
        // Arrange
        var (article, keyword) = await GetArticleWithKeywordAsync();

        var conditions = new List<RuleCondition>
        {
            new RuleCondition
            {
                Field = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = keyword,
                CombineWithNext = LogicalOperator.AND
            },
            new RuleCondition
            {
                Field = RuleFieldTarget.Content,
                Operator = RuleOperator.Contains,
                Value = "the"
            }
        };

        // Act
        var result = _ruleEngine.EvaluateConditionGroup(conditions, article);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateConditionGroup_WithOrOperator_EitherConditionCanMatch()
    {
        // Arrange
        var (article, keyword) = await GetArticleWithKeywordAsync();

        var conditions = new List<RuleCondition>
        {
            new RuleCondition
            {
                Field = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = "ThisPhraseDoesNotExist12345",
                CombineWithNext = LogicalOperator.OR
            },
            new RuleCondition
            {
                Field = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = keyword
            }
        };

        // Act
        var result = _ruleEngine.EvaluateConditionGroup(conditions, article);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateConditionGroup_WithComplexLogic_ShouldEvaluateCorrectly()
    {
        // Arrange
        var (article, keyword) = await GetArticleWithKeywordAsync();

        var conditions = new List<RuleCondition>
        {
            new RuleCondition
            {
                Field = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = keyword,
                CombineWithNext = LogicalOperator.AND
            },
            new RuleCondition
            {
                Field = RuleFieldTarget.Content,
                Operator = RuleOperator.Contains,
                Value = "the",
                CombineWithNext = LogicalOperator.OR
            },
            new RuleCondition
            {
                Field = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = "Ars"
            }
        };

        // Act
        var result = _ruleEngine.EvaluateConditionGroup(conditions, article);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Value Comparison Operators

    [Fact]
    public async Task EvaluateCondition_WithGreaterThanOperator_OnNumericValues_ShouldCompareCorrectly()
    {
        // Arrange
        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.GreaterThan,
            Value = "100"
        };

        var article = new Article
        {
            Title = "200"
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, condition);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateCondition_WithLessThanOperator_OnDateValues_ShouldCompareCorrectly()
    {
        // Arrange
        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.LessThan,
            Value = "2026-02-13"
        };

        var article = new Article
        {
            Title = "2026-02-12"
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, condition);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Regex Edge Cases

    [Fact]
    public void EvaluateCondition_WithInvalidRegexPattern_ShouldReturnFalseAndLogError()
    {
        // Arrange
        var article = new Article { Title = "Test Article" };
        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Regex,
            RegexPattern = "["
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, condition);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateCondition_WithNullRegexPattern_ShouldReturnFalse()
    {
        // Arrange
        var article = new Article { Title = "Test Article" };
        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Regex,
            RegexPattern = null!
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, condition);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Condition Validation

    [Fact]
    public void ValidateCondition_WithValidRegexPattern_ShouldReturnTrue()
    {
        // Arrange
        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Regex,
            RegexPattern = @"\d{4}",
            GroupId = 0,
            Order = 1
        };

        // Act
        var result = _ruleEngine.ValidateCondition(condition);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateCondition_WithInvalidRegexPattern_ShouldReturnFalse()
    {
        // Arrange
        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Regex,
            RegexPattern = "[",
            GroupId = 0,
            Order = 1
        };

        // Act
        var result = _ruleEngine.ValidateCondition(condition);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateCondition_WithMissingValueForContainsOperator_ShouldReturnFalse()
    {
        // Arrange
        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = null!,
            GroupId = 0,
            Order = 1
        };

        // Act
        var result = _ruleEngine.ValidateCondition(condition);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateCondition_WithNegativeGroupId_ShouldReturnFalse()
    {
        // Arrange
        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "test",
            GroupId = -1,
            Order = 1
        };

        // Act
        var result = _ruleEngine.ValidateCondition(condition);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Null and Edge Cases

    [Fact]
    public void EvaluateCondition_WithNullArticle_ShouldReturnFalse()
    {
        // Arrange
        var condition = new RuleCondition
        {
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "test"
        };

        // Act
        var result = _ruleEngine.EvaluateCondition(null!, condition);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateCondition_WithNullCondition_ShouldReturnFalse()
    {
        // Arrange
        var article = new Article { Title = "Test" };

        // Act
        var result = _ruleEngine.EvaluateCondition(article, null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateConditionGroup_WithEmptyConditions_ShouldReturnTrue()
    {
        // Arrange
        var article = new Article { Title = "Test" };
        var conditions = new List<RuleCondition>();

        // Act
        var result = _ruleEngine.EvaluateConditionGroup(conditions, article);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateConditionGroup_WithNullConditions_ShouldReturnTrue()
    {
        // Arrange
        var article = new Article { Title = "Test" };

        // Act
        var result = _ruleEngine.EvaluateConditionGroup(null!, article);

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}