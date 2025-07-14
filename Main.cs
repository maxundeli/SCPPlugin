using System.Net.Http;
using System.Text.Json;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp096;
using Exiled.Events.EventArgs.Server;
using Exiled.Events.EventArgs.Warhead;
using Exiled.Events.Handlers;
using MEC;
using MySql.Data.MySqlClient;
using PlayerRoles;
using UnityEngine;
using Cassie = Exiled.API.Features.Cassie;
using System.Threading.Tasks;
// библиотека корутин
using Map = Exiled.API.Features.Map;
using Player = Exiled.Events.Handlers.Player;
using Random = UnityEngine.Random;
using Server = Exiled.Events.Handlers.Server;
using Warhead = Exiled.API.Features.Warhead;

namespace MaxunPlugin;

public class Plugin : Plugin<Config>
{

    public static Plugin Instance;
    private readonly Dictionary<string, RoundPlayerStats> _roundStats = new();
    private MyDatabaseHelper _dbHelper;
    private CoroutineHandle _DeadManCoroutine;
    private int _generatorCount;
    private CoroutineHandle _heavyLightsStage1Coroutine;
    private CoroutineHandle _heavyLightsStage2Coroutine;
    private CoroutineHandle _lightsCoroutine;
    private int _warheadChanceCounter;
    private CoroutineHandle _warheadCoroutine;
    private RoundLogger _roundLogger;

    public override string Name => "MaxunPlugin";
    public override string Author => "maxundeli";
    public override Version Version => new(1, 0, 0);
    public override Version RequiredExiledVersion => new(6, 0, 0);

    public override void OnEnabled()
    {
        Instance = this;
        if (Config.Logging.Enabled)
        {
            _roundLogger = new RoundLogger();
            _roundLogger.Register(); 
        }
        if (Config.Database.Enabled)
        {
            _dbHelper = new MyDatabaseHelper(
                Config.Database.ConnectionString,
                Config.Database.HumanTable,
                Config.Database.ScpTable);
            Log.Info("Starting database initialization");
            _ = _dbHelper.InitializeAsync();
        }

        Player.Died += OnDie;
        Player.Hurt += PlayerHurt;
        Server.RespawnedTeam += OnTeamRespawned;
        Server.RoundStarted += OnRoundStarted;
        Player.Joined += OnJoined;
        Server.RoundEnded += OnRoundEnd;
        Server.RestartingRound += OnRoundRestart;
        Scp096.AddingTarget += RageStart;
        Exiled.Events.Handlers.Warhead.DeadmanSwitchInitiating += DeadmanS;
        Exiled.Events.Handlers.Map.GeneratorActivating += GeneratorAct;
        Player.Spawned += PlayerSpawned;
        Player.ActivatingGenerator += BeforeActGenerator;
        Player.PickingUpItem += pickingUpItem;

        base.OnEnabled();
        Log.Info("Plugin enabled!");
    }

    public override void OnDisabled()
    {
        _roundLogger.Unregister();
        _roundLogger.StopLogging();
        Player.Died -= OnDie;
        Player.Hurt -= PlayerHurt;
        Server.RoundStarted -= OnRoundStarted;
        Player.Joined -= OnJoined;
        Exiled.Events.Handlers.Warhead.DeadmanSwitchInitiating -= DeadmanS;
        Server.RoundEnded -= OnRoundEnd;
        Server.RestartingRound -= OnRoundRestart;
        Server.RespawnedTeam -= OnTeamRespawned;
        Scp096.AddingTarget -= RageStart;
        Player.Spawned -= PlayerSpawned;
        Exiled.Events.Handlers.Map.GeneratorActivating -= GeneratorAct;
        Player.PickingUpItem -= pickingUpItem;
        Player.ActivatingGenerator -= BeforeActGenerator;
        base.OnDisabled();
        Log.Info("Plugin disabled!");
    }

    private void DeadmanS(DeadmanSwitchInitiatingEventArgs ev)
    {
        ev.IsAllowed = false;
    }


    private void pickingUpItem(PickingUpItemEventArgs ev)
    {
        if (ev.Pickup.Category == ItemCategory.SCPItem)
        {
            string id = ev.Player.Id.ToString();
            if (_roundStats.TryGetValue(id, out var stats))
                stats.Human.ScpItems++;
        }
    }

