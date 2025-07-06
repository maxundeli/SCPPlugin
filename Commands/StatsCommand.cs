using System;
using CommandSystem;
using Exiled.API.Features;

namespace MaxunPlugin.Commands
{
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class StatsCommand : ICommand
    {
        public string Command => "stats";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Show your statistics.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            if (player is null)
            {
                response = "This command can only be used in-game.";
                return false;
            }

            var plugin = Plugin.Instance;
            if (!plugin.Config.Stats.Enabled || !plugin.Config.Database.Enabled)
            {
                response = "Statistics are disabled.";
                return false;
            }

            _ = plugin.ShowPlayerStatsAsync(player, player.UserId);
            response = "Statistics hint shown.";
            return true;
        }
    }
}
