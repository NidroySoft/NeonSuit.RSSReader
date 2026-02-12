using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Professional repository for managing automated business rules.
    /// Supports complex rule logic, conditions, and statistics tracking.
    /// </summary>
    public class RuleRepository : BaseRepository<Rule>, IRuleRepository
    {
        private readonly ILogger _logger;
        private readonly DbSet<RuleCondition> _ruleConditions;

        public RuleRepository(RssReaderDbContext context, ILogger logger) : base(context)
        {
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<RuleRepository>();
            _ruleConditions = context.Set<RuleCondition>();
        }

        /// <summary>
        /// Retrieves all active rules for the background processing engine.
        /// </summary>
        public async Task<List<Rule>> GetActiveRulesAsync()
        {
            try
            {
                var rules = await _dbSet
                    .AsNoTracking()
                    .Where(r => r.IsEnabled)
                    .ToListAsync();

                // Load conditions for active rules with advanced conditions
                var advancedRuleIds = rules
                    .Where(r => r.UsesAdvancedConditions)
                    .Select(r => r.Id)
                    .ToList();

                if (advancedRuleIds.Any())
                {
                    var conditions = await _ruleConditions
                        .AsNoTracking()
                        .Where(c => advancedRuleIds.Contains(c.RuleId))
                        .OrderBy(c => c.GroupId)
                        .ThenBy(c => c.Order)
                        .ToListAsync();

                    var conditionsByRule = conditions
                        .GroupBy(c => c.RuleId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var rule in rules.Where(r => r.UsesAdvancedConditions))
                    {
                        if (conditionsByRule.TryGetValue(rule.Id, out var ruleConditions))
                        {
                            rule.Conditions = ruleConditions;
                        }
                    }
                }

                _logger.Debug("Retrieved {Count} active rules", rules.Count);
                return rules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve active rules");
                throw;
            }
        }

        /// <summary>
        /// Retrieves rules ordered by priority (lower numbers execute first).
        /// </summary>
        public async Task<List<Rule>> GetRulesByPriorityAsync()
        {
            try
            {
                var rules = await _dbSet
                    .AsNoTracking()
                    .Where(r => r.IsEnabled)
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.Name)
                    .ToListAsync();

                // Load conditions for prioritized rules with advanced conditions
                var advancedRuleIds = rules
                    .Where(r => r.UsesAdvancedConditions)
                    .Select(r => r.Id)
                    .ToList();

                if (advancedRuleIds.Any())
                {
                    var conditions = await _ruleConditions
                        .AsNoTracking()
                        .Where(c => advancedRuleIds.Contains(c.RuleId))
                        .OrderBy(c => c.GroupId)
                        .ThenBy(c => c.Order)
                        .ToListAsync();

                    var conditionsByRule = conditions
                        .GroupBy(c => c.RuleId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var rule in rules.Where(r => r.UsesAdvancedConditions))
                    {
                        if (conditionsByRule.TryGetValue(rule.Id, out var ruleConditions))
                        {
                            rule.Conditions = ruleConditions;
                        }
                    }
                }

                _logger.Debug("Retrieved {Count} rules ordered by priority", rules.Count);
                return rules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve rules by priority");
                throw;
            }
        }

        /// <summary>
        /// Retrieves rules that apply to a specific feed.
        /// </summary>
        public async Task<List<Rule>> GetRulesByFeedIdAsync(int feedId)
        {
            try
            {
                var allRules = await _dbSet
                    .AsNoTracking()
                    .ToListAsync();

                var feedRules = allRules.Where(r =>
                    (r.Scope == Core.Enums.RuleScope.AllFeeds) ||
                    (r.Scope == Core.Enums.RuleScope.SpecificFeeds && r.FeedIdList.Contains(feedId)) ||
                    (r.Scope == Core.Enums.RuleScope.SpecificCategories)
                ).ToList();

                _logger.Debug("Found {Count} rules applicable to feed ID: {FeedId}", feedRules.Count, feedId);
                return feedRules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve rules for feed ID: {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves rules that apply to a specific category.
        /// </summary>
        public async Task<List<Rule>> GetRulesByCategoryIdAsync(int categoryId)
        {
            try
            {
                var allRules = await _dbSet
                    .AsNoTracking()
                    .ToListAsync();

                var categoryRules = allRules.Where(r =>
                    (r.Scope == Core.Enums.RuleScope.AllFeeds) ||
                    (r.Scope == Core.Enums.RuleScope.SpecificCategories && r.CategoryIdList.Contains(categoryId))
                ).ToList();

                _logger.Debug("Found {Count} rules applicable to category ID: {CategoryId}",
                    categoryRules.Count, categoryId);
                return categoryRules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve rules for category ID: {CategoryId}", categoryId);
                throw;
            }
        }

        /// <summary>
        /// Inserts a new rule into the database.
        /// </summary>
        public override async Task<int> InsertAsync(Rule rule)
        {
            try
            {
                // ✅ VALIDAR NULL PRIMERO
                if (rule == null)
                    throw new ArgumentNullException(nameof(rule));

                rule.CreatedAt = DateTime.UtcNow;
                rule.LastModified = DateTime.UtcNow;

                await _dbSet.AddAsync(rule);
                var result = await _context.SaveChangesAsync();

                // Insert conditions if using advanced conditions
                if (rule.UsesAdvancedConditions && rule.Conditions != null && rule.Conditions.Any())
                {
                    await InsertRuleConditionsAsync(rule.Id, rule.Conditions);
                }

                _logger.Information("Created new rule: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to insert rule: {RuleName}", rule?.Name ?? "null");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing rule.
        /// </summary>
        public override async Task<int> UpdateAsync(Rule rule)
        {
            try
            {
                rule.LastModified = DateTime.UtcNow;
                _context.Entry(rule).State = EntityState.Modified;
                var result = await _context.SaveChangesAsync();

                // Update conditions if using advanced conditions
                if (rule.UsesAdvancedConditions)
                {
                    await DeleteRuleConditionsAsync(rule.Id);
                    if (rule.Conditions != null && rule.Conditions.Any())
                    {
                        await InsertRuleConditionsAsync(rule.Id, rule.Conditions);
                    }
                }

                _logger.Debug("Updated rule: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update rule: {RuleName} (ID: {RuleId})", rule.Name, rule.Id);
                throw;
            }
        }

        /// <summary>
        /// Deletes a rule by its ID.
        /// </summary>
        public async Task<int> DeleteAsync(int id)
        {
            try
            {
                var rule = await _dbSet.FindAsync(id);
                if (rule == null)
                {
                    _logger.Warning("Attempted to delete non-existent rule: ID {RuleId}", id);
                    return 0;
                }

                // Delete associated conditions first
                await DeleteRuleConditionsAsync(id);

                // Delete the rule
                _dbSet.Remove(rule);
                var result = await _context.SaveChangesAsync();

                _logger.Information("Deleted rule: {RuleName} (ID: {RuleId})", rule.Name, id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete rule: ID {RuleId}", id);
                throw;
            }
        }

        /// <summary>
        /// Increments the match count for a rule (statistics).
        /// </summary>
        public async Task<int> IncrementMatchCountAsync(int ruleId)
        {
            try
            {
                var rule = await _dbSet.FindAsync(ruleId);
                if (rule == null) return 0;

                rule.MatchCount++;
                rule.LastMatchDate = DateTime.UtcNow;

                _context.Entry(rule).State = EntityState.Modified;
                var result = await _context.SaveChangesAsync();

                _logger.Debug("Incremented match count for rule: {RuleName} (ID: {RuleId})", rule.Name, ruleId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to increment match count for rule ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all rule conditions for a specific rule.
        /// </summary>
        public async Task<List<RuleCondition>> GetRuleConditionsAsync(int ruleId)
        {
            try
            {
                var conditions = await _ruleConditions
                    .AsNoTracking()
                    .Where(c => c.RuleId == ruleId)
                    .OrderBy(c => c.GroupId)
                    .ThenBy(c => c.Order)
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
        /// Checks if a rule with the specified name already exists.
        /// </summary>
        public async Task<bool> RuleExistsByNameAsync(string name)
        {
            try
            {
                var exists = await _dbSet
                    .AsNoTracking()
                    .AnyAsync(r => r.Name == name);

                _logger.Debug("Rule existence check for '{Name}': {Exists}", name, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check rule existence for name: {Name}", name);
                throw;
            }
        }

        /// <summary>
        /// Updates the last match date for a rule.
        /// </summary>
        public async Task<int> UpdateLastMatchDateAsync(int ruleId)
        {
            try
            {
                var rule = await _dbSet.FindAsync(ruleId);
                if (rule == null) return 0;

                rule.LastMatchDate = DateTime.UtcNow;
                _context.Entry(rule).State = EntityState.Modified;
                var result = await _context.SaveChangesAsync();

                _logger.Debug("Updated last match date for rule: {RuleName} (ID: {RuleId})", rule.Name, ruleId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update last match date for rule ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves the total number of times all rules have been triggered.
        /// </summary>
        public async Task<int> GetTotalMatchCountAsync()
        {
            try
            {
                var total = await _dbSet
                    .AsNoTracking()
                    .SumAsync(r => r.MatchCount);

                _logger.Debug("Total rule match count: {TotalMatches}", total);
                return total;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve total match count");
                throw;
            }
        }

        /// <summary>
        /// Retrieves the most frequently triggered rules.
        /// </summary>
        public async Task<List<Rule>> GetTopRulesByMatchCountAsync(int limit = 10)
        {
            try
            {
                var topRules = await _dbSet
                    .AsNoTracking()
                    .OrderByDescending(r => r.MatchCount)
                    .ThenBy(r => r.Name)
                    .Take(limit)
                    .ToListAsync();

                _logger.Debug("Retrieved top {Count} rules by match count", topRules.Count);
                return topRules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve top rules by match count");
                throw;
            }
        }

        /// <summary>
        /// Helper method to insert rule conditions.
        /// </summary>
        private async Task InsertRuleConditionsAsync(int ruleId, List<RuleCondition> conditions)
        {
            try
            {
                foreach (var condition in conditions)
                {
                    condition.RuleId = ruleId;
                    condition.Id = 0;  // ✅ FORZAR Id = 0 ANTES DE INSERTAR
                    await _ruleConditions.AddAsync(condition);
                }

                await _context.SaveChangesAsync();
                _logger.Debug("Inserted {Count} conditions for rule ID: {RuleId}", conditions.Count, ruleId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to insert conditions for rule ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <summary>
        /// Helper method to delete all conditions for a rule.
        /// </summary>
        private async Task DeleteRuleConditionsAsync(int ruleId)
        {
            try
            {
                var deletedCount = await _ruleConditions
                    .Where(c => c.RuleId == ruleId)
                    .ExecuteDeleteAsync();

                _logger.Debug("Deleted {Count} conditions for rule ID: {RuleId}", deletedCount, ruleId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete conditions for rule ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves rules that need to be evaluated for a specific article.
        /// </summary>
        public async Task<List<Rule>> GetRulesForArticleEvaluationAsync(int feedId, int? categoryId)
        {
            try
            {
                var allRules = await GetActiveRulesAsync();

                // Filter rules based on scope
                var applicableRules = allRules.Where(r =>
                {
                    switch (r.Scope)
                    {
                        case Core.Enums.RuleScope.AllFeeds:
                            return true;

                        case Core.Enums.RuleScope.SpecificFeeds:
                            return r.FeedIdList.Contains(feedId);

                        case Core.Enums.RuleScope.SpecificCategories:
                            return categoryId.HasValue && r.CategoryIdList.Contains(categoryId.Value);

                        default:
                            return false;
                    }
                }).OrderBy(r => r.Priority).ToList();

                _logger.Debug("Found {Count} applicable rules for article evaluation (Feed: {FeedId}, Category: {CategoryId})",
                    applicableRules.Count, feedId, categoryId);

                return applicableRules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve rules for article evaluation");
                throw;
            }
        }

        /// <summary>
        /// Retrieves rules with advanced conditions (complex logic).
        /// </summary>
        public async Task<List<Rule>> GetRulesWithAdvancedConditionsAsync()
        {
            try
            {
                var rules = await _dbSet
                    .AsNoTracking()
                    .Where(r => r.UsesAdvancedConditions && r.IsEnabled)
                    .ToListAsync();

                // Load all conditions for these rules
                var ruleIds = rules.Select(r => r.Id).ToList();
                var conditions = await _ruleConditions
                    .AsNoTracking()
                    .Where(c => ruleIds.Contains(c.RuleId))
                    .OrderBy(c => c.GroupId)
                    .ThenBy(c => c.Order)
                    .ToListAsync();

                var conditionsByRule = conditions
                    .GroupBy(c => c.RuleId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var rule in rules)
                {
                    if (conditionsByRule.TryGetValue(rule.Id, out var ruleConditions))
                    {
                        rule.Conditions = ruleConditions;
                    }
                }

                _logger.Debug("Retrieved {Count} rules with advanced conditions", rules.Count);
                return rules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve rules with advanced conditions");
                throw;
            }
        }

        /// <summary>
        /// Retrieves rules that use simple conditions (non-advanced).
        /// </summary>
        public async Task<List<Rule>> GetRulesWithSimpleConditionsAsync()
        {
            try
            {
                var rules = await _dbSet
                    .AsNoTracking()
                    .Where(r => !r.UsesAdvancedConditions && r.IsEnabled)
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} rules with simple conditions", rules.Count);
                return rules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve rules with simple conditions");
                throw;
            }
        }

        /// <summary>
        /// Toggles the enabled status of a rule.
        /// </summary>
        public async Task<bool> ToggleRuleStatusAsync(int ruleId)
        {
            try
            {
                var rule = await _dbSet.FindAsync(ruleId);
                if (rule == null)
                {
                    _logger.Warning("Attempted to toggle status of non-existent rule: ID {RuleId}", ruleId);
                    return false;
                }

                rule.IsEnabled = !rule.IsEnabled;
                rule.LastModified = DateTime.UtcNow;

                _context.Entry(rule).State = EntityState.Modified;
                var result = await _context.SaveChangesAsync();
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Rule {RuleName} (ID: {RuleId}) is now {Status}",
                        rule.Name, ruleId, rule.IsEnabled ? "enabled" : "disabled");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to toggle status for rule ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <summary>
        /// Resets statistics for a rule (match count and last match date).
        /// </summary>
        public async Task<bool> ResetRuleStatisticsAsync(int ruleId)
        {
            try
            {
                var rule = await _dbSet.FindAsync(ruleId);
                if (rule == null)
                {
                    _logger.Warning("Attempted to reset statistics for non-existent rule: ID {RuleId}", ruleId);
                    return false;
                }

                rule.MatchCount = 0;
                rule.LastMatchDate = null;
                rule.LastModified = DateTime.UtcNow;

                _context.Entry(rule).State = EntityState.Modified;
                var result = await _context.SaveChangesAsync();
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Reset statistics for rule: {RuleName} (ID: {RuleId})", rule.Name, ruleId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reset statistics for rule ID: {RuleId}", ruleId);
                throw;
            }
        }

        /// <summary>
        /// Bulk load conditions for multiple rules (optimized for batch operations).
        /// </summary>
        public async Task<Dictionary<int, List<RuleCondition>>> GetConditionsForRulesAsync(IEnumerable<int> ruleIds)
        {
            try
            {
                var ids = ruleIds.ToList();
                if (!ids.Any()) return new Dictionary<int, List<RuleCondition>>();

                var conditions = await _ruleConditions
                    .AsNoTracking()
                    .Where(c => ids.Contains(c.RuleId))
                    .OrderBy(c => c.GroupId)
                    .ThenBy(c => c.Order)
                    .ToListAsync();

                var result = conditions
                    .GroupBy(c => c.RuleId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                _logger.Debug("Loaded conditions for {Count} rules", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load conditions for rules");
                throw;
            }
        }
    }
}