    private void OnDie(DiedEventArgs ev)
    {
        if (!Config.Stats.Enabled)
            return;

        string victimId = ev.Player.UserId;
        if (_roundStats.TryGetValue(victimId, out var vStats))
        {
            var roleStats = vStats.CurrentIsScp ? vStats.Scp : vStats.Human;
            roleStats.Deaths++;
            if (ev.Attacker != null)
            {
                if (ev.Attacker.Role.Side == Side.Scp)
                    roleStats.DeathsFromScp++;
                else
                    roleStats.DeathsFromHuman++;
            }
            if (!vStats.IsSpectator)
            {
                float now = (float)Round.ElapsedTime.TotalSeconds;
                roleStats.TimeAlive += TimeSpan.FromSeconds(now - vStats.AliveStart);
                roleStats.TimePlayed += TimeSpan.FromSeconds(now - vStats.ActiveStart);
                vStats.IsSpectator = true;
            }
        }

        if (ev.Attacker != null)
        {
            string attackerId = ev.Attacker.UserId;
            if (_roundStats.TryGetValue(attackerId, out var aStats))
            {
                var att = aStats.CurrentIsScp ? aStats.Scp : aStats.Human;
                if (ev.Attacker != ev.Player)
                    att.Kills++;

                bool allyKill = ev.Attacker.Role.Team == ev.Player.Role.Team && ev.Attacker.Role.Side != Side.Scp;

                if (!aStats.CurrentIsScp)
                {
                    if (ev.Player.Role.Side == Side.Scp)
                        att.ScpsKilled++;
                    if (allyKill)
                    {
                        att.FFKills++;
                        if (att.FFKills >= Config.Stats.TeamkillLimit && Config.Stats.TeamkillBroadcast)
                        {
                            string msg = Config.Stats.TeamkillMessage.Replace("{killer}", ev.Attacker.Nickname)
                                .Replace("{victim}", ev.Player.Nickname)
                                .Replace("{count}", att.FFKills.ToString());
                            foreach (var pl in Exiled.API.Features.Player.List)
                                pl.Broadcast(10, msg, Broadcast.BroadcastFlags.Normal, true);
                        }
                    }
                }
            }
        }
    }

    private void PlayerHurt(HurtEventArgs ev)
    {
        if (!Config.Stats.Enabled)
            return;
        if (ev.Attacker == null || ev.Attacker.Id == ev.Player.Id)
            return;

        int dmg = (int)ev.Amount;
        if (ev.Attacker.Role.Type == RoleTypeId.Scp173 && dmg < 0)
            dmg = (int)ev.Player.MaxHealth;

        string attackerId = ev.Attacker.UserId;
        if (_roundStats.TryGetValue(attackerId, out var st))
        {
            var rs = st.CurrentIsScp ? st.Scp : st.Human;
            rs.Damage += dmg;
            if (ev.Player.Role.Side == Side.Scp)
                rs.DamageToScp += dmg;
        }

        
    }

    private void OnJoined(JoinedEventArgs ev)
    {
        if (!Config.Stats.Enabled)
            return;

        string id = ev.Player.UserId;
        string nickname = ev.Player.Nickname;
        _roundStats[id] = new RoundPlayerStats();

        if (Config.Database.Enabled)
        {
            _dbHelper.CreateRow(id, nickname);
            _dbHelper.UpdateNickname(id, nickname);
        }
    }

