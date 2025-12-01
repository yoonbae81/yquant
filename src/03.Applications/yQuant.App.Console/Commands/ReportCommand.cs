using System;
using System.Linq;
/*
using System.Threading.Tasks;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Reporting.Performance.Interfaces;
using yQuant.Infra.Reporting.Performance.Services;

namespace yQuant.App.Console.Commands
{
    public class ReportCommand : ICommand
    {
        private readonly IPerformanceRepository _repo;
        private readonly IQuantStatsService _service;
        private readonly string _targetAlias;

        public ReportCommand(IPerformanceRepository repo, IQuantStatsService service, string targetAlias)
        {
            _repo = repo;
            _service = service;
            _targetAlias = targetAlias;
        }

        public string Name => "report";
        public string Description => "Show performance report";

        public async Task ExecuteAsync(string[] args)
        {
            var logs = await _repo.GetLogsAsync(_targetAlias);
            if (!logs.Any())
            {
                System.Console.WriteLine($"No performance logs found for account: {_targetAlias}");
                return;
            }

            // Generate CSV content but print to screen instead of saving
            var csv = _service.GenerateCsvReport(logs);
            System.Console.WriteLine("\n[Performance Report]");
            System.Console.WriteLine(csv);
        }
    }
}
*/
