using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor.Services;
using yQuant.App.Dashboard.Components;
using yQuant.App.Dashboard.Services;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Valkey.Extensions;
using yQuant.Infra.Valkey.Interfaces;
using yQuant.Infra.Reporting.Repositories;
using yQuant.Infra.Valkey.Services;

using StackExchange.Redis;
using yQuant.Infra.Notification;
using yQuant.Infra.Notification.Discord;






// 🔍 Find the configuration directory (climb up to find appsettings.json)
var searchDir = Directory.GetCurrentDirectory();
string? configDir = null;

while (searchDir != null)
{
    if (File.Exists(Path.Combine(searchDir, "appsettings.json")))
    {
        configDir = searchDir;
        break;
    }
    searchDir = Path.GetDirectoryName(searchDir);
}

configDir ??= AppContext.BaseDirectory;

// 📁 Determine project and web root paths
var isDev = Directory.Exists(Path.Combine(configDir, "src", "03.Applications", "yQuant.App.Dashboard"));
var projectDir = isDev
    ? Path.Combine(configDir, "src", "03.Applications", "yQuant.App.Dashboard")
    : configDir;

var webRootPath = Path.Combine(projectDir, "wwwroot");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = projectDir,
    WebRootPath = webRootPath
});

builder.WebHost.UseStaticWebAssets();

// ⚙️ Load configuration files
builder.Configuration.SetBasePath(configDir)
                     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: true);



// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Configure Authentication
builder.Services.Configure<UserAuthSettings>(builder.Configuration.GetSection("Dashboard"));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/unlock";
        options.LogoutPath = "/lock";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(
            builder.Configuration.GetValue<int>("Dashboard:SessionTimeout", 480));
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.Cookie.Name = ".yQuant.Dashboard.Auth";
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<SimpleAuthService>();

// Register Valkey Middleware
builder.Services.AddValkeyMiddleware(builder.Configuration);

// Register Notification Services (Valkey based)
builder.Services.AddSingleton<NotificationPublisher>();
builder.Services.AddSingleton<ITradingLogger, ValkeyTradingLogger>();
builder.Services.AddSingleton<ISystemLogger, ValkeySystemLogger>();

// Discord Direct Notification (for Startup/System status)
builder.AddDiscordDirectNotification();


// Register Scheduled Order Repository
builder.Services.AddSingleton<IScheduledOrderRepository, ValkeyScheduledOrderRepository>();

// Register SchedulerService as Singleton (CRUD only, execution handled by OrderManager)
builder.Services.AddSingleton<SchedulerService>();

// Register Performance Repositories
builder.Services.AddSingleton<IPerformanceRepository, JsonPerformanceRepository>();
builder.Services.AddSingleton<ITradeRepository, yQuant.Infra.Reporting.Repositories.ValkeyTradeRepository>();
builder.Services.AddSingleton<IDailySnapshotRepository, yQuant.Infra.Reporting.Repositories.ValkeyDailySnapshotRepository>();

// Register Broker Adapter Factory and Order Publisher

builder.Services.AddSingleton<OrderPublisher>();
builder.Services.AddSingleton<AssetService>();
builder.Services.AddSingleton<LiquidateService>();
builder.Services.AddSingleton<AccountCacheService>();
builder.Services.AddSingleton<StockService>();
builder.Services.AddSingleton<SystemHealthService>();
builder.Services.AddSingleton<RealtimeEventService>();
builder.Services.AddHostedService<ExecutionListener>();

var app = builder.Build();

var pathBase = app.Configuration.GetValue<string>("Dashboard:PathBase");
if (!string.IsNullOrWhiteSpace(pathBase) && pathBase != "/")
{
    app.UsePathBase(pathBase);
}

Console.WriteLine($"[Dashboard] PathBase: {pathBase ?? "None"}");
Console.WriteLine($"[Dashboard] ContentRoot: {app.Environment.ContentRootPath}");
Console.WriteLine($"[Dashboard] WebRoot: {app.Environment.WebRootPath}");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    // app.UseHsts(); // Disabled for HAProxy TLS termination
}
app.UseStatusCodePagesWithReExecute("/not-found");
// app.UseHttpsRedirection(); // Disabled for HAProxy TLS termination

app.UseStaticFiles();

app.UseAntiforgery();

// Authentication & Authorization middleware (must be before MapRazorComponents)
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


