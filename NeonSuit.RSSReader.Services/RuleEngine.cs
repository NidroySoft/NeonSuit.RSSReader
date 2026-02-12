using NeonSuit.RSSReader.Core.Interfaces;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;
using System.Text.RegularExpressions;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Advanced rule engine with support for complex logical expressions.
    /// Provides pattern matching, regex evaluation, and multi-condition processing.
    /// </summary>
    public class IRuleEngine
    {
        private readonly ILogger _logger;

        public IRuleEngine(ILogger logger)
        {
            _logger = logger.ForContext<IRuleEngine>();
        }

        /// <summary>
        /// Evaluates an article against a single condition.
        /// </summary>
        public bool EvaluateCondition(Article article, RuleCondition condition)
        {
            try
            {
                if (article == null || condition == null)
                {
                    return false;
                }

                var targetText = GetTargetText(article, condition.Field);
                var comparison = condition.IsCaseSensitive ?
                    StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                var result = EvaluateTextCondition(targetText, condition, comparison);

                // Apply negation
                if (condition.Negate)
                {
                    result = !result;
                }

                _logger.Debug("Condition evaluation: {Condition} = {Result}",
                    condition.HumanReadable, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to evaluate condition");
                return false;
            }
        }

        /// <summary>
        /// Evaluates multiple conditions with logical operators.
        /// </summary>
        public bool EvaluateConditionGroup(List<RuleCondition> conditions, Article article)
        {
            try
            {
                if (conditions == null || !conditions.Any() || article == null)
                {
                    return true; // Empty group evaluates to true
                }

                bool groupResult = EvaluateCondition(article, conditions[0]);

                for (int i = 1; i < conditions.Count; i++)
                {
                    var currentResult = EvaluateCondition(article, conditions[i]);

                    switch (conditions[i - 1].CombineWithNext)
                    {
                        case Core.Enums.LogicalOperator.AND:
                            groupResult = groupResult && currentResult;
                            break;
                        case Core.Enums.LogicalOperator.OR:
                            groupResult = groupResult || currentResult;
                            break;
                    }
                }

                _logger.Debug("Condition group evaluation result: {Result} for {Count} conditions",
                    groupResult, conditions.Count);

                return groupResult;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to evaluate condition group");
                return false;
            }
        }

        /// <summary>
        /// Gets the target text from an article based on field specification.
        /// </summary>
        private string GetTargetText(Article article, Core.Enums.RuleFieldTarget field)
        {
            return field switch
            {
                Core.Enums.RuleFieldTarget.Title => article.Title ?? "",
                Core.Enums.RuleFieldTarget.Content => article.Content ?? article.Summary ?? "",
                Core.Enums.RuleFieldTarget.Author => article.Author ?? "",
                Core.Enums.RuleFieldTarget.Categories => article.Categories ?? "",
                Core.Enums.RuleFieldTarget.AllFields =>
                    $"{article.Title} {article.Content} {article.Summary}",
                Core.Enums.RuleFieldTarget.AnyField =>
                    $"{article.Title} {article.Content} {article.Summary}",
                _ => ""
            };
        }

        /// <summary>
        /// Evaluates text against a condition with specified operator.
        /// </summary>
        private bool EvaluateTextCondition(string text, RuleCondition condition, StringComparison comparison)
        {
            switch (condition.Operator)
            {
                case Core.Enums.RuleOperator.Contains:
                    return text.Contains(condition.Value, comparison);

                case Core.Enums.RuleOperator.Equals:
                    return text.Equals(condition.Value, comparison);

                case Core.Enums.RuleOperator.StartsWith:
                    return text.StartsWith(condition.Value, comparison);

                case Core.Enums.RuleOperator.EndsWith:
                    return text.EndsWith(condition.Value, comparison);

                case Core.Enums.RuleOperator.NotContains:
                    return !text.Contains(condition.Value, comparison);

                case Core.Enums.RuleOperator.NotEquals:
                    return !text.Equals(condition.Value, comparison);

                case Core.Enums.RuleOperator.Regex:
                    return EvaluateRegexCondition(text, condition.RegexPattern, condition.IsCaseSensitive);

                case Core.Enums.RuleOperator.GreaterThan:
                    return CompareText(text, condition.Value) > 0;

                case Core.Enums.RuleOperator.LessThan:
                    return CompareText(text, condition.Value) < 0;

                case Core.Enums.RuleOperator.IsEmpty:
                    return string.IsNullOrWhiteSpace(text);

                case Core.Enums.RuleOperator.IsNotEmpty:
                    return !string.IsNullOrWhiteSpace(text);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Evaluates regex pattern matching with improved error handling.
        /// </summary>
        private bool EvaluateRegexCondition(string text, string pattern, bool caseSensitive)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    return false;

                var regexOptions = caseSensitive ?
                    RegexOptions.None :
                    RegexOptions.IgnoreCase;
                regexOptions |= RegexOptions.Compiled;

                var regex = new Regex(pattern, regexOptions, TimeSpan.FromMilliseconds(100));
                return regex.IsMatch(text);
            }
            catch (RegexParseException ex)
            {
                _logger.Error(ex, "Invalid regex pattern syntax: {Pattern}", pattern);
                return false;
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.Warning("Regex evaluation timed out for pattern: {Pattern} on text length: {Length}",
                    pattern, text?.Length ?? 0);
                return false; // Timeout = no match
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in regex evaluation for pattern: {Pattern}", pattern);
                return false;
            }
        }

        /// <summary>
        /// Compares two text values for numeric/date comparisons.
        /// </summary>
        private int CompareText(string text1, string text2)
        {
            // Try numeric comparison
            if (double.TryParse(text1, out double num1) && double.TryParse(text2, out double num2))
            {
                return num1.CompareTo(num2);
            }

            // Try date comparison
            if (DateTime.TryParse(text1, out DateTime date1) && DateTime.TryParse(text2, out DateTime date2))
            {
                return date1.CompareTo(date2);
            }

            // Fall back to string comparison
            return string.Compare(text1, text2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates if a condition configuration is syntactically correct.
        /// </summary>
        public bool ValidateCondition(RuleCondition condition)
        {
            try
            {
                if (condition == null)
                {
                    return false;
                }

                // Check required fields for specific operators
                if (condition.Operator == Core.Enums.RuleOperator.Regex)
                {
                    if (string.IsNullOrWhiteSpace(condition.RegexPattern))
                        return false;

                    // Validate regex pattern syntax
                    try
                    {
                        Regex.IsMatch("", condition.RegexPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                    }
                    catch
                    {
                        return false;
                    }
                }

                if ((condition.Operator == Core.Enums.RuleOperator.Contains ||
                     condition.Operator == Core.Enums.RuleOperator.Equals ||
                     condition.Operator == Core.Enums.RuleOperator.StartsWith ||
                     condition.Operator == Core.Enums.RuleOperator.EndsWith ||
                     condition.Operator == Core.Enums.RuleOperator.NotContains ||
                     condition.Operator == Core.Enums.RuleOperator.NotEquals ||
                     condition.Operator == Core.Enums.RuleOperator.GreaterThan ||
                     condition.Operator == Core.Enums.RuleOperator.LessThan) &&
                    string.IsNullOrWhiteSpace(condition.Value))
                {
                    return false;
                }

                // Validate GroupId and Order are positive
                if (condition.GroupId < 0) return false;
                if (condition.Order < 0) return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to validate condition");
                return false;
            }
        }
    }
}