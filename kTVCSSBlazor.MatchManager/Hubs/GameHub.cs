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
    }
}
