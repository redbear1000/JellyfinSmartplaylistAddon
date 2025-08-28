// File: Services/PlaylistService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Playlists;
using SmartPlaylist.Configuration;
using SmartPlaylist.Models;
using Microsoft.Extensions.Logging;

namespace SmartPlaylist.Services
{
    public class PlaylistService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IPlaylistManager _playlistManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IUserManager _userManager;
        private readonly ExpressionParser _parser;
        private readonly ILogger<PlaylistService> _logger;

        public PlaylistService(
            ILibraryManager libraryManager, 
            IPlaylistManager playlistManager,
            IUserDataManager userDataManager,
            IUserManager userManager,
            ExpressionParser parser,
            ILogger<PlaylistService> logger)
        {
            _libraryManager = libraryManager;
            _playlistManager = playlistManager;
            _userDataManager = userDataManager;
            _userManager = userManager;
            _parser = parser;
            _logger = logger;
        }

        public async Task<Playlist> GenerateSmartPlaylist(string userId, PlaylistRule rule)
        {
            var user = _userManager.GetUserById(Guid.Parse(userId));
            if (user == null)
            {
                throw new ArgumentException($"User with ID {userId} not found");
            }

            var allItems = new List<BaseItem>();

            // Get all content and convert to ContentItem format
            var contentItems = GetAllContentItems(user);

            // Process each expression and collect items
            foreach (var expression in rule.Expressions)
            {
                try
                {
                    var parsedExpression = _parser.Parse(expression);
                    var filteredItems = contentItems.Where(item => parsedExpression.Filter.Evaluate(item)).ToList();
                    
                    // Apply sorting
                    var sortedItems = ApplySorting(filteredItems, parsedExpression.SortBy);
                    
                    // Take the specified count
                    var selectedItems = sortedItems.Take(parsedExpression.Count);
                    
                    // Convert back to BaseItem and add to result
                    allItems.AddRange(selectedItems.Select(item => (BaseItem)item.OriginalItem));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing expression: {Expression}", expression);
                }
            }

            // Create the playlist
            var playlistName = $"Smart Playlist: {rule.Name}";
            var playlistId = await _playlistManager.CreatePlaylist(new PlaylistCreationOptions
            {
                Name = playlistName,
                ItemIdList = allItems.Select(i => i.Id.ToString()).ToArray(),
                UserId = user.Id,
                MediaType = "Audio,Video"
            });

            // Return the created playlist
            var createdPlaylist = _libraryManager.GetItemById(playlistId) as Playlist;
            return createdPlaylist ?? throw new InvalidOperationException("Failed to create playlist");
        }

        private List<ContentItem> GetAllContentItems(User user)
        {
            var items = new List<ContentItem>();

            // Get all movies and episodes
            var allItems = _libraryManager.GetItemList(new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
                IsVirtualItem = false
            });

            foreach (var item in allItems)
            {
                var contentItem = new ContentItem
                {
                    Id = item.Id.ToString(),
                    Name = item.Name,
                    Type = item is Movie ? "Movie" : "Episode",
                    Genres = item.Genres?.ToList() ?? new List<string>(),
                    RuntimeMinutes = item.RunTimeTicks.HasValue ? (int)(item.RunTimeTicks.Value / TimeSpan.TicksPerMinute) : null,
                    Language = item.GetPreferredMetadataLanguage(),
                    ReleaseYear = item.ProductionYear,
                    CommunityRating = item.CommunityRating,
                    DateAdded = item.DateCreated,
                    OriginalItem = item
                };

                // Get watch status
                var userData = _userDataManager.GetUserData(user, item);
                contentItem.IsWatched = userData.Played;

                items.Add(contentItem);
            }

            return items;
        }

        private List<ContentItem> ApplySorting(List<ContentItem> items, string sortBy)
        {
            return sortBy.ToLower() switch
            {
                "random" => items.OrderBy(x => Guid.NewGuid()).ToList(),
                "alphabetical" => items.OrderBy(x => x.Name).ToList(),
                "releasedate" => items.OrderBy(x => x.ReleaseYear ?? 0).ToList(),
                "releasedate_desc" => items.OrderByDescending(x => x.ReleaseYear ?? 0).ToList(),
                "rating" => items.OrderByDescending(x => x.CommunityRating ?? 0).ToList(),
                "dateadded" => items.OrderByDescending(x => x.DateAdded ?? DateTime.MinValue).ToList(),
                "runtime" => items.OrderBy(x => x.RuntimeMinutes ?? 0).ToList(),
                "runtime_desc" => items.OrderByDescending(x => x.RuntimeMinutes ?? 0).ToList(),
                _ => items // No sorting
            };
        }
    }
}