    private async void OnRoundStarted()
    {
        UnityEngine.Random.InitState((int)DateTime.UtcNow.Ticks);
        bool isScp3114Spawned = false;
        _warheadChanceCounter = 0;
        foreach (var player in Exiled.API.Features.Player.List)
        {

            
            string id = player.UserId;
            string nickname = player.Nickname;
            int nonId = player.Id;
            Log.Warn(id + nickname + nonId);
            if (player.Role == RoleTypeId.Scp049 || player.Role == RoleTypeId.Scp0492 ||
                player.Role == RoleTypeId.Scp079 || player.Role == RoleTypeId.Scp096 ||
                player.Role == RoleTypeId.Scp106 || player.Role == RoleTypeId.Scp173 ||
                player.Role == RoleTypeId.Scp939)
            {
                if (Config.Scp3114 && !isScp3114Spawned)
                {
                    int chanceTo3114 = Random.Range(1, 101);
                    if ((chanceTo3114 <= Config.Scp3114Chance))
                    {
                        player.RoleManager.ServerSetRole(RoleTypeId.Scp3114, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.All);
                        isScp3114Spawned = true;
                        _roundLogger.Write("Player List", "Round", nickname + " respawned as " + player.Role.Type);
                    }
                }
                
                
                
            }
            
            if (Config.Database.Enabled)
                _dbHelper.CreateRow(id, nickname);

            if (Config.Stats.Enabled && Config.Database.Enabled)
            {
                _ = ShowPlayerStatsAsync(player, id);
            }

            if (Config.SpawnItems.TryGetValue(player.Role, out var spawnList))
            {
                foreach (var spawn in spawnList)
                {
                    if (Random.Range(1, 101) <= spawn.Chance)
                        player.AddItem(spawn.Item);
                }
            }
        }

        try
        {
            Log.Info("System.Net.Http assembly: " + typeof(HttpClient).Assembly.FullName);
            Log.Info("System.Text.Json assembly: " + typeof(JsonSerializer).Assembly.FullName);
        }
        catch (Exception e)
        {
            Log.Error(e);
            throw;
        }


        Respawn.AdvanceTimer(SpawnableFaction.NtfWave, 50);
        Respawn.AdvanceTimer(SpawnableFaction.ChaosWave, 50);
        if (Config.Blackout.Enabled)
        {
            _generatorCount = 0;
            Log.Info("OnRoundStarted - starting coroutine!");
            _lightsCoroutine = Timing.RunCoroutine(LightsCoroutine());
            Log.Info("Coroutine started, _lightsCoroutine: " + _lightsCoroutine);
            Map.TurnOffAllLights(12000f, ZoneType.HeavyContainment);
        }

        if (Config.AutoBomb.Enabled)
            _warheadCoroutine = Timing.RunCoroutine(WarheadCoroutine());
    }

    private void OnRoundEnd(RoundEndedEventArgs ev)
    {
        ev.TimeToRestart = 15;
        Log.Info("Stopping coroutine");
        if (Config.Blackout.Enabled)
        {
            Timing.KillCoroutines(_lightsCoroutine);
            Timing.KillCoroutines(_heavyLightsStage1Coroutine);
            Timing.KillCoroutines(_heavyLightsStage2Coroutine);
        }
        if (Config.AutoBomb.Enabled)
            Timing.KillCoroutines(_warheadCoroutine);
        foreach (var player in Exiled.API.Features.Player.List)
        {
            string id = player.UserId;
            if (!_roundStats.TryGetValue(id, out var stats))
                continue;

            var current = stats.CurrentIsScp ? stats.Scp : stats.Human;
            if (!stats.IsSpectator)
            {
                float now = (float)Round.ElapsedTime.TotalSeconds;
                current.TimeAlive += TimeSpan.FromSeconds(now - stats.AliveStart);
                current.TimePlayed += TimeSpan.FromSeconds(now - stats.ActiveStart);
            }

            if (Config.Database.Enabled)
            {
                if (stats.Human.TimePlayed > TimeSpan.Zero)
                    _dbHelper.UpdateHumanStats(id, new HumanDbStats(stats.Human));
                if (stats.Scp.TimePlayed > TimeSpan.Zero)
                    _dbHelper.UpdateScpStats(id, new ScpDbStats(stats.Scp));
            }
        }
    }

    private void OnRoundRestart()
    {
        Log.Info("Stopping coroutine");
        if (Config.Blackout.Enabled)
        {
            Timing.KillCoroutines(_lightsCoroutine);
            Timing.KillCoroutines(_heavyLightsStage1Coroutine);
            Timing.KillCoroutines(_heavyLightsStage2Coroutine);
        }

        if (Config.AutoBomb.Enabled)
            Timing.KillCoroutines(_warheadCoroutine);
    }

