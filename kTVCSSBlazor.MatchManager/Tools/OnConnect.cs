using Dapper;
using kTVCSSBlazor.Db.Models.Highlights;
using Microsoft.Data.SqlClient;

namespace kTVCSSBlazor.MatchManager.Tools
{
    public class OnConnect
    {
        public static async Task<bool> IsBannedAsync(int id, IConfiguration configuration)
        {
            int result = 0;

            using (var db = new SqlConnection(configuration.GetConnectionString("db")))
            {
                result = await db.QueryFirstOrDefaultAsync<int>($"SELECT BLOCK FROM Players WHERE ID = {id}");
            }

            return result == 1;
        }

        public static async Task<bool> IsPlayerPlayingAsync(string steam, IConfiguration configuration)
        {
            string result = "";

            using (var db = new SqlConnection(configuration.GetConnectionString("db")))
            {
                result = await db.QueryFirstOrDefaultAsync<string>($"SELECT STEAMID FROM MixesAllowedPlayers WHERE STEAMID = '{steam}'");
            }

            return !string.IsNullOrEmpty(result);
        }

        public static async Task<bool> IsMixesEnabledAsync(IConfiguration configuration)
        {
            using (var db = new SqlConnection(configuration.GetConnectionString("db")))
            {
                var testDb =
                    await db.QueryFirstOrDefaultAsync<int>($"SELECT ParamValue FROM Settings WHERE ParamName = 'MixesEnabled'");

                if (testDb == 0)
                {
                    var reason = 
                        await db.QueryFirstOrDefaultAsync<string>(
                            $"SELECT ParamDescription FROM Settings WHERE ParamName = 'MixesEnabled'");

                    return false;
                }
            }

            return true;
        }
    }
}
