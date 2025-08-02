
using Dapper;
using kTVCSS.Models.Models;
using kTVCSSBlazor.MatchManager.Hubs;
using kTVCSSBlazor.MatchManager.Tools;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using Telegram.Bot;

namespace kTVCSSBlazor.MatchManager.Workers
{
    public class Selection(ILogger<Selection> logger, IConfiguration configuration, IHubContext<GameHub> hub) : BackgroundService
    {
        private ILogger<Selection> _logger { get; set; } = logger;
        private IConfiguration _configuration { get; set; } = configuration;
        private IHubContext<GameHub> _hub { get; set; } = hub;
        private Telegram.Bot.TelegramBotClient botClient;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            botClient = new Telegram.Bot.TelegramBotClient(_configuration.GetValue<string>("tg_alert_token"));

            while (!stoppingToken.IsCancellationRequested)
            {
                int delay = _configuration.GetValue<int>("selection_timer_delay");

                var players = GameHub.GetPlayers();

                if (players.Where(x => !x.Value.IsPlaying).Count() >= 10)
                {
                    await CreateMatchAsync(players.Where(x => !x.Value.IsPlaying).OrderBy(x => x.Value.StartSearchDateTime).Take(10).ToDictionary());
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogTrace("cts requested");
                    break;
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay));
                }
            }
        }

        private async Task CreateMatchAsync(Dictionary<string, User> players)
        {
            int serverId = await GetFreeServerId();

            if (serverId == -1)
            {
                await _hub.Clients.Users(players.Select(x => x.Key)).SendAsync("GetMixCreateResult", GameHubMixCreateResult.NoServersAvailable);

                return;
            }

            Mix mix = new();

            List<User> ATeam = [];
            List<User> BTeam = [];

            var mp = FiveOnFiveTeamCreation.CreateTeams(players.Values.ToList(), _configuration.GetValue<int>("player_default_mmr"));

            ATeam.AddRange(mp.Where(x => x.TeamID == "0"));
            BTeam.AddRange(mp.Where(x => x.TeamID == "1"));

            using (var db = new SqlConnection(_configuration.GetConnectionString("db")))
            {
                var lastMaps = db.Query<string>("SELECT TOP(6) MAP FROM Matches ORDER BY ID DESC");

                string map = db.QueryFirst<string>("SELECT TOP 1 MAP FROM [kTVCSS].[dbo].[MixesMaps] ORDER BY NEWID()");

                while (lastMaps.Contains(map))
                {
                    map = db.QueryFirst<string>("SELECT TOP 1 MAP FROM [kTVCSS].[dbo].[MixesMaps] ORDER BY NEWID()");
                }

                var address = db.QueryFirst<string>($"SELECT PUBLICADDRESS FROM GameServers WHERE ID = {serverId}");
                var port = db.QueryFirst<string>($"SELECT GAMEPORT FROM GameServers WHERE ID = {serverId}");

                mix = new()
                {
                    MapName = map,
                    MapImage = $"/images/mapsbackgrs/{map}.jpg",
                    ServerID = serverId,
                    ServerAddress = $"{address}:{port}",
                    MixPlayers = mp,
                    Guid = Guid.NewGuid(),
                    DtStart = DateTime.Now.AddMinutes(5)
                };

                db.Execute(
                    $"INSERT INTO Mixes VALUES ('{mix.Guid}', 0, {mix.ServerID}, '{mix.MapName}', '{DateTime.Now.AddMinutes(5).ToString("yyyy-MM-dd HH:mm:ss")}')");

                foreach (var player in ATeam)
                {
                    player.TeamID = "0";
                    using (SqlCommand query = new SqlCommand($"InsertMixMember", db))
                    {
                        query.CommandType = CommandType.StoredProcedure;
                        query.Parameters.AddWithValue("@GUID", mix.Guid);
                        query.Parameters.AddWithValue("@STEAMID", player.SteamId);
                        query.Parameters.AddWithValue("@TEAM", 0);
                        query.Parameters.AddWithValue("@CAPTAIN", 0);
                        query.ExecuteNonQuery();
                    }

                    db.Execute(
                        $"INSERT INTO MixesAllowedPlayers VALUES ('{player.SteamId}', {mix.ServerID}, '{mix.Guid}')");
                }

                foreach (var player in BTeam)
                {
                    player.TeamID = "1";
                    using (SqlCommand query = new SqlCommand($"InsertMixMember", db))
                    {
                        query.CommandType = CommandType.StoredProcedure;
                        query.Parameters.AddWithValue("@GUID", mix.Guid);
                        query.Parameters.AddWithValue("@STEAMID", player.SteamId);
                        query.Parameters.AddWithValue("@TEAM", 1);
                        query.Parameters.AddWithValue("@CAPTAIN", 0);
                        query.ExecuteNonQuery();
                    }

                    db.Execute(
                        $"INSERT INTO MixesAllowedPlayers VALUES ('{player.SteamId}', {mix.ServerID}, '{mix.Guid}')");
                }

                db.Execute($"UPDATE GameServers SET BUSY = 1 WHERE ID = {serverId}");
            }

            await _hub.Clients.Users(players.Select(x => x.Key)).SendAsync("MixCreated", mix.Guid.ToString());

            GameHub.Mixes.Add(mix);

            SendAlertsToTelegram(players.Values.Select(x => x.TelegramId).ToArray(), mix.Guid.ToString());

            foreach (var player in players)
            {
                GameHub.Players[player.Key].IsPlaying = true;
            }
        }

        private async Task SendAlertsToTelegram(string[] ids, string guid)
        {
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    try
                    {
                        await botClient.SendMessage(new Telegram.Bot.Types.ChatId(long.Parse(id)), $"Игра найдена!\r\nhttps://ktvcss.ru/mixroom/{guid}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.ToString());
                    }
                }
            }
        }

        private async Task<int> GetFreeServerId()
        {
            int id = -1;

            using (var db = new SqlConnection(_configuration.GetConnectionString("db")))
            {
                id = await db.QueryFirstOrDefaultAsync<int>($"SELECT TOP(1) ID FROM GameServers WHERE BUSY = 0 AND TYPE = 1 AND ENABLED = 1");
            }

            if (id == default) return -1;
            else return id;
        }
    }
}