    private async Task ShowPlayerStatsAsync(Exiled.API.Features.Player player, string id)
    {
        await Task.Delay(TimeSpan.FromSeconds(15));

        if (player.Role.Side == Side.Scp)
        {
            var stats = await _dbHelper.GetScpStatsAsync(id);
            var killsRank = await _dbHelper.GetStatRankAsync(id, "kills", Config.Database.ScpTable);
            var dmgRank = await _dbHelper.GetStatRankAsync(id, "damage", Config.Database.ScpTable);
            var deathsRank = await _dbHelper.GetStatRankAsync(id, "deaths", Config.Database.ScpTable);
            var kills10Rank = await _dbHelper.GetStatRankAsync(id, "kills_10m", Config.Database.ScpTable);
            var dmg10Rank = await _dbHelper.GetStatRankAsync(id, "damage_10m", Config.Database.ScpTable);
            var deaths10Rank = await _dbHelper.GetStatRankAsync(id, "deaths_10m", Config.Database.ScpTable);

            string hint =
                "<size=22><b><color=#ffb84d>Statistics</color></b></size>\n" +
                "<size=20>Kills: <color=red>" + stats.Kills + "</color>" + FormatRank(killsRank) + " / <color=red>" + PerTen(stats.Kills, stats.TimePlayed) + "</color>" + FormatRank(kills10Rank) + " (10m)</size>\n" +
                "<size=20>Damage: <color=red>" + stats.Damage + "</color>" + FormatRank(dmgRank) + " / <color=red>" + PerTen(stats.Damage, stats.TimePlayed) + "</color>" + FormatRank(dmg10Rank) + " (10m)</size>\n" +
                "<size=20>Deaths: <color=red>" + stats.Deaths + "</color>" + FormatRank(deathsRank) + " / <color=red>" + PerTen(stats.Deaths, stats.TimePlayed) + "</color>" + FormatRank(deaths10Rank) + " (10m)</size>\n" +
                "<size=20>Playtime: <color=green>" + stats.TimePlayed.ToString("hh':'mm':'ss") + "</color></size>";

            player.ShowHint(hint, 7f);
        }
        else
        {
            var stats = await _dbHelper.GetHumanStatsAsync(id);
            var killsRank = await _dbHelper.GetStatRankAsync(id, "kills", Config.Database.HumanTable);
            var dmgRank = await _dbHelper.GetStatRankAsync(id, "damage", Config.Database.HumanTable);
            var ffRank = await _dbHelper.GetStatRankAsync(id, "ff_kills", Config.Database.HumanTable);
            var kills10Rank = await _dbHelper.GetStatRankAsync(id, "kills_10m", Config.Database.HumanTable);
            var dmg10Rank = await _dbHelper.GetStatRankAsync(id, "damage_10m", Config.Database.HumanTable);
            var ff10Rank = await _dbHelper.GetStatRankAsync(id, "ff_kills_10m", Config.Database.HumanTable);
            var deaths10Rank = await _dbHelper.GetStatRankAsync(id, "deaths_10m", Config.Database.HumanTable);

            string hint =
                "<size=22><b><color=#ffb84d>Statistics</color></b></size>\n" +
                "<size=20>Kills: <color=red>" + stats.Kills + "</color>" + FormatRank(killsRank) + " / <color=red>" + PerTen(stats.Kills, stats.TimePlayed) + "</color>" + FormatRank(kills10Rank) + " (10m)</size>\n" +
                "<size=20>Damage: <color=red>" + stats.Damage + "</color>" + FormatRank(dmgRank) + " / <color=red>" + PerTen(stats.Damage, stats.TimePlayed) + "</color>" + FormatRank(dmg10Rank) + " (10m)</size>\n" +
                "<size=20>Teamkills: <color=red>" + stats.FFKills + "</color>" + FormatRank(ffRank) + " / <color=red>" + PerTen(stats.FFKills, stats.TimePlayed) + "</color>" + FormatRank(ff10Rank) + " (10m)</size>\n" +
                "<size=20>Deaths: <color=red>" + stats.Deaths + "</color>" + FormatRank(null) + " / <color=red>" + PerTen(stats.Deaths, stats.TimePlayed) + "</color>" + FormatRank(deaths10Rank) + " (10m)</size>\n" +
                "<size=20>SCP kills: <color=red>" + stats.ScpsKilled + "</color> | Items: <color=red>" + stats.ScpItems + "</color></size>\n" +
                "<size=20>Escapes: <color=red>" + stats.Escapes + "</color> | Playtime: <color=green>" + stats.TimePlayed.ToString("hh':'mm':'ss") + "</color></size>";

            player.ShowHint(hint, 7f);
        }
    }

    private static string PerTen(int value, TimeSpan time)
    {
        if (time.TotalMinutes < 0.1)
            return "0";
        double val = value / (time.TotalMinutes / 10.0);
        return Math.Round(val, 1).ToString();
    }

