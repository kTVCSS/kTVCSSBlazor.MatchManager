using kTVCSSBlazor.Db;
using kTVCSSBlazor.MatchManager.Hubs;
using kTVCSSBlazor.MatchManager.Services;
using kTVCSSBlazor.MatchManager.Workers;
using NLog;
using NLog.Web;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

builder.Host.UseNLog();

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
    });

builder.Services.AddSingleton<IRepository, kTVCSSRepository>();
builder.Services.AddDbContext<EFContext>(ServiceLifetime.Singleton);
builder.Services.AddDbContextFactory<EFContext>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("OnlyDevkTVCSS", builder =>
    {
        builder
            .WithOrigins("http://localhost:5001", "https://dev.ktvcss.ru", "https://ktvcss.ru")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHostedService<Selection>();

var app = builder.Build();

app.MapGet("/api/getmixbyguid", (string guid) =>
{
    return GameHub.Mixes.FirstOrDefault(x => x.Guid.ToString().Equals(guid, StringComparison.CurrentCultureIgnoreCase));
});

app.MapGet("/api/getplayerscount", () =>
{
    return GameHub.GetCurrentPlayersCount();
});

app.UseCors("OnlyDevkTVCSS");

app.UseDomainRestriction(["ktvcss.ru", "dev.ktvcss.ru", "localhost:5001"]);

app.UseStaticFiles(new StaticFileOptions() { ServeUnknownFileTypes = true, DefaultContentType = "application/octet-stream" });

app.Run();
