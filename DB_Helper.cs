﻿using Exiled.API.Features;

namespace MaxunPlugin;
using MySql.Data.MySqlClient;
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
}