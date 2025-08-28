// File: Controllers/SmartPlaylistController.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartPlaylist.Configuration;
using SmartPlaylist.Services;

namespace SmartPlaylist.Controllers
{
    [ApiController]
    [Route("SmartPlaylist")]
    [Authorize]
    public class SmartPlaylistController : ControllerBase
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IPlaylistManager _playlistManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IUserManager _userManager;
        private readonly ILogger<SmartPlaylistController> _logger;

        public SmartPlaylistController(
            ILibraryManager libraryManager,
            IPlaylistManager playlistManager,
            IUserDataManager userDataManager,
            IUserManager userManager,
            ILogger<SmartPlaylistController> logger)
        {
            _libraryManager = libraryManager;
            _playlistManager = playlistManager;
            _userDataManager = userDataManager;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet("Rules")]
        public ActionResult<List<PlaylistRule>> GetRules()
        {
            return Ok(Plugin.Instance?.Configuration.PlaylistRules ?? new List<PlaylistRule>());
        }

        [HttpPost("Rules")]
        public ActionResult AddRule([FromBody] PlaylistRule rule)
        {
            try
            {
                if (Plugin.Instance == null)
                    return BadRequest("Plugin not initialized");

                Plugin.Instance.Configuration.PlaylistRules.Add(rule);
                Plugin.Instance.SaveConfiguration();
                return Ok(rule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding playlist rule");
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("Rules/{id}")]
        public ActionResult UpdateRule(string id, [FromBody] PlaylistRule rule)
        {
            try
            {
                if (Plugin.Instance == null)
                    return BadRequest("Plugin not initialized");

                var existingRule = Plugin.Instance.Configuration.PlaylistRules
                    .Find(r => r.Id == id);
                
                if (existingRule == null)
                    return NotFound();

                var index = Plugin.Instance.Configuration.PlaylistRules.IndexOf(existingRule);
                Plugin.Instance.Configuration.PlaylistRules[index] = rule;
                Plugin.Instance.SaveConfiguration();
                
                return Ok(rule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating playlist rule");
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("Rules/{id}")]
        public ActionResult DeleteRule(string id)
        {
            try
            {
                if (Plugin.Instance == null)
                    return BadRequest("Plugin not initialized");

                var rule = Plugin.Instance.Configuration.PlaylistRules
                    .Find(r => r.Id == id);
                
                if (rule == null)
                    return NotFound();

                Plugin.Instance.Configuration.PlaylistRules.Remove(rule);
                Plugin.Instance.SaveConfiguration();
                
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting playlist rule");
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("ValidateExpression")]
        public ActionResult ValidateExpression([FromBody] string expression)
        {
            try
            {
                var parser = new ExpressionParser();
                var parsed = parser.Parse(expression);
                return Ok(new { Valid = true, Parsed = parsed });
            }
            catch (Exception ex)
            {
                return Ok(new { Valid = false, Error = ex.Message });
            }
        }

        [HttpPost("Generate/{ruleId}")]
        public async Task<ActionResult> GeneratePlaylist(string ruleId, [FromQuery] string userId)
        {
            try
            {
                if (Plugin.Instance == null)
                    return BadRequest("Plugin not initialized");

                var rule = Plugin.Instance.Configuration.PlaylistRules
                    .Find(r => r.Id == ruleId);
                
                if (rule == null)
                    return NotFound("Rule not found");

                var parser = new ExpressionParser();
                var playlistService = new PlaylistService(
                    _libraryManager,
                    _playlistManager,
                    _userDataManager,
                    _userManager,
                    parser,
                    _logger.CreateLogger<PlaylistService>()
                );

                var playlist = await playlistService.GenerateSmartPlaylist(userId, rule);
                return Ok(new { PlaylistId = playlist.Id, Name = playlist.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating playlist");
                return BadRequest(ex.Message);
            }
        }
    }
}