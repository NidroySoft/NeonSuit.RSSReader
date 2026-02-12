using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Contract for the automated rules engine.
    /// Defines comprehensive rule management and article processing capabilities.
    /// </summary>
    public interface IRuleService
    {
        // Rule Management
        Task<Rule?> GetRuleByIdAsync(int ruleId);
        Task<List<Rule>> GetAllRulesAsync();
        Task<List<Rule>> GetActiveRulesAsync();
        Task<Rule> CreateRuleAsync(Rule rule);
        Task<bool> UpdateRuleAsync(Rule rule);
        Task<bool> DeleteRuleAsync(int ruleId);

        // Rule Evaluation
        Task<List<Rule>> EvaluateArticleAgainstRulesAsync(Article article);
        Task<bool> ExecuteRuleActionsAsync(Rule rule, Article article);

        // Statistics
        Task<int> GetTotalMatchCountAsync();
        Task<List<Rule>> GetTopRulesByMatchCountAsync(int limit = 10);

        // Validation
        Task<bool> ValidateRuleAsync(Rule rule);
        Task<bool> RuleExistsByNameAsync(string name);
    }
}