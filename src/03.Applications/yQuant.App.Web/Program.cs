using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor.Services;
using yQuant.App.Web.Components;
using yQuant.App.Web.Services;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Redis.Extensions;
using yQuant.Infra.Redis.Interfaces;
using yQuant.Infra.Reporting.Repositories;

using StackExchange.Redis;
using yQuant.Infra.Notification;
using yQuant.Infra.Notification.Discord;






// Find the correct content root by looking for wwwroot directory
var currentDir = Directory.GetCurrentDirectory();
var contentRoot = currentDir;
var webRoot = Path.Combine(currentDir, "wwwroot");

// If wwwroot doesn't exist in current directory, try to find project root
if (!Directory.Exists(webRoot))
{
    // We're likely in bin/Debug/net10.0, go up to find project root
    var projectRoot = currentDir;
    for (int i = 0; i < 5; i++)
    {
        projectRoot = Path.GetDirectoryName(projectRoot);
        if (projectRoot == null) break;

        var testWebRoot = Path.Combine(projectRoot, "wwwroot");
        if (Directory.Exists(testWebRoot))
        {
            contentRoot = projectRoot;
            webRoot = testWebRoot;
            break;
        }
    }
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot,
    WebRootPath = webRoot
});

builder.WebHost.UseStaticWebAssets();

// Load configuration files - try current directory first, then AppContext.BaseDirectory
var configPath = File.Exists(Path.Combine(currentDir, "appsettings.json"))
    ? currentDir
    : AppContext.BaseDirectory;

builder.Configuration.SetBasePath(configPath)
                     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: true);



// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Configure Authentication
builder.Services.Configure<UserAuthSettings>(builder.Configuration.GetSection("Web"));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(
            builder.Configuration.GetValue<int>("Web:SessionTimeout", 480));
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<SimpleAuthService>();

// Register Redis Middleware
builder.Services.AddRedisMiddleware(builder.Configuration);

// Register Notification Services (Redis based)
builder.Services.AddSingleton<NotificationPublisher>();
builder.Services.AddSingleton<ITradingLogger, RedisTradingLogger>();
builder.Services.AddSingleton<ISystemLogger, RedisSystemLogger>();

// Discord Direct Notification (for Startup/System status)
builder.AddDiscordDirectNotification();

// Register SchedulerService as Singleton (CRUD only, execution handled by OrderManager)
builder.Services.AddSingleton<SchedulerService>();

// Register Performance Repositories
builder.Services.AddSingleton<IPerformanceRepository, JsonPerformanceRepository>();
builder.Services.AddSingleton<ITradeRepository, yQuant.Infra.Reporting.Repositories.RedisTradeRepository>();
builder.Services.AddSingleton<IDailySnapshotRepository, yQuant.Infra.Reporting.Repositories.RedisDailySnapshotRepository>();

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
        return Results.Ok(new { Status = "Healthy", Redis = "Connected", Timestamp = DateTime.UtcNow, Service = "yQuant.App.Web" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Status = "Unhealthy", Redis = "Disconnected", Error = ex.Message }, statusCode: 503);
    }
});

// Authentication Endpoints
app.MapPost("/account/login", async (HttpContext context, SimpleAuthService authService) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(returnUrl)) returnUrl = "/";

    if (await authService.ValidateCredentialsAsync(username, password))
    {
        var role = authService.GetUserRole(username);
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, username),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role ?? "User")
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
        var errorUrl = $"/login?error=Invalid username or password&returnUrl={System.Net.WebUtility.UrlEncode(returnUrl)}";
        context.Response.Redirect(errorUrl);
    }
});

app.MapGet("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Redirect("/login");
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
    var appName = "App.Web";
    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    await systemLoggerService.LogStartupAsync(appName, version);
}

app.Run();