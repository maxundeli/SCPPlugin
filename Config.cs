using Exiled.API.Interfaces;
using System.ComponentModel;

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

        [Description("Automatic warhead detonation module.")]
        public AutoBombModule AutoBomb { get; set; } = new();

        [Description("Chance of giving a flashlight to D-Class or Scientist players at round start (0-100).")]
        public int FlashlightChance { get; set; } = 33;
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
            "Server=localhost;Database=scp;User ID=maxundeli;Password=Maxx1826583ru;Pooling=true;";
    }

    public class BlackoutModule : ModuleBase
    {
        [Description("Minimum seconds between blackout attempts.")]
        public int IntervalMin { get; set; } = 150;

        [Description("Maximum seconds between blackout attempts.")]
        public int IntervalMax { get; set; } = 150;

        [Description("Minimum duration of blackout in seconds.")]
        public int DurationMin { get; set; } = 30;

        [Description("Maximum duration of blackout in seconds.")]
        public int DurationMax { get; set; } = 90;

        [Description("Chance of blackout occurring per check (0-100).")]
        public int Chance { get; set; } = 33;
    }

    public class StatsModule : ModuleBase
    {
    }

    public class AutoBombModule : ModuleBase
    {
    }
}