    private static string FormatRank(int? rank)
    {
        if (!rank.HasValue || rank.Value > 3)
            return string.Empty;

        string color = rank.Value switch
        {
            1 => "#FFD700",
            2 => "#C0C0C0",
            3 => "#CD7F32",
            _ => "white"
        };

        return $" <color={color}>★{rank.Value}★</color>";
    }


    private void OnTeamRespawned(RespawnedTeamEventArgs ev)
    {
        Respawn.AdvanceTimer(SpawnableFaction.ChaosWave, 50);
        Respawn.AdvanceTimer(SpawnableFaction.NtfWave, 50);
    }

    private void BeforeActGenerator(ActivatingGeneratorEventArgs ev)
    {
        if (!Config.Blackout.Enabled)
            return;
        ev.Generator.ActivationTime = 10f;
    }

    private void GeneratorAct(GeneratorActivatingEventArgs ev)
    {
        if (!Config.Blackout.Enabled)
            return;
        _generatorCount++;
        if (_generatorCount == 1)
        {
            Log.Info("First generator activated. Lights on, starting stage1 coroutine");
            Map.TurnOnAllLights(new[]
            {
                ZoneType.HeavyContainment
            });
            _heavyLightsStage1Coroutine = Timing.RunCoroutine(HeavyLightsStage1Coroutine());
        }

        if (_generatorCount == 2)
        {
            Log.Info("Second generator activated. Lights on, starting stage2 coroutine");
            Timing.KillCoroutines(_heavyLightsStage1Coroutine);
            Map.TurnOnAllLights(new[]
            {
                ZoneType.HeavyContainment
            });
            _heavyLightsStage2Coroutine = Timing.RunCoroutine(HeavyLightsStage2Coroutine());
        }

        if (_generatorCount == 3)
        {
            Log.Info("Third generator activated. Lights on");
            Timing.KillCoroutines(_heavyLightsStage2Coroutine);
            Map.ChangeLightsColor(Color.clear);
            Timing.KillCoroutines(_lightsCoroutine);
            Map.TurnOnAllLights(new[]
            {
                ZoneType.Entrance,
                ZoneType.LightContainment,
                ZoneType.Surface,
                ZoneType.HeavyContainment
            });
        }
    }

    private void RageStart(AddingTargetEventArgs ev)
    {
        ev.Target.PlaceTantrum();
    }

    private void PlayerSpawned(SpawnedEventArgs ev)
    {
        if (Config.Stats.Enabled)
        {
            string id = ev.Player.UserId;
            if (_roundStats.TryGetValue(id, out var st))
            {
                st.CurrentIsScp = ev.Player.Role.Side == Side.Scp;
                st.IsSpectator = false;
                float time = (float)Round.ElapsedTime.TotalSeconds;
                st.ActiveStart = time;
                st.AliveStart = time;
            }
        }
        if (ev.Player.IsScp)
        {
            _roundLogger.Write("Player List", "Round", ev.Player.Nickname + " respawned as " + ev.Player.Role.Type);
            var scpType = ev.Player.Role.Type;
            switch (scpType)
            {
                case RoleTypeId.Scp049:
                    ev.Player.MaxHealth = 1600;
                    ev.Player.Health = ev.Player.MaxHealth;
                    break;
                case RoleTypeId.Scp096:
                    ev.Player.MaxHealth = 1800;
                    ev.Player.Health = ev.Player.MaxHealth;
                    break;
                case RoleTypeId.Scp106:
                    ev.Player.MaxHealth = 1800;
                    ev.Player.Health = ev.Player.MaxHealth;
                    break;
                case RoleTypeId.Scp173:
                    ev.Player.MaxHealth = 2500;
                    ev.Player.Health = ev.Player.MaxHealth;
                    break;
                case RoleTypeId.Scp939:
                    ev.Player.MaxHealth = 2000;
                    ev.Player.Health = ev.Player.MaxHealth;
                    break;
                case RoleTypeId.Scp3114:
                    ev.Player.MaxHealth = 700;
                    ev.Player.Health = ev.Player.MaxHealth;
                    break;
                        
            }
        }
        if (ev.Player.IsCHI)
        {
            ev.Player.AddItem(ItemType.Radio);
        }
    }

