using Exiled.API.Interfaces;
using System.ComponentModel;
using PlayerRoles;
using InventorySystem.Items;
using System.Collections.Generic;

namespace MaxunPlugin
{
    public class Config : IConfig
    {
        [Description("Whether the plugin is enabled.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Enable debug messages.")]
        public bool Debug { get; set; } = false;

        [Description("Database related settings.")]
        public DatabaseModule Database { get; set; } = new();

        [Description("Settings for random blackouts.")]
        public BlackoutModule Blackout { get; set; } = new();

        [Description("Statistics tracking module.")]
        public StatsModule Stats { get; set; } = new();
        
        [Description("Settings for logging module")]
        public LoggingModule Logging { get; set; } = new();

        [Description("Automatic warhead detonation module.")]
        public AutoBombModule AutoBomb { get; set; } = new();

        [Description("Enable SCP-3114")]
        public bool Scp3114 { get; set; } = true;
        
        [Description("Chance to SCP-3114")]
        public int Scp3114Chance { get; set; } = 20;
        
        [Description("Item spawn configuration per role. Key is the player role and the value is a list of items with their spawn chances.")]
        public Dictionary<RoleTypeId, List<ItemChance>> SpawnItems { get; set; } = new()
        {
            {
                RoleTypeId.ClassD,
                new List<ItemChance>
                {
                    new ItemChance { Item = ItemType.Flashlight, Chance = 33 }
                }
            },
            {
                RoleTypeId.Scientist,
                new List<ItemChance>
                {
                    new ItemChance { Item = ItemType.Flashlight, Chance = 33 }
                }
            }
        };
    }

    public class ItemChance
    {
        [Description("Type of the item to give")]
        public ItemType Item { get; set; }

        [Description("Chance to receive the item (0-100)")]
        public int Chance { get; set; }
    }

    public class ModuleBase
    {
        [Description("Whether this module is enabled.")]
        public bool Enabled { get; set; } = true;
    }

    public class DatabaseModule : ModuleBase
    {
        [Description("Connection string for the MySQL database.")]
        public string ConnectionString { get; set; } =
            "Server=localhost;Database=scp_db;User ID=scp_user;Password=scp_password;Pooling=true;";

        [Description("Table name for human player statistics.")]
        public string HumanTable { get; set; } = "human_stats";

        [Description("Table name for SCP statistics.")]
        public string ScpTable { get; set; } = "scp_stats";
    }

    public class BlackoutModule : ModuleBase
    {
        [Description("Minimum seconds between blackout attempts.")]
        public int IntervalMin { get; set; } = 130;

        [Description("Maximum seconds between blackout attempts.")]
        public int IntervalMax { get; set; } = 170;

        [Description("Minimum duration of blackout in seconds.")]
        public int DurationMin { get; set; } = 30;

        [Description("Maximum duration of blackout in seconds.")]
        public int DurationMax { get; set; } = 90;

        [Description("Chance of blackout occurring per check (0-100).")]
        public int Chance { get; set; } = 33;
    }
    
        
    public class StatsModule : ModuleBase
    {
        [Description("Broadcast template when a player kills too many allies. {killer} - attacker, {victim} - victim, {count} - kill count")]
        public string TeamkillMessage { get; set; } = "<color=blue>{killer}</color> <color=white>killed {victim}. Teamkills: <color=red>{count}</color>";

        [Description("Enable broadcast on excessive teamkills.")]
        public bool TeamkillBroadcast { get; set; } = true;

        [Description("Teamkill threshold for broadcast.")]
        public int TeamkillLimit { get; set; } = 3;
    }

    public class AutoBombModule : ModuleBase
    {
    }
    public class LoggingModule : ModuleBase
    {
    
    }
}


