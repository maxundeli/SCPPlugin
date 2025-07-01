using System;
using System.IO;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Scp096;
using Exiled.Events.EventArgs.Warhead;
using Exiled.Events.Handlers;
using Map = Exiled.Events.Handlers.Map;
using Player = Exiled.Events.Handlers.Player;
using Server = Exiled.Events.Handlers.Server;
using Warhead = Exiled.Events.Handlers.Warhead;

namespace MaxunPlugin
{
    public class RoundLogger
    {
        private StreamWriter? _writer;
        private readonly string _logDir;

        public RoundLogger()
        {
            _logDir = Path.Combine(Paths.Plugins, "RoundLogs");
        }

        public void Register()
        {
            Directory.CreateDirectory(_logDir);
            Server.WaitingForPlayers += OnWaitingForPlayers;
            Server.RoundStarted += OnRoundStarted;
            Server.RoundEnded += OnRoundEnded;
            Server.RestartingRound += OnRoundRestart;
            Server.RespawnedTeam += OnTeamRespawned;

            Player.Joined += OnJoined;
            Player.Left += OnLeft;
            Player.Hurt += OnHurt;
            Player.Died += OnDied;
            Player.Spawned += OnSpawned;
            Player.PickingUpItem += OnPickingUpItem;
            Player.DroppingItem += OnDroppingItem;
            Player.ActivatingGenerator += OnActivatingGenerator;
            Player.InteractingDoor += OnDoorInteract;
            Player.TriggeringTesla += OnTriggerTesla;

            Map.GeneratorActivating += OnGeneratorActivating;
            Map.Decontaminating += OnDecontaminating;
            Map.AnnouncingNtfEntrance += OnNtfAnnounced;

            Scp096.AddingTarget += On096AddingTarget;
            Scp096.Enraging += On096Enraging;
            Scp096.CalmingDown += On096Calming;

            Warhead.Detonated += OnWarheadDetonated;
            Warhead.Starting += OnWarheadStarting;
            Warhead.Stopping += OnWarheadStopping;
            Warhead.DeadmanSwitchInitiating += OnDeadmanSwitch;
        }

        public void Unregister()
        {
            Server.WaitingForPlayers -= OnWaitingForPlayers;
            Server.RoundStarted -= OnRoundStarted;
            Server.RoundEnded -= OnRoundEnded;
            Server.RestartingRound -= OnRoundRestart;
            Server.RespawnedTeam -= OnTeamRespawned;

            Player.Joined -= OnJoined;
            Player.Left -= OnLeft;
            Player.Hurt -= OnHurt;
            Player.Died -= OnDied;
            Player.Spawned -= OnSpawned;
            Player.PickingUpItem -= OnPickingUpItem;
            Player.DroppingItem -= OnDroppingItem;
            Player.ActivatingGenerator -= OnActivatingGenerator;
            Player.InteractingDoor -= OnDoorInteract;
            Player.TriggeringTesla -= OnTriggerTesla;

            Map.GeneratorActivating -= OnGeneratorActivating;
            Map.Decontaminating -= OnDecontaminating;
            Map.AnnouncingNtfEntrance -= OnNtfAnnounced;

            Scp096.AddingTarget -= On096AddingTarget;
            Scp096.Enraging -= On096Enraging;
            Scp096.CalmingDown -= On096Calming;

            Warhead.Detonated -= OnWarheadDetonated;
            Warhead.Starting -= OnWarheadStarting;
            Warhead.Stopping -= OnWarheadStopping;
            Warhead.DeadmanSwitchInitiating -= OnDeadmanSwitch;
        }

        public void StopLogging()
        {
            CloseFile();
        }

        private void StartFile()
        {
            string name = DateTimeOffset.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
            string path = Path.Combine(_logDir, name);
            _writer = new StreamWriter(path, true);
            Log.Info($"Round log file created at {path}");
        }

        private void CloseFile()
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }

        private void Write(string category, string sub, string message)
        {
            if (_writer == null) return;
            string ts = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
            _writer.WriteLine($"{ts} | {category.PadRight(18)} | {sub.PadRight(14)} | {message}");
            _writer.Flush();
        }

