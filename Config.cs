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

        [Description("Connection string for the MySQL database.")]
        public string ConnectionString { get; set; } = "Server=localhost;Database=scp;User ID=maxundeli;Password=Maxx1826583ru;Pooling=true;";

        [Description("Minimum seconds between blackout attempts.")]
        public int BlackoutIntervalMin { get; set; } = 150;

        [Description("Maximum seconds between blackout attempts.")]
        public int BlackoutIntervalMax { get; set; } = 150;

        [Description("Minimum duration of blackout in seconds.")]
        public int BlackoutDurationMin { get; set; } = 30;

        [Description("Maximum duration of blackout in seconds.")]
        public int BlackoutDurationMax { get; set; } = 90;

        [Description("Chance of blackout occurring per check (0-100).")]
        public int BlackoutChance { get; set; } = 33;

        [Description("Chance of giving a flashlight to D-Class or Scientist players at round start (0-100).")]
        public int FlashlightChance { get; set; } = 33;
    }
}
