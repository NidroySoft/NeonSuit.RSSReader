using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Repository for managing rule conditions.
    /// Handles complex logical expressions and validation.
    /// </summary>
    public class RuleConditionRepository : BaseRepository<RuleCondition>, IRuleConditionRepository
    {
        private readonly ILogger _logger;
        private readonly RssReaderDbContext _dbContext;

        public RuleConditionRepository(RssReaderDbContext context, ILogger logger) : base(context)
        {
            _logger = (logger?? throw new ArgumentNullException(nameof(logger))).ForContext<RuleConditionRepository>();
            _dbContext = context;
        }

        /// <summary>
        /// Retrieves all conditions for a specific rule.
        /// </summary>
        public async Task<List<RuleCondition>> GetByRuleIdAsync(int ruleId)
        {
            try
            {
                // CHANGED: Use EF Core DbSet
                var conditions = await _dbSet
                    .Where(c => c.RuleId == ruleId)
                    .OrderBy(c => c.GroupId)
                    .ThenBy(c => c.Order)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} conditions for rule ID: {RuleId}", conditions.Count, ruleId);
                return conditions;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve conditions for rule ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves conditions grouped by group ID for a rule.
        /// </summary>
        public async Task<Dictionary<int, List<RuleCondition>>> GetConditionGroupsAsync(int ruleId)
        {
            try
            {
                // CHANGED: Optimize with server-side query
                var conditions = await _dbSet
                    .Where(c => c.RuleId == ruleId)
                    .OrderBy(c => c.GroupId)
                    .ThenBy(c => c.Order)
                    .AsNoTracking()
                    .ToListAsync();

                var groups = conditions.GroupBy(c => c.GroupId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                _logger.Debug("Grouped {Count} conditions into {GroupCount} groups for rule ID: {RuleId}",
                    conditions.Count, groups.Count, ruleId);
                return groups;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to group conditions for rule ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <summary>
        /// Inserts a new condition.
        /// </summary>
        // En RuleConditionRepository.cs - InsertAsync
        public override async Task<int> InsertAsync(RuleCondition condition)
        {
            try
            {
                if (condition == null)  // ✅ Verificar null PRIMERO
                {
                    throw new ArgumentNullException(nameof(condition));
                }

                if (!condition.IsValid)
                {
                    _logger.Warning("Attempted to insert invalid condition");
                    throw new ArgumentException("Condition configuration is invalid");
                }

                var result = await base.InsertAsync(condition);
                _logger.Debug("Inserted new condition for rule ID: {RuleId}", condition.RuleId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to insert condition for rule ID: {RuleId}", condition?.RuleId);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing condition.
        /// </summary>
        public override async Task<int> UpdateAsync(RuleCondition condition)
        {
            try
            {
                if (!condition.IsValid)
                {
                    _logger.Warning("Attempted to update condition with invalid configuration");
                    throw new ArgumentException("Condition configuration is invalid");
                }

                var result = await base.UpdateAsync(condition);
                _logger.Debug("Updated condition ID: {ConditionId}", condition.Id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update condition ID: {ConditionId}", condition.Id);
                throw;
            }
        }

        /// <summary>
        /// Deletes a condition by its ID.
        /// </summary>
        public async Task<int> DeleteAsync(int id)
        {
            try
            {
                var condition = await GetByIdAsync(id);
                if (condition == null)
                {
                    _logger.Warning("Attempted to delete non-existent condition: ID {ConditionId}", id);
                    return 0;
                }

                var result = await base.DeleteAsync(condition);
                _logger.Debug("Deleted condition ID: {ConditionId}", id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete condition ID: {ConditionId}", id);
                throw;
            }
        }

        /// <summary>
        /// Deletes all conditions for a rule.
        /// </summary>
        public async Task<int> DeleteByRuleIdAsync(int ruleId)
        {
            try
            {
                // CHANGED: Use EF Core ExecuteDelete for better performance
                var deletedCount = await _dbSet
                    .Where(c => c.RuleId == ruleId)
                    .ExecuteDeleteAsync();

                _logger.Information("Deleted {Count} conditions for rule ID: {RuleId}", deletedCount, ruleId);
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete conditions for rule ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <summary>
        /// Validates if a condition configuration is syntactically correct.
        /// </summary>
        public Task<bool> ValidateConditionAsync(RuleCondition condition)
        {
            try
            {
                var isValid = condition.IsValid;
                _logger.Debug("Condition validation result: {IsValid}", isValid);
                return Task.FromResult(isValid);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to validate condition");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Validates a collection of conditions for logical consistency.
        /// </summary>
        public async Task<bool> ValidateConditionGroupAsync(List<RuleCondition> conditions)
        {
            try
            {
                if (conditions == null || !conditions.Any())
                {
                    _logger.Debug("Empty condition group is valid");
                    return true;
                }

                // Check for duplicate orders within same group
                var groupId = conditions.First().GroupId;
                var orders = conditions.Select(c => c.Order).ToList();
                if (orders.Distinct().Count() != orders.Count)
                {
                    _logger.Warning("Duplicate order values found in condition group {GroupId}", groupId);
                    return false;
                }

                // Validate each condition
                foreach (var condition in conditions)
                {
                    if (!condition.IsValid)
                    {
                        _logger.Warning("Invalid condition found in group {GroupId}", groupId);
                        return false;
                    }
                }

                _logger.Debug("Condition group {GroupId} validation passed with {Count} conditions",
                    groupId, conditions.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to validate condition group");
                return false;
            }
        }

        /// <summary>
        /// Gets the maximum order value for conditions in a specific group.
        /// </summary>
        public async Task<int> GetMaxOrderInGroupAsync(int ruleId, int groupId)
        {
            try
            {
                // CHANGED: Use EF Core MaxAsync
                var maxOrder = await _dbSet
                    .Where(c => c.RuleId == ruleId && c.GroupId == groupId)
                    .AsNoTracking()
                    .MaxAsync(c => (int?)c.Order) ?? 0;

                _logger.Debug("Max order in rule {RuleId}, group {GroupId}: {MaxOrder}", ruleId, groupId, maxOrder);
                return maxOrder;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get max order for rule {RuleId}, group {GroupId}", ruleId, groupId);
                return 0;
            }
        }

        /// <summary>
        /// Reorders conditions within a group.
        /// </summary>
        /// <summary>
        /// Reorders conditions within a group.
        /// </summary>
        public async Task<int> ReorderConditionsAsync(int ruleId, int groupId, Dictionary<int, int> conditionOrderMap)
        {
            try
            {
                // ✅ SOLUCIÓN: Extraer keys primero para evitar problemas de traducción SQL
                var conditionIds = conditionOrderMap.Keys.ToList();

                var conditions = await _dbSet
                    .Where(c => c.RuleId == ruleId &&
                               c.GroupId == groupId &&
                               conditionIds.Contains(c.Id))  // ✅ List<int>.Contains funciona correctamente
                    .ToListAsync();

                if (!conditions.Any())
                {
                    return 0;
                }

                foreach (var condition in conditions)
                {
                    if (conditionOrderMap.TryGetValue(condition.Id, out var newOrder))
                    {
                        condition.Order = newOrder;
                    }
                }

                var updatedCount = await _dbContext.SaveChangesAsync();
                _logger.Information("Reordered {Count} conditions in rule {RuleId}, group {GroupId}",
                    updatedCount, ruleId, groupId);
                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reorder conditions in rule {RuleId}, group {GroupId}", ruleId, groupId);
                throw;
            }
        }
    }
}