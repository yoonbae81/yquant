using Microsoft.Extensions.Options;

namespace yQuant.App.Dashboard.Services;

public class SimpleAuthService
{
    private readonly UserAuthSettings _settings;
    private readonly ILogger<SimpleAuthService> _logger;
    private readonly yQuant.Core.Ports.Output.Infrastructure.ISystemLogger _securityLogger;

    public SimpleAuthService(
        IOptions<UserAuthSettings> settings,
        ILogger<SimpleAuthService> logger,
        yQuant.Core.Ports.Output.Infrastructure.ISystemLogger securityLogger)
    {
        _settings = settings.Value;
        _logger = logger;
        _securityLogger = securityLogger;
    }

    public async Task<bool> ValidatePinAsync(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            _logger.LogWarning("Login attempt with empty PIN");
            return false;
        }

        if (_settings.Pin == pin)
        {
            _logger.LogInformation("Successful login with PIN");
            await _securityLogger.LogSecurityAsync("LoginSuccess", "User logged in with PIN");
            return true;
        }

        _logger.LogWarning("Failed login attempt with incorrect PIN");
        await _securityLogger.LogSecurityAsync("LoginFailed", "Invalid PIN attempt");
        return false;
    }

    public string GetDefaultRole() => "Admin";
}

public class UserAuthSettings
{
    public string Pin { get; set; } = "0000";
    public int SessionTimeout { get; set; } = 480; // 8 hours default
}
