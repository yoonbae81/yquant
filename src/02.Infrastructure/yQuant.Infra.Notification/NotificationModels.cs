namespace yQuant.Infra.Notification;

/// <summary>
/// Redis 알림 채널 상수 정의
/// 시스템의 핵심 도메인 규약이므로 코드에 하드코딩
/// </summary>
public static class NotificationChannels
{
    /// <summary>주문 관련 알림 (요청, 체결, 실패)</summary>
    public const string Orders = "notifications:orders";

    /// <summary>스케줄 관련 알림 (등록, 실행, 성공/실패)</summary>
    public const string Schedules = "notifications:schedules";

    /// <summary>포지션 관련 알림 (변경, 청산, 손익)</summary>
    public const string Positions = "notifications:positions";

    /// <summary>시스템 상태 알림 (시작, 작업 완료, 정상 종료)</summary>
    public const string System = "notifications:system";

    /// <summary>보안 관련 알림 (로그인, 권한 변경 등)</summary>
    public const string Security = "notifications:security";

    /// <summary>모든 채널 목록</summary>
    public static readonly string[] All =
    {
        Orders,
        Schedules,
        Positions,
        System,
        Security
    };
}

/// <summary>
/// 알림 메시지 타입
/// </summary>
public enum NotificationType
{
    Order,
    Schedule,
    Position,
    System
}

/// <summary>
/// Redis로 전송되는 알림 메시지 구조
/// </summary>
public class NotificationMessage
{
    /// <summary>메시지 타입 (예: "OrderExecuted", "ScheduleTriggered")</summary>
    public required string Type { get; set; }

    /// <summary>계정 별칭 (계정별 라우팅에 사용)</summary>
    public string? AccountAlias { get; set; }

    /// <summary>Redis 채널 (Notifier에서 설정)</summary>
    public string? Channel { get; set; }

    /// <summary>메시지 생성 시각 (UTC)</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>메시지 데이터 (JSON 직렬화 가능한 객체)</summary>
    public required object Data { get; set; }
}