        private void OnWaitingForPlayers() => Write("Connection update", "Server", "Waiting for players");
        private void OnRoundStarted()
        {
            StartFile();
            Write("Game Event", "Round", "Round started");
        }
        private void OnRoundEnded(RoundEndedEventArgs ev)
        {
            Write("Game Event", "Round", $"Round ended. Leading team: {ev.LeadingTeam}");
            CloseFile();
        }
        private void OnRoundRestart()
        {
            Write("Game Event", "Round", "Round restarting");
            CloseFile();
        }
        private void OnTeamRespawned(RespawnedTeamEventArgs ev)
        {
            Write("Game Event", "Respawn", $"Team respawned: {ev.Wave} with players: {ev.Players}");
        }
        private void OnJoined(JoinedEventArgs ev)
        {
            Write("Connection update", "Networking", $"{ev.Player.Nickname} ({ev.Player.UserId}) joined");
        }
        private void OnLeft(LeftEventArgs ev)
        {
            Write("Connection update", "Networking", $"{ev.Player.Nickname} ({ev.Player.UserId}) left");
        }
        private void OnHurt(HurtEventArgs ev)
        {
            Write("Game Event", "Damage", $"{ev.Attacker?.Nickname} damaged {ev.Player.Nickname} for {ev.Amount}");
        }
        private void OnDied(DiedEventArgs ev)
        {
            Write("Game Event", "Death", $"{ev.Player.Nickname} was killed by {ev.Attacker?.Nickname}");
        }
        private void OnSpawned(SpawnedEventArgs ev)
        {
            Write("Game Event", "Spawn", $"{ev.Player.Nickname} spawned as {ev.Player.Role}");
        }
        private void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            Write("Game Event", "Item", $"{ev.Player.Nickname} picked up {ev.Pickup?.Info.ItemId}");
        }
        private void OnDroppingItem(DroppingItemEventArgs ev)
        {
            Write("Game Event", "Item", $"{ev.Player.Nickname} dropped {ev.Item.Type}");
        }
        private void OnActivatingGenerator(ActivatingGeneratorEventArgs ev)
        {
            Write("Game Event", "Generator", $"{ev.Player.Nickname} activating generator");
        }
        private void OnDoorInteract(InteractingDoorEventArgs ev)
        {
            Write("Game Event", "Door", $"{ev.Player.Nickname} {(ev.IsAllowed ? "opened" : "failed to open")} {ev.Door?.Name}");
        }
        private void OnTriggerTesla(TriggeringTeslaEventArgs ev)
        {
            Write("Game Event", "Tesla", $"{ev.Player.Nickname} triggered tesla {ev.IsAllowed}");
        }
        private void OnGeneratorActivating(GeneratorActivatingEventArgs ev)
        {
            Write("Game Event", "Generator", "Generator activating");
        }
        private void OnDecontaminating(DecontaminatingEventArgs ev)
        {
            Write("Game Event", "Map", "Decontamination starting");
        }
        private void OnNtfAnnounced(AnnouncingNtfEntranceEventArgs ev)
        {
            Write("Game Event", "Map", "MTF entrance announced");
        }
        private void On096AddingTarget(AddingTargetEventArgs ev)
        {
            Write("Game Event", "SCP-096", $"New target: {ev.Target.Nickname}");
        }
        private void On096Enraging(EnragingEventArgs ev)
        {
            Write("Game Event", "SCP-096", $"{ev.Player.Nickname} started enraging");
        }
        private void On096Calming(CalmingDownEventArgs ev)
        {
            Write("Game Event", "SCP-096", $"{ev.Player.Nickname} calmed down");
        }
        private void OnWarheadDetonated()
        {
            Write("Game Event", "Warhead", "Warhead detonated");
        }
        private void OnWarheadStarting(StartingEventArgs ev)
        {
            Write("Game Event", "Warhead", "Warhead starting");
        }
        private void OnWarheadStopping(StoppingEventArgs ev)
        {
            Write("Game Event", "Warhead", "Warhead stopping");
        }
        private void OnDeadmanSwitch(DeadmanSwitchInitiatingEventArgs ev)
        {
            Write("Game Event", "Warhead", "Deadman switch activating");
        }
    }
}

