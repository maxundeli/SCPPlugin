using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.API.Enums;
using PlayerRoles;

namespace MaxunPlugin;

public class StatsService
{
    private readonly Dictionary<string, PlayerStats> _playerStats = new();
    private readonly Dictionary<string, PlayerLifeStats> _playerLifeStats = new();
    private readonly MyDatabaseHelper _dbHelper;

    public StatsService(MyDatabaseHelper dbHelper)
    {
        _dbHelper = dbHelper;
    }

    public void OnPlayerJoined(JoinedEventArgs ev)
    {
        string idStr = ev.Player.Id.ToString();
        _playerStats[idStr] = new PlayerStats();
        _playerLifeStats[idStr] = new PlayerLifeStats();
    }

    public void RegisterScpItem(int playerId)
    {
        string id = playerId.ToString();
        if (_playerStats.TryGetValue(id, out var stats))
            stats.takedSCPObjects++;
    }

    public void OnPlayerHurt(HurtEventArgs ev)
    {
        if (ev.Attacker.Id == ev.Player.Id)
            return;

        int damage = Convert.ToInt32(ev.Amount);
        string attackerId = ev.Attacker.Id.ToString();
        _playerStats[attackerId].DamageDealed += damage;
        _playerLifeStats[attackerId].DamageDealedLife += damage;
    }

    public void OnPlayerDied(DiedEventArgs ev)
    {
        string playerId = ev.Player.Id.ToString();
        ev.Player.Broadcast(7,
            $"За эту жизнь убито: <color=red>{_playerLifeStats[playerId].KillsLife}</color>, нанесено урона: <color=red>{_playerLifeStats[playerId].DamageDealedLife}</color>");
        _playerLifeStats[playerId].KillsLife = 0;
        _playerLifeStats[playerId].DamageDealedLife = 0;

        if (ev.Player.Id != ev.Attacker.Id)
        {
            string attackerId = ev.Attacker.Id.ToString();
            _playerStats[attackerId].Kills++;
            _playerLifeStats[attackerId].KillsLife++;

            if (IsScp(ev.Player.PreviousRole))
                _playerStats[attackerId].SCPsKilled++;

            if (IsFriendlyFire(ev))
            {
                if (_playerStats[attackerId].FFkills == -50)
                {
                    _playerStats[attackerId].FFkillsCount++;
                    foreach (var player in Player.List)
                        player.Broadcast(10,
                            $"<color=blue>{ev.Attacker.Nickname}</color> <color=white>- </color><color=red>ДОЛБАЕБ</color><color=white>, и убил уже </color><color=red>{_playerStats[attackerId].FFkillsCount}</color><color=white> союзников</color>",
                            Broadcast.BroadcastFlags.Normal, true);
                }
                else
                {
                    _playerStats[attackerId].FFkills++;
                    _playerStats[attackerId].FFkillsCount++;
                }
            }

            if (_playerStats[attackerId].FFkills >= 3)
            {
                _playerStats[attackerId].FFkills = -50;
                foreach (var player in Player.List)
                    player.Broadcast(10,
                        $"<color=blue>{ev.Attacker.Nickname}</color> <color=white>- </color><color=red>ДОЛБАЕБ</color><color=white>, и убил уже </color><color=red>{_playerStats[attackerId].FFkillsCount}</color><color=white> союзников</color>",
                        Broadcast.BroadcastFlags.Normal, true);
            }
        }
    }

    public void OnRoundEnd(RoundEndedEventArgs ev)
    {
        ev.TimeToRestart = 15;
        int maxKills = 0;
        string maxNickname = "никто";
        string word = "убийств";

        foreach (var player in Player.List)
        {
            string id = player.Id.ToString();
            int score = _playerStats[id].Kills - _playerStats[id].FFkillsCount;
            if (score > maxKills)
            {
                maxKills = score;
                maxNickname = player.Nickname;
            }
        }

        if (maxKills != 0)
            word = CalculateRightWord(maxKills);

        foreach (var player in Player.List)
        {
            string id = player.Id.ToString();
            string userId = player.UserId;
            int damage = _playerStats[id].DamageDealed;
            int kills = _playerStats[id].Kills;
            int ff = _playerStats[id].FFkillsCount;
            int scpObjects = _playerStats[id].takedSCPObjects;
            int scps = _playerStats[id].SCPsKilled;
            _dbHelper.UpdateStat(userId, kills, damage, Round.ElapsedTime, ff, scpObjects, scps);
            player.Broadcast(7,
                $"Вы убили <color=red>{kills}</color> человек, из них союзников - <color=red>{ff}</color>. Всего нанесено урона: <color=red>{damage}</color>\nСамый результативный игрок - <color=red>{maxNickname}</color> с <color=red>{maxKills} </color>{word}");
        }
    }

    private static bool IsScp(RoleTypeId role) => role is RoleTypeId.Scp049 or RoleTypeId.Scp079 or RoleTypeId.Scp096 or RoleTypeId.Scp106 or RoleTypeId.Scp173 or RoleTypeId.Scp939;

    private static bool IsFriendlyFire(DiedEventArgs ev)
    {
        return (ev.Attacker.Role.Side == Side.ChaosInsurgency && (ev.Player.PreviousRole == RoleTypeId.ChaosConscript ||
                ev.Player.PreviousRole == RoleTypeId.ChaosMarauder ||
                ev.Player.PreviousRole == RoleTypeId.ChaosRepressor ||
                ev.Player.PreviousRole == RoleTypeId.ChaosRifleman ||
                ev.Player.PreviousRole == RoleTypeId.ClassD)) ||
               (ev.Attacker.Role.Side == Side.Mtf &&
                (ev.Player.PreviousRole == RoleTypeId.NtfCaptain ||
                 ev.Player.PreviousRole == RoleTypeId.NtfPrivate ||
                 ev.Player.PreviousRole == RoleTypeId.NtfSergeant ||
                 ev.Player.PreviousRole == RoleTypeId.NtfSpecialist ||
                 ev.Player.PreviousRole == RoleTypeId.Scientist ||
                 ev.Player.PreviousRole == RoleTypeId.FacilityGuard));
    }

    private static string CalculateRightWord(int number) => number == 1 ? "убийством" : "убийствами";
}
