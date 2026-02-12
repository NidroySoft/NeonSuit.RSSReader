using NeonSuit.RSSReader.Core.Helpers;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;
using System.Text.RegularExpressions;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Professional implementation of the rules engine.
    /// Analyzes articles and triggers automated actions based on database-stored rules.
    /// </summary>
    public class RuleService : IRuleService
    {
        private readonly IRuleRepository _ruleRepository;
        private readonly IArticleRepository _articleRepository;
        private readonly IFeedRepository _feedRepository;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the RuleService class.
        /// </summary>
        /// <param name="ruleRepository">The rule repository.</param>
        /// <param name="articleRepository">The article repository.</param>
        /// <param name="feedRepository">The feed repository.</param>
        /// <param name="logger">The logger instance.</param>
        public RuleService(
            IRuleRepository ruleRepository,
            IArticleRepository articleRepository,
            IFeedRepository feedRepository,
            ILogger logger)
        {
            _ruleRepository = ruleRepository ?? throw new ArgumentNullException(nameof(ruleRepository));
            _articleRepository = articleRepository ?? throw new ArgumentNullException(nameof(articleRepository));
            _feedRepository = feedRepository ?? throw new ArgumentNullException(nameof(feedRepository));
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<RuleService>();
        }

        /// <inheritdoc />
        public async Task<Rule?> GetRuleByIdAsync(int ruleId)
        {
            try
            {
                _logger.Debug("Retrieving rule by ID: {RuleId}", ruleId);
                var rule = await _ruleRepository.GetByIdAsync(ruleId);

                if (rule != null)
                {
                    _logger.Debug("Found rule: {RuleName} (ID: {RuleId})", rule.Name, ruleId);
                }
                else
                {
                    _logger.Debug("Rule not found with ID: {RuleId}", ruleId);
                }

                return rule;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve rule by ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Rule>> GetAllRulesAsync()
        {
            try
            {
                _logger.Debug("Retrieving all rules");
                var rules = await _ruleRepository.GetAllAsync();
                _logger.Information("Retrieved {Count} rules", rules.Count);
                return rules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve all rules");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Rule>> GetActiveRulesAsync()
        {
            try
            {
                _logger.Debug("Retrieving active rules");
                var rules = await _ruleRepository.GetActiveRulesAsync();
                _logger.Information("Retrieved {Count} active rules", rules.Count);
                return rules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve active rules");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Rule> CreateRuleAsync(Rule rule)
        {
            try
            {
                if (rule == null)
                    throw new ArgumentNullException(nameof(rule));

                // Validate JSON fields using centralized helper
                JsonValidationHelper.EnsureValidJson(rule.FeedIds, nameof(rule.FeedIds), expectIntArray: true);
                JsonValidationHelper.EnsureValidJson(rule.CategoryIds, nameof(rule.CategoryIds), expectIntArray: true);
                JsonValidationHelper.EnsureValidJson(rule.TagIds, nameof(rule.TagIds), expectIntArray: true);

                // Complete rule validation
                var validationResult = await ValidateRuleCompletelyAsync(rule);
                if (!validationResult.IsValid)
                    throw new ArgumentException(validationResult.ErrorMessage);

                if (await _ruleRepository.RuleExistsByNameAsync(rule.Name))
                {
                    _logger.Warning("Rule with name '{RuleName}' already exists", rule.Name);
                    throw new InvalidOperationException($"A rule with name '{rule.Name}' already exists.");
                }

                _logger.Information("Creating new rule: {RuleName}", rule.Name);

                // Set default values if not provided
                if (rule.Priority <= 0) rule.Priority = 100;
                rule.IsEnabled = true;
                rule.CreatedAt = DateTime.UtcNow;
                rule.LastModified = DateTime.UtcNow;
                rule.MatchCount = 0;

                await _ruleRepository.InsertAsync(rule);

                _logger.Information("Successfully created rule: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);
                return rule;
            }
            catch (Exception ex) when (ex is not ArgumentNullException and not ArgumentException and not InvalidOperationException)
            {
                _logger.Error(ex, "Failed to create rule: {RuleName}", rule?.Name ?? "null");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateRuleAsync(Rule rule)
        {
            try
            {
                if (rule == null)
                    throw new ArgumentNullException(nameof(rule));

                // Validate JSON fields using centralized helper
                JsonValidationHelper.EnsureValidJson(rule.FeedIds, nameof(rule.FeedIds), expectIntArray: true);
                JsonValidationHelper.EnsureValidJson(rule.CategoryIds, nameof(rule.CategoryIds), expectIntArray: true);
                JsonValidationHelper.EnsureValidJson(rule.TagIds, nameof(rule.TagIds), expectIntArray: true);

                // Complete rule validation
                var validationResult = await ValidateRuleCompletelyAsync(rule);
                if (!validationResult.IsValid)
                    throw new ArgumentException(validationResult.ErrorMessage);

                var existingRule = await _ruleRepository.GetByIdAsync(rule.Id);
                if (existingRule == null)
                {
                    _logger.Warning("Attempted to update non-existent rule: ID {RuleId}", rule.Id);
                    return false;
                }

                _logger.Debug("Updating rule: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);

                rule.LastModified = DateTime.UtcNow;
                var result = await _ruleRepository.UpdateAsync(rule);
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Successfully updated rule: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);
                }
                else
                {
                    _logger.Warning("No changes made to rule: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);
                }

                return success;
            }
            catch (Exception ex) when (ex is not ArgumentNullException and not ArgumentException)
            {
                _logger.Error(ex, "Failed to update rule: {RuleName} (ID: {RuleId})",
                    rule?.Name ?? "null",
                    rule?.Id ?? 0);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteRuleAsync(int ruleId)
        {
            try
            {
                _logger.Information("Deleting rule with ID: {RuleId}", ruleId);
                var result = await _ruleRepository.DeleteAsync(ruleId);
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Successfully deleted rule with ID: {RuleId}", ruleId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete rule with ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Rule>> EvaluateArticleAgainstRulesAsync(Article article)
        {
            try
            {
                if (article == null)
                {
                    _logger.Warning("Attempted to evaluate null article");
                    return new List<Rule>();
                }

                _logger.Debug("Evaluating article against rules: {ArticleTitle}", article.Title);

                var matchedRules = new List<Rule>();
                var activeRules = await _ruleRepository.GetActiveRulesAsync();

                if (!activeRules.Any())
                {
                    _logger.Debug("No active rules found for article evaluation");
                    return matchedRules;
                }

                foreach (var rule in activeRules.OrderBy(r => r.Priority))
                {
                    if (await EvaluateRuleAgainstArticleAsync(rule, article))
                    {
                        matchedRules.Add(rule);

                        // Update rule statistics
                        await _ruleRepository.IncrementMatchCountAsync(rule.Id);
                        rule.LastMatchDate = DateTime.UtcNow;
                        await _ruleRepository.UpdateAsync(rule);

                        // Stop processing if rule has StopOnMatch enabled
                        if (rule.StopOnMatch)
                        {
                            _logger.Debug("Rule {RuleName} matched with StopOnMatch enabled, stopping evaluation",
                                rule.Name);
                            break;
                        }
                    }
                }

                _logger.Information("Article '{ArticleTitle}' matched {Count} rules",
                    article.Title, matchedRules.Count);

                return matchedRules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to evaluate article against rules");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExecuteRuleActionsAsync(Rule rule, Article article)
        {
            try
            {
                if (rule == null || article == null)
                {
                    _logger.Warning("Rule or article not found for action execution");
                    return false;
                }

                if (!await EvaluateRuleAgainstArticleAsync(rule, article))
                {
                    _logger.Debug("Rule {RuleName} does not match article '{ArticleTitle}'",
                        rule.Name, article.Title);
                    return false;
                }

                _logger.Information("Executing rule actions for '{RuleName}' on article '{ArticleTitle}'",
                    rule.Name, article.Title);

                bool articleModified = false;

                // Execute actions based on action type
                switch (rule.ActionType)
                {
                    case Core.Enums.RuleActionType.MarkAsRead:
                        article.Status = Core.Enums.ArticleStatus.Read;
                        articleModified = true;
                        break;

                    case Core.Enums.RuleActionType.MarkAsStarred:
                        article.IsStarred = true;
                        articleModified = true;
                        break;

                    case Core.Enums.RuleActionType.ApplyTags:
                        _logger.Debug("ApplyTags action not yet implemented for rule {RuleName}", rule.Name);
                        break;

                    case Core.Enums.RuleActionType.MoveToCategory:
                        if (rule.CategoryId.HasValue)
                        {
                            var feed = await _feedRepository.GetByIdAsync(article.FeedId);
                            if (feed != null)
                            {
                                feed.CategoryId = rule.CategoryId.Value;
                                await _feedRepository.UpdateAsync(feed);
                            }
                        }
                        break;

                    case Core.Enums.RuleActionType.HighlightArticle:
                        if (!string.IsNullOrEmpty(rule.HighlightColor))
                        {
                            _logger.Debug("Highlight action with color {Color} for rule {RuleName}",
                                rule.HighlightColor, rule.Name);
                        }
                        break;
                }

                // Update article if modified
                if (articleModified)
                {
                    await _articleRepository.UpdateAsync(article);
                    _logger.Debug("Article updated with rule actions from {RuleName}", rule.Name);
                }

                // Update rule statistics
                await _ruleRepository.IncrementMatchCountAsync(rule.Id);
                rule.LastMatchDate = DateTime.UtcNow;
                await _ruleRepository.UpdateAsync(rule);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to execute rule actions for rule: {RuleName}", rule.Name);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> GetTotalMatchCountAsync()
        {
            try
            {
                return await _ruleRepository.GetTotalMatchCountAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get total match count");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Rule>> GetTopRulesByMatchCountAsync(int limit = 10)
        {
            try
            {
                return await _ruleRepository.GetTopRulesByMatchCountAsync(limit);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get top rules by match count");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ValidateRuleAsync(Rule rule)
        {
            var result = await ValidateRuleCompletelyAsync(rule);
            return result.IsValid;
        }

        /// <inheritdoc />
        public async Task<bool> RuleExistsByNameAsync(string name)
        {
            try
            {
                return await _ruleRepository.RuleExistsByNameAsync(name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check rule existence for name: {Name}", name);
                throw;
            }
        }

        #region Private Validation Methods

        /// <summary>
        /// Performs comprehensive rule validation including business rules and constraints.
        /// </summary>
        /// <param name="rule">The rule to validate.</param>
        /// <returns>A tuple indicating validation success and error message if any.</returns>
        /// <summary>
        /// Performs comprehensive rule validation including business rules and constraints.
        /// </summary>
        /// <param name="rule">The rule to validate.</param>
        /// <returns>A tuple indicating validation success and error message if any.</returns>
        private async Task<(bool IsValid, string ErrorMessage)> ValidateRuleCompletelyAsync(Rule rule)
        {
            try
            {
                // Basic validation
                if (rule == null)
                    return (false, "Rule cannot be null");

                if (string.IsNullOrWhiteSpace(rule.Name))
                    return (false, "Rule name is required");

                if (rule.Name.Length > 200)
                    return (false, "Rule name cannot exceed 200 characters");

                // JSON validation - must catch exceptions and return false
                try
                {
                    JsonValidationHelper.EnsureValidJson(rule.FeedIds, nameof(rule.FeedIds), expectIntArray: true);
                    JsonValidationHelper.EnsureValidJson(rule.CategoryIds, nameof(rule.CategoryIds), expectIntArray: true);
                    JsonValidationHelper.EnsureValidJson(rule.TagIds, nameof(rule.TagIds), expectIntArray: true);
                }
                catch (ArgumentException)
                {
                    return (false, "Invalid JSON format in FeedIds, CategoryIds, or TagIds");
                }

                // Scope validation
                if (rule.Scope == Core.Enums.RuleScope.SpecificFeeds && string.IsNullOrWhiteSpace(rule.FeedIds))
                    return (false, "FeedIds are required when scope is SpecificFeeds");

                if (rule.Scope == Core.Enums.RuleScope.SpecificCategories && string.IsNullOrWhiteSpace(rule.CategoryIds))
                    return (false, "CategoryIds are required when scope is SpecificCategories");

                // Action validation
                if (rule.ActionType == Core.Enums.RuleActionType.ApplyTags && string.IsNullOrWhiteSpace(rule.TagIds))
                    return (false, "TagIds are required when action is ApplyTags");

                if (rule.ActionType == Core.Enums.RuleActionType.MoveToCategory && !rule.CategoryId.HasValue)
                    return (false, "CategoryId is required when action is MoveToCategory");

                if (rule.ActionType == Core.Enums.RuleActionType.HighlightArticle && string.IsNullOrWhiteSpace(rule.HighlightColor))
                    return (false, "HighlightColor is required when action is HighlightArticle");

                // Condition validation for simple rules
                if (!rule.UsesAdvancedConditions)
                {
                    if (rule.Operator == Core.Enums.RuleOperator.Regex && string.IsNullOrWhiteSpace(rule.RegexPattern))
                        return (false, "RegexPattern is required when operator is Regex");

                    if (rule.Operator != Core.Enums.RuleOperator.IsEmpty &&
                        rule.Operator != Core.Enums.RuleOperator.IsNotEmpty &&
                        string.IsNullOrWhiteSpace(rule.Value))
                        return (false, $"Value is required for operator {rule.Operator}");
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during rule validation");
                return (false, "Validation error occurred");
            }
        }

        #endregion

        #region Private Evaluation Methods

        /// <summary>
        /// Evaluates whether a rule applies to a specific article.
        /// </summary>
        private async Task<bool> EvaluateRuleAgainstArticleAsync(Rule rule, Article article)
        {
            try
            {
                if (!await DoesRuleApplyToArticleAsync(rule, article))
                    return false;

                var comparison = rule.IsCaseSensitive ?
                    StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                var targetText = GetTargetText(article, rule.Target);
                return EvaluateSimpleCondition(targetText, rule.Operator, rule.Value,
                    rule.RegexPattern, comparison);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to evaluate rule {RuleName} against article", rule.Name);
                return false;
            }
        }

        /// <summary>
        /// Determines if a rule should be applied to an article based on scope.
        /// </summary>
        private async Task<bool> DoesRuleApplyToArticleAsync(Rule rule, Article article)
        {
            try
            {
                var feed = await _feedRepository.GetByIdAsync(article.FeedId);
                if (feed == null) return false;

                switch (rule.Scope)
                {
                    case Core.Enums.RuleScope.AllFeeds:
                        return true;
                    case Core.Enums.RuleScope.SpecificFeeds:
                        return rule.FeedIdList.Contains(article.FeedId);
                    case Core.Enums.RuleScope.SpecificCategories:
                        return feed.CategoryId.HasValue && rule.CategoryIdList.Contains(feed.CategoryId.Value);
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extracts the target text from an article based on field specification.
        /// </summary>
        private string GetTargetText(Article article, Core.Enums.RuleFieldTarget field) => field switch
        {
            Core.Enums.RuleFieldTarget.Title => article.Title ?? "",
            Core.Enums.RuleFieldTarget.Content => article.Content ?? article.Summary ?? "",
            Core.Enums.RuleFieldTarget.Author => article.Author ?? "",
            Core.Enums.RuleFieldTarget.Categories => article.Categories ?? "",
            Core.Enums.RuleFieldTarget.AllFields or Core.Enums.RuleFieldTarget.AnyField =>
                $"{article.Title} {article.Content} {article.Summary}",
            _ => ""
        };

        /// <summary>
        /// Evaluates a simple condition without advanced grouping.
        /// </summary>
        private bool EvaluateSimpleCondition(string text, Core.Enums.RuleOperator op, string value,
            string regexPattern, StringComparison comparison) => op switch
            {
                Core.Enums.RuleOperator.Contains => text.Contains(value, comparison),
                Core.Enums.RuleOperator.Equals => text.Equals(value, comparison),
                Core.Enums.RuleOperator.StartsWith => text.StartsWith(value, comparison),
                Core.Enums.RuleOperator.EndsWith => text.EndsWith(value, comparison),
                Core.Enums.RuleOperator.NotContains => !text.Contains(value, comparison),
                Core.Enums.RuleOperator.NotEquals => !text.Equals(value, comparison),
                Core.Enums.RuleOperator.Regex => EvaluateRegexCondition(text, regexPattern, comparison == StringComparison.Ordinal),
                Core.Enums.RuleOperator.GreaterThan => CompareValues(text, value) > 0,
                Core.Enums.RuleOperator.LessThan => CompareValues(text, value) < 0,
                Core.Enums.RuleOperator.IsEmpty => string.IsNullOrWhiteSpace(text),
                Core.Enums.RuleOperator.IsNotEmpty => !string.IsNullOrWhiteSpace(text),
                _ => false
            };

        /// <summary>
        /// Evaluates a regex pattern against text with timeout protection.
        /// </summary>
        private bool EvaluateRegexCondition(string text, string pattern, bool caseSensitive)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pattern)) return false;
                var regexOptions = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                regexOptions |= RegexOptions.Compiled;
                var regex = new Regex(pattern, regexOptions, TimeSpan.FromMilliseconds(100));
                return regex.IsMatch(text);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Invalid regex pattern: {Pattern}", pattern);
                return false;
            }
        }

        /// <summary>
        /// Compares two values with support for numeric and date comparisons.
        /// </summary>
        private int CompareValues(string text, string value)
        {
            if (double.TryParse(text, out double textNum) && double.TryParse(value, out double valueNum))
                return textNum.CompareTo(valueNum);
            if (DateTime.TryParse(text, out DateTime textDate) && DateTime.TryParse(value, out DateTime valueDate))
                return textDate.CompareTo(valueDate);
            return string.Compare(text, value, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}