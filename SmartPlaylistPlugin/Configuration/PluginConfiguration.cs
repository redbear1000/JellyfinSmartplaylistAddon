// File: Configuration/PluginConfiguration.cs
using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace SmartPlaylist.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public List<PlaylistRule> PlaylistRules { get; set; }

        public PluginConfiguration()
        {
            PlaylistRules = new List<PlaylistRule>();
        }
    }

    public class PlaylistRule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Expressions { get; set; } // Boolean logic expressions
        public bool IsEnabled { get; set; }
        public int Priority { get; set; }

        public PlaylistRule()
        {
            Id = Guid.NewGuid().ToString();
            Name = string.Empty;
            Expressions = new List<string>();
            IsEnabled = true;
            Priority = 0;
        }
    }
}