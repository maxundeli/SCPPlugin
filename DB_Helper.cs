using Exiled.API.Features;
using MySql.Data.MySqlClient;

namespace MaxunPlugin;

public class MyDatabaseHelper
{
    private readonly string _connectionString;

    public MyDatabaseHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task TestConnectionAsync()
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = new MySqlCommand("SELECT VERSION();", conn);
        object? result = await cmd.ExecuteScalarAsync();
        Log.Info("MySQL version: " + result);
    }

    public async Task CreateRow(string id, string nickname)
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmdHuman = new MySqlCommand(
            "INSERT IGNORE INTO human_stats (ID,nickname,damage,kills,deaths,deaths_scp,deaths_human,scp_items,scps_killed,ff_kills,escapes,damage_to_scp,time_played,time_alive) " +
            "VALUES (@id,@n,0,0,0,0,0,0,0,0,0,0,'00:00:00','00:00:00');",
            conn);
        cmdHuman.Parameters.AddWithValue("@id", id);
        cmdHuman.Parameters.AddWithValue("@n", nickname);
        await cmdHuman.ExecuteNonQueryAsync();

        var cmdScp = new MySqlCommand(
            "INSERT IGNORE INTO scp_stats (ID,nickname,damage,kills,deaths,deaths_scp,deaths_human,damage_to_scp,time_played,time_alive) " +
            "VALUES (@id,@n,0,0,0,0,0,0,'00:00:00','00:00:00');",
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
            "UPDATE human_stats SET nickname=@n WHERE ID=@id; UPDATE scp_stats SET nickname=@n WHERE ID=@id;",
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
            @"UPDATE human_stats
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
                  time_alive = ADDTIME(time_alive,@ta)
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
            @"UPDATE scp_stats
              SET damage = damage + @dmg,
                  kills = kills + @k,
                  deaths = deaths + @de,
                  deaths_scp = deaths_scp + @dscp,
                  deaths_human = deaths_human + @dhum,
                  damage_to_scp = damage_to_scp + @dmgscp,
                  time_played = ADDTIME(time_played,@tp),
                  time_alive = ADDTIME(time_alive,@ta)
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
            "SELECT damage,kills,deaths,deaths_scp,deaths_human,scp_items,scps_killed,ff_kills,escapes,damage_to_scp,time_played,time_alive FROM human_stats WHERE ID=@id;",
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
        }
        return stats;
    }

    public async Task<ScpDbStats> GetScpStatsAsync(string id)
    {
        using var conn = new MySqlConnection(_connectionString);
        var stats = new ScpDbStats();
        await conn.OpenAsync();
        var cmd = new MySqlCommand(
            "SELECT damage,kills,deaths,deaths_scp,deaths_human,damage_to_scp,time_played,time_alive FROM scp_stats WHERE ID=@id;",
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
        int value = Convert.ToInt32(obj);
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
}
