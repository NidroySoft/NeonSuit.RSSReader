using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Notifications;
using NeonSuit.RSSReader.Core.DTOs.Rules;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Helpers;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="IRuleService"/> providing comprehensive rule management and article processing.
    /// Analyzes articles and triggers automated actions based on database-stored rules.
    /// </summary>
    internal class RuleService : IRuleService
    {
        private readonly IRuleRepository _ruleRepository;
        private readonly IRuleConditionRepository _ruleConditionRepository;
        private readonly IArticleRepository _articleRepository;
        private readonly IFeedRepository _feedRepository;
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();
        private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(100);

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleService"/> class.
        /// </summary>
        /// <param name="ruleRepository">The rule repository.</param>
        /// <param name="ruleConditionRepository">The rule condition repository.</param>
        /// <param name="articleRepository">The article repository.</param>
        /// <param name="feedRepository">The feed repository.</param>
        /// <param name="notificationService">The notification service.</param>
        /// <param name="mapper">AutoMapper instance for DTO transformations.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
        public RuleService(
            IRuleRepository ruleRepository,
            IRuleConditionRepository ruleConditionRepository,
            IArticleRepository articleRepository,
            IFeedRepository feedRepository,
            INotificationService notificationService,
            IMapper mapper,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(ruleRepository);
            ArgumentNullException.ThrowIfNull(ruleConditionRepository);
            ArgumentNullException.ThrowIfNull(articleRepository);
            ArgumentNullException.ThrowIfNull(feedRepository);
            ArgumentNullException.ThrowIfNull(notificationService);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(logger);

            _ruleRepository = ruleRepository;
            _ruleConditionRepository = ruleConditionRepository;
            _articleRepository = articleRepository;
            _feedRepository = feedRepository;
            _notificationService = notificationService;
            _mapper = mapper;
            _logger = logger.ForContext<RuleService>();

#if DEBUG
            _logger.Debug("RuleService initialized");
#endif
        }

        #endregion

        #region Rule Management

        /// <inheritdoc />
        public async Task<RuleDto?> GetRuleByIdAsync(int ruleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (ruleId <= 0)
                {
                    _logger.Warning("Invalid rule ID provided: {RuleId}", ruleId);
                    throw new ArgumentOutOfRangeException(nameof(ruleId), "Rule ID must be greater than 0");
                }

                _logger.Debug("Retrieving rule by ID: {RuleId}", ruleId);
                var rule = await _ruleRepository.GetByIdAsync(ruleId, cancellationToken).ConfigureAwait(false);

                if (rule == null)
                {
                    _logger.Debug("Rule not found with ID: {RuleId}", ruleId);
                    return null;
                }

                var ruleDto = _mapper.Map<RuleDto>(rule);
                ruleDto.HumanReadableCondition = await GenerateHumanReadableConditionAsync(rule, cancellationToken).ConfigureAwait(false);

                _logger.Debug("Found rule: {RuleName} (ID: {RuleId})", rule.Name, ruleId);
                return ruleDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetRuleByIdAsync operation was cancelled for ID: {RuleId}", ruleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve rule by ID: {RuleId}", ruleId);
                throw new InvalidOperationException($"Failed to retrieve rule with ID {ruleId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<RuleSummaryDto>> GetAllRulesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving all rules");
                var rules = await _ruleRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
                var ruleDtos = _mapper.Map<List<RuleSummaryDto>>(rules);

                foreach (var dto in ruleDtos)
                {
                    var rule = rules.First(r => r.Id == dto.Id);
                    dto.LastMatchTimeAgo = FormatTimeAgo(rule.LastMatchDate);
                }

                _logger.Information("Retrieved {Count} rules", ruleDtos.Count);
                return ruleDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAllRulesAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve all rules");
                throw new InvalidOperationException("Failed to retrieve all rules", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<RuleDto>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving active rules");

                var rules = await _ruleRepository.GetActiveRulesAsync(cancellationToken).ConfigureAwait(false);
                var ruleDtos = _mapper.Map<List<RuleDto>>(rules);

                foreach (var dto in ruleDtos)
                {
                    dto.HumanReadableCondition = await GenerateHumanReadableConditionAsync(
                        rules.First(r => r.Id == dto.Id), cancellationToken).ConfigureAwait(false);
                }

                _logger.Information("Retrieved {Count} active rules", ruleDtos.Count);
                return ruleDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetActiveRulesAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve active rules");
                throw new InvalidOperationException("Failed to retrieve active rules", ex);
            }
        }

        /// <inheritdoc />
        public async Task<RuleDto> CreateRuleAsync(CreateRuleDto createDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(createDto);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.Information("Creating new rule: {RuleName}", createDto.Name);

                // Validate name uniqueness
                var exists = await _ruleRepository.ExistsByNameAsync(createDto.Name, cancellationToken).ConfigureAwait(false);
                if (exists)
                {
                    _logger.Warning("Rule with name '{RuleName}' already exists", createDto.Name);
                    throw new InvalidOperationException($"A rule with name '{createDto.Name}' already exists.");
                }

                var rule = _mapper.Map<Rule>(createDto);

                // Set default values
                rule.Priority = rule.Priority <= 0 ? 100 : rule.Priority;
                rule.CreatedAt = DateTime.UtcNow;
                rule.LastModified = DateTime.UtcNow;
                rule.MatchCount = 0;
                rule.LastMatchDate = null;

                // Validate rule
                var validationResult = await ValidateRuleCompletelyAsync(rule, cancellationToken).ConfigureAwait(false);
                if (!validationResult.IsValid)
                {
                    _logger.Warning("Rule validation failed: {ErrorMessage}", validationResult.ErrorMessage);
                    throw new ArgumentException(validationResult.ErrorMessage);
                }

                await _ruleRepository.InsertAsync(rule, cancellationToken).ConfigureAwait(false);

                _logger.Information("Successfully created rule: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);

                var ruleDto = _mapper.Map<RuleDto>(rule);
                ruleDto.HumanReadableCondition = await GenerateHumanReadableConditionAsync(rule, cancellationToken).ConfigureAwait(false);

                return ruleDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CreateRuleAsync operation was cancelled for rule: {RuleName}", createDto.Name);
                throw;
            }
            catch (Exception ex) when (ex is not ArgumentNullException and not ArgumentException and not InvalidOperationException)
            {
                _logger.Error(ex, "Failed to create rule: {RuleName}", createDto.Name);
                throw new InvalidOperationException($"Failed to create rule '{createDto.Name}'", ex);
            }
        }

        /// <inheritdoc />
        public async Task<RuleDto?> UpdateRuleAsync(int ruleId, UpdateRuleDto updateDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updateDto);

            try
            {
                if (ruleId <= 0)
                {
                    _logger.Warning("Invalid rule ID provided for update: {RuleId}", ruleId);
                    throw new ArgumentOutOfRangeException(nameof(ruleId), "Rule ID must be greater than 0");
                }

                cancellationToken.ThrowIfCancellationRequested();

                _logger.Debug("Updating rule ID: {RuleId}", ruleId);

                var existingRule = await _ruleRepository.GetByIdAsync(ruleId, cancellationToken).ConfigureAwait(false);
                if (existingRule == null)
                {
                    _logger.Warning("Rule not found for update: ID {RuleId}", ruleId);
                    return null;
                }

                // Track original name for uniqueness check
                var originalName = existingRule.Name;

                // Apply updates from DTO
                _mapper.Map(updateDto, existingRule);

                // Check name uniqueness if changed
                if (!originalName.Equals(existingRule.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var nameExists = await _ruleRepository.ExistsByNameAsync(existingRule.Name, cancellationToken).ConfigureAwait(false);
                    if (nameExists)
                    {
                        _logger.Warning("Another rule with name '{RuleName}' already exists", existingRule.Name);
                        throw new InvalidOperationException($"Another rule with name '{existingRule.Name}' already exists.");
                    }
                }

                existingRule.LastModified = DateTime.UtcNow;

                // Handle match count reset if requested
                if (updateDto.ResetMatchCount == true)
                {
                    existingRule.MatchCount = 0;
                    existingRule.LastMatchDate = null;
                }

                // Validate rule
                var validationResult = await ValidateRuleCompletelyAsync(existingRule, cancellationToken).ConfigureAwait(false);
                if (!validationResult.IsValid)
                {
                    _logger.Warning("Rule validation failed: {ErrorMessage}", validationResult.ErrorMessage);
                    throw new ArgumentException(validationResult.ErrorMessage);
                }

                var result = await _ruleRepository.UpdateAsync(existingRule, cancellationToken).ConfigureAwait(false);

                if (result > 0)
                {
                    _logger.Information("Successfully updated rule: {RuleName} (ID: {RuleId})", existingRule.Name, ruleId);

                    var ruleDto = _mapper.Map<RuleDto>(existingRule);
                    ruleDto.HumanReadableCondition = await GenerateHumanReadableConditionAsync(existingRule, cancellationToken).ConfigureAwait(false);

                    return ruleDto;
                }

                _logger.Warning("No changes made to rule ID: {RuleId}", ruleId);
                return null;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateRuleAsync operation was cancelled for rule ID: {RuleId}", ruleId);
                throw;
            }
            catch (Exception ex) when (ex is not ArgumentNullException and not ArgumentException and not InvalidOperationException)
            {
                _logger.Error(ex, "Failed to update rule ID: {RuleId}", ruleId);
                throw new InvalidOperationException($"Failed to update rule with ID {ruleId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (ruleId <= 0)
                {
                    _logger.Warning("Invalid rule ID provided for deletion: {RuleId}", ruleId);
                    throw new ArgumentOutOfRangeException(nameof(ruleId), "Rule ID must be greater than 0");
                }

                _logger.Information("Deleting rule with ID: {RuleId}", ruleId);

                // First get the rule entity
                var rule = await _ruleRepository.GetByIdAsync(ruleId, cancellationToken).ConfigureAwait(false);
                if (rule == null)
                {
                    _logger.Warning("Rule with ID {RuleId} not found for deletion", ruleId);
                    return false;
                }

                // Delete associated conditions first
                await _ruleConditionRepository.DeleteByRuleIdAsync(ruleId, cancellationToken).ConfigureAwait(false);

                // CORREGIDO: Pasar la entidad completa, no solo el ID
                var result = await _ruleRepository.DeleteAsync(rule, cancellationToken).ConfigureAwait(false);
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Successfully deleted rule with ID: {RuleId}", ruleId);
                }

                return success;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteRuleAsync operation was cancelled for ID: {RuleId}", ruleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete rule with ID: {RuleId}", ruleId);
                throw new InvalidOperationException($"Failed to delete rule with ID {ruleId}", ex);
            }
        }

        #endregion

        #region Rule Evaluation

        /// <inheritdoc />
        public async Task<List<RuleDto>> EvaluateArticleAgainstRulesAsync(int articleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (articleId <= 0)
                {
                    _logger.Warning("Invalid article ID provided for evaluation: {ArticleId}", articleId);
                    return new List<RuleDto>();
                }

                cancellationToken.ThrowIfCancellationRequested();

                var article = await _articleRepository.GetByIdAsync(articleId, cancellationToken).ConfigureAwait(false);
                if (article == null)
                {
                    _logger.Warning("Article not found for evaluation: ID {ArticleId}", articleId);
                    return new List<RuleDto>();
                }

                _logger.Debug("Evaluating article against rules: {ArticleTitle} (ID: {ArticleId})",
                    article.Title, article.Id);

                var matchedRules = new List<RuleDto>();
                var activeRules = await GetActiveRulesAsync(cancellationToken).ConfigureAwait(false);

                if (!activeRules.Any())
                {
                    _logger.Debug("No active rules found for article evaluation");
                    return matchedRules;
                }

                foreach (var rule in activeRules.OrderBy(r => r.Priority))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var ruleEntity = await _ruleRepository.GetByIdAsync(rule.Id, cancellationToken).ConfigureAwait(false);
                    if (ruleEntity == null) continue;

                    if (await EvaluateRuleAgainstArticleAsync(ruleEntity, article, cancellationToken).ConfigureAwait(false))
                    {
                        matchedRules.Add(rule);

                        // Update rule statistics
                        await UpdateRuleMatchStatsAsync(rule.Id, cancellationToken).ConfigureAwait(false);

                        // Stop processing if rule has StopOnMatch enabled
                        if (rule.StopOnMatch)
                        {
                            _logger.Debug("Rule {RuleName} matched with StopOnMatch enabled, stopping evaluation", rule.Name);
                            break;
                        }
                    }
                }

                _logger.Information("Article '{ArticleTitle}' matched {Count} rules", article.Title, matchedRules.Count);
                return matchedRules;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("EvaluateArticleAgainstRulesAsync operation was cancelled for article ID: {ArticleId}", articleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to evaluate article against rules for article ID: {ArticleId}", articleId);
                throw new InvalidOperationException($"Failed to evaluate article {articleId} against rules", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExecuteRuleActionsAsync(int ruleId, int articleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (ruleId <= 0 || articleId <= 0)
                {
                    _logger.Warning("Invalid IDs for action execution: RuleId {RuleId}, ArticleId {ArticleId}", ruleId, articleId);
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var rule = await _ruleRepository.GetByIdAsync(ruleId, cancellationToken).ConfigureAwait(false);
                if (rule == null)
                {
                    _logger.Warning("Rule not found for action execution: ID {RuleId}", ruleId);
                    return false;
                }

                var article = await _articleRepository.GetByIdAsync(articleId, cancellationToken).ConfigureAwait(false);
                if (article == null)
                {
                    _logger.Warning("Article not found for action execution: ID {ArticleId}", articleId);
                    return false;
                }

                _logger.Information("Executing rule actions for '{RuleName}' on article '{ArticleTitle}' (ID: {ArticleId})",
                    rule.Name, article.Title, article.Id);

                if (!await EvaluateRuleAgainstArticleAsync(rule, article, cancellationToken).ConfigureAwait(false))
                {
                    _logger.Debug("Rule {RuleName} does not match article '{ArticleTitle}'", rule.Name, article.Title);
                    return false;
                }

                bool articleModified = false;

                // Execute actions based on action type
                switch (rule.ActionType)
                {
                    case RuleActionType.MarkAsRead:
                        article.Status = ArticleStatus.Read;
                        articleModified = true;
                        _logger.Debug("MarkAsRead action applied to article {ArticleId}", article.Id);
                        break;

                    case RuleActionType.MarkAsStarred:
                        article.IsStarred = true;
                        articleModified = true;
                        _logger.Debug("MarkAsStarred action applied to article {ArticleId}", article.Id);
                        break;

                    case RuleActionType.MarkAsFavorite:
                        article.IsFavorite = true;
                        articleModified = true;
                        _logger.Debug("MarkAsFavorite action applied to article {ArticleId}", article.Id);
                        break;

                    case RuleActionType.SendNotification:
                        await SendRuleNotificationAsync(rule, article, cancellationToken).ConfigureAwait(false);
                        break;

                    case RuleActionType.ApplyTags:
                        _logger.Debug("ApplyTags action not yet fully implemented for rule {RuleName}", rule.Name);
                        // TODO: Implement tag application
                        break;

                    case RuleActionType.MoveToCategory:
                        if (rule.CategoryId.HasValue)
                        {
                            var feed = await _feedRepository.GetByIdAsync(article.FeedId, cancellationToken).ConfigureAwait(false);
                            if (feed != null)
                            {
                                feed.CategoryId = rule.CategoryId.Value;
                                await _feedRepository.UpdateAsync(feed, cancellationToken).ConfigureAwait(false);
                                _logger.Information("Moved feed {FeedId} to category {CategoryId}",
                                    feed.Id, rule.CategoryId.Value);
                            }
                        }
                        break;

                    case RuleActionType.HighlightArticle:
                        if (!string.IsNullOrEmpty(rule.HighlightColor))
                        {
                            // TODO: Implement article highlighting
                            _logger.Debug("Highlight action with color {Color} for rule {RuleName}",
                                rule.HighlightColor, rule.Name);
                        }
                        break;

                    case RuleActionType.PlaySound:
                        if (!string.IsNullOrEmpty(rule.SoundPath))
                        {
                            // TODO: Implement sound playing
                            _logger.Debug("PlaySound action with sound {SoundPath} for rule {RuleName}",
                                rule.SoundPath, rule.Name);
                        }
                        break;
                }

                // Update article if modified
                if (articleModified)
                {
                    await _articleRepository.UpdateAsync(article, cancellationToken).ConfigureAwait(false);
                    _logger.Debug("Article updated with rule actions from {RuleName}", rule.Name);
                }

                // Update rule statistics
                await UpdateRuleMatchStatsAsync(rule.Id, cancellationToken).ConfigureAwait(false);

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ExecuteRuleActionsAsync operation was cancelled for rule: {RuleId}, article: {ArticleId}", ruleId, articleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to execute rule actions for rule: {RuleId}, article: {ArticleId}", ruleId, articleId);
                throw new InvalidOperationException($"Failed to execute rule actions for rule {ruleId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, List<RuleDto>>> EvaluateArticlesBatchAsync(List<int> articleIds, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(articleIds);

                if (!articleIds.Any())
                {
                    _logger.Debug("Empty article list provided for batch evaluation");
                    return new Dictionary<int, List<RuleDto>>();
                }

                cancellationToken.ThrowIfCancellationRequested();

                _logger.Debug("Batch evaluating {ArticleCount} articles against rules", articleIds.Count);

                var results = new Dictionary<int, List<RuleDto>>();

                foreach (var articleId in articleIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (articleId > 0)
                    {
                        var matchedRules = await EvaluateArticleAgainstRulesAsync(articleId, cancellationToken).ConfigureAwait(false);
                        if (matchedRules.Any())
                        {
                            results[articleId] = matchedRules;
                        }
                    }
                }

                _logger.Information("Batch evaluation completed: {MatchedCount} articles matched rules out of {TotalCount}",
                    results.Count, articleIds.Count);

                return results;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("EvaluateArticlesBatchAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to batch evaluate {ArticleCount} articles", articleIds?.Count ?? 0);
                throw new InvalidOperationException("Failed to batch evaluate articles", ex);
            }
        }

        #endregion

        #region Rule Conditions

        /// <inheritdoc />
        public async Task<List<RuleConditionGroupDto>> GetRuleConditionsAsync(int ruleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (ruleId <= 0)
                {
                    _logger.Warning("Invalid rule ID for conditions: {RuleId}", ruleId);
                    throw new ArgumentOutOfRangeException(nameof(ruleId), "Rule ID must be greater than 0");
                }

                _logger.Debug("Retrieving conditions for rule ID: {RuleId}", ruleId);

                var conditionGroups = await _ruleConditionRepository.GetConditionGroupsAsync(ruleId, cancellationToken).ConfigureAwait(false);
                var result = new List<RuleConditionGroupDto>();

                foreach (var group in conditionGroups.OrderBy(g => g.Key))
                {
                    var groupDto = new RuleConditionGroupDto
                    {
                        GroupId = group.Key,
                        GroupOperator = group.Key == 0 ? LogicalOperator.AND : LogicalOperator.OR, // Default logic
                        Conditions = _mapper.Map<List<RuleConditionDto>>(group.Value.OrderBy(c => c.Order))
                    };

                    // Enrich condition DTOs
                    foreach (var condition in groupDto.Conditions)
                    {
                        condition.HumanReadable = GenerateConditionHumanReadable(condition);
                        condition.FieldDisplayName = GetFieldDisplayName(condition.Field);
                        condition.OperatorDisplayName = GetOperatorDisplayName(condition.Operator);
                        condition.IsValid = true; // Assume valid for now
                    }

                    groupDto.HumanReadable = string.Join($" {groupDto.GroupOperator} ", groupDto.Conditions.Select(c => c.HumanReadable));
                    result.Add(groupDto);
                }

                _logger.Debug("Retrieved {GroupCount} condition groups for rule ID: {RuleId}", result.Count, ruleId);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetRuleConditionsAsync operation was cancelled for rule ID: {RuleId}", ruleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve conditions for rule ID: {RuleId}", ruleId);
                throw new InvalidOperationException($"Failed to retrieve conditions for rule {ruleId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<RuleConditionDto> AddRuleConditionAsync(CreateRuleConditionDto createDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(createDto);

            try
            {
                if (createDto.RuleId <= 0)
                {
                    _logger.Warning("Invalid rule ID in create condition DTO: {RuleId}", createDto.RuleId);
                    throw new ArgumentException("Rule ID must be greater than 0", nameof(createDto));
                }

                cancellationToken.ThrowIfCancellationRequested();

                _logger.Debug("Adding condition to rule ID: {RuleId}", createDto.RuleId);

                var rule = await _ruleRepository.GetByIdAsync(createDto.RuleId, cancellationToken).ConfigureAwait(false);
                if (rule == null)
                {
                    _logger.Warning("Rule not found: ID {RuleId}", createDto.RuleId);
                    throw new InvalidOperationException($"Rule with ID {createDto.RuleId} not found");
                }

                var condition = _mapper.Map<RuleCondition>(createDto);

                // Set default order if not provided
                if (condition.Order <= 0)
                {
                    condition.Order = await _ruleConditionRepository.GetMaxOrderInGroupAsync(
                        condition.RuleId, condition.GroupId, cancellationToken).ConfigureAwait(false) + 1;
                }

                await _ruleConditionRepository.InsertAsync(condition, cancellationToken).ConfigureAwait(false);

                var conditionDto = _mapper.Map<RuleConditionDto>(condition);
                conditionDto.HumanReadable = GenerateConditionHumanReadable(conditionDto);
                conditionDto.FieldDisplayName = GetFieldDisplayName(conditionDto.Field);
                conditionDto.OperatorDisplayName = GetOperatorDisplayName(conditionDto.Operator);
                conditionDto.IsValid = true;

                rule.UsesAdvancedConditions = true;
                await _ruleRepository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);

                _logger.Information("Added condition to rule ID: {RuleId} (Condition ID: {ConditionId})",
                    createDto.RuleId, condition.Id);

                return conditionDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("AddRuleConditionAsync operation was cancelled for rule ID: {RuleId}", createDto.RuleId);
                throw;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.Error(ex, "Failed to add condition to rule ID: {RuleId}", createDto.RuleId);
                throw new InvalidOperationException($"Failed to add condition to rule {createDto.RuleId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<RuleConditionDto?> UpdateRuleConditionAsync(int conditionId, UpdateRuleConditionDto updateDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updateDto);

            try
            {
                if (conditionId <= 0)
                {
                    _logger.Warning("Invalid condition ID for update: {ConditionId}", conditionId);
                    throw new ArgumentOutOfRangeException(nameof(conditionId), "Condition ID must be greater than 0");
                }

                cancellationToken.ThrowIfCancellationRequested();

                _logger.Debug("Updating condition ID: {ConditionId}", conditionId);

                var condition = await _ruleConditionRepository.GetByIdAsync(conditionId, cancellationToken).ConfigureAwait(false);
                if (condition == null)
                {
                    _logger.Warning("Condition not found: ID {ConditionId}", conditionId);
                    return null;
                }

                _mapper.Map(updateDto, condition);
                await _ruleConditionRepository.UpdateAsync(condition, cancellationToken).ConfigureAwait(false);

                var conditionDto = _mapper.Map<RuleConditionDto>(condition);
                conditionDto.HumanReadable = GenerateConditionHumanReadable(conditionDto);
                conditionDto.FieldDisplayName = GetFieldDisplayName(conditionDto.Field);
                conditionDto.OperatorDisplayName = GetOperatorDisplayName(conditionDto.Operator);
                conditionDto.IsValid = true;

                _logger.Information("Updated condition ID: {ConditionId}", conditionId);

                return conditionDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateRuleConditionAsync operation was cancelled for condition ID: {ConditionId}", conditionId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update condition ID: {ConditionId}", conditionId);
                throw new InvalidOperationException($"Failed to update condition {conditionId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteRuleConditionAsync(int conditionId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (conditionId <= 0)
                {
                    _logger.Warning("Invalid condition ID for deletion: {ConditionId}", conditionId);
                    throw new ArgumentOutOfRangeException(nameof(conditionId), "Condition ID must be greater than 0");
                }

                _logger.Debug("Deleting condition ID: {ConditionId}", conditionId);

                // CORREGIDO: Obtener la entidad primero
                var condition = await _ruleConditionRepository.GetByIdAsync(conditionId, cancellationToken).ConfigureAwait(false);
                if (condition == null)
                {
                    _logger.Warning("Condition ID {ConditionId} not found for deletion", conditionId);
                    return false;
                }

                var result = await _ruleConditionRepository.DeleteAsync(condition, cancellationToken).ConfigureAwait(false);
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Deleted condition ID: {ConditionId}", conditionId);
                }

                return success;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteRuleConditionAsync operation was cancelled for condition ID: {ConditionId}", conditionId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete condition ID: {ConditionId}", conditionId);
                throw new InvalidOperationException($"Failed to delete condition {conditionId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<int> ReorderConditionsAsync(int ruleId, int groupId, Dictionary<int, int> conditionOrderMap, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(conditionOrderMap);

            try
            {
                if (ruleId <= 0 || groupId <= 0)
                {
                    _logger.Warning("Invalid parameters for reorder: RuleId {RuleId}, GroupId {GroupId}", ruleId, groupId);
                    throw new ArgumentException("Rule ID and Group ID must be greater than 0");
                }

                _logger.Debug("Reordering {Count} conditions in rule {RuleId}, group {GroupId}",
                    conditionOrderMap.Count, ruleId, groupId);

                var result = await _ruleConditionRepository.ReorderConditionsAsync(ruleId, groupId, conditionOrderMap, cancellationToken).ConfigureAwait(false);

                _logger.Information("Reordered {Count} conditions in rule {RuleId}, group {GroupId}", result, ruleId, groupId);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ReorderConditionsAsync operation was cancelled for rule ID: {RuleId}", ruleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reorder conditions for rule ID: {RuleId}", ruleId);
                throw new InvalidOperationException($"Failed to reorder conditions for rule {ruleId}", ex);
            }
        }

        #endregion

        #region Statistics

        /// <inheritdoc />
        public async Task<List<RuleHealthDto>> GetTopRulesByMatchCountAsync(int limit = 10, CancellationToken cancellationToken = default)
        {
            try
            {
                if (limit < 1)
                {
                    _logger.Warning("Invalid limit for top rules: {Limit}", limit);
                    throw new ArgumentException("Limit must be at least 1", nameof(limit));
                }

                _logger.Debug("Retrieving top {Limit} rules by match count", limit);

                var rules = await _ruleRepository.GetTopRulesByMatchCountAsync(limit, cancellationToken).ConfigureAwait(false);
                var healthDtos = _mapper.Map<List<RuleHealthDto>>(rules);

                var now = DateTime.UtcNow;
                foreach (var dto in healthDtos)
                {
                    if (dto.LastMatchDate.HasValue)
                    {
                        var timeSpan = now - dto.LastMatchDate.Value;
                        dto.TimeSinceLastMatch = FormatTimeSpan(timeSpan);
                    }
                }

                _logger.Information("Retrieved top {Count} rules by match count", healthDtos.Count);
                return healthDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetTopRulesByMatchCountAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get top rules by match count");
                throw new InvalidOperationException("Failed to get top rules by match count", ex);
            }
        }

        /// <inheritdoc />
        public async Task<RuleHealthDto?> GetRuleStatisticsAsync(int ruleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (ruleId <= 0)
                {
                    _logger.Warning("Invalid rule ID for statistics: {RuleId}", ruleId);
                    throw new ArgumentOutOfRangeException(nameof(ruleId), "Rule ID must be greater than 0");
                }

                _logger.Debug("Retrieving statistics for rule ID: {RuleId}", ruleId);

                var rule = await _ruleRepository.GetByIdAsync(ruleId, cancellationToken).ConfigureAwait(false);
                if (rule == null)
                {
                    _logger.Warning("Rule not found for statistics: ID {RuleId}", ruleId);
                    return null;
                }

                var healthDto = _mapper.Map<RuleHealthDto>(rule);

                if (healthDto.LastMatchDate.HasValue)
                {
                    var timeSpan = DateTime.UtcNow - healthDto.LastMatchDate.Value;
                    healthDto.TimeSinceLastMatch = FormatTimeSpan(timeSpan);
                }

                // Calculate average matches per day if rule is old enough
                var daysSinceCreation = (DateTime.UtcNow - rule.CreatedAt).TotalDays;
                if (daysSinceCreation > 0)
                {
                    healthDto.AverageMatchesPerDay = rule.MatchCount / daysSinceCreation;
                }

                return healthDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetRuleStatisticsAsync operation was cancelled for rule ID: {RuleId}", ruleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get statistics for rule ID: {RuleId}", ruleId);
                throw new InvalidOperationException($"Failed to get statistics for rule {ruleId}", ex);
            }
        }

        #endregion

        #region Validation

        /// <inheritdoc />
        public async Task<bool> RuleExistsByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.Warning("Attempted to check rule existence with empty name");
                    throw new ArgumentException("Rule name cannot be empty", nameof(name));
                }

                _logger.Debug("Checking rule existence for name: {Name}", name);
                var exists = await _ruleRepository.ExistsByNameAsync(name, cancellationToken).ConfigureAwait(false);
                _logger.Debug("Rule '{Name}' exists: {Exists}", name, exists);
                return exists;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("RuleExistsByNameAsync operation was cancelled for name: {Name}", name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check rule existence for name: {Name}", name);
                throw new InvalidOperationException($"Failed to check rule existence for '{name}'", ex);
            }
        }

        #endregion

        #region Testing and Simulation

        /// <inheritdoc />
        public async Task<RuleTestResultDto> TestRuleAsync(int ruleId, List<int> sampleArticleIds, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sampleArticleIds);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                Rule? rule;
                if (ruleId > 0)
                {
                    rule = await _ruleRepository.GetByIdAsync(ruleId, cancellationToken).ConfigureAwait(false);
                    if (rule == null)
                    {
                        throw new InvalidOperationException($"Rule with ID {ruleId} not found");
                    }
                }
                else
                {
                    // For testing unsaved rules, we'd need a different approach
                    throw new NotSupportedException("Testing unsaved rules is not yet supported");
                }

                _logger.Debug("Testing rule '{RuleName}' against {ArticleCount} sample articles",
                    rule.Name, sampleArticleIds.Count);

                var result = new RuleTestResultDto
                {
                    RuleName = rule.Name,
                    TotalTested = sampleArticleIds.Count,
                    MatchedArticleIds = new List<int>()
                };

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                foreach (var articleId in sampleArticleIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (articleId <= 0) continue;

                    var article = await _articleRepository.GetByIdAsync(articleId, cancellationToken).ConfigureAwait(false);
                    if (article != null && await EvaluateRuleAgainstArticleAsync(rule, article, cancellationToken).ConfigureAwait(false))
                    {
                        result.MatchedCount++;
                        result.MatchedArticleIds.Add(articleId);
                    }
                }

                stopwatch.Stop();
                result.AverageEvaluationTimeMs = sampleArticleIds.Count > 0
                    ? stopwatch.ElapsedMilliseconds / (double)sampleArticleIds.Count
                    : 0;

                _logger.Information("Rule test completed: {MatchedCount}/{TotalTested} matches, Avg time: {AvgTime:F2}ms",
                    result.MatchedCount, result.TotalTested, result.AverageEvaluationTimeMs);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("TestRuleAsync operation was cancelled for rule: {RuleId}", ruleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to test rule: {RuleId}", ruleId);
                throw new InvalidOperationException($"Failed to test rule {ruleId}", ex);
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Performs comprehensive rule validation including business rules and constraints.
        /// </summary>
        private async Task<(bool IsValid, string ErrorMessage)> ValidateRuleCompletelyAsync(Rule rule, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Basic validation
                if (rule == null)
                    return (false, "Rule cannot be null");

                if (string.IsNullOrWhiteSpace(rule.Name))
                    return (false, "Rule name is required");

                if (rule.Name.Length > 200)
                    return (false, "Rule name cannot exceed 200 characters");

                // Scope validation
                if (rule.Scope == RuleScope.SpecificFeeds && (rule.FeedIdList == null || !rule.FeedIdList.Any()))
                    return (false, "Feed IDs are required when scope is SpecificFeeds");

                if (rule.Scope == RuleScope.SpecificCategories && (rule.CategoryIdList == null || !rule.CategoryIdList.Any()))
                    return (false, "Category IDs are required when scope is SpecificCategories");

                // Action validation
                if (rule.ActionType == RuleActionType.ApplyTags && (rule.TagIdList == null || !rule.TagIdList.Any()))
                    return (false, "Tag IDs are required when action is ApplyTags");

                if (rule.ActionType == RuleActionType.MoveToCategory && !rule.CategoryId.HasValue)
                    return (false, "Category ID is required when action is MoveToCategory");

                if (rule.ActionType == RuleActionType.HighlightArticle && string.IsNullOrWhiteSpace(rule.HighlightColor))
                    return (false, "Highlight color is required when action is HighlightArticle");

                if (rule.ActionType == RuleActionType.PlaySound && string.IsNullOrWhiteSpace(rule.SoundPath))
                    return (false, "Sound path is required when action is PlaySound");

                return (true, string.Empty);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during rule validation");
                return (false, "Validation error occurred");
            }
        }

        /// <summary>
        /// Evaluates whether a rule applies to a specific article.
        /// </summary>
        private async Task<bool> EvaluateRuleAgainstArticleAsync(Rule rule, Article article, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!await DoesRuleApplyToArticleAsync(rule, article, cancellationToken).ConfigureAwait(false))
                    return false;

                // Check if rule has advanced conditions
                if (rule.UsesAdvancedConditions)
                {
                    var conditions = await _ruleConditionRepository.GetByRuleIdAsync(rule.Id, cancellationToken).ConfigureAwait(false);
                    if (!conditions.Any())
                    {
                        _logger.Warning("Rule {RuleName} marked as using advanced conditions but has no conditions", rule.Name);
                        return false;
                    }

                    // Group conditions by GroupId
                    var groupedConditions = conditions.GroupBy(c => c.GroupId).ToList();

                    if (groupedConditions.Count == 1)
                    {
                        // Single group - evaluate all conditions with AND logic (default for group 0)
                        return EvaluateConditionGroup(conditions.ToList(), article);
                    }
                    else
                    {
                        // Multiple groups - OR between groups, AND within groups
                        foreach (var group in groupedConditions)
                        {
                            var groupConditions = group.ToList();
                            if (EvaluateConditionGroup(groupConditions, article))
                            {
                                return true;
                            }
                        }
                        return false;
                    }
                }
                else
                {
                    // Simple rule with single condition
                    var comparison = rule.IsCaseSensitive ?
                        StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                    var targetText = GetTargetText(article, rule.Target);
                    return EvaluateSimpleCondition(targetText, rule.Operator, rule.Value,
                        rule.RegexPattern, comparison);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to evaluate rule {RuleName} against article", rule.Name);
                return false;
            }
        }

        /// <summary>
        /// Evaluates a group of conditions against an article.
        /// </summary>
        private bool EvaluateConditionGroup(List<RuleCondition> conditions, Article article)
        {
            if (!conditions.Any()) return false;

            // For now, assume AND logic within group
            foreach (var condition in conditions.OrderBy(c => c.Order))
            {
                var conditionResult = EvaluateSingleCondition(condition, article);

                if (condition.Negate)
                    conditionResult = !conditionResult;

                if (!conditionResult)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Evaluates a single condition against an article.
        /// </summary>
        private bool EvaluateSingleCondition(RuleCondition condition, Article article)
        {
            try
            {
                var targetText = GetTargetText(article, condition.Field);
                var comparison = condition.IsCaseSensitive ?
                    StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                if (condition.Operator == RuleOperator.Regex && !string.IsNullOrEmpty(condition.RegexPattern))
                {
                    return EvaluateRegexCondition(targetText, condition.RegexPattern, condition.IsCaseSensitive);
                }

                return EvaluateSimpleCondition(targetText, condition.Operator, condition.Value,
                    condition.RegexPattern, comparison);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error evaluating condition {ConditionId}", condition.Id);
                return false;
            }
        }

        /// <summary>
        /// Determines if a rule should be applied to an article based on scope.
        /// </summary>
        private async Task<bool> DoesRuleApplyToArticleAsync(Rule rule, Article article, CancellationToken cancellationToken = default)
        {
            try
            {
                var feed = await _feedRepository.GetByIdAsync(article.FeedId, cancellationToken).ConfigureAwait(false);
                if (feed == null) return false;

                switch (rule.Scope)
                {
                    case RuleScope.AllFeeds:
                        return true;

                    case RuleScope.SpecificFeeds:
                        return rule.FeedIdList?.Contains(article.FeedId) == true;

                    case RuleScope.SpecificCategories:
                        return feed.CategoryId.HasValue &&
                               rule.CategoryIdList?.Contains(feed.CategoryId.Value) == true;

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
        private static string GetTargetText(Article article, RuleFieldTarget field) => field switch
        {
            RuleFieldTarget.Title => article.Title ?? "",
            RuleFieldTarget.Content => article.Content ?? article.Summary ?? "",
            RuleFieldTarget.Author => article.Author ?? "",
            RuleFieldTarget.Categories => article.Categories ?? "",
            RuleFieldTarget.AllFields or RuleFieldTarget.AnyField =>
                $"{article.Title} {article.Content} {article.Summary}".Trim(),
            _ => ""
        };

        /// <summary>
        /// Evaluates a simple condition without advanced grouping.
        /// </summary>
        private bool EvaluateSimpleCondition(string text, RuleOperator op, string value,
            string regexPattern, StringComparison comparison)
        {
            try
            {
                return op switch
                {
                    RuleOperator.Contains => text.Contains(value, comparison),
                    RuleOperator.Equals => text.Equals(value, comparison),
                    RuleOperator.StartsWith => text.StartsWith(value, comparison),
                    RuleOperator.EndsWith => text.EndsWith(value, comparison),
                    RuleOperator.NotContains => !text.Contains(value, comparison),
                    RuleOperator.NotEquals => !text.Equals(value, comparison),
                    RuleOperator.Regex => EvaluateRegexCondition(text, regexPattern, comparison == StringComparison.Ordinal),
                    RuleOperator.GreaterThan => CompareValues(text, value) > 0,
                    RuleOperator.LessThan => CompareValues(text, value) < 0,
                    RuleOperator.IsEmpty => string.IsNullOrWhiteSpace(text),
                    RuleOperator.IsNotEmpty => !string.IsNullOrWhiteSpace(text),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error evaluating simple condition with operator {Operator}", op);
                return false;
            }
        }

        /// <summary>
        /// Evaluates a regex pattern against text with timeout protection.
        /// </summary>
        private bool EvaluateRegexCondition(string text, string pattern, bool caseSensitive)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    _logger.Warning("Empty regex pattern provided");
                    return false;
                }

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
                _logger.Error(ex, "Invalid regex pattern: {Pattern}", pattern);
                return false;
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.Warning("Regex evaluation timed out for pattern: {Pattern}", pattern);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in regex evaluation for pattern: {Pattern}", pattern);
                return false;
            }
        }

        /// <summary>
        /// Compares two values with support for numeric and date comparisons.
        /// </summary>
        private static int CompareValues(string text, string value)
        {
            if (double.TryParse(text, out double textNum) && double.TryParse(value, out double valueNum))
                return textNum.CompareTo(valueNum);

            if (DateTime.TryParse(text, out DateTime textDate) && DateTime.TryParse(value, out DateTime valueDate))
                return textDate.CompareTo(valueDate);

            return string.Compare(text, value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Updates the match statistics for a rule.
        /// </summary>
        private async Task UpdateRuleMatchStatsAsync(int ruleId, CancellationToken cancellationToken = default)
        {
            try
            {
                var rule = await _ruleRepository.GetByIdAsync(ruleId, cancellationToken).ConfigureAwait(false);
                if (rule == null) return;

                rule.MatchCount++;
                rule.LastMatchDate = DateTime.UtcNow;
                // HealthStatus se calcula automáticamente desde la propiedad de solo lectura

                await _ruleRepository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);

                _logger.Debug("Updated match stats for rule {RuleId}: Count={MatchCount}, LastMatch={LastMatchDate}",
                    ruleId, rule.MatchCount, rule.LastMatchDate);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update rule match stats for rule ID: {RuleId}", ruleId);
            }
        }

        /// <summary>
        /// Sends a notification for a rule match.
        /// </summary>
        private async Task SendRuleNotificationAsync(Rule rule, Article article, CancellationToken cancellationToken = default)
        {
            try
            {
                var createNotificationDto = new CreateNotificationDto
                {
                    ArticleId = article.Id,
                    RuleId = rule.Id,
                    NotificationType = NotificationType.Toast,
                    Priority = rule.NotificationPriority,
                    Title = $"Rule matched: {rule.Name}",
                    Message = rule.NotificationTemplate?
                        .Replace("{Title}", article.Title)
                        .Replace("{Summary}", article.Summary ?? "")
                        .Replace("{Author}", article.Author ?? "")
                        ?? $"Article '{article.Title}' matched rule '{rule.Name}'",
                    Channel = "RuleEngine",
                    Duration = 7
                };

                await _notificationService.SendNotificationAsync(createNotificationDto, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to send notification for rule {RuleName}", rule.Name);
            }
        }

        /// <summary>
        /// Generates a human-readable condition string for a rule.
        /// </summary>
        private async Task<string> GenerateHumanReadableConditionAsync(Rule rule, CancellationToken cancellationToken = default)
        {
            try
            {
                if (rule.UsesAdvancedConditions)
                {
                    var conditions = await _ruleConditionRepository.GetByRuleIdAsync(rule.Id, cancellationToken).ConfigureAwait(false);
                    if (!conditions.Any())
                        return "No conditions defined";

                    var groupedConditions = conditions.GroupBy(c => c.GroupId).ToList();

                    if (groupedConditions.Count == 1)
                    {
                        var conditionStrings = groupedConditions.First()
                            .OrderBy(c => c.Order)
                            .Select(c => GenerateConditionHumanReadable(_mapper.Map<RuleConditionDto>(c)));
                        return string.Join(" AND ", conditionStrings);
                    }
                    else
                    {
                        var groupStrings = new List<string>();
                        foreach (var group in groupedConditions)
                        {
                            var conditionStrings = group.OrderBy(c => c.Order)
                                .Select(c => GenerateConditionHumanReadable(_mapper.Map<RuleConditionDto>(c)));
                            groupStrings.Add($"({string.Join(" AND ", conditionStrings)})");
                        }
                        return string.Join(" OR ", groupStrings);
                    }
                }
                else
                {
                    return $"{rule.Target} {rule.Operator} '{rule.Value}'";
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating human-readable condition for rule {RuleId}", rule.Id);
                return "Complex condition";
            }
        }

        /// <summary>
        /// Generates a human-readable string for a condition DTO.
        /// </summary>
        private string GenerateConditionHumanReadable(RuleConditionDto condition)
        {
            try
            {
                var fieldName = GetFieldDisplayName(condition.Field);
                var operatorName = GetOperatorDisplayName(condition.Operator);
                var value = condition.Operator == RuleOperator.Regex ? condition.RegexPattern : condition.Value;

                var result = $"{fieldName} {operatorName} '{value}'";
                if (condition.Negate)
                    result = $"NOT ({result})";

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating condition human-readable");
                return "Complex condition";
            }
        }

        /// <summary>
        /// Gets display name for a field target.
        /// </summary>
        private static string GetFieldDisplayName(RuleFieldTarget field) => field switch
        {
            RuleFieldTarget.Title => "Título",
            RuleFieldTarget.Content => "Contenido",
            RuleFieldTarget.Author => "Autor",
            RuleFieldTarget.Categories => "Categorías",
            RuleFieldTarget.AllFields => "Todos los campos",
            RuleFieldTarget.AnyField => "Cualquier campo",
            _ => field.ToString()
        };

        /// <summary>
        /// Gets display name for an operator.
        /// </summary>
        private static string GetOperatorDisplayName(RuleOperator op) => op switch
        {
            RuleOperator.Contains => "contiene",
            RuleOperator.Equals => "es igual a",
            RuleOperator.StartsWith => "comienza con",
            RuleOperator.EndsWith => "termina con",
            RuleOperator.NotContains => "no contiene",
            RuleOperator.NotEquals => "no es igual a",
            RuleOperator.Regex => "coincide con regex",
            RuleOperator.GreaterThan => "mayor que",
            RuleOperator.LessThan => "menor que",
            RuleOperator.IsEmpty => "está vacío",
            RuleOperator.IsNotEmpty => "no está vacío",
            _ => op.ToString()
        };

        /// <summary>
        /// Formats a time span for display.
        /// </summary>
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalMinutes < 1)
                return "hace menos de un minuto";
            if (timeSpan.TotalMinutes < 60)
                return $"hace {timeSpan.Minutes} minutos";
            if (timeSpan.TotalHours < 24)
                return $"hace {timeSpan.Hours} horas";
            if (timeSpan.TotalDays < 30)
                return $"hace {timeSpan.Days} días";
            if (timeSpan.TotalDays < 365)
                return $"hace {timeSpan.Days / 30} meses";
            return $"hace {timeSpan.Days / 365} años";
        }

        /// <summary>
        /// Formats a time ago string.
        /// </summary>
        private static string FormatTimeAgo(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
                return "Nunca";

            var timeSpan = DateTime.UtcNow - dateTime.Value;
            return FormatTimeSpan(timeSpan);
        }

        #endregion
    }
}