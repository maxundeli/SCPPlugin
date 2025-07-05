using Exiled.API.Features;
using MySql.Data.MySqlClient;
using System;

namespace MaxunPlugin;
public class MyDatabaseHelper
{
    private readonly string connectionString;

    public MyDatabaseHelper(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task TestConnectionAsync()
    {
        using (var conn = new MySqlConnection(connectionString))
        {
            await conn.OpenAsync();
            // Выполняем запрос для получения версии MySQL
            var cmd = new MySqlCommand("SELECT VERSION();", conn);
            object? result = await cmd.ExecuteScalarAsync();
            string version = result?.ToString();
            Log.Info("MySQL version: " + version);
        }
    }

    public async Task CreateRow(string id, string nickname)
    {
        using (var conn = new MySqlConnection(connectionString))
        {
            try
            {
                await conn.OpenAsync();
                var cmd = new MySqlCommand(
                    "INSERT IGNORE INTO `scp_stat` (`ID`, `nickname`, `kills`, `damageDealed`, `timePlayed`, `FFkills`, `takedSCPObjects`, `SCPsKilled`) " +
                    "VALUES (@id, @nickname, 0, 0, '00:00:00', 0, 0, 0);",
                    conn
                );
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@nickname", nickname);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }
    }

    public async Task UpdateStat(string id, int kills, int damageDealed, TimeSpan timeplayed, int FFkillsCount,
        int takedSCPObjects, int SCPsKilled)
    {
        using var conn = new MySqlConnection(connectionString);
        try
        {
            await conn.OpenAsync();

            var cmd = new MySqlCommand(
                @"UPDATE scp_stat
                  SET kills = kills + @kills,
                      damageDealed = damageDealed + @damageDealed,
                      timePlayed = ADDTIME(timePlayed, @timePlayed),
                      FFkills = FFkills + @FFkillsCount,
                      takedSCPObjects = takedSCPObjects + @takedSCPObjects,
                      SCPsKilled = SCPsKilled + @SCPsKilled
                  WHERE ID = @userId;",
                conn
            );

            cmd.Parameters.AddWithValue("@kills", kills);
            cmd.Parameters.AddWithValue("@damageDealed", damageDealed);
            cmd.Parameters.AddWithValue("@timePlayed", timeplayed);
            cmd.Parameters.AddWithValue("@FFkillsCount", FFkillsCount);
            cmd.Parameters.AddWithValue("@takedSCPObjects", takedSCPObjects);
            cmd.Parameters.AddWithValue("@SCPsKilled", SCPsKilled);
            cmd.Parameters.AddWithValue("@userId", id);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            Log.Error(e);
            throw;
        }
    }

    public async Task<StoredPlayerStats?> GetStatsAsync(string id)
    {
        using var conn = new MySqlConnection(connectionString);
        try
        {
            await conn.OpenAsync();
            var cmd = new MySqlCommand(
                "SELECT kills, damageDealed, timePlayed, FFkills, takedSCPObjects, SCPsKilled FROM scp_stat WHERE ID = @id;",
                conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new StoredPlayerStats
                {
                    Kills = Convert.ToInt32(reader["kills"]),
                    DamageDealed = Convert.ToInt32(reader["damageDealed"]),
                    TimePlayed = reader["timePlayed"].ToString() ?? "0:00:00",
                    FFkills = Convert.ToInt32(reader["FFkills"]),
                    TakedSCPObjects = Convert.ToInt32(reader["takedSCPObjects"]),
                    SCPsKilled = Convert.ToInt32(reader["SCPsKilled"])
                };
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            throw;
        }

        return null;
    }
}

public class StoredPlayerStats
{
    public int Kills { get; set; }
    public int DamageDealed { get; set; }
    public string TimePlayed { get; set; } = string.Empty;
    public int FFkills { get; set; }
    public int TakedSCPObjects { get; set; }
    public int SCPsKilled { get; set; }
}
