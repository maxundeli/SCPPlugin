using Exiled.API.Features;

namespace MaxunPlugin;
using MySql.Data.MySqlClient;
public class MyDatabaseHelper
{
    private readonly string connectionString;

    public MyDatabaseHelper()
    {
        // Собери Connection String
        connectionString = "Server=localhost;" +
                           "Database=scp;" +
                           "User ID=maxundeli;" +
                           "Password=Maxx1826583ru;" +
                           "Pooling=true;";
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
        using (var conn = new MySqlConnection(connectionString))
        {
            try
            {
                await conn.OpenAsync();
                var cmd1 = new MySqlCommand(
                    "SELECT kills FROM scp_stat WHERE ID = @userId;",
                    conn
                );
                cmd1.Parameters.AddWithValue("@userId", id);
                object result = await cmd1.ExecuteScalarAsync();
                int killsTemp = result != null ? Convert.ToInt32(result) : 0;


                var cmd3 = new MySqlCommand(
                    "SELECT damageDealed FROM scp_stat WHERE ID = @userId;",
                    conn
                );
                cmd3.Parameters.AddWithValue("@userId", id);
                object result2 = await cmd3.ExecuteScalarAsync();
                int damageTemp = result2 != null ? Convert.ToInt32(result2) : 0;
                int damageFinal = damageTemp + damageDealed;
                int killsFinal = kills + killsTemp;


                var cmd2 = new MySqlCommand(
                    "UPDATE scp_stat SET kills = @killsFinal WHERE ID = @userId;"
                    ,
                    conn
                );
                cmd2.Parameters.AddWithValue("@userId", id);
                cmd2.Parameters.AddWithValue("@killsFinal", killsFinal);
                await cmd2.ExecuteNonQueryAsync();


                var cmd4 = new MySqlCommand(
                    "UPDATE scp_stat SET damageDealed = @damageFinal WHERE ID = @userId;"
                    ,
                    conn
                );
                cmd4.Parameters.AddWithValue("@userId", id);
                cmd4.Parameters.AddWithValue("@damageFinal", damageFinal);
                await cmd4.ExecuteNonQueryAsync();


                var cmd5 = new MySqlCommand(
                    "SELECT timePlayed FROM scp_stat WHERE ID = @userId;"
                    ,
                    conn
                );
                cmd5.Parameters.AddWithValue("@userId", id);
                object result3 = await cmd5.ExecuteScalarAsync();
                string timePlayedTemp = result3 != null ? Convert.ToString(result3) : "00:00:00";
                var timePlayedTempSpan = TimeSpan.Parse(timePlayedTemp);
                Log.Info("Time played: " + timePlayedTempSpan);
                await cmd5.ExecuteNonQueryAsync();
                var timePlayedFinal = timePlayedTempSpan + timeplayed;

                var cmd6 = new MySqlCommand(
                    "UPDATE scp_stat SET timePlayed = @timePlayed WHERE ID = @userId;"
                    ,
                    conn
                );
                cmd6.Parameters.AddWithValue("@userId", id);
                cmd6.Parameters.AddWithValue("@timePlayed", Convert.ToString(timePlayedFinal));
                await cmd6.ExecuteNonQueryAsync();

                var cmd7 = new MySqlCommand(
                    "UPDATE scp_stat SET FFkills = @FFkillsCount + FFkills WHERE ID = @userId;"
                    ,
                    conn
                );
                cmd7.Parameters.AddWithValue("@userId", id);
                cmd7.Parameters.AddWithValue("@FFkillsCount", FFkillsCount);
                await cmd7.ExecuteNonQueryAsync();

                var cmd8 = new MySqlCommand(
                    "UPDATE scp_stat SET takedSCPObjects = @takedSCPObjects + takedSCPObjects WHERE ID = @userId;"
                    ,
                    conn
                );
                cmd8.Parameters.AddWithValue("@userId", id);
                cmd8.Parameters.AddWithValue("@takedSCPObjects", takedSCPObjects);
                await cmd8.ExecuteNonQueryAsync();

                var cmd9 = new MySqlCommand(
                    "UPDATE scp_stat SET SCPsKilled = @SCPsKilled + SCPsKilled WHERE ID = @userId;"
                    ,
                    conn
                );
                cmd9.Parameters.AddWithValue("@userId", id);
                cmd9.Parameters.AddWithValue("@SCPsKilled", SCPsKilled);
                await cmd9.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }
    }
}