    private IEnumerator<float> HeavyLightsStage1Coroutine()
    {
        Map.ChangeLightsColor(Color.blue);
        yield return Timing.WaitForSeconds(8f);
        Map.ChangeLightsColor(Color.clear);
        Log.Info("Stage1 coroutine started");
        yield return Timing.WaitForSeconds(60);
        while (true)
        {
            Map.TurnOffAllLights(120, ZoneType.HeavyContainment);
            yield return Timing.WaitForSeconds(180);
        }
    }

    public string CalculateRightWord(int number)
    {
        switch (number)
        {
           case 1: return "убийством";
           case int n when n > 1:
               return "убийствами";
           default:
               throw new Exception();
        }
    }

    private IEnumerator<float> HeavyLightsStage2Coroutine()
    {
        Map.ChangeLightsColor(Color.blue);
        yield return Timing.WaitForSeconds(8f);
        Map.ChangeLightsColor(Color.clear);
        Log.Info("Stage2 coroutine started");
        yield return Timing.WaitForSeconds(60);
        while (true)
        {
            Map.TurnOffAllLights(60, ZoneType.HeavyContainment);
            yield return Timing.WaitForSeconds(120);
        }
    }

    private IEnumerator<float> DeadManActivation()
    {
        Cassie.GlitchyMessage("BY ORDER OF O5 COMMAND . DEAD MAN SEQUENCE ACTIVATED", 0.1f, 0.05f);
        yield return Timing.WaitForSeconds(8f);
        Warhead.Start();
    }

    private IEnumerator<float> LightsCoroutine()
    {
        int delay = Random.Range(60, 90);
        Log.Info("Delay " + delay + " seconds");
        yield return Timing.WaitForSeconds(delay);
        Log.Info("Starting loop");
        while (true)
        {
            int lightOffTime = Random.Range(Config.Blackout.DurationMin, Config.Blackout.DurationMax);
            int chance = Random.Range(1, 101);
            Log.Info("Random attempt: got " + chance + " and " + lightOffTime);

            if (chance <= Config.Blackout.Chance)
            {
                Map.TurnOffAllLights(lightOffTime, ZoneType.Entrance);
                Map.TurnOffAllLights(lightOffTime, ZoneType.LightContainment);
                Map.TurnOffAllLights(lightOffTime, ZoneType.Surface);

                Cassie.GlitchyMessage("Lights out for " + lightOffTime + " seconds.", 0.4f, 0.2f);
            }

            int nextDelay = Random.Range(Config.Blackout.IntervalMin, Config.Blackout.IntervalMax);
            yield return Timing.WaitForSeconds(nextDelay);
        }
    }



    private IEnumerator<float> WarheadCoroutine()
    {
        if (!Config.AutoBomb.Enabled)
            yield break;

        yield return Timing.WaitForSeconds(660f);
        while (true)
        {
            _warheadChanceCounter++;
            int warheadActChance = Random.Range(1, 5);
            int warheadActMegaChance = Random.Range(1, 3);

            if (_warheadChanceCounter > 3)
            {
                Log.Info("Increased detonation chance. Rolled - " + warheadActMegaChance + " Target - 2");
                if (warheadActMegaChance == 2)
                {
                    _DeadManCoroutine = Timing.RunCoroutine(DeadManActivation());
                    Timing.KillCoroutines(_warheadCoroutine);
                }
            }

            else
            {
                Log.Info("Detonation chance. Rolled - " + warheadActChance + " Target - 2");
                if (warheadActChance == 2)
                {
                    _DeadManCoroutine = Timing.RunCoroutine(DeadManActivation());
                    Timing.KillCoroutines(_warheadCoroutine);
                }
            }

            yield return Timing.WaitForSeconds(60f);
        }
    }

public class RoleStats
{
    public int Damage;
    public int DamageToScp;
    public int Kills;
    public int Deaths;
    public int DeathsFromScp;
    public int DeathsFromHuman;
    public int ScpItems;
    public int ScpsKilled;
    public int FFKills;
    public int Escapes;
    public TimeSpan TimePlayed;
    public TimeSpan TimeAlive;
}

public class RoundPlayerStats
{
    public RoleStats Human = new();
    public RoleStats Scp = new();
    public bool CurrentIsScp;
    public bool IsSpectator = true;
    public float ActiveStart;
    public float AliveStart;
}
}
