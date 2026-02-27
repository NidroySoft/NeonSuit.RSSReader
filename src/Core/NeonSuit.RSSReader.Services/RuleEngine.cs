using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Rules;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="IRuleEngine"/> providing advanced rule evaluation with support for complex logical expressions.
    /// Provides pattern matching, regex evaluation, and multi-condition processing with performance optimizations.
    /// </summary>
    internal class RuleEngine : IRuleEngine
    {
        private readonly IRuleConditionRepository _ruleConditionRepository;
        private readonly IRuleRepository _ruleRepository;
        private readonly IArticleRepository _articleRepository;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();
        private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(100);

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleEngine"/> class.
        /// </summary>
        /// <param name="ruleConditionRepository">Repository for rule conditions.</param>
        /// <param name="ruleRepository">Repository for rules.</param>
        /// <param name="articleRepository">Repository for articles.</param>
        /// <param name="mapper">AutoMapper instance for DTO transformations.</param>
        /// <param name="logger">Serilog logger instance for structured logging.</param>
        /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
        public RuleEngine(
            IRuleConditionRepository ruleConditionRepository,
            IRuleRepository ruleRepository,
            IArticleRepository articleRepository,
            IMapper mapper,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(ruleConditionRepository);
            ArgumentNullException.ThrowIfNull(ruleRepository);
            ArgumentNullException.ThrowIfNull(articleRepository);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(logger);

            _ruleConditionRepository = ruleConditionRepository;
            _ruleRepository = ruleRepository;
            _articleRepository = articleRepository;
            _mapper = mapper;
            _logger = logger.ForContext<RuleEngine>();

#if DEBUG
            _logger.Debug("RuleEngine initialized");
#endif
        }

        #endregion

        #region Single Condition Evaluation

        /// <inheritdoc />
        public async Task<bool> EvaluateConditionAsync(int articleId, int conditionId, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (articleId <= 0)
                {
                    _logger.Warning("Invalid article ID in condition evaluation: {ArticleId}", articleId);
                    throw new ArgumentOutOfRangeException(nameof(articleId), "Article ID must be greater than 0");
                }

                if (conditionId <= 0)
                {
                    _logger.Warning("Invalid condition ID in evaluation: {ConditionId}", conditionId);
                    throw new ArgumentOutOfRangeException(nameof(conditionId), "Condition ID must be greater than 0");
                }

                var article = await _articleRepository.GetByIdAsync(articleId, cancellationToken).ConfigureAwait(false);
                if (article == null)
                {
                    _logger.Warning("Article not found: ID {ArticleId}", articleId);
                    return false;
                }

                var condition = await _ruleConditionRepository.GetByIdAsync(conditionId, cancellationToken).ConfigureAwait(false);
                if (condition == null)
                {
                    _logger.Warning("Condition not found: ID {ConditionId}", conditionId);
                    return false;
                }

                _logger.Debug("Evaluating condition: {ConditionId} on article: {ArticleId}", conditionId, articleId);

                var targetText = GetTargetText(article, condition.Field);
                var comparison = condition.IsCaseSensitive ?
                    StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                var result = EvaluateTextCondition(targetText, condition, comparison, cancellationToken);

                // Apply negation
                if (condition.Negate)
                {
                    result = !result;
                }

                _logger.Debug("Condition evaluation result: {Result} for condition {ConditionId}", result, conditionId);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("EvaluateConditionAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to evaluate condition {ConditionId} on article {ArticleId}", conditionId, articleId);
                throw new InvalidOperationException($"Failed to evaluate condition {conditionId}", ex);
            }
        }

        #endregion

        #region Group Evaluation

        /// <inheritdoc />
        public async Task<bool> EvaluateConditionGroupAsync(
            int articleId,
            int ruleId,
            LogicalOperator logicalOperator = LogicalOperator.AND,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (articleId <= 0)
                {
                    _logger.Warning("Invalid article ID in group evaluation: {ArticleId}", articleId);
                    throw new ArgumentOutOfRangeException(nameof(articleId), "Article ID must be greater than 0");
                }

                if (ruleId <= 0)
                {
                    _logger.Warning("Invalid rule ID in group evaluation: {RuleId}", ruleId);
                    throw new ArgumentOutOfRangeException(nameof(ruleId), "Rule ID must be greater than 0");
                }

                var article = await _articleRepository.GetByIdAsync(articleId, cancellationToken).ConfigureAwait(false);
                if (article == null)
                {
                    _logger.Warning("Article not found: ID {ArticleId}", articleId);
                    return false;
                }

                var rule = await _ruleRepository.GetByIdAsync(ruleId, cancellationToken).ConfigureAwait(false);
                if (rule == null)
                {
                    _logger.Warning("Rule not found: ID {RuleId}", ruleId);
                    return false;
                }

                var conditions = await _ruleConditionRepository.GetByRuleIdAsync(ruleId, cancellationToken).ConfigureAwait(false);
                if (!conditions.Any())
                {
                    _logger.Debug("No conditions found for rule {RuleId}, returning true", ruleId);
                    return true; // Rule with no conditions always matches
                }

                _logger.Debug("Evaluating condition group for rule {RuleId} with {Count} conditions using {Operator}",
                    ruleId, conditions.Count, logicalOperator);

                // Group conditions by GroupId
                var groupedConditions = conditions.GroupBy(c => c.GroupId).ToList();

                if (groupedConditions.Count == 1)
                {
                    // Single group - evaluate with specified logical operator
                    return EvaluateConditionGroupInternal(groupedConditions.First().ToList(), article, logicalOperator, cancellationToken);
                }
                else
                {
                    // Multiple groups - OR between groups, AND within groups
                    foreach (var group in groupedConditions)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var groupConditions = group.ToList();
                        if (EvaluateConditionGroupInternal(groupConditions, article, LogicalOperator.AND, cancellationToken))
                        {
                            return true; // OR between groups - any true group makes whole true
                        }
                    }
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("EvaluateConditionGroupAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to evaluate condition group for rule {RuleId} on article {ArticleId}", ruleId, articleId);
                throw new InvalidOperationException($"Failed to evaluate condition group for rule {ruleId}", ex);
            }
        }

        #endregion

        #region Batch Evaluation

        /// <inheritdoc />
        public async Task<Dictionary<int, bool>> EvaluateBatchAsync(
            List<int> articleIds,
            int ruleId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                ArgumentNullException.ThrowIfNull(articleIds);

                if (ruleId <= 0)
                {
                    _logger.Warning("Invalid rule ID in batch evaluation: {RuleId}", ruleId);
                    throw new ArgumentOutOfRangeException(nameof(ruleId), "Rule ID must be greater than 0");
                }

                _logger.Debug("Batch evaluating {ArticleCount} articles against rule {RuleId}",
                    articleIds.Count, ruleId);

                var results = new Dictionary<int, bool>(articleIds.Count);

                foreach (var articleId in articleIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (articleId > 0)
                    {
                        var result = await EvaluateConditionGroupAsync(articleId, ruleId, LogicalOperator.AND, cancellationToken).ConfigureAwait(false);
                        results[articleId] = result;
                    }
                }

                _logger.Debug("Batch evaluation completed for {ArticleCount} articles", articleIds.Count);
                return results;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("EvaluateBatchAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to batch evaluate {ArticleCount} articles against rule {RuleId}", articleIds?.Count ?? 0, ruleId);
                throw new InvalidOperationException($"Failed to batch evaluate articles against rule {ruleId}", ex);
            }
        }

        #endregion

        #region Validation

        /// <inheritdoc />
        public async Task<bool> ValidateConditionAsync(int conditionId, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (conditionId <= 0)
                {
                    _logger.Warning("Invalid condition ID for validation: {ConditionId}", conditionId);
                    throw new ArgumentOutOfRangeException(nameof(conditionId), "Condition ID must be greater than 0");
                }

                var condition = await _ruleConditionRepository.GetByIdAsync(conditionId, cancellationToken).ConfigureAwait(false);
                if (condition == null)
                {
                    _logger.Warning("Condition not found for validation: ID {ConditionId}", conditionId);
                    return false;
                }

                _logger.Debug("Validating condition: {ConditionId}", conditionId);

                // Check required fields for specific operators
                if (condition.Operator == RuleOperator.Regex)
                {
                    if (string.IsNullOrWhiteSpace(condition.RegexPattern))
                    {
                        _logger.Warning("Regex pattern is empty for regex condition {ConditionId}", conditionId);
                        return false;
                    }

                    // Validate regex pattern syntax
                    try
                    {
                        Regex.IsMatch("", condition.RegexPattern, RegexOptions.None, _regexTimeout);
                    }
                    catch (RegexParseException ex)
                    {
                        _logger.Warning(ex, "Invalid regex pattern syntax for condition {ConditionId}: {Pattern}", conditionId, condition.RegexPattern);
                        return false;
                    }
                }

                if (RequiresValue(condition.Operator) && string.IsNullOrWhiteSpace(condition.Value))
                {
                    _logger.Warning("Value is empty for operator that requires value for condition {ConditionId}: {Operator}", conditionId, condition.Operator);
                    return false;
                }

                // Validate GroupId and Order are non-negative
                if (condition.GroupId < 0)
                {
                    _logger.Warning("GroupId cannot be negative for condition {ConditionId}: {GroupId}", conditionId, condition.GroupId);
                    return false;
                }

                if (condition.Order < 0)
                {
                    _logger.Warning("Order cannot be negative for condition {ConditionId}: {Order}", conditionId, condition.Order);
                    return false;
                }

                _logger.Debug("Condition {ConditionId} validation passed", conditionId);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ValidateConditionAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to validate condition {ConditionId}", conditionId);
                throw new InvalidOperationException($"Failed to validate condition {conditionId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<RuleValidationResultDto> ValidateRuleConditionsAsync(int ruleId, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ruleId <= 0)
                {
                    _logger.Warning("Invalid rule ID for conditions validation: {RuleId}", ruleId);
                    throw new ArgumentOutOfRangeException(nameof(ruleId), "Rule ID must be greater than 0");
                }

                _logger.Debug("Validating all conditions for rule: {RuleId}", ruleId);

                var conditions = await _ruleConditionRepository.GetByRuleIdAsync(ruleId, cancellationToken).ConfigureAwait(false);
                var result = new RuleValidationResultDto { IsValid = true };

                foreach (var condition in conditions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!await ValidateConditionAsync(condition.Id, cancellationToken).ConfigureAwait(false))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Invalid condition: Field={condition.Field}, Operator={condition.Operator}, Value={condition.Value}");
                    }
                }

                _logger.Debug("Rule {RuleId} conditions validation completed. IsValid: {IsValid}, Errors: {ErrorCount}",
                    ruleId, result.IsValid, result.Errors.Count);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ValidateRuleConditionsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to validate conditions for rule {RuleId}", ruleId);
                throw new InvalidOperationException($"Failed to validate conditions for rule {ruleId}", ex);
            }
        }

        #endregion

        #region Testing

        /// <inheritdoc />
        public async Task<bool> TestConditionWithTextAsync(int conditionId, string sampleText, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (conditionId <= 0)
                {
                    _logger.Warning("Invalid condition ID for text testing: {ConditionId}", conditionId);
                    throw new ArgumentOutOfRangeException(nameof(conditionId), "Condition ID must be greater than 0");
                }

                if (sampleText == null)
                {
                    _logger.Warning("Sample text is null for condition testing");
                    throw new ArgumentException("Sample text cannot be null", nameof(sampleText));
                }

                var condition = await _ruleConditionRepository.GetByIdAsync(conditionId, cancellationToken).ConfigureAwait(false);
                if (condition == null)
                {
                    _logger.Warning("Condition not found for testing: ID {ConditionId}", conditionId);
                    return false;
                }

                _logger.Debug("Testing condition {ConditionId} with sample text length: {TextLength}", conditionId, sampleText.Length);

                var comparison = condition.IsCaseSensitive ?
                    StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                var result = EvaluateTextCondition(sampleText, condition, comparison, cancellationToken);

                if (condition.Negate)
                {
                    result = !result;
                }

                _logger.Debug("Condition test result: {Result} for condition {ConditionId}", result, conditionId);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("TestConditionWithTextAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to test condition {ConditionId} with text", conditionId);
                throw new InvalidOperationException($"Failed to test condition {conditionId}", ex);
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Gets the target text from an article based on field specification.
        /// </summary>
        private static string GetTargetText(Article article, RuleFieldTarget field)
        {
            return field switch
            {
                RuleFieldTarget.Title => article.Title ?? "",
                RuleFieldTarget.Content => article.Content ?? article.Summary ?? "",
                RuleFieldTarget.Author => article.Author ?? "",
                RuleFieldTarget.Categories => article.Categories ?? "",
                RuleFieldTarget.AllFields => $"{article.Title} {article.Content} {article.Summary}".Trim(),
                RuleFieldTarget.AnyField => $"{article.Title} {article.Content} {article.Summary}".Trim(),
                _ => ""
            };
        }

        /// <summary>
        /// Evaluates a group of conditions internally.
        /// </summary>
        private bool EvaluateConditionGroupInternal(
            List<RuleCondition> conditions,
            Article article,
            LogicalOperator logicalOperator,
            CancellationToken cancellationToken)
        {
            if (!conditions.Any()) return true;

            bool groupResult = logicalOperator == LogicalOperator.AND;

            foreach (var condition in conditions.OrderBy(c => c.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentResult = EvaluateSingleConditionInternal(condition, article, cancellationToken);

                if (logicalOperator == LogicalOperator.AND)
                {
                    groupResult = groupResult && currentResult;
                    if (!groupResult) break; // Short-circuit
                }
                else // OR
                {
                    groupResult = groupResult || currentResult;
                    if (groupResult) break; // Short-circuit
                }
            }

            return groupResult;
        }

        /// <summary>
        /// Evaluates a single condition internally.
        /// </summary>
        private bool EvaluateSingleConditionInternal(RuleCondition condition, Article article, CancellationToken cancellationToken)
        {
            var targetText = GetTargetText(article, condition.Field);
            var comparison = condition.IsCaseSensitive ?
                StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            var result = EvaluateTextCondition(targetText, condition, comparison, cancellationToken);

            return condition.Negate ? !result : result;
        }

        /// <summary>
        /// Evaluates text against a condition with specified operator.
        /// </summary>
        private bool EvaluateTextCondition(
            string text,
            RuleCondition condition,
            StringComparison comparison,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return condition.Operator switch
            {
                RuleOperator.Contains => text.Contains(condition.Value, comparison),
                RuleOperator.Equals => text.Equals(condition.Value, comparison),
                RuleOperator.StartsWith => text.StartsWith(condition.Value, comparison),
                RuleOperator.EndsWith => text.EndsWith(condition.Value, comparison),
                RuleOperator.NotContains => !text.Contains(condition.Value, comparison),
                RuleOperator.NotEquals => !text.Equals(condition.Value, comparison),
                RuleOperator.Regex => EvaluateRegexCondition(text, condition.RegexPattern, condition.IsCaseSensitive, cancellationToken),
                RuleOperator.GreaterThan => CompareText(text, condition.Value) > 0,
                RuleOperator.LessThan => CompareText(text, condition.Value) < 0,
                RuleOperator.IsEmpty => string.IsNullOrWhiteSpace(text),
                RuleOperator.IsNotEmpty => !string.IsNullOrWhiteSpace(text),
                _ => false
            };
        }

        /// <summary>
        /// Evaluates regex pattern matching with improved error handling and caching.
        /// </summary>
        private bool EvaluateRegexCondition(
            string text,
            string pattern,
            bool caseSensitive,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(pattern))
                {
                    _logger.Warning("Empty regex pattern provided");
                    return false;
                }

                // Create cache key based on pattern and case sensitivity
                var cacheKey = $"{pattern}_{caseSensitive}";

                var regex = _regexCache.GetOrAdd(cacheKey, _ =>
                {
                    var options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
                    if (!caseSensitive)
                        options |= RegexOptions.IgnoreCase;

                    return new Regex(pattern, options, _regexTimeout);
                });

                return regex.IsMatch(text);
            }
            catch (RegexParseException ex)
            {
                _logger.Error(ex, "Invalid regex pattern syntax: {Pattern}", pattern);
                return false;
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.Warning("Regex evaluation timed out for pattern: {Pattern} on text length: {Length}",
                    pattern, text?.Length ?? 0);
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in regex evaluation for pattern: {Pattern}", pattern);
                return false;
            }
        }

        /// <summary>
        /// Compares two text values for numeric/date comparisons.
        /// </summary>
        private static int CompareText(string text1, string text2)
        {
            // Try numeric comparison
            if (double.TryParse(text1, out double num1) && double.TryParse(text2, out double num2))
            {
                return num1.CompareTo(num2);
            }

            // Try date comparison
            if (DateTime.TryParse(text1, out DateTime date1) && DateTime.TryParse(text2, out DateTime date2))
            {
                return date1.CompareTo(date2);
            }

            // Fall back to string comparison
            return string.Compare(text1, text2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if an operator requires a value to be present.
        /// </summary>
        private static bool RequiresValue(RuleOperator op)
        {
            return op is not (RuleOperator.IsEmpty or RuleOperator.IsNotEmpty or RuleOperator.Regex);
        }

        #endregion
    }
}