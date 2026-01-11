using System;
using System.Threading.Tasks;

namespace yQuant.Core.Ports.Output.Infrastructure
{
    public interface ISystemLogger
    {
        // 앱 시작 시 호출 (AppInfo: 버전, 환경 등)
        Task LogStartupAsync(string appName, string version);

        // 계좌와 무관한 시스템 전역 에러 (Valkey 접속 불가 등)
        Task LogSystemErrorAsync(string context, Exception ex);

        // 시스템 상태/정보 로깅 (예: 작업 완료 알림)
        Task LogStatusAsync(string context, string message);

        // 보안 관련 로깅 (예: 로그인 성공/실패)
        Task LogSecurityAsync(string context, string message);

        // Catalog 관련 로깅
        Task LogCatalogAsync(string context, string message);
    }
}
