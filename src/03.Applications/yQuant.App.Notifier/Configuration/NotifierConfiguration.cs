namespace yQuant.App.Notifier.Configuration;

/// <summary>
/// Notifier 전체 설정
/// </summary>
public class NotifierConfiguration
{
    public MessageRoutingConfiguration MessageRouting { get; set; } = new();
    public NotifierDiscordSettings Discord { get; set; } = new();
    public NotifierTelegramSettings Telegram { get; set; } = new();
    public PerformanceConfiguration Performance { get; set; } = new();
}

/// <summary>
/// 메시지 라우팅 설정
/// </summary>
public class MessageRoutingConfiguration
{
    public RoutingTargetConfiguration Orders { get; set; } = new();
    public RoutingTargetConfiguration Schedules { get; set; } = new();
    public RoutingTargetConfiguration Positions { get; set; } = new();
    public RoutingTargetConfiguration System { get; set; } = new();
}

/// <summary>
/// 라우팅 대상 설정
/// </summary>
public class RoutingTargetConfiguration
{
    public string[] Targets { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Telegram으로 전송할 메시지 타입 필터 (비어있으면 모두 전송)
    /// </summary>
    public string[] TelegramFilter { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Notifier Discord 설정 (간소화)
/// </summary>
public class NotifierDiscordSettings
{
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Notifier Telegram 설정 (간소화)
/// </summary>
public class NotifierTelegramSettings
{
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// 성능 설정
/// </summary>
public class PerformanceConfiguration
{
    public int BatchSize { get; set; } = 10;
    public int BatchDelayMs { get; set; } = 100;
    public int MaxQueueSize { get; set; } = 1000;
}
