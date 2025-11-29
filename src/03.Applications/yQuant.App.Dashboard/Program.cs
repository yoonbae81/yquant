using Microsoft.AspNetCore.Components.Web;
using MudBlazor.Services;
using StackExchange.Redis;
using yQuant.App.Dashboard.Components;
using yQuant.App.Dashboard.Services;
using Microsoft.Extensions.Hosting; // For AddHostedService
using Microsoft.Extensions.Configuration; // For GetConnectionString
using Microsoft.Extensions.DependencyInjection; // For GetRequiredService
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
// Retrieve configuration values for KISClient
var configuration = builder.Configuration;
var userId = configuration["KIS:UserId"];
var appKey = configuration["KIS:AppKey"];
var appSecret = configuration["KIS:AppSecret"];
var baseUrl = configuration["KIS:BaseUrl"];
var accountNo = configuration["KIS:AccountNo"];

builder.Services.AddSingleton<IKisConnector>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(KisConnector));
    var logger = sp.GetRequiredService<ILogger<KisConnector>>();
    var redis = sp.GetService<IRedisService>();
    var apiConfig = KISApiConfig.Load(Path.Combine(AppContext.BaseDirectory, "API"));
    apiConfig.BaseUrl = baseUrl!;
    return new KisConnector(httpClient, logger, redis, "DashboardAccount", appKey!, appSecret!, apiConfig);
});

builder.Services.AddSingleton<KisBrokerAdapter>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<KisBrokerAdapter>>();
    var client = sp.GetRequiredService<IKisConnector>();
    var prefix = accountNo?.Length >= 8 ? accountNo.Substring(0, 8) : "00000000";
    return new KisBrokerAdapter(logger, client, prefix, "DashboardAccount");
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