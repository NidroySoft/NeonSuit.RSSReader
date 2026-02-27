using NeonSuit.RSSReader.Core.DTOs.Rules;
using NeonSuit.RSSReader.Core.Enums;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for rule evaluation engine that processes articles against defined rules and conditions.
    /// Provides comprehensive rule evaluation with support for complex condition groups and validation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service centralizes rule evaluation logic with the following capabilities:
    /// <list type="bullet">
    /// <item>Evaluation of individual conditions against article properties</item>
    /// <item>Evaluation of condition groups with logical operators (AND/OR)</item>
    /// <item>Validation of condition syntax and semantic correctness</item>
    /// <item>Support for various condition types (title, content, author, date, etc.)</item>
    /// </list>
    /// </para>
    /// <para>
    /// All methods use DTOs to maintain separation of concerns.
    /// </para>
    /// </remarks>
    public interface IRuleEngine
    {
        #region Single Condition Evaluation

        /// <summary>
        /// Evaluates a single condition against an article to determine if it matches.
        /// </summary>
        /// <param name="articleId">The ID of the article to evaluate against the condition.</param>
        /// <param name="conditionId">The ID of the condition to evaluate.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if the article satisfies the condition; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId or conditionId is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> EvaluateConditionAsync(int articleId, int conditionId, CancellationToken cancellationToken = default);

        #endregion

        #region Group Evaluation

        /// <summary>
        /// Evaluates a group of conditions against an article using the specified logical operator.
        /// </summary>
        /// <param name="articleId">The ID of the article to evaluate against the conditions.</param>
        /// <param name="ruleId">The ID of the rule containing the conditions.</param>
        /// <param name="logicalOperator">The logical operator to apply (AND or OR). Default: AND.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if the condition group evaluates to true; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId or ruleId is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// Evaluation behavior:
        /// <list type="bullet">
        /// <item>For AND operator: Returns true only if ALL conditions evaluate to true</item>
        /// <item>For OR operator: Returns true if ANY condition evaluates to true</item>
        /// </list>
        /// Short-circuit evaluation is applied for performance.
        /// </remarks>
        Task<bool> EvaluateConditionGroupAsync(int articleId, int ruleId, LogicalOperator logicalOperator = LogicalOperator.AND, CancellationToken cancellationToken = default);

        #endregion

        #region Validation

        /// <summary>
        /// Validates that a condition is properly formed and contains all required properties.
        /// </summary>
        /// <param name="conditionId">The ID of the condition to validate.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if the condition is valid; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if conditionId is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// Validation checks include:
        /// <list type="bullet">
        /// <item>For regex conditions: regex pattern is valid and compiles</item>
        /// <item>Required values are present for the operator type</item>
        /// <item>GroupId and Order are non-negative</item>
        /// </list>
        /// </remarks>
        Task<bool> ValidateConditionAsync(int conditionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates all conditions for a rule.
        /// </summary>
        /// <param name="ruleId">The ID of the rule whose conditions to validate.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A validation result containing success flag and any validation errors.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if ruleId is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<RuleValidationResultDto> ValidateRuleConditionsAsync(int ruleId, CancellationToken cancellationToken = default);

        #endregion

        #region Batch Evaluation

        /// <summary>
        /// Evaluates multiple articles against a rule in batch.
        /// </summary>
        /// <param name="articleIds">The list of article IDs to evaluate.</param>
        /// <param name="ruleId">The ID of the rule to evaluate against.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A dictionary mapping article IDs to evaluation results.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="articleIds"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if ruleId is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<Dictionary<int, bool>> EvaluateBatchAsync(List<int> articleIds, int ruleId, CancellationToken cancellationToken = default);

        #endregion

        #region Testing

        /// <summary>
        /// Tests a condition against sample text without requiring an article.
        /// </summary>
        /// <param name="conditionId">The ID of the condition to test.</param>
        /// <param name="sampleText">The sample text to test against.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if the sample text satisfies the condition; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if conditionId is less than or equal to 0.</exception>
        /// <exception cref="ArgumentException">Thrown if sampleText is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// Useful for testing conditions in the rule editor without fetching real articles.
        /// </remarks>
        Task<bool> TestConditionWithTextAsync(int conditionId, string sampleText, CancellationToken cancellationToken = default);

        #endregion
    }
}