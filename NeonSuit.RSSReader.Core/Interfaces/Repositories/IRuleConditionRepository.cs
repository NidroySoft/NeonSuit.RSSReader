using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for managing rule conditions.
    /// Provides methods for condition management and validation.
    /// </summary>
    public interface IRuleConditionRepository
    {
        /// <summary>
        /// Retrieves a condition by its ID.
        /// </summary>
        /// <param name="id">The condition ID.</param>
        /// <returns>The RuleCondition or null if not found.</returns>
        Task<RuleCondition?> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves all conditions for a specific rule.
        /// </summary>
        /// <param name="ruleId">The rule ID.</param>
        /// <returns>List of conditions for the rule.</returns>
        Task<List<RuleCondition>> GetByRuleIdAsync(int ruleId);

        /// <summary>
        /// Retrieves conditions grouped by group ID for a rule.
        /// </summary>
        /// <param name="ruleId">The rule ID.</param>
        /// <returns>Dictionary of condition groups.</returns>
        Task<Dictionary<int, List<RuleCondition>>> GetConditionGroupsAsync(int ruleId);

        /// <summary>
        /// Inserts a new condition.
        /// </summary>
        /// <param name="condition">The condition to insert.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> InsertAsync(RuleCondition condition);

        /// <summary>
        /// Updates an existing condition.
        /// </summary>
        /// <param name="condition">The condition with updated values.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> UpdateAsync(RuleCondition condition);

        /// <summary>
        /// Deletes a condition by its ID.
        /// </summary>
        /// <param name="id">The condition ID to delete.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> DeleteAsync(int id);

        /// <summary>
        /// Deletes all conditions for a rule.
        /// </summary>
        /// <param name="ruleId">The rule ID.</param>
        /// <returns>The number of conditions deleted.</returns>
        Task<int> DeleteByRuleIdAsync(int ruleId);

        /// <summary>
        /// Validates if a condition configuration is syntactically correct.
        /// </summary>
        /// <param name="condition">The condition to validate.</param>
        /// <returns>True if valid, otherwise false.</returns>
        Task<bool> ValidateConditionAsync(RuleCondition condition);
    }
}