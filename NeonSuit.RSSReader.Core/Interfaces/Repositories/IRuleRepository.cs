using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for managing automated business rules.
    /// Provides methods for rule creation, evaluation, and execution.
    /// </summary>
    public interface IRuleRepository
    {
        /// <summary>
        /// Retrieves a rule by its unique identifier.
        /// </summary>
        /// <param name="id">The rule ID.</param>
        /// <returns>The Rule object or null if not found.</returns>
        Task<Rule?> GetByIdAsync(int id);

        /// <summary>
        /// Detaches an entity from the database.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task DetachEntityAsync(int id);

        /// <summary>
        /// Retrieves all rules from the database.
        /// </summary>
        /// <returns>A list of all rules.</returns>
        Task<List<Rule>> GetAllAsync();

        /// <summary>
        /// Retrieves all active rules for the background processing engine.
        /// </summary>
        /// <returns>A list of enabled rules.</returns>
        Task<List<Rule>> GetActiveRulesAsync();

        /// <summary>
        /// Retrieves rules ordered by priority (lower numbers execute first).
        /// </summary>
        /// <returns>Rules sorted by priority.</returns>
        Task<List<Rule>> GetRulesByPriorityAsync();

        /// <summary>
        /// Retrieves rules that apply to a specific feed.
        /// </summary>
        /// <param name="feedId">The feed ID.</param>
        /// <returns>Rules that apply to the specified feed.</returns>
        Task<List<Rule>> GetRulesByFeedIdAsync(int feedId);

        /// <summary>
        /// Retrieves rules that apply to a specific category.
        /// </summary>
        /// <param name="categoryId">The category ID.</param>
        /// <returns>Rules that apply to the specified category.</returns>
        Task<List<Rule>> GetRulesByCategoryIdAsync(int categoryId);

        /// <summary>
        /// Inserts a new rule into the database.
        /// </summary>
        /// <param name="rule">The rule to insert.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> InsertAsync(Rule rule);

        /// <summary>
        /// Updates an existing rule.
        /// </summary>
        /// <param name="rule">The rule with updated values.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> UpdateAsync(Rule rule);

        /// <summary>
        /// Deletes a rule by its ID.
        /// </summary>
        /// <param name="id">The rule ID to delete.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> DeleteAsync(int id);

        /// <summary>
        /// Increments the match count for a rule (statistics).
        /// </summary>
        /// <param name="ruleId">The rule ID.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> IncrementMatchCountAsync(int ruleId);

        /// <summary>
        /// Retrieves all rule conditions for a specific rule.
        /// </summary>
        /// <param name="ruleId">The rule ID.</param>
        /// <returns>List of conditions for the rule.</returns>
        Task<List<RuleCondition>> GetRuleConditionsAsync(int ruleId);

        /// <summary>
        /// Checks if a rule with the specified name already exists.
        /// </summary>
        /// <param name="name">The rule name to check.</param>
        /// <returns>True if a rule with this name exists, otherwise false.</returns>
        Task<bool> RuleExistsByNameAsync(string name);

        /// <summary>
        /// Updates the last match date for a rule.
        /// </summary>
        /// <param name="ruleId">The rule ID.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> UpdateLastMatchDateAsync(int ruleId);

        /// <summary>
        /// Retrieves the total number of times all rules have been triggered.
        /// </summary>
        /// <returns>Total match count across all rules.</returns>
        Task<int> GetTotalMatchCountAsync();

        /// <summary>
        /// Retrieves the most frequently triggered rules.
        /// </summary>
        /// <param name="limit">Maximum number of rules to return.</param>
        /// <returns>Top rules by match count.</returns>
        Task<List<Rule>> GetTopRulesByMatchCountAsync(int limit = 10);
    }
}