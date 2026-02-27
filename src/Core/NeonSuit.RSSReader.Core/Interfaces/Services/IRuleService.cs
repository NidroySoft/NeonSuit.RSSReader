using NeonSuit.RSSReader.Core.DTOs.Rules;
using NeonSuit.RSSReader.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for automated rules engine management.
    /// Provides comprehensive rule lifecycle management, article evaluation, and action execution capabilities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service centralizes all rule-related functionality including:
    /// <list type="bullet">
    /// <item>Rule CRUD operations with validation and duplicate prevention</item>
    /// <item>Article evaluation against all active rules</item>
    /// <item>Rule action execution (notifications, tagging, marking read, etc.)</item>
    /// <item>Statistics tracking for rule effectiveness monitoring</item>
    /// <item>Performance-optimized batch processing for multiple articles</item>
    /// </list>
    /// </para>
    /// <para>
    /// All methods return DTOs instead of entities to maintain separation of concerns.
    /// </para>
    /// </remarks>
    public interface IRuleService
    {
        #region Rule Management

        /// <summary>
        /// Retrieves a specific rule by its unique identifier.
        /// </summary>
        /// <param name="ruleId">The unique identifier of the rule.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The rule DTO if found; otherwise, null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="ruleId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<RuleDto?> GetRuleByIdAsync(int ruleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all rules from the system, regardless of active status.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of all rule DTOs.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<RuleSummaryDto>> GetAllRulesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves only active rules that should be evaluated against new articles.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of active rule DTOs.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// Only rules with <c>IsEnabled = true</c> are returned.
        /// This method is optimized for frequent calls during article processing.
        /// </remarks>
        Task<List<RuleDto>> GetActiveRulesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new rule in the system.
        /// </summary>
        /// <param name="createDto">The DTO containing rule creation data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created rule DTO with generated ID and timestamps.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="createDto"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if rule validation fails (invalid name, conditions, etc.).</exception>
        /// <exception cref="InvalidOperationException">Thrown if a rule with the same name already exists.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<RuleDto> CreateRuleAsync(CreateRuleDto createDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing rule.
        /// </summary>
        /// <param name="ruleId">The ID of the rule to update.</param>
        /// <param name="updateDto">The DTO containing rule update data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated rule DTO if successful; otherwise, null.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="updateDto"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="ruleId"/> is less than or equal to 0.</exception>
        /// <exception cref="ArgumentException">Thrown if rule validation fails.</exception>
        /// <exception cref="InvalidOperationException">Thrown if another rule with the same name already exists.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<RuleDto?> UpdateRuleAsync(int ruleId, UpdateRuleDto updateDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a rule by its unique identifier.
        /// </summary>
        /// <param name="ruleId">The ID of the rule to delete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if deletion was successful; otherwise, false (e.g., rule not found).</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="ruleId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default);

        #endregion

        #region Rule Evaluation

        /// <summary>
        /// Evaluates an article against all active rules to determine which rules match.
        /// </summary>
        /// <param name="articleId">The ID of the article to evaluate.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of rule DTOs that matched the article.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="articleId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<RuleDto>> EvaluateArticleAgainstRulesAsync(int articleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes all actions associated with a rule for a specific article.
        /// </summary>
        /// <param name="ruleId">The ID of the rule whose actions to execute.</param>
        /// <param name="articleId">The ID of the article that triggered the rule.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if all actions were executed successfully; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="ruleId"/> or <paramref name="articleId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> ExecuteRuleActionsAsync(int ruleId, int articleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates multiple articles against all active rules in batch.
        /// </summary>
        /// <param name="articleIds">The list of article IDs to evaluate.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A dictionary mapping article IDs to lists of matching rule DTOs.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="articleIds"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<Dictionary<int, List<RuleDto>>> EvaluateArticlesBatchAsync(List<int> articleIds, CancellationToken cancellationToken = default);

        #endregion

        #region Rule Conditions

        /// <summary>
        /// Retrieves all conditions for a specific rule, organized by group.
        /// </summary>
        /// <param name="ruleId">The ID of the rule.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of condition groups for the rule.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="ruleId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<RuleConditionGroupDto>> GetRuleConditionsAsync(int ruleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a condition to an existing rule.
        /// </summary>
        /// <param name="createDto">The DTO containing condition creation data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created condition DTO.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="createDto"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if condition validation fails.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the parent rule doesn't exist.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<RuleConditionDto> AddRuleConditionAsync(CreateRuleConditionDto createDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing rule condition.
        /// </summary>
        /// <param name="conditionId">The ID of the condition to update.</param>
        /// <param name="updateDto">The DTO containing condition update data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated condition DTO if successful; otherwise, null.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="updateDto"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="conditionId"/> is less than or equal to 0.</exception>
        /// <exception cref="ArgumentException">Thrown if condition validation fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<RuleConditionDto?> UpdateRuleConditionAsync(int conditionId, UpdateRuleConditionDto updateDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a rule condition.
        /// </summary>
        /// <param name="conditionId">The ID of the condition to delete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if deletion was successful; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="conditionId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> DeleteRuleConditionAsync(int conditionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reorders conditions within a rule group.
        /// </summary>
        /// <param name="ruleId">The ID of the rule.</param>
        /// <param name="groupId">The group ID.</param>
        /// <param name="conditionOrderMap">Dictionary mapping condition ID to new order value.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The number of conditions updated.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="conditionOrderMap"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if ruleId or groupId is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> ReorderConditionsAsync(int ruleId, int groupId, Dictionary<int, int> conditionOrderMap, CancellationToken cancellationToken = default);

        #endregion

        #region Statistics

        /// <summary>
        /// Retrieves the most frequently matched rules.
        /// </summary>
        /// <param name="limit">Maximum number of rules to return. Default: 10.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of rule health DTOs ordered by match count descending.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="limit"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<RuleHealthDto>> GetTopRulesByMatchCountAsync(int limit = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed statistics for a specific rule.
        /// </summary>
        /// <param name="ruleId">The ID of the rule.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A rule health DTO with detailed metrics.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="ruleId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<RuleHealthDto?> GetRuleStatisticsAsync(int ruleId, CancellationToken cancellationToken = default);

        #endregion

        #region Validation

        /// <summary>
        /// Checks if a rule with the specified name already exists.
        /// </summary>
        /// <param name="name">The rule name to check.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if a rule with this name exists; otherwise, false.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> RuleExistsByNameAsync(string name, CancellationToken cancellationToken = default);

        #endregion

        #region Testing and Simulation

        /// <summary>
        /// Tests a rule against sample articles without saving or executing actions.
        /// </summary>
        /// <param name="ruleId">The ID of the rule to test (can be unsaved, pass 0 for new rule).</param>
        /// <param name="sampleArticleIds">List of article IDs to test against.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A test result containing match statistics.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="sampleArticleIds"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// This method provides a safe way to preview which articles would match a rule
        /// without affecting real data or executing actions.
        /// </remarks>
        Task<RuleTestResultDto> TestRuleAsync(int ruleId, List<int> sampleArticleIds, CancellationToken cancellationToken = default);

        #endregion
    }
}