using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories;

/// <summary>
/// Repository implementation for managing <see cref="Rule"/> entities.
/// Provides data access for rule CRUD, activation status, priority ordering, feed/category scoping,
/// condition retrieval, statistics tracking, and match counting.
/// </summary>
internal class RuleRepository : BaseRepository<Rule>, IRuleRepository
{
    private readonly DbSet<RuleCondition> _ruleConditions;

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
    public RuleRepository(RSSReaderDbContext context, ILogger logger)
        : base(context, logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _ruleConditions = context.Set<RuleCondition>();

#if DEBUG
        _logger.Debug("RuleRepository initialized");
#endif
    }

    #endregion

    #region Read Operations

    /// <inheritdoc />
    public override async Task<Rule?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(r => r.Conditions)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByIdAsync cancelled for rule ID {RuleId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving rule by ID {RuleId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<List<Rule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(r => r.Conditions)
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetAllAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving all rules");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Rule>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(r => r.Conditions)
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetActiveRulesAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving active rules");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Rule>> GetRulesByPriorityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(r => r.Conditions)
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetRulesByPriorityAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving rules by priority");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Rule>> GetRulesByFeedIdAsync(int feedId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(r => r.Conditions)
                .Where(r => r.FeedIds != null && r.FeedIds.Contains($"[{feedId}]"))
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetRulesByFeedIdAsync cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving rules for feed {FeedId}", feedId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Rule>> GetRulesByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(r => r.Conditions)
                .Where(r => r.CategoryIds != null && r.CategoryIds.Contains($"[{categoryId}]"))
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetRulesByCategoryIdAsync cancelled for category {CategoryId}", categoryId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving rules for category {CategoryId}", categoryId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Rule>> SearchRulesByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(r => r.Conditions)
                .Where(r => EF.Functions.Like(r.Name, $"%{searchTerm}%"))
                .OrderBy(r => r.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("SearchRulesByNameAsync cancelled for '{SearchTerm}'", searchTerm);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error searching rules by name for '{SearchTerm}'", searchTerm);
            throw;
        }
    }

    #endregion

    #region Write Operations

    /// <inheritdoc />
    public override async Task<int> InsertAsync(Rule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        try
        {
            var result = await base.InsertAsync(rule, cancellationToken).ConfigureAwait(false);
            _logger.Information("Inserted rule '{RuleName}' (ID: {RuleId})", rule.Name, rule.Id);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("InsertAsync cancelled for rule '{RuleName}'", rule.Name);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.Error(ex, "Database error inserting rule '{RuleName}'", rule.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error inserting rule '{RuleName}'", rule.Name);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<int> UpdateAsync(Rule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        try
        {
            var result = await base.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
            _logger.Information("Updated rule '{RuleName}' (ID: {RuleId})", rule.Name, rule.Id);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("UpdateAsync cancelled for rule ID {RuleId}", rule.Id);
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.Error(ex, "Concurrency conflict updating rule ID {RuleId}", rule.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating rule ID {RuleId}", rule.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        try
        {
            var rule = await _dbSet.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
            if (rule == null)
            {
                _logger.Warning("Rule ID {RuleId} not found for deletion", id);
                return 0;
            }

            var result = await base.DeleteAsync(rule, cancellationToken).ConfigureAwait(false);
            _logger.Information("Deleted rule '{RuleName}' (ID: {RuleId})", rule.Name, id);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DeleteAsync cancelled for rule ID {RuleId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting rule ID {RuleId}", id);
            throw;
        }
    }

    #endregion

    #region Statistics & Match Tracking

    /// <inheritdoc />
    public async Task<int> IncrementMatchCountAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ruleId);

        try
        {
            var result = await _dbSet
                .Where(r => r.Id == ruleId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(r => r.MatchCount, r => r.MatchCount + 1)
                        .SetProperty(r => r.LastMatchDate, DateTime.UtcNow),
                    cancellationToken)
                .ConfigureAwait(false);

            if (result > 0)
                _logger.Debug("Incremented match count for rule ID {RuleId}", ruleId);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("IncrementMatchCountAsync cancelled for rule ID {RuleId}", ruleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error incrementing match count for rule ID {RuleId}", ruleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, int>> GetMatchStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(r => r.IsEnabled)
                .ToDictionaryAsync(r => r.Name, r => r.MatchCount, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetMatchStatisticsAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving match statistics");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Rule>> GetTopRulesByMatchCountAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(r => r.Conditions)
                .OrderByDescending(r => r.MatchCount)
                .ThenBy(r => r.Name)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetTopRulesByMatchCountAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving top rules by match count");
            throw;
        }
    }

    #endregion

    #region Condition & Existence Checks

    /// <inheritdoc />
    public async Task<List<RuleCondition>> GetRuleConditionsAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ruleId);

        try
        {
            return await _ruleConditions
                .AsNoTracking()
                .Where(c => c.RuleId == ruleId)
                .OrderBy(c => c.GroupId)
                .ThenBy(c => c.Order)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetRuleConditionsAsync cancelled for rule ID {RuleId}", ruleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving conditions for rule ID {RuleId}", ruleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .AnyAsync(r => r.Name == name, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ExistsByNameAsync cancelled for '{Name}'", name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking rule existence for '{Name}'", name);
            throw;
        }
    }

    #endregion
}