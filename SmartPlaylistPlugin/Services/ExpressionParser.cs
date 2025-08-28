// File: Services/ExpressionParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SmartPlaylist.Models;

namespace SmartPlaylist.Services
{
    public class ExpressionParser
    {
        public ParsedExpression Parse(string expression)
        {
            // Format: {[Filter Expression] | Sort | Count}
            var match = Regex.Match(expression.Trim(), @"^\{(.+)\|(.+)\|(\d+)\}$");
            if (!match.Success)
                throw new ArgumentException("Invalid expression format. Expected: {[filters] | sort | count}");

            var filterPart = match.Groups[1].Value.Trim();
            var sortPart = match.Groups[2].Value.Trim();
            var countPart = int.Parse(match.Groups[3].Value.Trim());

            return new ParsedExpression
            {
                Filter = ParseFilter(filterPart),
                SortBy = sortPart,
                Count = countPart
            };
        }

        private FilterExpression ParseFilter(string filterExpression)
        {
            // Remove outer brackets if present
            filterExpression = filterExpression.Trim();
            if (filterExpression.StartsWith("[") && filterExpression.EndsWith("]"))
                filterExpression = filterExpression[1..^1];

            return ParseLogicalExpression(filterExpression);
        }

        private FilterExpression ParseLogicalExpression(string expression)
        {
            // Handle AND/OR at the top level
            var andParts = SplitLogicalOperator(expression, "AND");
            if (andParts.Count > 1)
            {
                return new AndExpression
                {
                    Expressions = andParts.Select(ParseLogicalExpression).ToList()
                };
            }

            var orParts = SplitLogicalOperator(expression, "OR");
            if (orParts.Count > 1)
            {
                return new OrExpression
                {
                    Expressions = orParts.Select(ParseLogicalExpression).ToList()
                };
            }

            // Handle NOT
            expression = expression.Trim();
            if (expression.StartsWith("NOT ", StringComparison.OrdinalIgnoreCase))
            {
                var innerExpression = expression[4..].Trim();
                return new NotExpression
                {
                    Expression = ParseLogicalExpression(innerExpression)
                };
            }

            // Handle bracketed expressions
            if (expression.StartsWith("[") && expression.EndsWith("]"))
            {
                return ParseSingleFilter(expression[1..^1]);
            }

            return ParseSingleFilter(expression);
        }

