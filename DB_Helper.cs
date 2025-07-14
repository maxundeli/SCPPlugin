using Exiled.API.Features;
using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;

namespace MaxunPlugin;

public class MyDatabaseHelper
{
    private readonly string _connectionString;
    private readonly string _humanTable;
    private readonly string _scpTable;

    public MyDatabaseHelper(string connectionString, string humanTable, string scpTable)
    {
        _connectionString = connectionString;
        _humanTable = humanTable;
        _scpTable = scpTable;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await TestConnectionAsync();
            await EnsureTablesAsync();
        }
        catch (Exception ex)
        {
            Log.Error($"Database initialization failed: {ex.Message}");
            Log.Debug(ex.StackTrace);
        }
    }

    public async Task TestConnectionAsync()
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = new MySqlCommand("SELECT VERSION();", conn);
        object? result = await cmd.ExecuteScalarAsync();
        Log.Info("MySQL version: " + result);
    }

    public async Task EnsureTablesAsync()
    {
        Log.Info("Opening database connection...");
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        Log.Info("Connection opened. Ensuring tables...");

        var humanCmd = new MySqlCommand($@"CREATE TABLE IF NOT EXISTS `{_humanTable}` (
            ID VARCHAR(64) NOT NULL PRIMARY KEY,
            nickname VARCHAR(32) NOT NULL,
            damage INT NOT NULL DEFAULT 0,
                kills INT NOT NULL DEFAULT 0,
                deaths INT NOT NULL DEFAULT 0,
                deaths_scp INT NOT NULL DEFAULT 0,
                deaths_human INT NOT NULL DEFAULT 0,
                scp_items INT NOT NULL DEFAULT 0,
                scps_killed INT NOT NULL DEFAULT 0,
                ff_kills INT NOT NULL DEFAULT 0,
                escapes INT NOT NULL DEFAULT 0,
                damage_to_scp INT NOT NULL DEFAULT 0,
                time_played TIME NOT NULL DEFAULT '00:00:00',
                time_alive TIME NOT NULL DEFAULT '00:00:00',
                damage_10m DOUBLE NOT NULL DEFAULT 0,
                kills_10m DOUBLE NOT NULL DEFAULT 0,
                ff_kills_10m DOUBLE NOT NULL DEFAULT 0,
                deaths_10m DOUBLE NOT NULL DEFAULT 0
            );", conn);
        await humanCmd.ExecuteNonQueryAsync();
        Log.Info($"Ensured table '{_humanTable}'");

        var scpCmd = new MySqlCommand($@"CREATE TABLE IF NOT EXISTS `{_scpTable}` (
            ID VARCHAR(64) NOT NULL PRIMARY KEY,
            nickname VARCHAR(32) NOT NULL,
            damage INT NOT NULL DEFAULT 0,
                kills INT NOT NULL DEFAULT 0,
                deaths INT NOT NULL DEFAULT 0,
                deaths_scp INT NOT NULL DEFAULT 0,
                deaths_human INT NOT NULL DEFAULT 0,
                damage_to_scp INT NOT NULL DEFAULT 0,
                time_played TIME NOT NULL DEFAULT '00:00:00',
                time_alive TIME NOT NULL DEFAULT '00:00:00',
                damage_10m DOUBLE NOT NULL DEFAULT 0,
                kills_10m DOUBLE NOT NULL DEFAULT 0,
                deaths_10m DOUBLE NOT NULL DEFAULT 0
            );", conn);
        await scpCmd.ExecuteNonQueryAsync();
        Log.Info($"Ensured table '{_scpTable}'");
    }

    public async Task CreateRow(string id, string nickname)
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmdHuman = new MySqlCommand(
            $"INSERT IGNORE INTO `{_humanTable}` (ID,nickname,damage,kills,deaths,deaths_scp,deaths_human,scp_items,scps_killed,ff_kills,escapes,damage_to_scp,time_played,time_alive,damage_10m,kills_10m,ff_kills_10m,deaths_10m) " +
            "VALUES (@id,@n,0,0,0,0,0,0,0,0,0,0,'00:00:00','00:00:00',0,0,0,0);",
            conn);
        cmdHuman.Parameters.AddWithValue("@id", id);
        cmdHuman.Parameters.AddWithValue("@n", nickname);
        await cmdHuman.ExecuteNonQueryAsync();

        var cmdScp = new MySqlCommand(
            $"INSERT IGNORE INTO `{_scpTable}` (ID,nickname,damage,kills,deaths,deaths_scp,deaths_human,damage_to_scp,time_played,time_alive,damage_10m,kills_10m,deaths_10m) " +
            "VALUES (@id,@n,0,0,0,0,0,0,'00:00:00','00:00:00',0,0,0);",
            conn);
        cmdScp.Parameters.AddWithValue("@id", id);
        cmdScp.Parameters.AddWithValue("@n", nickname);
        await cmdScp.ExecuteNonQueryAsync();
    }

    public async Task UpdateNickname(string id, string nickname)
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = new MySqlCommand(
            $"UPDATE `{_humanTable}` SET nickname=@n WHERE ID=@id; UPDATE `{_scpTable}` SET nickname=@n WHERE ID=@id;",
            conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@n", nickname);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateHumanStats(string id, HumanDbStats s)
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = new MySqlCommand(
            @$"UPDATE `{_humanTable}`
              SET damage = damage + @dmg,
                  kills = kills + @k,
                  deaths = deaths + @de,
                  deaths_scp = deaths_scp + @dscp,
                  deaths_human = deaths_human + @dhum,
                  scp_items = scp_items + @scpitems,
                  scps_killed = scps_killed + @scpk,
                  ff_kills = ff_kills + @ff,
                  escapes = escapes + @esc,
                  damage_to_scp = damage_to_scp + @dmgscp,
                  time_played = ADDTIME(time_played,@tp),
                  time_alive = ADDTIME(time_alive,@ta),
                  damage_10m = (damage + @dmg) / (GREATEST(TIME_TO_SEC(ADDTIME(time_played,@tp)),1)/600),
                  kills_10m = (kills + @k) / (GREATEST(TIME_TO_SEC(ADDTIME(time_played,@tp)),1)/600),
                  ff_kills_10m = (ff_kills + @ff) / (GREATEST(TIME_TO_SEC(ADDTIME(time_played,@tp)),1)/600),
                  deaths_10m = (deaths + @de) / (GREATEST(TIME_TO_SEC(ADDTIME(time_played,@tp)),1)/600)
              WHERE ID=@id;",
            conn);
        cmd.Parameters.AddWithValue("@dmg", s.Damage);
        cmd.Parameters.AddWithValue("@k", s.Kills);
        cmd.Parameters.AddWithValue("@de", s.Deaths);
        cmd.Parameters.AddWithValue("@dscp", s.DeathsFromScp);
        cmd.Parameters.AddWithValue("@dhum", s.DeathsFromHuman);
        cmd.Parameters.AddWithValue("@scpitems", s.ScpItems);
        cmd.Parameters.AddWithValue("@scpk", s.ScpsKilled);
        cmd.Parameters.AddWithValue("@ff", s.FFKills);
        cmd.Parameters.AddWithValue("@esc", s.Escapes);
        cmd.Parameters.AddWithValue("@dmgscp", s.DamageToScp);
        cmd.Parameters.AddWithValue("@tp", s.TimePlayed);
        cmd.Parameters.AddWithValue("@ta", s.TimeAlive);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateScpStats(string id, ScpDbStats s)
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = new MySqlCommand(
            @$"UPDATE `{_scpTable}`
              SET damage = damage + @dmg,
                  kills = kills + @k,
                  deaths = deaths + @de,
                  deaths_scp = deaths_scp + @dscp,
                  deaths_human = deaths_human + @dhum,
                  damage_to_scp = damage_to_scp + @dmgscp,
                  time_played = ADDTIME(time_played,@tp),
                  time_alive = ADDTIME(time_alive,@ta),
                  damage_10m = (damage + @dmg) / (GREATEST(TIME_TO_SEC(ADDTIME(time_played,@tp)),1)/600),
                  kills_10m = (kills + @k) / (GREATEST(TIME_TO_SEC(ADDTIME(time_played,@tp)),1)/600),
                  deaths_10m = (deaths + @de) / (GREATEST(TIME_TO_SEC(ADDTIME(time_played,@tp)),1)/600)
              WHERE ID=@id;",
            conn);
        cmd.Parameters.AddWithValue("@dmg", s.Damage);
        cmd.Parameters.AddWithValue("@k", s.Kills);
        cmd.Parameters.AddWithValue("@de", s.Deaths);
        cmd.Parameters.AddWithValue("@dscp", s.DeathsFromScp);
        cmd.Parameters.AddWithValue("@dhum", s.DeathsFromHuman);
        cmd.Parameters.AddWithValue("@dmgscp", s.DamageToScp);
        cmd.Parameters.AddWithValue("@tp", s.TimePlayed);
        cmd.Parameters.AddWithValue("@ta", s.TimeAlive);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<HumanDbStats> GetHumanStatsAsync(string id)
    {
        using var conn = new MySqlConnection(_connectionString);
        var stats = new HumanDbStats();
        await conn.OpenAsync();
        var cmd = new MySqlCommand(
            $"SELECT damage,kills,deaths,deaths_scp,deaths_human,scp_items,scps_killed,ff_kills,escapes,damage_to_scp,time_played,time_alive,damage_10m,kills_10m,ff_kills_10m,deaths_10m FROM `{_humanTable}` WHERE ID=@id;",
            conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            stats.Damage = reader.GetInt32(0);
            stats.Kills = reader.GetInt32(1);
            stats.Deaths = reader.GetInt32(2);
            stats.DeathsFromScp = reader.GetInt32(3);
            stats.DeathsFromHuman = reader.GetInt32(4);
            stats.ScpItems = reader.GetInt32(5);
            stats.ScpsKilled = reader.GetInt32(6);
            stats.FFKills = reader.GetInt32(7);
            stats.Escapes = reader.GetInt32(8);
            stats.DamageToScp = reader.GetInt32(9);
            stats.TimePlayed = reader.GetTimeSpan(10);
            stats.TimeAlive = reader.GetTimeSpan(11);
            stats.DamagePerTen = reader.GetDouble(12);
            stats.KillsPerTen = reader.GetDouble(13);
            stats.FFKillsPerTen = reader.GetDouble(14);
            stats.DeathsPerTen = reader.GetDouble(15);
        }
        return stats;
    }

    public async Task<ScpDbStats> GetScpStatsAsync(string id)
    {
        using var conn = new MySqlConnection(_connectionString);
        var stats = new ScpDbStats();
        await conn.OpenAsync();
        var cmd = new MySqlCommand(
            $"SELECT damage,kills,deaths,deaths_scp,deaths_human,damage_to_scp,time_played,time_alive,damage_10m,kills_10m,deaths_10m FROM `{_scpTable}` WHERE ID=@id;",
            conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            stats.Damage = reader.GetInt32(0);
            stats.Kills = reader.GetInt32(1);
            stats.Deaths = reader.GetInt32(2);
            stats.DeathsFromScp = reader.GetInt32(3);
            stats.DeathsFromHuman = reader.GetInt32(4);
            stats.DamageToScp = reader.GetInt32(5);
            stats.TimePlayed = reader.GetTimeSpan(6);
            stats.TimeAlive = reader.GetTimeSpan(7);
            stats.DamagePerTen = reader.GetDouble(8);
            stats.KillsPerTen = reader.GetDouble(9);
            stats.DeathsPerTen = reader.GetDouble(10);
        }
        return stats;
    }

    public async Task<int?> GetStatRankAsync(string id, string column, string table)
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        var valCmd = new MySqlCommand($"SELECT `{column}` FROM {table} WHERE ID=@id;", conn);
        valCmd.Parameters.AddWithValue("@id", id);
        object? obj = await valCmd.ExecuteScalarAsync();
        if (obj == null || obj == DBNull.Value)
            return null;
        double value = Convert.ToDouble(obj);
        var rankCmd = new MySqlCommand($"SELECT COUNT(*) + 1 FROM {table} WHERE `{column}` > @v;", conn);
        rankCmd.Parameters.AddWithValue("@v", value);
        object? rankObj = await rankCmd.ExecuteScalarAsync();
        return Convert.ToInt32(rankObj);
    }
}

