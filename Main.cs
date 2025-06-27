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
    private readonly Dictionary<string, PlayerLifeStats> _playerLifeStats = new();

    private readonly Dictionary<string, PlayerStats> _playerStats = new();
    private MyDatabaseHelper _dbHelper;
    private CoroutineHandle _DeadManCoroutine;
    private int _generatorCount;
    private CoroutineHandle _heavyLightsStage1Coroutine;
    private CoroutineHandle _heavyLightsStage2Coroutine;
    private CoroutineHandle _lightsCoroutine;
    private int _warheadChanceCounter;
    private CoroutineHandle _warheadCoroutine;

    public override string Name => "MaxunPlugin";
    public override string Author => "maxundeli";
    public override Version Version => new(1, 0, 0);
    public override Version RequiredExiledVersion => new(6, 0, 0);

    public override void OnEnabled()
    {
        Instance = this;

        if (Config.Database.Enabled)
        {
            _dbHelper = new MyDatabaseHelper(Config.Database.ConnectionString);
            _dbHelper.TestConnectionAsync();
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
        // Останавливаем корутину при отключении
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
            int playerID = ev.Player.Id;
            string playerIDstr = playerID.ToString();
            _playerStats[playerIDstr].takedSCPObjects++;
        }
    }

    private void OnDie(DiedEventArgs ev)
    {
        if (!Config.Stats.Enabled)
            return;

        int playerId = ev.Player.Id;
        string playerIdSt = Convert.ToString(playerId);
        ev.Player.Broadcast(7,
            "За эту жизнь убито: " + "<color=red>" + _playerLifeStats[playerIdSt].KillsLife + "</color>" +
            ", нанесено урона: " + "<color=red>" + _playerLifeStats[playerIdSt].DamageDealedLife + "</color>");
        _playerLifeStats[playerIdSt].KillsLife = 0;
        _playerLifeStats[playerIdSt].DamageDealedLife = 0;
        int attackerId = ev.Attacker.Id;

        string attackerIdSt = Convert.ToString(attackerId);
        if (playerId != attackerId)
        {
            _playerStats[attackerIdSt].Kills++;
            _playerLifeStats[attackerIdSt].KillsLife++;
        }

        if (ev.Player.PreviousRole == RoleTypeId.Scp049 || ev.Player.PreviousRole == RoleTypeId.Scp079 ||
            ev.Player.PreviousRole == RoleTypeId.Scp096 || ev.Player.PreviousRole == RoleTypeId.Scp106 ||
            ev.Player.PreviousRole == RoleTypeId.Scp173 ||
            ev.Player.PreviousRole == RoleTypeId.Scp939) _playerStats[attackerIdSt].SCPsKilled++;


        if ((ev.Attacker.Role.Side == Side.ChaosInsurgency && (ev.Player.PreviousRole == RoleTypeId.ChaosConscript ||
                                                               ev.Player.PreviousRole == RoleTypeId.ChaosMarauder ||
                                                               ev.Player.PreviousRole == RoleTypeId.ChaosRepressor ||
                                                               ev.Player.PreviousRole == RoleTypeId.ChaosRifleman ||
                                                               ev.Player.PreviousRole == RoleTypeId.ClassD)) ||
            (ev.Attacker.Role.Side == Side.Mtf &&
             (ev.Player.PreviousRole == RoleTypeId.NtfCaptain || ev.Player.PreviousRole == RoleTypeId.NtfPrivate ||
              ev.Player.PreviousRole == RoleTypeId.NtfSergeant || ev.Player.PreviousRole == RoleTypeId.NtfSpecialist ||
              ev.Player.PreviousRole == RoleTypeId.Scientist || ev.Player.PreviousRole == RoleTypeId.FacilityGuard)))
        {
            if (_playerStats[attackerIdSt].FFkills == -50)
            {
                _playerStats[attackerIdSt].FFkillsCount++;
                foreach (var player in Exiled.API.Features.Player.List)
                    player.Broadcast(10,
                        "<color=blue>" + ev.Attacker.Nickname + "</color>" + "<color=white> - </color>" +
                        "<color=red>ДОЛБАЕБ</color><color=white>, и убил уже " + "<color=red>" +
                        _playerStats[attackerIdSt].FFkillsCount + "</color>" + "<color=white> союзников</color>",
                        Broadcast.BroadcastFlags.Normal, true);
            }
            else
            {
                _playerStats[attackerIdSt].FFkills++;
                _playerStats[attackerIdSt].FFkillsCount++;
            }
        }


        if (_playerStats[attackerIdSt].FFkills >= 3)
        {
            _playerStats[attackerIdSt].FFkills = -50;
            foreach (var player in Exiled.API.Features.Player.List)
                player.Broadcast(10,
                    "<color=blue>" + ev.Attacker.Nickname + "</color>" + "<color=white> - </color>" +
                    "<color=red>ДОЛБАЕБ</color><color=white>, и убил уже " + "<color=red>" +
                    _playerStats[attackerIdSt].FFkillsCount + "</color>" + "<color=white> союзников</color>",
                    Broadcast.BroadcastFlags.Normal, true);
        }
    }

    private void PlayerHurt(HurtEventArgs ev)
    {
        if (!Config.Stats.Enabled)
            return;

        float damageDealed = ev.Amount;
        int damageDealedInt = Convert.ToInt32(damageDealed);
        int playerid = ev.Attacker.Id;
        string playerIdSt = Convert.ToString(playerid);
        if (ev.Attacker.Id != ev.Player.Id)
        {
            _playerStats[playerIdSt].DamageDealed = damageDealedInt + _playerStats[playerIdSt].DamageDealed;
            _playerLifeStats[playerIdSt].DamageDealedLife =
                damageDealedInt + _playerLifeStats[playerIdSt].DamageDealedLife;
        }
    }

    private void OnJoined(JoinedEventArgs ev)
    {
        if (!Config.Stats.Enabled)
            return;

        string id = Convert.ToString(ev.Player.UserId);
        string nickname = ev.Player.Nickname;
        int nonId = ev.Player.Id;
        Log.Warn(id + nickname + nonId);
        int playerId = ev.Player.Id;
        string playerIdSt = Convert.ToString(playerId);
        _playerStats[playerIdSt] = new PlayerStats { Kills = 0, DamageDealed = 0, FFkills = 0 };
        _playerLifeStats[playerIdSt] = new PlayerLifeStats { KillsLife = 0, DamageDealedLife = 0 };
    }

    private void OnRoundStarted()
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
            if (Config.Scp3114 && !isScp3114Spawned)
            {
                int chanceTo3114 = Random.Range(1, 101);
                if ((chanceTo3114 <= Config.Scp3114Chance) && (player.Role == RoleTypeId.Scp049 || player.Role == RoleTypeId.Scp0492 ||
                    player.Role == RoleTypeId.Scp079 || player.Role == RoleTypeId.Scp096 ||
                    player.Role == RoleTypeId.Scp106 || player.Role == RoleTypeId.Scp173 ||
                    player.Role == RoleTypeId.Scp939))
                {
                    player.RoleManager.ServerSetRole(RoleTypeId.Scp3114, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.All);
                    isScp3114Spawned = true;
                }
            }
            if (Config.Database.Enabled)
                _dbHelper.CreateRow(id, nickname);

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
        int MaxKills = 0;
        string MaxKillsNickname = null;
        string word = "убийств";
        foreach (var player in Exiled.API.Features.Player.List)
        {
            int playerid = player.Id;
            string playerIdSt = Convert.ToString(playerid);
            if (Config.Stats.Enabled &&
                _playerStats[playerIdSt].Kills - _playerStats[playerIdSt].FFkillsCount > MaxKills)
            {
                MaxKills = _playerStats[playerIdSt].Kills - _playerStats[playerIdSt].FFkillsCount;
                MaxKillsNickname = player.Nickname;
            };
        }

        if (MaxKills != 0)
        {
           word = CalculateRightWord(MaxKills);
        }
        else
        {
            MaxKillsNickname = "никто";
        }
        
        foreach (var player in Exiled.API.Features.Player.List)
        {
            int playerid = player.Id;
            string userId = player.UserId;
            string playerIdSt = Convert.ToString(playerid);

            if (Config.Stats.Enabled)
            {
                int damageDealed = _playerStats[playerIdSt].DamageDealed;
                int kills = _playerStats[playerIdSt].Kills;
                int FFkillsCount = _playerStats[playerIdSt].FFkillsCount;
                int takedSCPObjects = _playerStats[playerIdSt].takedSCPObjects;
                int SCPsKilled = _playerStats[playerIdSt].SCPsKilled;

                if (Config.Database.Enabled)
                {
                    Log.Warn(userId + kills + damageDealed);
                    _dbHelper.UpdateStat(userId, kills, damageDealed, Round.ElapsedTime, FFkillsCount,
                        takedSCPObjects, SCPsKilled);
                }

                string elapsedTime = Round.ElapsedTime.ToString("mm':'ss");
                player.Broadcast(7,
                    "Вы убили " + "<color=red>" + kills + "</color>" + " человек, из них союзников - " +
                    "<color=red>" + FFkillsCount + "</color>" + ". Всего нанесено урона: " +
                    "<color=red>" + damageDealed + "</color>\n" + "Самый результативный игрок - " + "<color=red>" +
                    MaxKillsNickname + "</color>" + " с " + "<color=red>" + MaxKills + " </color>" + word);
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

    public class PlayerStats
    {
        public int Kills { get; set; }
        public int FFkills { get; set; }
        public int FFkillsCount { get; set; }
        public int takedSCPObjects { get; set; }
        public int SCPsKilled { get; set; }
        public int DamageDealed { get; set; }
        // Можешь добавить другие свойства, как тебе нужно
    }

    public class PlayerLifeStats
    {
        public int KillsLife { get; set; }

        public int DamageDealedLife { get; set; }
        // Можешь добавить другие свойства, как тебе нужно
    }
}