        private List<string> SplitLogicalOperator(string expression, string op)
        {
            var parts = new List<string>();
            var current = "";
            var bracketDepth = 0;
            var i = 0;

            while (i < expression.Length)
            {
                if (expression[i] == '[')
                    bracketDepth++;
                else if (expression[i] == ']')
                    bracketDepth--;

                if (bracketDepth == 0 && i <= expression.Length - op.Length)
                {
                    var substring = expression.Substring(i, op.Length);
                    if (substring.Equals(op, StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if it's a complete word (not part of another word)
                        var beforeChar = i > 0 ? expression[i - 1] : ' ';
                        var afterChar = i + op.Length < expression.Length ? expression[i + op.Length] : ' ';
                        
                        if (char.IsWhiteSpace(beforeChar) && char.IsWhiteSpace(afterChar))
                        {
                            parts.Add(current.Trim());
                            current = "";
                            i += op.Length;
                            continue;
                        }
                    }
                }

                current += expression[i];
                i++;
            }

            parts.Add(current.Trim());
            return parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        }

        private FilterExpression ParseSingleFilter(string filter)
        {
            var colonIndex = filter.IndexOf(':');
            if (colonIndex == -1)
                throw new ArgumentException($"Invalid filter format: {filter}");

            var filterType = filter[..colonIndex].Trim();
            var filterValue = filter[(colonIndex + 1)..].Trim();

            return filterType.ToLower() switch
            {
                "genre" => ParseGenreFilter(filterValue),
                "length" => ParseLengthFilter(filterValue),
                "language" => ParseLanguageFilter(filterValue),
                "type" => ParseTypeFilter(filterValue),
                "watchstatus" => new WatchStatusFilter { IsWatched = bool.Parse(filterValue) },
                "released" => ParseReleaseDateFilter(filterValue),
                "rating" => ParseRatingFilter(filterValue),
                _ => throw new ArgumentException($"Unknown filter type: {filterType}")
            };
        }

        private FilterExpression ParseGenreFilter(string value)
        {
            return new GenreFilter 
            { 
                GenreExpression = ParseInternalBooleanExpression(value, "genre")
            };
        }

        private FilterExpression ParseLanguageFilter(string value)
        {
            return new LanguageFilter 
            { 
                LanguageExpression = ParseInternalBooleanExpression(value, "language")
            };
        }

        private FilterExpression ParseTypeFilter(string value)
        {
            return new TypeFilter 
            { 
                TypeExpression = ParseInternalBooleanExpression(value, "type")
            };
        }

        private FilterExpression ParseInternalBooleanExpression(string expression, string filterType)
        {
            // Handle AND/OR at the top level within the filter value
            var andParts = SplitInternalLogicalOperator(expression, "AND");
            if (andParts.Count > 1)
            {
                return new AndExpression
                {
                    Expressions = andParts.Select(part => ParseInternalBooleanExpression(part, filterType)).ToList()
                };
            }

            var orParts = SplitInternalLogicalOperator(expression, "OR");
            if (orParts.Count > 1)
            {
                return new OrExpression
                {
                    Expressions = orParts.Select(part => ParseInternalBooleanExpression(part, filterType)).ToList()
                };
            }

            // Handle NOT
            expression = expression.Trim();
            if (expression.StartsWith("NOT ", StringComparison.OrdinalIgnoreCase))
            {
                var innerExpression = expression[4..].Trim();
                return new NotExpression
                {
                    Expression = ParseInternalBooleanExpression(innerExpression, filterType)
                };
            }

            // Handle parentheses
            if (expression.StartsWith("(") && expression.EndsWith(")"))
            {
                return ParseInternalBooleanExpression(expression[1..^1], filterType);
            }

            // Create single filter based on type
            return filterType.ToLower() switch
            {
                "genre" => new SingleGenreFilter { Genre = expression.Trim() },
                "language" => new SingleLanguageFilter { Language = expression.Trim() },
                "type" => new SingleTypeFilter { ContentType = expression.Trim() },
                _ => throw new ArgumentException($"Unknown internal filter type: {filterType}")
            };
        }

        private List<string> SplitInternalLogicalOperator(string expression, string op)
        {
            var parts = new List<string>();
            var current = "";
            var parenthesesDepth = 0;
            var i = 0;

            while (i < expression.Length)
            {
                if (expression[i] == '(')
                    parenthesesDepth++;
                else if (expression[i] == ')')
                    parenthesesDepth--;

                if (parenthesesDepth == 0 && i <= expression.Length - op.Length)
                {
                    var substring = expression.Substring(i, op.Length);
                    if (substring.Equals(op, StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if it's a complete word (not part of another word)
                        var beforeChar = i > 0 ? expression[i - 1] : ' ';
                        var afterChar = i + op.Length < expression.Length ? expression[i + op.Length] : ' ';
                        
                        if (char.IsWhiteSpace(beforeChar) && char.IsWhiteSpace(afterChar))
                        {
                            parts.Add(current.Trim());
                            current = "";
                            i += op.Length;
                            continue;
                        }
                    }
                }

                current += expression[i];
                i++;
            }

            parts.Add(current.Trim());
            return parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        }

        private FilterExpression ParseLengthFilter(string value)
        {
            var match = Regex.Match(value, @"^([<>=]+)(\d+)$");
            if (!match.Success)
                throw new ArgumentException($"Invalid length filter: {value}");

            return new LengthFilter
            {
                Operator = match.Groups[1].Value,
                Minutes = int.Parse(match.Groups[2].Value)
            };
        }

        private FilterExpression ParseReleaseDateFilter(string value)
        {
            var match = Regex.Match(value, @"^([<>=]+)(\d{4})$");
            if (!match.Success)
                throw new ArgumentException($"Invalid release date filter: {value}");

            return new ReleaseDateFilter
            {
                Operator = match.Groups[1].Value,
                Year = int.Parse(match.Groups[2].Value)
            };
        }

        private FilterExpression ParseRatingFilter(string value)
        {
            var match = Regex.Match(value, @"^([<>=]+)(\d+\.?\d*)$");
            if (!match.Success)
                throw new ArgumentException($"Invalid rating filter: {value}");

            return new RatingFilter
            {
                Operator = match.Groups[1].Value,
                Rating = double.Parse(match.Groups[2].Value)
            };
        }
    }
}