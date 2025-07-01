using System;
using System.IO;
using System.Linq;
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
            Player.InteractingElevator += OnElevatorInteract;
            Player.InteractingLocker += OnLockerInteract;
            Player.DroppingAmmo += OnDroppingAmmo;
            Player.DroppedAmmo += OnDroppedAmmo;
            Player.ThrownProjectile += OnThrownProjectile;
            Player.Escaping += OnEscaping;

            Map.GeneratorActivating += OnGeneratorActivating;
            Map.Decontaminating += OnDecontaminating;
            Map.AnnouncingNtfEntrance += OnNtfAnnounced;
            Map.ExplodingGrenade += OnGrenadeExploding;
            Map.SpawningItem += OnSpawningItem;
            Map.PickupAdded += OnPickupAdded;
            Map.PickupDestroyed += OnPickupDestroyed;

            Scp096.AddingTarget += On096AddingTarget;
            Scp096.Enraging += On096Enraging;
            Scp096.CalmingDown += On096Calming;

            Warhead.Detonated += OnWarheadDetonated;
            Warhead.Starting += OnWarheadStarting;
            Warhead.Stopping += OnWarheadStopping;
            Warhead.DeadmanSwitchInitiating += OnDeadmanSwitch;
            Warhead.Detonating += OnWarheadDetonating;
            Warhead.ChangingLeverStatus += OnChangingLever;
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
            Player.InteractingElevator -= OnElevatorInteract;
            Player.InteractingLocker -= OnLockerInteract;
            Player.DroppingAmmo -= OnDroppingAmmo;
            Player.DroppedAmmo -= OnDroppedAmmo;
            Player.ThrownProjectile -= OnThrownProjectile;
            Player.Escaping -= OnEscaping;

            Map.GeneratorActivating -= OnGeneratorActivating;
            Map.Decontaminating -= OnDecontaminating;
            Map.AnnouncingNtfEntrance -= OnNtfAnnounced;
            Map.ExplodingGrenade -= OnGrenadeExploding;
            Map.SpawningItem -= OnSpawningItem;
            Map.PickupAdded -= OnPickupAdded;
            Map.PickupDestroyed -= OnPickupDestroyed;

            Scp096.AddingTarget -= On096AddingTarget;
            Scp096.Enraging -= On096Enraging;
            Scp096.CalmingDown -= On096Calming;

            Warhead.Detonated -= OnWarheadDetonated;
            Warhead.Starting -= OnWarheadStarting;
            Warhead.Stopping -= OnWarheadStopping;
            Warhead.DeadmanSwitchInitiating -= OnDeadmanSwitch;
            Warhead.Detonating -= OnWarheadDetonating;
            Warhead.ChangingLeverStatus -= OnChangingLever;
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
            Log.Info("Round log file created at " + path);
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
            _writer.WriteLine(ts + " | " + category.PadRight(18) + " | " + sub.PadRight(14) + " | " + message);
            _writer.Flush();
        }

        private void OnWaitingForPlayers() => Write("Connection update", "Server", "Waiting for players");
        private void OnRoundStarted()
        {
            StartFile();
            Write("Game Event", "Round", "Round started");
            foreach (var pl in Player.List)
            {
                Write("Player List", "Round", pl.Nickname + " - " + pl.Role.Type);
            }
        }
        private void OnRoundEnded(RoundEndedEventArgs ev)
        {
            Write("Game Event", "Round", "Round ended. Leading team: " + ev.LeadingTeam);
            CloseFile();
        }
        private void OnRoundRestart()
        {
            Write("Game Event", "Round", "Round restarting");
            CloseFile();
        }
        private void OnTeamRespawned(RespawnedTeamEventArgs ev)
        {
            Write("Game Event", "Respawn", "Team respawned: " + ev.Wave + " with players: " + ev.Players);
        }
        private void OnJoined(JoinedEventArgs ev)
        {
            Write("Connection update", "Networking", ev.Player.Nickname + " (" + ev.Player.UserId + ") joined");
        }
        private void OnLeft(LeftEventArgs ev)
        {
            Write("Connection update", "Networking", ev.Player.Nickname + " (" + ev.Player.UserId + ") left");
        }
        private void OnHurt(HurtEventArgs ev)
        {
            Write("Game Event", "Damage", (ev.Attacker?.Nickname) + " damaged " + ev.Player.Nickname + " for " + ev.Amount + " type " + ev.DamageHandler.Type);
        }
        private void OnDied(DiedEventArgs ev)
        {
            Write("Game Event", "Death", ev.Player.Nickname + " was killed by " + ev.Attacker?.Nickname);
        }
        private void OnSpawned(SpawnedEventArgs ev)
        {
            Write("Game Event", "Spawn", ev.Player.Nickname + " spawned as " + ev.Player.Role.Type);
        }
        private void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            Write("Game Event", "Item", ev.Player.Nickname + " picked up " + ev.Pickup?.Info.ItemId);
        }
        private void OnDroppingItem(DroppingItemEventArgs ev)
        {
            Write("Game Event", "Item", ev.Player.Nickname + " dropped " + ev.Item.Type + " thrown " + ev.IsThrown);
        }
        private void OnActivatingGenerator(ActivatingGeneratorEventArgs ev)
        {
            Write("Game Event", "Generator", ev.Player.Nickname + " activating generator");
        }
        private void OnDoorInteract(InteractingDoorEventArgs ev)
        {
            Write("Game Event", "Door", ev.Player.Nickname + " " + (ev.IsAllowed ? "opened" : "failed to open") + " " + ev.Door?.Name + " as " + ev.Player.Role.Type);
        }
        private void OnTriggerTesla(TriggeringTeslaEventArgs ev)
        {
            Write("Game Event", "Tesla", ev.Player.Nickname + " triggered tesla " + ev.IsAllowed);
        }
        private void OnGeneratorActivating(GeneratorActivatingEventArgs ev)
        {
            Write("Game Event", "Generator", "Generator activating allowed " + ev.IsAllowed);
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
            Write("Game Event", "SCP-096", "New target: " + ev.Target.Nickname);
        }
        private void On096Enraging(EnragingEventArgs ev)
        {
            Write("Game Event", "SCP-096", ev.Player.Nickname + " started enraging");
        }
        private void On096Calming(CalmingDownEventArgs ev)
        {
            Write("Game Event", "SCP-096", ev.Player.Nickname + " calmed down");
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

        private void OnElevatorInteract(InteractingElevatorEventArgs ev)
        {
            Write("Game Event", "Elevator", ev.Player.Nickname + " used elevator");
        }

        private void OnLockerInteract(InteractingLockerEventArgs ev)
        {
            Write("Game Event", "Locker", ev.Player.Nickname + " interacted with locker");
        }

        private void OnDroppingAmmo(DroppingAmmoEventArgs ev)
        {
            Write("Game Event", "Ammo", ev.Player.Nickname + " dropping " + ev.AmmoType + " x" + ev.Amount + " pickups " + ev.AmmoPickups.Count());
        }

        private void OnDroppedAmmo(DroppedAmmoEventArgs ev)
        {
            Write("Game Event", "Ammo", ev.Player.Nickname + " dropped " + ev.AmmoType + " x" + ev.Amount + " pickups " + ev.AmmoPickups.Count());
        }

        private void OnGrenadeExploding(ExplodingGrenadeEventArgs ev)
        {
            Write("Game Event", "Grenade", (ev.Player?.Nickname ?? "Unknown") + " grenade exploding");
        }

        private void OnSpawningItem(SpawningItemEventArgs ev)
        {
            Write("Game Event", "Map", "Spawning item " + ev.Pickup.Type);
        }

        private void OnPickupAdded(PickupAddedEventArgs ev)
        {
            Write("Game Event", "Pickup", "Pickup spawned " + ev.Pickup.Type);
        }

        private void OnPickupDestroyed(PickupDestroyedEventArgs ev)
        {
            Write("Game Event", "Pickup", "Pickup destroyed " + ev.Pickup.Type);
        }

        private void OnWarheadDetonating(DetonatingEventArgs ev)
        {
            Write("Game Event", "Warhead", "Warhead detonating");
        }

        private void OnChangingLever(ChangingLeverStatusEventArgs ev)
        {
            Write("Game Event", "Warhead", "Lever changed by " + (ev.Player?.Nickname ?? "Unknown"));
        }

        private void OnThrownProjectile(ThrownProjectileEventArgs ev)
        {
            Write("Game Event", "Projectile", ev.Player.Nickname + " threw " + ev.Throwable.Type);
        }

        private void OnEscaping(EscapingEventArgs ev)
        {
            Write("Game Event", "Escape", ev.Player.Nickname + " escaped as " + ev.NewRole + " scenario " + ev.EscapeScenario + " tickets " + ev.RespawnTickets);
        }
    }
}

