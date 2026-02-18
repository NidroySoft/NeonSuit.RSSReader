using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    public interface IRuleEngine
    {
        bool EvaluateCondition(Article article, RuleCondition condition);
        bool EvaluateConditionGroup(List<RuleCondition> conditions, Article article);
        bool ValidateCondition(RuleCondition condition);
    }
}