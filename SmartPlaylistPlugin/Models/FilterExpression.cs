// File: Models/FilterExpression.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartPlaylist.Models
{
    public class ParsedExpression
    {
        public required FilterExpression Filter { get; set; }
        public required string SortBy { get; set; }
        public int Count { get; set; }
    }

    public abstract class FilterExpression
    {
        public abstract bool Evaluate(ContentItem item);
    }

    public class AndExpression : FilterExpression
    {
        public List<FilterExpression> Expressions { get; set; } = [];

        public override bool Evaluate(ContentItem item)
        {
            foreach (var expr in Expressions)
            {
                if (!expr.Evaluate(item))
                    return false;
            }
            return true;
        }
    }

    public class OrExpression : FilterExpression
    {
        public List<FilterExpression> Expressions { get; set; } = [];

        public override bool Evaluate(ContentItem item)
        {
            foreach (var expr in Expressions)
            {
                if (expr.Evaluate(item))
                    return true;
            }
            return false;
        }
    }

    public class NotExpression : FilterExpression
    {
        public required FilterExpression Expression { get; set; }

        public override bool Evaluate(ContentItem item)
        {
            return !Expression.Evaluate(item);
        }
    }

    public class GenreFilter : FilterExpression
    {
        public required FilterExpression GenreExpression { get; set; }

        public override bool Evaluate(ContentItem item)
        {
            return GenreExpression.Evaluate(item);
        }
    }

    public class SingleGenreFilter : FilterExpression
    {
        public required string Genre { get; set; }

        public override bool Evaluate(ContentItem item)
        {
            return item.Genres?.Contains(Genre, StringComparer.OrdinalIgnoreCase) ?? false;
        }
    }

    public class LengthFilter : FilterExpression
    {
        public required string Operator { get; set; } // "<", ">", "=", "<=", ">="
        public int Minutes { get; set; }

        public override bool Evaluate(ContentItem item)
        {
            var itemMinutes = item.RuntimeMinutes ?? 0;
            return Operator switch
            {
                "<" => itemMinutes < Minutes,
                ">" => itemMinutes > Minutes,
                "=" => itemMinutes == Minutes,
                "<=" => itemMinutes <= Minutes,
                ">=" => itemMinutes >= Minutes,
                _ => false
            };
        }
    }

    public class LanguageFilter : FilterExpression
    {
        public required FilterExpression LanguageExpression { get; set; }

        public override bool Evaluate(ContentItem item)
        {
            return LanguageExpression.Evaluate(item);
        }
    }

    public class SingleLanguageFilter : FilterExpression
    {
        public required string Language { get; set; }

        public override bool Evaluate(ContentItem item)
        {
            return item.Language?.Equals(Language, StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }

    public class TypeFilter : FilterExpression
    {
        public required FilterExpression TypeExpression { get; set; }

        public override bool Evaluate(ContentItem item)
        {
            return TypeExpression.Evaluate(item);
        }
    }

    public class SingleTypeFilter : FilterExpression
    {
        public required string ContentType { get; set; } // "Movie", "Episode", "Series"

        public override bool Evaluate(ContentItem item)
        {
            return item.Type.Equals(ContentType, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class WatchStatusFilter : FilterExpression
    {
        public bool IsWatched { get; set; }

        public override bool Evaluate(ContentItem item)
        {
            return item.IsWatched == IsWatched;
        }
    }

    public class ReleaseDateFilter : FilterExpression
    {
        public required string Operator { get; set; } // "<", ">", "=", "<=", ">="
        public int Year { get; set; }

        public override bool Evaluate(ContentItem item)
        {
            var itemYear = item.ReleaseYear ?? 0;
            return Operator switch
            {
                "<" => itemYear < Year,
                ">" => itemYear > Year,
                "=" => itemYear == Year,
                "<=" => itemYear <= Year,
                ">=" => itemYear >= Year,
                _ => false
            };
        }
    }

    public class RatingFilter : FilterExpression
    {
        public required string Operator { get; set; }
        public double Rating { get; set; }

        public override bool Evaluate(ContentItem item)
        {
            var itemRating = item.CommunityRating ?? 0;
            return Operator switch
            {
                "<" => itemRating < Rating,
                ">" => itemRating > Rating,
                "=" => Math.Abs(itemRating - Rating) < 0.1,
                "<=" => itemRating <= Rating,
                ">=" => itemRating >= Rating,
                _ => false
            };
        }
    }

    public class ContentItem
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string Type { get; set; } // "Movie", "Episode", "Series"
        public List<string> Genres { get; set; } = [];
        public int? RuntimeMinutes { get; set; }
        public string? Language { get; set; }
        public bool IsWatched { get; set; }
        public int? ReleaseYear { get; set; }
        public double? CommunityRating { get; set; }
        public DateTime? DateAdded { get; set; }
        public required object OriginalItem { get; set; } // Reference to original Jellyfin BaseItem
    }
}