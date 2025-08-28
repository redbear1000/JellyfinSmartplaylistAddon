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
        private readonly PlaylistService _playlistService;
        private readonly ExpressionParser _parser;
        private readonly ILogger<SmartPlaylistController> _logger;

        public SmartPlaylistController(
            PlaylistService playlistService,
            ExpressionParser parser,
            ILogger<SmartPlaylistController> logger)
        {
            _playlistService = playlistService;
            _parser = parser;
            _logger = logger;
        }

        [HttpGet("Rules")]
        public ActionResult<List<PlaylistRule>> GetRules()
        {
            return Ok(Plugin.Instance.Configuration.PlaylistRules);
        }

        [HttpPost("Rules")]
        public ActionResult AddRule([FromBody] PlaylistRule rule)
        {
            try
            {
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
                var parsed = _parser.Parse(expression);
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
                var rule = Plugin.Instance.Configuration.PlaylistRules
                    .Find(r => r.Id == ruleId);
                
                if (rule == null)
                    return NotFound("Rule not found");

                var playlist = await _playlistService.GenerateSmartPlaylist(userId, rule);
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
