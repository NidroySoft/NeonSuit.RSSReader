using NeonSuit.RSSReader.Core.Models;
using System.Linq.Expressions;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for managing automated business rules (<see cref="Rule"/> entities).
/// Defines the contract for rule persistence, querying, scoping, statistics, and condition retrieval.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts all data access operations related to user-defined rules in the RSS reader application.
/// It supports rule lifecycle management (CRUD), activation filtering, scoping by feed/category, priority-based execution,
/// match statistics, and retrieval of associated conditions for the rule engine.
/// </para>
/// <para>
/// Implementations must ensure:
/// - Efficient indexed queries on frequently filtered columns (e.g., IsEnabled, Priority).
/// - Atomicity in write operations where business rules require it.
/// - Avoidance of full table scans in production (prefer server-side filtering).
/// </para>
/// </remarks>
public interface IRuleRepository : IRepository<Rule>
{
    #region Read Collection Operations

    /// <summary>
    /// Retrieves all rules currently enabled in the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active <see cref="Rule"/> entities.</returns>
    Task<List<Rule>> GetActiveRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all rules that are ordered by their priority (lowest first).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of <see cref="Rule"/> entities ordered by <see cref="Rule.Priority"/> ASC.</returns>
    Task<List<Rule>> GetRulesByPriorityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves rules specifically targeting a given feed.
    /// </summary>
    /// <param name="feedId">The ID of the feed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of rules applicable to the feed.</returns>
    Task<List<Rule>> GetRulesByFeedIdAsync(int feedId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves rules targeting all feeds within a specific category.
    /// </summary>
    /// <param name="categoryId">The ID of the category.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of rules applicable to the category.</returns>
    Task<List<Rule>> GetRulesByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a case-insensitive search for rules by name.
    /// </summary>
    /// <param name="searchTerm">The string to search for in rule names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching <see cref="Rule"/> entities.</returns>
    Task<List<Rule>> SearchRulesByNameAsync(string searchTerm, CancellationToken cancellationToken = default);

    #endregion

    #region Statistics & Metadata

    /// <summary>
    /// Increments the count of times a specific rule has been successfully matched.
    /// </summary>
    /// <param name="ruleId">The unique identifier of the rule.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows affected.</returns>
    Task<int> IncrementMatchCountAsync(int ruleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a summary of match counts for all enabled rules.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary where key is the rule name and value is its match count.</returns>
    Task<Dictionary<string, int>> GetMatchStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most frequently triggered rules, ordered by match count descending.
    /// </summary>
    /// <param name="limit">Maximum number of rules to return (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of top <see cref="Rule"/> entities by match count.</returns>
    Task<List<Rule>> GetTopRulesByMatchCountAsync(int limit = 10, CancellationToken cancellationToken = default);

    #endregion

    #region Condition & Existence Checks

    /// <summary>
    /// Retrieves all conditions associated with a specific rule.
    /// </summary>
    /// <param name="ruleId">The unique identifier of the rule.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of <see cref="RuleCondition"/> entities for the rule.</returns>
    Task<List<RuleCondition>> GetRuleConditionsAsync(int ruleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a rule with the specified name already exists.
    /// </summary>
    /// <param name="name">The name of the rule to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a rule with this name exists; otherwise false.</returns>
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    #endregion
}