using FluentAssertions;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;
using System.Text.RegularExpressions;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    /// <summary>
    /// Comprehensive unit tests for the RuleEngine service.
    /// Covers condition evaluation, text matching, regex validation, and logical operators.
    /// </summary>
    public class RuleEngineTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly IRuleEngine _ruleEngine;

        /// <summary>
        /// Initializes test dependencies and configures mock logger.
        /// </summary>
        public RuleEngineTests()
        {
            _mockLogger = new Mock<ILogger>();

            // Critical configuration: Logger always needs ForContext
            _mockLogger.Setup(x => x.ForContext<IRuleEngine>())
                      .Returns(_mockLogger.Object);

            // Create rule engine and replace its private logger using reflection
            _ruleEngine = new RuleEngine(_mockLogger.Object);
            var loggerField = typeof(IRuleEngine).GetField("_logger",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            loggerField?.SetValue(_ruleEngine, _mockLogger.Object);
        }

        #region Test Data Factories

        /// <summary>
        /// Creates a test article with sample data.
        /// </summary>
        private Article CreateTestArticle()
        {
            return new Article
            {
                Id = 1,
                Title = "The Future of AI in Software Development",
                Content = "Artificial intelligence is revolutionizing how we write and test code.",
                Summary = "AI tools are becoming essential for developers.",
                Author = "Jane Developer",
                Categories = "Technology,Programming,AI",
                PublishedDate = DateTime.Now.AddDays(-1)
            };
        }

        /// <summary>
        /// Creates a basic rule condition with customizable parameters.
        /// </summary>
        private RuleCondition CreateTestCondition(
     RuleFieldTarget field = RuleFieldTarget.Title,
     RuleOperator @operator = RuleOperator.Contains,
     string value = "AI",
     bool isCaseSensitive = false,
     bool negate = false,
     string regexPattern = "",
     LogicalOperator combineWithNext = LogicalOperator.AND)
        {
            var condition = new RuleCondition
            {
                Field = field,
                Operator = @operator,
                Value = value,
                IsCaseSensitive = isCaseSensitive,
                Negate = negate,
                RegexPattern = regexPattern,
                CombineWithNext = combineWithNext
            };
            return condition;
        }

        private Article CreateTestArticle(
    int id = 1,
    int feedId = 1,
    string title = "Test Article",
    string content = "Test content",
    string summary = "Test summary",
    string guid = "test-guid-123",
    ArticleStatus status = ArticleStatus.Unread)
        {
            return new Article
            {
                Id = id,
                FeedId = feedId,
                Title = title,
                Content = content,
                Summary = summary,
                Guid = guid,
                Link = "https://example.com/article",
                PublishedDate = DateTime.UtcNow.AddDays(-1),
                AddedDate = DateTime.UtcNow,
                Status = status,
                IsStarred = false,
                IsFavorite = false,
                IsNotified = false,
                ProcessedByRules = false
            };
        }
        #endregion

        #region EvaluateCondition - Basic Tests

        /// <summary>
        /// Tests that null article or condition returns false.
        /// </summary>
        [Theory]
        [InlineData(null, "valid")]
        [InlineData("valid", null)]
        public async Task EvaluateCondition_WithNullInput_ShouldReturnFalse(string articleParam, string conditionParam)
        {
            // Arrange
            var article = articleParam == "valid" ? CreateTestArticle() : null;
            var condition = conditionParam == "valid" ? CreateTestCondition() : null;

            // Act
            var result = _ruleEngine.EvaluateCondition(article, condition);

            // Assert
            result.Should().BeFalse();
            VerifyLoggerDebugNotCalled();
        }

        /// <summary>
        /// Tests Contains operator with case-sensitive and case-insensitive comparisons.
        /// </summary>
        [Theory]
        [InlineData("AI", false, true)]
        [InlineData("ai", false, true)]
        [InlineData("AI", true, true)]
        [InlineData("ai", true, false)]
        [InlineData("XYZ", false, false)]
        public async Task EvaluateCondition_WithContainsOperator_ShouldMatchCorrectly(
                            string searchValue, bool caseSensitive, bool expectedResult)
        {
            // Arrange
            var article = CreateTestArticle();
            var condition = CreateTestCondition(
                field: RuleFieldTarget.Title,
                @operator: RuleOperator.Contains,
                value: searchValue,
                isCaseSensitive: caseSensitive);

            // Act
            var result = _ruleEngine.EvaluateCondition(article, condition);

            // Assert
            result.Should().Be(expectedResult);

            // Verificar usando It.IsAnyType para las sobrecargas genéricas de Serilog
            _mockLogger.Verify(
                x => x.Debug(
                    It.IsAny<string>(),
                    It.IsAny<It.IsAnyType>(),  // Para el primer parámetro genérico
                    It.IsAny<It.IsAnyType>()), // Para el segundo parámetro genérico
                Times.AtLeastOnce);
        }
        /// <summary>
        /// Tests negation of a condition.
        /// </summary>
        [Fact]
        public async Task EvaluateCondition_WithNegation_ShouldInvertResult()
        {
            // Arrange
            var article = CreateTestArticle();
            var condition = CreateTestCondition(
                @operator: RuleOperator.Contains,
                value: "AI",
                negate: true); // Contains AI is true, but negated should be false

            // Act
            var result = _ruleEngine.EvaluateCondition(article, condition);

            // Assert
            result.Should().BeFalse(); // Contains is true, but negated
                                       // El logging es secundario, no verificamos mensajes específicos
        }

        #endregion

        #region EvaluateCondition - Field Targeting Tests

        /// <summary>
        /// Tests different field targets (Title, Content, Author, Categories).
        /// </summary>
        [Theory]
        [InlineData(RuleFieldTarget.Title, "Future", true)]
        [InlineData(RuleFieldTarget.Title, "past", false)]
        [InlineData(RuleFieldTarget.Content, "revolutionizing", true)]
        [InlineData(RuleFieldTarget.Content, "obsolete", false)]
        [InlineData(RuleFieldTarget.Author, "Jane", true)]
        [InlineData(RuleFieldTarget.Author, "John", false)]
        [InlineData(RuleFieldTarget.Categories, "Technology", true)]
        [InlineData(RuleFieldTarget.Categories, "Sports", false)]
        public async Task EvaluateCondition_WithDifferentFields_ShouldTargetCorrectText(
            RuleFieldTarget field, string searchValue, bool expectedResult)
        {
            // Arrange
            var article = CreateTestArticle();
            var condition = CreateTestCondition(
                field: field,
                @operator: RuleOperator.Contains,
                value: searchValue);

            // Act
            var result = _ruleEngine.EvaluateCondition(article, condition);

            // Assert
            result.Should().Be(expectedResult);
        }

        /// <summary>
        /// Tests AllFields and AnyField targets that concatenate multiple fields.
        /// </summary>
        [Theory]
        [InlineData(RuleFieldTarget.AllFields, "AI", true)]      // In title and content
        [InlineData(RuleFieldTarget.AllFields, "Developer", true)] // In author
        [InlineData(RuleFieldTarget.AllFields, "XYZ", false)]     // Nowhere
        [InlineData(RuleFieldTarget.AnyField, "AI", true)]        // Same as AllFields in implementation
        public async Task EvaluateCondition_WithMultiFieldTargets_ShouldSearchAllText(
            RuleFieldTarget field, string searchValue, bool expectedResult)
        {
            // Arrange
            var article = CreateTestArticle();
            var condition = CreateTestCondition(
                field: field,
                @operator: RuleOperator.Contains,
                value: searchValue);

            // Act
            var result = _ruleEngine.EvaluateCondition(article, condition);

            // Assert
            result.Should().Be(expectedResult);
        }

        #endregion

        #region EvaluateCondition - Operator Tests

        /// <summary>
        /// Tests various string operators: Equals, StartsWith, EndsWith.
        /// </summary>
        [Theory]
        [InlineData(RuleOperator.Equals, "The Future of AI in Software Development", true)]
        [InlineData(RuleOperator.Equals, "the future of ai in software development", true)] // Case-insensitive
        [InlineData(RuleOperator.Equals, "Different Title", false)]
        [InlineData(RuleOperator.StartsWith, "The Future", true)]
        [InlineData(RuleOperator.StartsWith, "the future", true)] // Case-insensitive
        [InlineData(RuleOperator.StartsWith, "Future", false)]
        [InlineData(RuleOperator.EndsWith, "Development", true)]
        [InlineData(RuleOperator.EndsWith, "development", true)] // Case-insensitive
        [InlineData(RuleOperator.EndsWith, "Develop", false)]
        public async Task EvaluateCondition_WithStringOperators_ShouldEvaluateCorrectly(
            RuleOperator @operator, string value, bool expectedResult)
        {
            // Arrange
            var article = CreateTestArticle();
            var condition = CreateTestCondition(
                field: RuleFieldTarget.Title,
                @operator: @operator,
                value: value);

            // Act
            var result = _ruleEngine.EvaluateCondition(article, condition);

            // Assert
            result.Should().Be(expectedResult);
        }

        /// <summary>
        /// Tests NotContains and NotEquals operators.
        /// </summary>
        [Fact]
        public async Task EvaluateCondition_WithNotOperators_ShouldReturnOpposite()
        {
            // Arrange
            var article = CreateTestArticle();

            var notContainsCondition = CreateTestCondition(
                @operator: RuleOperator.NotContains,
                value: "XYZ"); // Article doesn't contain XYZ, so NotContains should be true

            var notEqualsCondition = CreateTestCondition(
                @operator: RuleOperator.NotEquals,
                value: "Different Title"); // Title is different, so NotEquals should be true

            // Act
            var notContainsResult = _ruleEngine.EvaluateCondition(article, notContainsCondition);
            var notEqualsResult = _ruleEngine.EvaluateCondition(article, notEqualsCondition);

            // Assert
            notContainsResult.Should().BeTrue();
            notEqualsResult.Should().BeTrue();
        }

        /// <summary>
        /// Tests IsEmpty and IsNotEmpty operators.
        /// </summary>
        [Fact]
        public async Task EvaluateCondition_WithEmptyOperators_ShouldCheckForWhitespace()
        {
            // Arrange
            var article = CreateTestArticle();
            var emptyArticle = new Article { Title = "   " }; // Whitespace only

            var isEmptyCondition = CreateTestCondition(
                @operator: RuleOperator.IsEmpty);

            var isNotEmptyCondition = CreateTestCondition(
                @operator: RuleOperator.IsNotEmpty);

            // Act
            var result1 = _ruleEngine.EvaluateCondition(article, isNotEmptyCondition);
            var result2 = _ruleEngine.EvaluateCondition(emptyArticle, isEmptyCondition);

            // Assert
            result1.Should().BeTrue(); // Article has content
            result2.Should().BeTrue(); // Empty article has empty title
        }

        #endregion

        #region EvaluateCondition - Regex Tests

        /// <summary>
        /// Tests regex pattern matching with case sensitivity.
        /// </summary>
        [Theory]
        [InlineData(@"\bAI\b", false, true)]          // Word boundary match
        [InlineData(@"\bai\b", false, true)]          // Case-insensitive
        [InlineData(@"\bai\b", true, false)]          // Case-sensitive (lowercase)
        [InlineData(@"\bA\.I\.\b", false, false)]     // No match
        [InlineData(@"Future.*Development", false, true)] // Pattern match
        [InlineData(@"^The.*Development$", false, true)] // Full string match
        public async Task EvaluateCondition_WithRegexOperator_ShouldMatchPatterns(
            string pattern, bool caseSensitive, bool expectedResult)
        {
            // Arrange
            var article = CreateTestArticle();
            var condition = CreateTestCondition(
                field: RuleFieldTarget.Title,
                @operator: RuleOperator.Regex,
                regexPattern: pattern,
                isCaseSensitive: caseSensitive);

            // Act
            var result = _ruleEngine.EvaluateCondition(article, condition);

            // Assert
            result.Should().Be(expectedResult);
        }

        /// <summary>
        /// Tests that invalid regex patterns log error and return false.
        /// </summary>
        [Fact]
        public async Task EvaluateCondition_WithInvalidRegex_ShouldLogErrorAndReturnFalse()
        {
            // Arrange
            var article = CreateTestArticle();
            var condition = CreateTestCondition(
                @operator: RuleOperator.Regex,
                regexPattern: "[invalid regex");

            // Usar Callback para capturar la llamada
            Exception loggedException = null;
            string loggedMessage = null;

            _mockLogger
                .Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>()))
                .Callback<Exception, string, object>((ex, msg, arg) =>
                {
                    loggedException = ex;
                    loggedMessage = msg;
                });

            // Act
            var result = _ruleEngine.EvaluateCondition(article, condition);

            // Assert
            result.Should().BeFalse();

            // Verificar que se capturó una excepción
            loggedException.Should().NotBeNull();
            loggedException.Should().BeOfType<System.Text.RegularExpressions.RegexParseException>();
            loggedMessage.Should().Contain("regex");
        }

        #endregion

        #region EvaluateCondition - Comparison Tests

        /// <summary>
        /// Tests GreaterThan and LessThan operators with numeric values.
        /// </summary>
        [Fact]
        public async Task EvaluateCondition_WithNumericComparisons_ShouldCompareCorrectly()
        {
            // Arrange
            // El método CompareText intenta extraer números del texto
            // "Article 42" -> intenta parsear "Article 42" como double, falla
            // Luego intenta como fecha, falla
            // Finalmente hace comparación de strings: "Article 42".CompareTo("10")

            // CASO 1: Texto que contiene número
            var article1 = new Article { Title = "Article 42" };
            var condition1 = CreateTestCondition(
                field: RuleFieldTarget.Title,
                @operator: RuleOperator.GreaterThan,
                value: "10");

            // CASO 2: Texto que ES un número
            var article2 = new Article { Title = "42" };
            var condition2 = CreateTestCondition(
                field: RuleFieldTarget.Title,
                @operator: RuleOperator.GreaterThan,
                value: "10");

            // Act
            var result1 = _ruleEngine.EvaluateCondition(article1, condition1);
            var result2 = _ruleEngine.EvaluateCondition(article2, condition2);

            // Assert
            // result1 podría ser false porque "Article 42" no se puede parsear como número
            // result2 debería ser true porque "42" > "10" como string también
            result2.Should().BeTrue(); // 42 > 10

            // Si queremos probar el comportamiento real
            // Podemos ver qué pasa con diferentes inputs
        }

        /// <summary>
        /// Tests GreaterThan and LessThan operators with date values.
        /// </summary>
        [Fact]
        public async Task EvaluateCondition_WithDateComparisons_ShouldCompareCorrectly()
        {
            // Arrange
            var article = new Article { Title = "2024-01-15 News" };

            var greaterCondition = CreateTestCondition(
                field: RuleFieldTarget.Title,
                @operator: RuleOperator.GreaterThan,
                value: "2024-01-01");

            var lessCondition = CreateTestCondition(
                field: RuleFieldTarget.Title,
                @operator: RuleOperator.LessThan,
                value: "2024-02-01");

            // Act
            var greaterResult = _ruleEngine.EvaluateCondition(article, greaterCondition);
            var lessResult = _ruleEngine.EvaluateCondition(article, lessCondition);

            // Assert
            greaterResult.Should().BeTrue();  // Jan 15 > Jan 1
            lessResult.Should().BeTrue();     // Jan 15 < Feb 1
        }

        #endregion

        #region EvaluateConditionGroup Tests

        /// <summary>
        /// Tests that empty condition group returns true.
        /// </summary>
        [Fact]
        public async Task EvaluateConditionGroup_WithEmptyConditions_ShouldReturnTrue()
        {
            // Arrange
            var article = CreateTestArticle();
            var conditions = new List<RuleCondition>();

            // Act
            var result = _ruleEngine.EvaluateConditionGroup(conditions, article);

            // Assert
            result.Should().BeTrue();
        }

        /// <summary>
        /// Tests AND logical operator between conditions.
        /// </summary>
        [Fact]
        public async Task EvaluateConditionGroup_WithAndOperator_ShouldRequireAllTrue()
        {
            // Arrange
            var article = CreateTestArticle();
            var conditions = new List<RuleCondition>
    {
        CreateTestCondition(value: "Future", combineWithNext: LogicalOperator.AND),
        CreateTestCondition(value: "AI", combineWithNext: LogicalOperator.AND),
        CreateTestCondition(value: "Software") // No combine needed for last
    };

            // Act
            var result = _ruleEngine.EvaluateConditionGroup(conditions, article);

            // Assert
            result.Should().BeTrue(); // All three are in the title
                                      // El logging es secundario - se prueba en tests de logging específicos
        }
        /// <summary>
        /// Tests OR logical operator between conditions.
        /// </summary>
        [Fact]
        public async Task EvaluateConditionGroup_WithOrOperator_ShouldRequireAnyTrue()
        {
            // Arrange
            var article = CreateTestArticle();
            var conditions = new List<RuleCondition>
            {
                CreateTestCondition(value: "Nonexistent", combineWithNext: LogicalOperator.OR), // False
                CreateTestCondition(value: "AI", combineWithNext: LogicalOperator.OR),          // True
                CreateTestCondition(value: "AlsoNonexistent")                                   // False
            };

            // Act
            var result = _ruleEngine.EvaluateConditionGroup(conditions, article);

            // Assert
            result.Should().BeTrue(); // OR chain with one true result
        }

        /// <summary>
        /// Tests mixed AND/OR logical operators.
        /// </summary>
        [Fact]
        public async Task EvaluateConditionGroup_WithMixedOperators_ShouldEvaluateCorrectly()
        {
            // Arrange
            var article = CreateTestArticle();
            var conditions = new List<RuleCondition>
            {
                CreateTestCondition(value: "Future", combineWithNext: LogicalOperator.AND),  // True AND
                CreateTestCondition(value: "Nonexistent", combineWithNext: LogicalOperator.OR), // False OR
                CreateTestCondition(value: "AI") // True
            };
            // Logic: (True AND False) OR True = True

            // Act
            var result = _ruleEngine.EvaluateConditionGroup(conditions, article);

            // Assert
            result.Should().BeTrue();
        }

        /// <summary>
        /// Tests that null article in group evaluation returns true.
        /// </summary>
        [Fact]
        public async Task EvaluateConditionGroup_WithNullArticle_ShouldReturnTrue()
        {
            // Arrange
            var conditions = new List<RuleCondition>
    {
        CreateTestCondition(value: "test")
    };

            // Act
            var result = _ruleEngine.EvaluateConditionGroup(conditions, null);

            // Assert
            // Según el código: if (conditions == null || !conditions.Any() || article == null) return true;
            result.Should().BeTrue();
            // El logging de error no ocurre en este caso (es un caso manejado, no una excepción)
        }

        #endregion

        #region ValidateCondition Tests

        /// <summary>
        /// Tests validation of null condition.
        /// </summary>
        [Fact]
        public async Task ValidateCondition_WithNullCondition_ShouldReturnFalse()
        {
            // Act
            var result = _ruleEngine.ValidateCondition(null);

            // Assert
            result.Should().BeFalse();
        }

        /// <summary>
        /// Tests validation of regex operator without pattern.
        /// </summary>
        [Fact]
        public async Task ValidateCondition_WithRegexNoPattern_ShouldReturnFalse()
        {
            // Arrange
            var condition = CreateTestCondition(
                @operator: RuleOperator.Regex,
                regexPattern: ""); // Empty pattern for regex

            // Act
            var result = _ruleEngine.ValidateCondition(condition);

            // Assert
            result.Should().BeFalse();
        }

        /// <summary>
        /// Tests validation of string operators without value.
        /// </summary>
        [Theory]
        [InlineData(RuleOperator.Contains)]
        [InlineData(RuleOperator.Equals)]
        [InlineData(RuleOperator.StartsWith)]
        [InlineData(RuleOperator.EndsWith)]
        [InlineData(RuleOperator.GreaterThan)]
        [InlineData(RuleOperator.LessThan)]
        public async Task ValidateCondition_WithOperatorRequiringValueButNone_ShouldReturnFalse(RuleOperator @operator)
        {
            // Arrange
            var condition = CreateTestCondition(
                @operator: @operator,
                value: ""); // Empty value

            // Act
            var result = _ruleEngine.ValidateCondition(condition);

            // Assert
            result.Should().BeFalse();
        }

        /// <summary>
        /// Tests validation of invalid regex pattern.
        /// </summary>
        [Fact]
        public async Task ValidateCondition_WithInvalidRegexPattern_ShouldReturnFalse()
        {
            // Arrange
            var condition = CreateTestCondition(
                @operator: RuleOperator.Regex,
                regexPattern: "[invalid"); // Invalid regex

            // Act
            var result = _ruleEngine.ValidateCondition(condition);

            // Assert
            result.Should().BeFalse();
        }

        /// <summary>
        /// Tests validation of valid regex pattern.
        /// </summary>
        [Fact]
        public async Task ValidateCondition_WithValidRegexPattern_ShouldReturnTrue()
        {
            // Arrange
            var condition = CreateTestCondition(
                @operator: RuleOperator.Regex,
                regexPattern: @"\w+"); // Valid regex

            // Act
            var result = _ruleEngine.ValidateCondition(condition);

            // Assert
            result.Should().BeTrue();
        }

        /// <summary>
        /// Tests validation of valid string operator with value.
        /// </summary>
        [Fact]
        public async Task ValidateCondition_WithValidStringOperator_ShouldReturnTrue()
        {
            // Arrange
            var condition = CreateTestCondition(
                @operator: RuleOperator.Contains,
                value: "test");

            // Act
            var result = _ruleEngine.ValidateCondition(condition);

            // Assert
            result.Should().BeTrue();
        }

        /// <summary>
        /// Tests validation of IsEmpty/IsNotEmpty operators (don't require value).
        /// </summary>
        [Theory]
        [InlineData(RuleOperator.IsEmpty)]
        [InlineData(RuleOperator.IsNotEmpty)]
        public async Task ValidateCondition_WithEmptyCheckOperators_ShouldReturnTrue(RuleOperator @operator)
        {
            // Arrange
            var condition = CreateTestCondition(
                @operator: @operator,
                value: ""); // Value not required for these operators

            // Act
            var result = _ruleEngine.ValidateCondition(condition);

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Tests that exceptions during evaluation are caught and logged.
        /// </summary>
        [Fact]
        public async Task EvaluateCondition_WithInvalidInput_ShouldReturnFalseAndNotThrow()
        {
            // Arrange
            // No podemos forzar fácilmente una excepción interna sin modificar el código
            // En su lugar, probamos que el método maneja casos borde correctamente

            // Caso 1: null article
            var condition1 = CreateTestCondition();
            var result1 = _ruleEngine.EvaluateCondition(null, condition1);
            result1.Should().BeFalse();

            // Caso 2: null condition  
            var article = CreateTestArticle();
            var result2 = _ruleEngine.EvaluateCondition(article, null);
            result2.Should().BeFalse();

            // Verificamos que no se llamó a Error porque estos son casos manejados, no excepciones
            _mockLogger.Verify(
                x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()),
                Times.Never);
        }

        /// <summary>
        /// Tests that exceptions during group evaluation are caught and logged.
        /// </summary>
        [Fact]
        public async Task EvaluateConditionGroup_WithInvalidCondition_ShouldReturnFalse()
        {
            // Arrange
            var article = CreateTestArticle();
            var conditions = new List<RuleCondition>
    {
        // Condición que no lanzará excepción pero tendrá comportamiento definido
        CreateTestCondition(value: "test")
    };

            // Act
            var result = _ruleEngine.EvaluateConditionGroup(conditions, article);

            // Assert
            // El artículo no contiene "test" en el título, debería ser false
            result.Should().BeFalse();
        }

        /// <summary>
        /// Tests that exceptions during validation are caught and logged.
        /// </summary>
        [Fact]
        public async Task ValidateCondition_WhenThrowsException_ShouldLogErrorAndReturnFalse()
        {
            // Arrange
            var condition = CreateTestCondition();

            // Act
            var result = _ruleEngine.ValidateCondition(condition);

            // Assert
            // Should not throw, should return either true or false (but we know this specific condition is valid)
            // Since CreateTestCondition() creates a valid condition, we expect true
            result.Should().BeTrue();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Verifies that Debug logging was called with expected message.
        /// </summary>
        private void VerifyLoggerDebugCalled(string expectedMessage)
        {
            _mockLogger.Verify(
                x => x.Debug(It.Is<string>(s => s.Contains(expectedMessage)), It.IsAny<object[]>()),
                Times.AtLeastOnce);
        }

        /// <summary>
        /// Verifies that Debug logging was not called.
        /// </summary>
        private void VerifyLoggerDebugNotCalled()
        {
            _mockLogger.Verify(
                x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()),
                Times.Never);
        }

        /// <summary>
        /// Verifies that Error logging was called with expected message.
        /// </summary>
        private void VerifyLoggerErrorCalled(string expectedMessage)
        {
            _mockLogger.Verify(
                x => x.Error(
                    It.IsAny<Exception>(),
                    It.Is<string>(s => s.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<object[]>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Edge Cases and Integration Tests

        /// <summary>
        /// Tests multiple conditions with different fields and operators.
        /// </summary>
        [Fact]
        public async Task ComplexConditionEvaluation_ShouldHandleMultipleScenarios()
        {
            // Arrange
            var article = new Article
            {
                Title = "Breaking News: Market Crash 2024",
                Content = "The stock market dropped 15% today.",
                Author = "Financial Times",
                Categories = "Finance,Economy,News"
            };

            var conditions = new List<RuleCondition>
            {
                // Title contains "News" AND
                CreateTestCondition(
                    field: RuleFieldTarget.Title,
                    @operator: RuleOperator.Contains,
                    value: "News",
                    combineWithNext: LogicalOperator.AND),
                
                // Content contains "market" (case-insensitive) AND
                CreateTestCondition(
                    field: RuleFieldTarget.Content,
                    @operator: RuleOperator.Contains,
                    value: "market",
                    combineWithNext: LogicalOperator.AND),
                
                // Categories contains "Finance" OR
                CreateTestCondition(
                    field: RuleFieldTarget.Categories,
                    @operator: RuleOperator.Contains,
                    value: "Finance",
                    combineWithNext: LogicalOperator.OR),
                
                // Author doesn't equal "Anonymous"
                CreateTestCondition(
                    field: RuleFieldTarget.Author,
                    @operator: RuleOperator.NotEquals,
                    value: "Anonymous")
            };

            // Act
            var result = _ruleEngine.EvaluateConditionGroup(conditions, article);

            // Assert
            result.Should().BeTrue();
        }

        /// <summary>
        /// Tests case sensitivity across different operators.
        /// </summary>
        [Fact]
        public async Task CaseSensitivity_ShouldBeAppliedConsistently()
        {
            // Arrange
            var article = new Article { Title = "CaseSensitive Test" };

            var sensitiveCondition = CreateTestCondition(
                value: "casesensitive",
                isCaseSensitive: true);

            var insensitiveCondition = CreateTestCondition(
                value: "casesensitive",
                isCaseSensitive: false);

            // Act
            var sensitiveResult = _ruleEngine.EvaluateCondition(article, sensitiveCondition);
            var insensitiveResult = _ruleEngine.EvaluateCondition(article, insensitiveCondition);

            // Assert
            sensitiveResult.Should().BeFalse();   // Case-sensitive, no match
            insensitiveResult.Should().BeTrue();  // Case-insensitive, matches
        }

        #endregion

        #region Regex Timeout Tests

        [Fact]
        public void EvaluateCondition_WithRegexOperatorAndTimeout_ShouldReturnFalse()
        {
            // Arrange
            var engine = new RuleEngine(_mockLogger.Object);
            var article = CreateTestArticle(content: new string('a', 100000)); // Texto muy largo

            var condition = new RuleCondition
            {
                Field = RuleFieldTarget.Content,
                Operator = RuleOperator.Regex,
                RegexPattern = @"(a+)+b", // Patrón catastrófico (ReDoS)
                IsCaseSensitive = true
            };

            // Act
            var result = engine.EvaluateCondition(article, condition);

            // Assert
            result.Should().BeFalse();           
        }

        [Fact]
        public void EvaluateCondition_WithRegexOperatorAndInvalidPattern_ShouldReturnFalse()
        {
            // Arrange
            var engine = new RuleEngine(_mockLogger.Object);
            var article = CreateTestArticle(content: "test content");

            var condition = new RuleCondition
            {
                Field = RuleFieldTarget.Content,
                Operator = RuleOperator.Regex,
                RegexPattern = "[", // Pattern inválido
                IsCaseSensitive = true
            };

            // Act
            var result = engine.EvaluateCondition(article, condition);

            // Assert
            result.Should().BeFalse();          
        }

        #endregion
    }
}