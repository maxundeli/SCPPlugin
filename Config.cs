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
}