// Health Check Endpoint
app.MapGet("/health", async (IConnectionMultiplexer redis) =>
{
    try
    {
        var db = redis.GetDatabase();
        await db.PingAsync();
        return Results.Ok(new { Status = "Healthy", Valkey = "Connected", Timestamp = DateTime.UtcNow, Service = "yQuant.App.Dashboard" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Status = "Unhealthy", Valkey = "Disconnected", Error = ex.Message }, statusCode: 503);
    }
});

// Authentication Endpoints
app.MapPost("/account/unlock", async (HttpContext context, SimpleAuthService authService, IConfiguration config) =>
{
    var form = await context.Request.ReadFormAsync();
    var pin = form["pin"].ToString();
    var returnUrl = form["returnUrl"].ToString();
    var reqPathBase = context.Request.PathBase.Value ?? "";
    var configPathBase = config.GetValue<string>("Dashboard:PathBase")?.TrimEnd('/') ?? "";
    var activePathBase = !string.IsNullOrWhiteSpace(reqPathBase) ? reqPathBase : configPathBase;

    // Normalize returnUrl - if it's empty, root, or matches the pathbase exactly, go to pathbase/
    if (string.IsNullOrWhiteSpace(returnUrl) ||
        returnUrl == "/" ||
        returnUrl == activePathBase ||
        returnUrl.Equals(activePathBase + "/", StringComparison.OrdinalIgnoreCase))
    {
        returnUrl = !string.IsNullOrWhiteSpace(activePathBase) ? $"{activePathBase}/" : "/";
    }

    // Ensure returnUrl starts with PathBase if configured and doesn't already have it
    if (!string.IsNullOrWhiteSpace(activePathBase) &&
        returnUrl.StartsWith("/") &&
        !returnUrl.StartsWith(activePathBase, StringComparison.OrdinalIgnoreCase))
    {
        returnUrl = activePathBase + (returnUrl.StartsWith("/") ? returnUrl : "/" + returnUrl);
    }

    if (await authService.ValidatePinAsync(pin))
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Admin"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
        };

        var claimsIdentity = new System.Security.Claims.ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(480)
        };

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new System.Security.Claims.ClaimsPrincipal(claimsIdentity),
            authProperties);

        context.Response.Redirect(returnUrl);
    }
    else
    {
        activePathBase = !string.IsNullOrWhiteSpace(context.Request.PathBase.Value)
            ? context.Request.PathBase.Value
            : config.GetValue<string>("Dashboard:PathBase")?.TrimEnd('/') ?? "";

        var errorBase = string.IsNullOrWhiteSpace(activePathBase) ? "/unlock" : $"{activePathBase}/unlock";
        var errorUrl = $"{errorBase}?error=Invalid PIN&returnUrl={System.Net.WebUtility.UrlEncode(returnUrl)}";
        context.Response.Redirect(errorUrl);
    }
})
.DisableAntiforgery();

app.MapGet("/account/lock", async (HttpContext context, IConfiguration config) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    var reqPathBase = context.Request.PathBase.Value ?? "";
    var configPathBase = config.GetValue<string>("Dashboard:PathBase")?.TrimEnd('/') ?? "";
    var activePathBase = !string.IsNullOrWhiteSpace(reqPathBase) ? reqPathBase : configPathBase;

    context.Response.Redirect(string.IsNullOrWhiteSpace(activePathBase) ? "/unlock" : $"{activePathBase}/unlock");
});

// Notify systemd that the service is ready (Linux only)
if (OperatingSystem.IsLinux())
{
    try
    {
        // systemd expects READY=1 notification via NOTIFY_SOCKET
        var notifySocket = Environment.GetEnvironmentVariable("NOTIFY_SOCKET");
        if (!string.IsNullOrEmpty(notifySocket))
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.Unix,
                System.Net.Sockets.SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Unspecified);

            var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint(notifySocket);
            socket.Connect(endpoint);

            var message = System.Text.Encoding.UTF8.GetBytes("READY=1");
            socket.Send(message);

            app.Logger.LogInformation("Notified systemd that service is ready");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to notify systemd");
    }
}

// Direct Discord Startup Notification
var systemLoggerService = app.Services.GetService<ISystemLogger>();
if (systemLoggerService != null)
{
    var appName = "Dashboard";
    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    await systemLoggerService.LogStartupAsync(appName, version);
}

app.Run();