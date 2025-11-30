using Microsoft.AspNetCore.Components.Web;
using MudBlazor.Services;
using StackExchange.Redis;
using yQuant.App.Dashboard.Components;
using yQuant.App.Dashboard.Services;
using Microsoft.Extensions.Hosting; // For AddHostedService
using Microsoft.Extensions.Configuration; // For GetConnectionString
using Microsoft.Extensions.DependencyInjection; // For GetRequiredService
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Reporting.Performance.Interfaces;
using yQuant.Infra.Reporting.Performance.Repositories;
using yQuant.Infra.Reporting.Performance.Services;
using yQuant.Core.Services; // Added for AssetService
using yQuant.Infra.Broker.KIS; // Added for KIS components
using yQuant.Infra.Middleware.Redis.Interfaces; // Added for IRedisService
using Microsoft.Extensions.Logging; // Added for ILogger
using System.Linq; // Added for First()

var builder = WebApplication.CreateBuilder(args);

// Redis configuration removed as per user request
// var redisConn = Environment.GetEnvironmentVariable("Redis");
// if (!string.IsNullOrEmpty(redisConn))
// {
//     builder.Configuration.AddRedis(redisConn);
// }

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Register Redis Connection Multiplexer
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConn = Environment.GetEnvironmentVariable("Redis");
    if (string.IsNullOrEmpty(redisConn))
    {
        throw new InvalidOperationException("Redis connection string is missing. Please set 'Redis' environment variable.");
    }
    
    var options = ConfigurationOptions.Parse(redisConn);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});

// Add HttpClientFactory
builder.Services.AddHttpClient();

// Retrieve configuration values for KISClient
var configuration = builder.Configuration;
var appKey = configuration["KIS:AppKey"];
var appSecret = configuration["KIS:AppSecret"];
var accountNo = configuration["KIS:AccountNo"];

// Create Account object
var dashboardAccount = new Account
{
    Alias = "DashboardAccount",
    Number = accountNo!,
    Broker = "KIS",
    AppKey = appKey!,
    AppSecret = appSecret!,
    Deposits = new Dictionary<CurrencyType, decimal>(),
    Active = true
};

builder.Services.AddSingleton<IKISClient>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(KISClient));
    var logger = sp.GetRequiredService<ILogger<KISClient>>();
    var apiConfig = KISApiConfig.Load(Path.Combine(AppContext.BaseDirectory, "API"));
    return new KISClient(httpClient, logger, dashboardAccount, apiConfig);
});

builder.Services.AddSingleton<KISBrokerAdapter>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<KISBrokerAdapter>>();
    var client = sp.GetRequiredService<IKISClient>();
    return new KISBrokerAdapter(client, logger);
});

builder.Services.AddSingleton<yQuant.Core.Services.AssetService>();

// Register custom services
builder.Services.AddSingleton<OrderPublisher>();
// Register RedisService and SchedulerService both as IHostedService and as a regular service for direct injection
builder.Services.AddHostedService<RedisService>();
builder.Services.AddSingleton(sp => (RedisService)sp.GetServices<IHostedService>().First(s => s is RedisService));

// Register Infra RedisService
builder.Services.AddSingleton<IRedisService, yQuant.Infra.Middleware.Redis.Services.RedisService>();

builder.Services.AddHostedService<SchedulerService>();
builder.Services.AddSingleton(sp => (SchedulerService)sp.GetServices<IHostedService>().First(s => s is SchedulerService));

builder.Services.AddSingleton<IPerformanceRepository, JsonPerformanceRepository>();
builder.Services.AddSingleton<IQuantStatsService, QuantStatsService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseStaticFiles(); // Add this line if not present for static files like CSS/JS
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();