public class HumanDbStats
{
    public int Damage;
    public int Kills;
    public int Deaths;
    public int DeathsFromScp;
    public int DeathsFromHuman;
    public int ScpItems;
    public int ScpsKilled;
    public int FFKills;
    public int Escapes;
    public int DamageToScp;
    public TimeSpan TimePlayed;
    public TimeSpan TimeAlive;
    public double DamagePerTen;
    public double KillsPerTen;
    public double FFKillsPerTen;
    public double DeathsPerTen;

    public HumanDbStats() { }

    public HumanDbStats(Plugin.RoleStats r)
    {
        Damage = (int)Math.Round(r.Damage);
        Kills = r.Kills;
        Deaths = r.Deaths;
        DeathsFromScp = r.DeathsFromScp;
        DeathsFromHuman = r.DeathsFromHuman;
        ScpItems = r.ScpItems;
        ScpsKilled = r.ScpsKilled;
        FFKills = r.FFKills;
        Escapes = r.Escapes;
        DamageToScp = (int)Math.Round(r.DamageToScp);
        TimePlayed = r.TimePlayed;
        TimeAlive = r.TimeAlive;
        DamagePerTen = 0;
        KillsPerTen = 0;
        FFKillsPerTen = 0;
        DeathsPerTen = 0;
    }
}

public class ScpDbStats
{
    public int Damage;
    public int Kills;
    public int Deaths;
    public int DeathsFromScp;
    public int DeathsFromHuman;
    public int DamageToScp;
    public TimeSpan TimePlayed;
    public TimeSpan TimeAlive;
    public double DamagePerTen;
    public double KillsPerTen;
    public double DeathsPerTen;

    public ScpDbStats() { }

    public ScpDbStats(Plugin.RoleStats r)
    {
        Damage = (int)Math.Round(r.Damage);
        Kills = r.Kills;
        Deaths = r.Deaths;
        DeathsFromScp = r.DeathsFromScp;
        DeathsFromHuman = r.DeathsFromHuman;
        DamageToScp = (int)Math.Round(r.DamageToScp);
        TimePlayed = r.TimePlayed;
        TimeAlive = r.TimeAlive;
        DamagePerTen = 0;
        KillsPerTen = 0;
        DeathsPerTen = 0;
    }
}

