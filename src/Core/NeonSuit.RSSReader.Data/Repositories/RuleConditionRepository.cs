using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories;

/// <summary>
/// Repository for managing <see cref="RuleCondition"/> entities.
/// Handles storage, retrieval, validation, and reordering of complex rule conditions.
/// </summary>
internal class RuleConditionRepository : BaseRepository<RuleCondition>, IRuleConditionRepository
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleConditionRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
    public RuleConditionRepository(RSSReaderDbContext context, ILogger logger)
        : base(context, logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

#if DEBUG
        _logger.Debug("RuleConditionRepository initialized");
#endif
    }

    #endregion

    #region Read Operations

    /// <inheritdoc />
    public override async Task<RuleCondition?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(c => c.Rule)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByIdAsync cancelled for condition ID {ConditionId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving condition by ID {ConditionId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<RuleCondition>> GetByRuleIdAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ruleId);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(c => c.RuleId == ruleId)
                .OrderBy(c => c.GroupId)
                .ThenBy(c => c.Order)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByRuleIdAsync cancelled for rule ID {RuleId}", ruleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving conditions for rule ID {RuleId}", ruleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, List<RuleCondition>>> GetConditionGroupsAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ruleId);

        try
        {
            var conditions = await _dbSet
                .AsNoTracking()
                .Where(c => c.RuleId == ruleId)
                .OrderBy(c => c.GroupId)
                .ThenBy(c => c.Order)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return conditions
                .GroupBy(c => c.GroupId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetConditionGroupsAsync cancelled for rule ID {RuleId}", ruleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error grouping conditions for rule ID {RuleId}", ruleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetMaxOrderInGroupAsync(int ruleId, int groupId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ruleId);

        try
        {
            var maxOrder = await _dbSet
                .AsNoTracking()
                .Where(c => c.RuleId == ruleId && c.GroupId == groupId)
                .MaxAsync(c => (int?)c.Order, cancellationToken)
                .ConfigureAwait(false) ?? 0;

            return maxOrder;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetMaxOrderInGroupAsync cancelled for rule {RuleId}, group {GroupId}", ruleId, groupId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting max order for rule {RuleId}, group {GroupId}", ruleId, groupId);
            return 0;
        }
    }

    #endregion

    #region Write Operations

    /// <inheritdoc />
    public override async Task<int> InsertAsync(RuleCondition condition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);

        try
        {
            var result = await base.InsertAsync(condition, cancellationToken).ConfigureAwait(false);
            _logger.Information("Inserted condition for rule ID {RuleId}", condition.RuleId);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("InsertAsync cancelled for condition");
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.Error(ex, "Database error inserting condition for rule ID {RuleId}", condition.RuleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error inserting condition for rule ID {RuleId}", condition.RuleId);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<int> UpdateAsync(RuleCondition condition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);

        try
        {
            var result = await base.UpdateAsync(condition, cancellationToken).ConfigureAwait(false);
            _logger.Debug("Updated condition ID {ConditionId}", condition.Id);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("UpdateAsync cancelled for condition ID {ConditionId}", condition.Id);
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.Error(ex, "Concurrency conflict updating condition ID {ConditionId}", condition.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating condition ID {ConditionId}", condition.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        try
        {
            var condition = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (condition == null)
            {
                _logger.Warning("Condition ID {ConditionId} not found for deletion", id);
                return 0;
            }

            var result = await base.DeleteAsync(condition, cancellationToken).ConfigureAwait(false);
            _logger.Information("Deleted condition ID {ConditionId}", id);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DeleteAsync cancelled for condition ID {ConditionId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting condition ID {ConditionId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteByRuleIdAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ruleId);

        try
        {
            var deletedCount = await _dbSet
                .Where(c => c.RuleId == ruleId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Deleted {Count} conditions for rule ID {RuleId}", deletedCount, ruleId);
            return deletedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DeleteByRuleIdAsync cancelled for rule ID {RuleId}", ruleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting conditions for rule ID {RuleId}", ruleId);
            throw;
        }
    }

    #endregion

    #region Validation Operations

    /// <inheritdoc />
    public Task<bool> ValidateConditionAsync(RuleCondition condition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);

        try
        {
            var isValid = condition.IsValid;
            _logger.Debug("Condition validation result for rule {RuleId}: {IsValid}", condition.RuleId, isValid);
            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error validating condition");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateConditionGroupAsync(List<RuleCondition> conditions, CancellationToken cancellationToken = default)
    {
        try
        {
            if (conditions == null || !conditions.Any())
            {
                _logger.Debug("Empty condition group is valid");
                return true;
            }

            var groupId = conditions.First().GroupId;

            // Check for duplicate orders
            var orders = conditions.Select(c => c.Order).ToList();
            if (orders.Distinct().Count() != orders.Count)
            {
                _logger.Warning("Duplicate order values in group {GroupId}", groupId);
                return false;
            }

            // Validate each condition
            foreach (var condition in conditions)
            {
                if (!condition.IsValid)
                {
                    _logger.Warning("Invalid condition in group {GroupId}", groupId);
                    return false;
                }
            }

            _logger.Debug("Condition group {GroupId} validation passed", groupId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error validating condition group");
            return false;
        }
    }

    #endregion

    #region Reordering Operations

    /// <inheritdoc />
    public async Task<int> ReorderConditionsAsync(int ruleId, int groupId, Dictionary<int, int> conditionOrderMap, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ruleId);
        ArgumentNullException.ThrowIfNull(conditionOrderMap);

        if (!conditionOrderMap.Any())
            return 0;

        try
        {
            var conditionIds = conditionOrderMap.Keys.ToList();

            var conditions = await _dbSet
                .Where(c => c.RuleId == ruleId && c.GroupId == groupId && conditionIds.Contains(c.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!conditions.Any())
            {
                _logger.Debug("No conditions found to reorder in rule {RuleId}, group {GroupId}", ruleId, groupId);
                return 0;
            }

            foreach (var condition in conditions)
            {
                if (conditionOrderMap.TryGetValue(condition.Id, out var newOrder))
                {
                    condition.Order = newOrder;
                }
            }

            var updatedCount = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.Information("Reordered {Count} conditions in rule {RuleId}, group {GroupId}",
                updatedCount, ruleId, groupId);

            return updatedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ReorderConditionsAsync cancelled for rule {RuleId}, group {GroupId}", ruleId, groupId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reordering conditions in rule {RuleId}, group {GroupId}", ruleId, groupId);
            throw;
        }
    }

    #endregion
}