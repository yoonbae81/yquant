using Microsoft.Extensions.Options;

namespace yQuant.App.Web.Services;

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

    public bool ValidateCredentials(string username, string password)
    {
        // For synchronous compatibility, if needed by Blazor (though we'll update it to async)
        return ValidateCredentialsAsync(username, password).GetAwaiter().GetResult();
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Login attempt with empty username or password");
            return false;
        }

        if (!_settings.Users.TryGetValue(username, out var user))
        {
            _logger.LogWarning("Login attempt for non-existent user: {Username}", username);
            await _securityLogger.LogSecurityAsync("LoginFailed", $"User not found: {username}");
            return false;
        }

        try
        {
            var isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

            if (isValid)
            {
                _logger.LogInformation("Successful login for user: {Username}", username);
                await _securityLogger.LogSecurityAsync("LoginSuccess", $"User logged in: {username}");
            }
            else
            {
                _logger.LogWarning("Failed login attempt for user: {Username}", username);
                await _securityLogger.LogSecurityAsync("LoginFailed", $"Invalid password for user: {username}");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credentials for user: {Username}", username);
            await _securityLogger.LogSecurityAsync("LoginError", $"Error validating credentials for {username}: {ex.Message}");
            return false;
        }
    }

    public string? GetUserRole(string username)
    {
        if (_settings.Users.TryGetValue(username, out var user))
        {
            return user.Role;
        }

        return "User";
    }
}

public class UserAuthSettings
{
    public Dictionary<string, UserConfig> Users { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int SessionTimeout { get; set; } = 480; // 8 hours default
}

public class UserConfig
{
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}
