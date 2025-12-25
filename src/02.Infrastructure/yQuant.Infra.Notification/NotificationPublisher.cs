using System.Text.Json;
using StackExchange.Redis;

namespace yQuant.Infra.Notification;

/// <summary>
/// Redis Pub/Sub을 통해 알림 메시지를 발행하는 헬퍼 클래스
/// 다른 애플리케이션에서 쉽게 알림을 발행할 수 있도록 지원
/// </summary>
public class NotificationPublisher
{
    private readonly IConnectionMultiplexer _redis;

    public NotificationPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    /// <summary>
    /// 주문 관련 알림 발행
    /// </summary>
    public async Task PublishOrderNotificationAsync(
        string type,
        string? accountAlias,
        object data)
    {
        await PublishAsync(NotificationChannels.Orders, type, accountAlias, data);
    }

    /// <summary>
    /// 스케줄 관련 알림 발행
    /// </summary>
    public async Task PublishScheduleNotificationAsync(
        string type,
        string? accountAlias,
        object data)
    {
        await PublishAsync(NotificationChannels.Schedules, type, accountAlias, data);
    }

    /// <summary>
    /// 포지션 관련 알림 발행
    /// </summary>
    public async Task PublishPositionNotificationAsync(
        string type,
        string? accountAlias,
        object data)
    {
        await PublishAsync(NotificationChannels.Positions, type, accountAlias, data);
    }

    /// <summary>
    /// 시스템 상태 알림 발행
    /// </summary>
    public async Task PublishSystemNotificationAsync(
        string type,
        object data)
    {
        await PublishAsync(NotificationChannels.System, type, null, data);
    }

    /// <summary>
    /// 보안 관련 알림 발행
    /// </summary>
    public async Task PublishSecurityNotificationAsync(
        string type,
        object data)
    {
        await PublishAsync(NotificationChannels.Security, type, null, data);
    }

    /// <summary>
    /// 범용 알림 발행
    /// </summary>
    public async Task PublishAsync(
        string channel,
        string type,
        string? accountAlias,
        object data)
    {
        var message = new NotificationMessage
        {
            Type = type,
            AccountAlias = accountAlias,
            Data = data
        };

        var json = JsonSerializer.Serialize(message);
        var db = _redis.GetDatabase();
        await db.PublishAsync(RedisChannel.Literal(channel), json);
    }
}
