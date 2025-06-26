using System.Net.Http;
using System.Text;
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
using Newtonsoft.Json;
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

public class OllamaChunk
{
    // Поля, которые Ollama может присылать в каждом chunk’е
    [JsonProperty("model")] public string Model { get; set; }

    [JsonProperty("created_at")] public string CreatedAt { get; set; }

    [JsonProperty("response")] public string Response { get; set; }

    [JsonProperty("done")] public bool Done { get; set; }

    [JsonProperty("done_reason")] public string DoneReason { get; set; }

    // Можешь добавить остальные поля: context, total_duration и т.д. если надо парсить
    // [JsonProperty("context")]
    // public int[] Context { get; set; }
    // ...
}

public class OllamaClient
{
    private readonly HttpClient _httpClient;

    public OllamaClient(string baseAddress = "http://192.168.1.182:11411")
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
    }
    

    
    public async Task<string> SendRequestAsync(string prompt, string model = "gemma3:12b", int num_predict=30)
    {
        try
        {
            // Генерим свой объект
            var requestData = new { prompt, model, num_predict };

            // Сериализация через Newtonsoft
            string jsonRequest = JsonConvert.SerializeObject(requestData);

            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("/api/generate", content);
            response.EnsureSuccessStatusCode();

            string? responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }
        catch (Exception e)
        {
            Log.Error("SendRequestAsync упал с ошибкой: " + e);
            return "Error: " + e;
        }
    }
}



public class Plugin : Plugin<Config>
{
    private List<string> phrases = new List<string> { };
    private string OllamaRespone = null;
    public static Plugin Instance;
    private readonly Dictionary<string, PlayerLifeStats> _playerLifeStats = new();

    private readonly Dictionary<string, PlayerStats> _playerStats = new();
    private MyDatabaseHelper _dbHelper;
    private CoroutineHandle _DeadManCoroutine;
    private int _generatorCount;
    private CoroutineHandle _heavyLightsStage1Coroutine;
    private CoroutineHandle _heavyLightsStage2Coroutine;
    private CoroutineHandle _lightsCoroutine;
    private CoroutineHandle _phrasesCoroutine;
    private int _warheadChanceCounter;
    private CoroutineHandle _warheadCoroutine;
    private CoroutineHandle _ollamacoroutine;

    public override string Name => "MyPlugin";
    public override string Author => "Твоё Имя";
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
        Log.Info("Плагин включён!");
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
        Log.Info("Плагин выключён!");
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
            if (Config.Scp3114 == true)
            {
                int chanceTo3114 = Random.Range(1, 101);
                if ((chanceTo3114 <= Config.Scp3114Chance) && (player.Role == RoleTypeId.Scp049 || player.Role == RoleTypeId.Scp0492 ||
                    player.Role == RoleTypeId.Scp079 || player.Role == RoleTypeId.Scp096 ||
                    player.Role == RoleTypeId.Scp106 || player.Role == RoleTypeId.Scp173 ||
                    player.Role == RoleTypeId.Scp939) && isScp3114Spawned == false)
                {
                    player.RoleManager.ServerSetRole(RoleTypeId.Scp3114, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.All);
                    isScp3114Spawned = true;
                }
            }
            if (Config.Database.Enabled)
                _dbHelper.CreateRow(id, nickname);

            if ((player.Role == RoleTypeId.ClassD || player.Role == RoleTypeId.Scientist) &&
                Random.Range(1, 101) <= Config.FlashlightChance)
            {
                player.AddItem(ItemType.Flashlight);
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

        // _ollamacoroutine = Timing.RunCoroutine(OllamaCoroutine());
        // _phrasesCoroutine = Timing.RunCoroutine(PhrasesCoroutine());

        Respawn.AdvanceTimer(SpawnableFaction.NtfWave, 50);
        Respawn.AdvanceTimer(SpawnableFaction.ChaosWave, 50);
        if (Config.Blackout.Enabled)
        {
            _generatorCount = 0;
            Log.Info("Я в OnRoundStarted, сейчас буду запускать корутину!");
            _lightsCoroutine = Timing.RunCoroutine(LightsCoroutine());
            Log.Info("Корутину запустил, _lightsCoroutine: " + _lightsCoroutine);
            Map.TurnOffAllLights(12000f, ZoneType.HeavyContainment);
        }

        if (Config.AutoBomb.Enabled)
            _warheadCoroutine = Timing.RunCoroutine(WarheadCoroutine());
    }

