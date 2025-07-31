using kTVCSS.Models.Models;
using kTVCSSBlazor.Db;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace kTVCSSBlazor.MatchManager.Hubs
{
    public class GameHub(IConfiguration configuration, ILogger<GameHub> logger, IDbContextFactory<EFContext> factory, IRepository repo) : Hub
    {
        // Словарь с connectionId и данными игрока
        public static ConcurrentDictionary<string, User> Players = new ConcurrentDictionary<string, User>();
        public static int GetCurrentPlayersCount => Players.Count;

        private bool _isChecking = false;

        private IConfiguration _configuration { get; set; } = configuration;
        private ILogger<GameHub> _logger { get; set; } = logger;
        private IDbContextFactory<EFContext> _factory { get; set; } = factory;
        private IRepository _repo { get; set; } = repo;

        public async Task SendConnectResult(GameHubConnectResult result, User player)
        {
            if (player is not null)
            {
                _logger.LogInformation($"sended connect result {result.ToString()} to {player.Name}");
            }
            else
            {
                _logger.LogInformation($"sended connect result {result.ToString()} to unknown (no data)");
            }

            if (result == GameHubConnectResult.AlreadyPlaying)
            {
                // send room id or match id
            }

            await Clients.Caller.SendAsync("GetConnectResult", result);
        }

        public async Task OnAfterConnectAsync(User player)
        {
            string connectionId = Context.ConnectionId;

            if (player is not null)
            {
                if (Players.Any(x => x.Value.Id == player.Id))
                {
                    await SendConnectResult(GameHubConnectResult.AlreadyExists, player);
                }
                else
                {
                    if (await Tools.OnConnect.IsBannedAsync(player.Id, _configuration))
                    {
                        await SendConnectResult(GameHubConnectResult.IsBanned, player);
                        return;
                    }

                    if (await Tools.OnConnect.IsPlayerPlayingAsync(player.SteamId, _configuration))
                    {
                        await SendConnectResult(GameHubConnectResult.AlreadyPlaying, player);
                        return;
                    }

                    if (!await Tools.OnConnect.IsMixesEnabledAsync(_configuration))
                    {
                        await SendConnectResult(GameHubConnectResult.MixesDisabled, player);
                        return;
                    }

                    Players.AddOrUpdate(connectionId, player, (key, old) => player);

                    await SendConnectResult(GameHubConnectResult.Ok, player);
                }
            }
            else
            {
                await SendConnectResult(GameHubConnectResult.NoPlayerData, player);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string connectionId = Context.ConnectionId;

            if (exception is not null)
            {
                _logger.LogError(exception.ToString());
            }

            if (Players.ContainsKey(connectionId))
            {
                var result = Players.TryRemove(connectionId, out var player);

                if (result)
                {
                    _logger.LogInformation($"{player.Name} disconnected from hub");
                }
            }
        }
    }
}
