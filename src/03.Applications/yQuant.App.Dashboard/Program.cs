using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor.Services;
using yQuant.App.Dashboard.Components;
using yQuant.App.Dashboard.Services;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Redis.Extensions;
using yQuant.Infra.Redis.Interfaces;
using yQuant.Infra.Reporting.Performance.Repositories;
using yQuant.Infra.Reporting.Performance.Services;


using yQuant.Core.Utils;

File.WriteAllText("startup_log.txt", "Starting application...\n");
try
{
    EnvValidator.Validate();
    File.AppendAllText("startup_log.txt", "EnvValidator passed.\n");
}
catch (Exception ex)
{
    File.AppendAllText("startup_log.txt", $"EnvValidator failed: {ex.Message}\n");
    throw;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration.AddJsonFile("sharedsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"sharedsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Register Redis Middleware
builder.Services.AddRedisMiddleware(builder.Configuration);

builder.Services.AddHostedService<SchedulerService>();
builder.Services.AddSingleton(sp => (SchedulerService)sp.GetServices<IHostedService>().First(s => s is SchedulerService));

builder.Services.AddSingleton<IPerformanceRepository, JsonPerformanceRepository>();

// Register Broker Adapter Factory and Order Publisher
builder.Services.AddSingleton<IBrokerAdapterFactory, RedisBrokerAdapterFactory>();
builder.Services.AddSingleton<OrderPublisher>();
builder.Services.AddSingleton<AssetService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();

app.UseStaticFiles(); // Add this line if not present for static files like CSS/JS
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();