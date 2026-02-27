using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for managing rule conditions.
    /// Provides methods for condition management, querying, and validation within the rule engine context.
    /// </summary>
    /// <remarks>
    /// This interface defines the contract for data access operations related to <see cref="RuleCondition"/> entities.
    /// It ensures that business logic remains decoupled from persistence concerns and promotes efficient
    /// data handling, especially for low-resource environments.
    /// </remarks>
    public interface IRuleConditionRepository : IRepository<RuleCondition>
    {
        #region Read Collection Operations

        /// <summary>
        /// Retrieves all rule conditions associated with a specific rule.
        /// </summary>
        /// <param name="ruleId">The unique identifier of the parent rule.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of <see cref="RuleCondition"/> entities for the specified rule.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if ruleId is less than or equal to 0.</exception>
        Task<List<RuleCondition>> GetByRuleIdAsync(int ruleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves rule conditions grouped by their <see cref="RuleCondition.GroupId"/> for a specific rule.
        /// </summary>
        /// <param name="ruleId">The unique identifier of the parent rule.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary mapping GroupId to list of conditions in that group.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if ruleId is less than or equal to 0.</exception>
        Task<Dictionary<int, List<RuleCondition>>> GetConditionGroupsAsync(int ruleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the maximum order value within a specific group for a rule.
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The maximum order value, or 0 if no conditions exist.</returns>
        Task<int> GetMaxOrderInGroupAsync(int ruleId, int groupId, CancellationToken cancellationToken = default);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Deletes all rule conditions associated with a specific rule.
        /// </summary>
        /// <param name="ruleId">The unique identifier of the parent rule.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of rule conditions deleted.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if ruleId is less than or equal to 0.</exception>
        Task<int> DeleteByRuleIdAsync(int ruleId, CancellationToken cancellationToken = default);

        #endregion

        #region Validation Operations

        /// <summary>
        /// Validates if a rule condition's configuration is syntactically and semantically correct.
        /// </summary>
        /// <param name="condition">The <see cref="RuleCondition"/> entity to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the condition is valid; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if condition is null.</exception>
        Task<bool> ValidateConditionAsync(RuleCondition condition, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a group of conditions for consistency (no duplicate orders, all conditions valid).
        /// </summary>
        /// <param name="conditions">List of conditions to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the condition group is valid; otherwise, false.</returns>
        Task<bool> ValidateConditionGroupAsync(List<RuleCondition> conditions, CancellationToken cancellationToken = default);

        #endregion

        #region Reordering Operations

        /// <summary>
        /// Reorders conditions within a specific group.
        /// </summary>
        /// <param name="ruleId">The rule identifier.</param>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="conditionOrderMap">Dictionary mapping condition ID to new order value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of conditions updated.</returns>
        Task<int> ReorderConditionsAsync(int ruleId, int groupId, Dictionary<int, int> conditionOrderMap, CancellationToken cancellationToken = default);

        #endregion
    }
}