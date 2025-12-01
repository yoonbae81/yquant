using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor.Services;
using StackExchange.Redis;
using yQuant.App.Dashboard.Components;
using yQuant.App.Dashboard.Services;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Redis.Interfaces;
using yQuant.Infra.Reporting.Performance.Repositories;
using yQuant.Infra.Reporting.Performance.Services;

var builder = WebApplication.CreateBuilder(args);

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

// Register Infra RedisService
builder.Services.AddSingleton<IRedisService, yQuant.Infra.Redis.Services.RedisService>();

builder.Services.AddHostedService<SchedulerService>();
builder.Services.AddSingleton(sp => (SchedulerService)sp.GetServices<IHostedService>().First(s => s is SchedulerService));

builder.Services.AddSingleton<IPerformanceRepository, JsonPerformanceRepository>();

// Register Broker Adapter Factory and Order Publisher
builder.Services.AddSingleton<IBrokerAdapterFactory, RedisBrokerAdapterFactory>();
builder.Services.AddSingleton<OrderPublisher>();

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