    private void OnRoundEnd(RoundEndedEventArgs ev)
    {
        ev.TimeToRestart = 15;
        Log.Info("Выключаю корутину");
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
        Log.Info("Выключаю корутину");
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
            Log.Info("Запуск первого генератора. Включение света, запуск корутины stage1");
            Map.TurnOnAllLights(new[]
            {
                ZoneType.HeavyContainment
            });
            _heavyLightsStage1Coroutine = Timing.RunCoroutine(HeavyLightsStage1Coroutine());
        }

        if (_generatorCount == 2)
        {
            Log.Info("Запуск второго генератора. Включение света, запуск корутины stage2");
            Timing.KillCoroutines(_heavyLightsStage1Coroutine);
            Map.TurnOnAllLights(new[]
            {
                ZoneType.HeavyContainment
            });
            _heavyLightsStage2Coroutine = Timing.RunCoroutine(HeavyLightsStage2Coroutine());
        }

        if (_generatorCount == 3)
        {
            Log.Info("Запуск третьего генератора. Включение света");
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
        Log.Info("корутина stage1 запущена");
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
        Log.Info("корутина stage2 запущена");
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
        Log.Info("Задержка " + delay + " секунд");
        yield return Timing.WaitForSeconds(delay);
        Log.Info("запуск цикла");
        while (true)
        {
            int lightOffTime = Random.Range(Config.Blackout.DurationMin, Config.Blackout.DurationMax);
            int chance = Random.Range(1, 101);
            Log.Info("Попытка рандома: Выпало " + chance + " и " + lightOffTime);

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

    // private IEnumerator<float> OllamaCoroutine()
    // {
    //     Log.Info("Запускаю корутину OllamaCoroutine.");
    //
    //     // Будем делать несколько попыток, чтобы получить валидный текст
    //     const int maxAttempts = 20; // К примеру, максимум 3 попытки
    //     int attempts = 0;
    //
    //     while (attempts < maxAttempts)
    //     {
    //         attempts++;
    //         Log.Info($"Попытка #{attempts} получить текст от Ollama...");
    //
    //         // 1. Создаём OllamaClient и шлём запрос
    //         var ollamaClient = new OllamaClient();
    //         Task<string> task = ollamaClient.SendRequestAsync(
    //             "You are a generator of short, ominous, and eerie sentences using only a limited vocabulary. Do not invent or infer new words. Use only the exact words provided in the list. Do not conjugate, decline, or otherwise modify any word. Each output should be one complete sentence, with no more than 8 to 12 words. Sentences should sound disturbing, cryptic, and intense, like a warning or system announcement. Do not repeat the same phrase in multiple outputs. Especially avoid overusing phrases like \"Access Denied\" or \"System Alert\". Vary your language. Use different combinations of allowed words each time. If the output repeats a known cliché, discard and regenerate. Output only the sentence text, without explanation, formatting, punctuation beyond periods. The goal is to simulate an ominous AI message or SCP system alert. Here's your list of allowed words: [Able About Above Absolute Accepted Access Acid Acquired Across Activate Activated Activating Activation Activity Address Administer Administered Adrenaline Advanced Advise Again Against Agent Aid Airlock Alarm Alert Alive All Alpha Already Also Always Am Amount Amplitude An Analog Analysis And Announcement Anomaly Answer Any Approach April Are Area Armory Around Arrest Arrival As At Attack Attempt Attention August Authorized Automatic Autonomic Autonomous Available Avoid Away Back Backup Bad Ban Bank Base Basic Battery Be Because Been Begun Behavior Behind Being Believe Below Beside Best Beta Between Big Billiard Biological Black Blast Blue Board Body Both Bottom Breach Break Breaker Broad Broadcast Build Built But By Byte Bytes Cadet Calm Camera Can Cannot Cant Capture Card Cassie Cast Cause Caution Cease Celsius Center Centi Central Chamber Chaos ChaosInsurgency Charge Check Checkpoint Chi Class ClassD Clearance Close Closed Code Come Command Command2 Commander Commencing Community Complete Completed Complex Condition Confirm Console Contain Contained ContainedSuccessfully Containment ContainmentUnit Contamination Continue Control Cooperate Core Correct Corrupted Corruption Could Course Credit Critical Current Curved Damage Danger Dark Data Date Day Deactivate Deactivated Dead Deca December Deci Decision Decontamination Decrease Defense Degrees Delta Denied Designated Destroy Destroyed Destruct Destructed Destruction Detected Detonate Detonated Detonating Detonation Device Diagnostic Did Die Diesel Different Digital Dimension Disable Disabled Disables Discharge Disease Disengaged Distance Divided Division Do Doctor Does Done Door Doors Dose Down Earth East Effect Either Electric Electromagnetism Elevator Elevators Emergency Empty Enable Enabled Enables End Engaged Engine Enough Enter Entire Entrance Epsilon Equal Equals Error Escape Escort Escort2 Essential Estimated Eta Euclid Evacuate Evacuation Evasion Even Ever Every Evidence Except Exception Exclude Exclusion Execute Executed Executive Exit Expiration Explain Explosion Explosive Expunged External Extinguishment Extremely Facility Fahrenheit Failed Failure False Far February Feel Femur Few Field Find Fine Finished Fire First For Force Forward Found Foundation Frequency Friday From Front Fuel Full Game Gamma Gas Gasoline Gate Gates General Generator Generators Get Giga Give Global Go Goes Going Gone Good Got Gram Grams Granted Grave Great Greater Green Grenade Ground Group Guard Gun Hack Half Hallway Hand Hardware Has Have Hazard He Head Hear Heavy Hecto Helicopter Hello Help Her Here High His Hit Hour How Human Hundred Identification Identify If Immediate Immediately Important In Include Inclusion Incorrect Increase Indentified Infection Information Initializing Initiated Inside Installation Installed Installing Insurgency Insurgent Intercom Interest Internal Intersection Into Intruder Iota Is It January Job Join July Jump June Just Kappa Keep Kelvin Keter Key Kill Killer Kilo King Kit Know Laboratory Lambda Last Leak Leave Left Less Lethal Letter Level Lieutenant Life Light Lights Like Liquefied Liquid Live Load Local Locate Location Lock Lockdown Look Lost Low Magnetic Main Major Make Malfunction Man Manager Manual Many March Material May Me Med Medical Medium Mega Memory Men Message Meter Meters Micro Mili Military Milliard Million Minor Minus Minute Minutes Mobile Mode Module Monday Month More Morphine Most Move Mu Much Must My Name Nano Near Nearby Nearest Necessary Neck Need Negative Neutralize Never New Next Nice NineTailedFox No Nominal Nor Normal Normalize North Northwood Not Notice November Now Nu Nuclear Number O5 October Of Off Offline Often Old Omega Omicron On Once Ongoing Online Only Open Operate Operation Operational Operative Optimal Optional Or Orange Order Organism Other Out Outage OutOf Outside Over Overall Overcharge Overheat Override Pain Panic Partners Password Patch Patreon Pause Paused Pay People Percent Permanent Person Personnel Petroleum Phi Pi Pistol Plague Please Plus Pocket Positive Possible Potential Power Previous Primary Priority Private Probability Procedure Proceed Process Progress Project Project2 Prosecution Protect Protection Protocol Psi Public Purge Put Query Question Questioning Radiation Radio Rank Reactivating Reactivation Reactor Ready Real Really Recieve Recontained Recontainment Red Redacted Rejected Remaining Remind Repair Repairing Repeat Report Require Respawn Respect Restricted Result Resumed Resurrection Retreat Return Revoke Rifle Right Ro Room Rough Run Running Safe Saturday Saw Say Scan Scanning Science Scientist Scientists Scp ScpSubject ScpSubjects Seal Search Second Secondary Seconds Secret Sector Secure Security See Self Send Senior Sentient September Sequence Serious Seriously Serpents Service She Shelter Shoot Shooting Shot Should Shut Shutting Side Sigma Site Small Snap So Software Solid Some Somebody Someone Something Sorry Source South Space Spotted Squad Stabilize Stable Stand Standard Standby Start Started Starting Static Status Stay Step Stop Stopped Storage Straight Strictly Studio Substantial Subsystem Successfully Sunday Supply Support Supporter Surface Surge Surprise Surrender Survive Survivor System Systems Tactical Take Taken Tank Target Task Tau Team Tell Temperature Temporal Tera Terminal Terminate Terminated Termination Tesla Test Thank Thanks That The Their Them Then There Thermal Theta They The_Consonant Thing Think Third This Though Thought Thousand Threat Through Thursday Time Times To Today Tomorrow Too Top Touch Towards Trillion True Tuesday Turn Turned Turns Twice Unable Unauthorized Unavailable Under Unit Universal Unknown Unnecessary Unspecified Unstable Until Up Upsilon Us USBDrive Use User Using Very Virus Wait Wall Want Warhead Warning Was Way Weapon Weapons Wednesday Week Welcome Well Went Were West What When Where Which White Who Why Wide Will With Without Wood Word Work World Worst Would Wrong Xi Year Yellow Yes Yesterday Yet You Your Zeta Zone] Begin generating phrases. Each output must strictly use only the words from the list. No synonyms, no explanations, no instructions, no commentary. Just the sentence.");
    //
    //         // 2. Ждём завершения асинхронного запроса
    //         while (!task.IsCompleted) yield return 0f;
    //
    //         // 3. Проверяем, всё ли ОК
    //         if (task.IsFaulted)
    //         {
    //             Log.Error($"Ошибка в задаче Ollama: {task.Exception}");
    //             yield break; // или continue, если хочешь повторять
    //         }
    //
    //         if (task.IsCanceled)
    //         {
    //             Log.Info("Запрос отменён.");
    //             yield break;
    //         }
    //
    //         // 4. Разбиваем NDJSON и собираем ответ
    //         string ndjson = task.Result;
    //         Log.Info($"Сырой NDJSON: {ndjson}");
    //
    //         string[] lines = ndjson.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
    //         var sb = new StringBuilder();
    //
    //         foreach (string line in lines)
    //             try
    //             {
    //                 var chunk = JsonConvert.DeserializeObject<OllamaChunk>(line);
    //                 if (chunk == null)
    //                     continue;
    //
    //                 if (!string.IsNullOrEmpty(chunk.Response))
    //                     sb.Append(chunk.Response);
    //
    //                 if (chunk.Done)
    //                     break;
    //             }
    //             catch (Exception ex)
    //             {
    //                 Log.Error($"Ошибка при парсинге NDJSON");
    //             }
    //
    //         string fullResponse = sb.ToString();
    //         fullResponse = fullResponse.Replace(".", "");
    //         fullResponse = fullResponse.Replace("commence", "commencing");
    //         fullResponse = fullResponse.Replace(";", "");
    //         fullResponse = fullResponse.Replace(":", "");
    //         fullResponse = fullResponse.Replace(",", "");
    //         fullResponse = fullResponse.Replace("**", "");
    //         Log.Info($"Склеенный ответ модели: {fullResponse}");
    //
    //         // 5. Проверяем, валидно ли для Cassie
    //         bool isValid = Cassie.IsValidSentence(fullResponse) && fullResponse.Length != 0 && fullResponse.Length < 120;
    //         if (isValid)
    //         {
    //             phrases.Add(fullResponse);
    //             // Если валидно — выходим из цикла
    //             Log.Info("Текст прошёл проверку Cassie. Буду использовать его!");
    //             // Можно сразу проиграть Cassie, если надо:
    //             // Cassie.Message(fullResponse);
    //
    //             
    //         }
    //
    //         if (attempts > 20)
    //         {
    //             
    //             yield break;
    //         }
    //
    //         if (!isValid)
    //         {
    //             Log.Error("Ответ невалиден для Cassie, повторяю запрос...");
    //         }
    //         
    //     }
    //
    //     // Если вышли из цикла по лимиту попыток, значит так и не нашли валидного ответа
    //     
    //     Log.Info("Все фразы: " + string.Join(", ", phrases));
    // }

    // private IEnumerator<float> PhrasesCoroutine()
    // {
    //     int SecondToWait = Random.Range(70, 210);
    //     if (phrases.Count >= 1)
    //     {
    //         int PhraseIndex = Random.Range(0, phrases.Count);
    //         Cassie.GlitchyMessage(phrases[PhraseIndex], 0.4f, 0.4f);
    //     }
    //     else
    //     {
    //         yield break;
    //     }
    //
    //     yield return Timing.WaitForSeconds(SecondToWait);
    // }


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
                Log.Info("Повышенный шанс на взрыв. Выпало - " + warheadActMegaChance + " Цель - 2");
                if (warheadActMegaChance == 2)
                {
                    _DeadManCoroutine = Timing.RunCoroutine(DeadManActivation());
                    Timing.KillCoroutines(_warheadCoroutine);
                }
            }

            else
            {
                Log.Info("Шанс на взрыв. Выпало - " + warheadActChance + " Цель - 2");
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