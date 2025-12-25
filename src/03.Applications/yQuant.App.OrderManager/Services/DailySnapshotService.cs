using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.OrderManager.Services;

/// <summary>
/// 일별 계좌 스냅샷 생성 서비스
/// 매일 지정된 시각에 계좌 상태를 스냅샷으로 저장
/// </summary>
public class DailySnapshotService : BackgroundService
{
    private readonly ILogger<DailySnapshotService> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDailySnapshotRepository _snapshotRepository;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _snapshotTime;

    public DailySnapshotService(
        ILogger<DailySnapshotService> logger,
        IConnectionMultiplexer redis,
        IDailySnapshotRepository snapshotRepository,
        IConfiguration configuration)
    {
        _logger = logger;
        _redis = redis;
        _snapshotRepository = snapshotRepository;
        _configuration = configuration;

        // Default: 16:00 KST (한국 시장 마감 후)
        var snapshotTimeStr = _configuration.GetValue<string>("OrderManager:DailySnapshotTime", "16:00:00");
        if (!TimeSpan.TryParse(snapshotTimeStr, out _snapshotTime))
        {
            _snapshotTime = new TimeSpan(16, 0, 0);
            _logger.LogWarning("Invalid DailySnapshotTime configuration. Using default 16:00:00");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailySnapshotService started. Snapshot time: {Time}", _snapshotTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var nextRun = CalculateNextRunTime(now);
                var delay = nextRun - now;

                _logger.LogInformation("Next snapshot scheduled at: {NextRun} (in {Delay})",
                    nextRun, delay);

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await CreateDailySnapshotsAsync();
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DailySnapshotService loop");
                // Wait 1 hour before retrying
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        _logger.LogInformation("DailySnapshotService stopped");
    }

    private DateTime CalculateNextRunTime(DateTime now)
    {
        var today = now.Date + _snapshotTime;

        if (now < today)
        {
            return today; // Run today
        }
        else
        {
            return today.AddDays(1); // Run tomorrow
        }
    }

    private async Task CreateDailySnapshotsAsync()
    {
        _logger.LogInformation("Creating daily snapshots for all accounts...");

        try
        {
            var db = _redis.GetDatabase();
            var accountAliases = await db.SetMembersAsync("account:index");

            if (accountAliases.Length == 0)
            {
                _logger.LogWarning("No accounts found in Redis index");
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            int successCount = 0;

            foreach (var aliasValue in accountAliases)
            {
                var alias = aliasValue.ToString();

                try
                {
                    var snapshot = await CreateSnapshotForAccountAsync(db, alias, today);

                    if (snapshot != null)
                    {
                        await _snapshotRepository.SaveAsync(alias, snapshot);
                        successCount++;

                        _logger.LogInformation(
                            "Saved snapshot for {Account}: Equity={Equity:N2} {Currency}, Return={Return:P2}",
                            alias, snapshot.TotalEquity, snapshot.Currency, snapshot.DailyReturn);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create snapshot for account {Account}", alias);
                }
            }

            _logger.LogInformation(
                "Daily snapshot creation completed: {Success}/{Total} accounts",
                successCount, accountAliases.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create daily snapshots");
        }
    }

    private async Task<DailySnapshot?> CreateSnapshotForAccountAsync(
        IDatabase db,
        string accountAlias,
        DateOnly date)
    {
        try
        {
            // Get deposits
            var depositKey = $"deposit:{accountAlias}";
            var deposits = await db.HashGetAllAsync(depositKey);

            if (deposits.Length == 0)
            {
                _logger.LogWarning("No deposit data found for account {Account}", accountAlias);
                return null;
            }

            // Calculate total equity per currency
            var depositDict = deposits.ToDictionary(
                h => h.Name.ToString(),
                h => decimal.TryParse(h.Value.ToString(), out var v) ? v : 0m
            );

            // Get positions
            var positionKey = $"position:{accountAlias}";
            var positions = await db.HashGetAllAsync(positionKey);

            decimal positionValue = 0;
            var positionsList = new List<Position>();

            foreach (var posHash in positions)
            {
                try
                {
                    var position = JsonSerializer.Deserialize<Position>(posHash.Value.ToString());
                    if (position != null)
                    {
                        positionsList.Add(position);
                        positionValue += position.CurrentPrice * position.Qty;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse position for {Ticker}", posHash.Name);
                }
            }

            // Determine primary currency (most common in positions or first deposit)
            var currency = positionsList.FirstOrDefault()?.Currency
                ?? (depositDict.Keys.FirstOrDefault() is string currStr && Enum.TryParse<CurrencyType>(currStr, out var curr) ? curr : CurrencyType.KRW);

            var cashBalance = depositDict.TryGetValue(currency.ToString(), out var cash) ? cash : 0;
            var totalEquity = cashBalance + positionValue;

            // Get previous snapshot for daily return calculation
            var previousSnapshot = await _snapshotRepository.GetLatestSnapshotAsync(accountAlias);

            decimal dailyPnL = 0;
            double dailyReturn = 0;
            double cumulativeReturn = 0;
            double drawdownPct = 0;

            if (previousSnapshot != null && previousSnapshot.Date < date)
            {
                dailyPnL = totalEquity - previousSnapshot.TotalEquity;
                dailyReturn = previousSnapshot.TotalEquity > 0
                    ? (double)(dailyPnL / previousSnapshot.TotalEquity)
                    : 0;

                // Calculate cumulative return (needs initial equity - use first snapshot)
                var allSnapshots = await _snapshotRepository.GetAllSnapshotsAsync(accountAlias);
                var firstSnapshot = allSnapshots.OrderBy(s => s.Date).FirstOrDefault();

                if (firstSnapshot != null && firstSnapshot.TotalEquity > 0)
                {
                    cumulativeReturn = (double)((totalEquity - firstSnapshot.TotalEquity) / firstSnapshot.TotalEquity);

                    // Calculate drawdown
                    var peak = allSnapshots.Max(s => s.TotalEquity);
                    if (peak > 0)
                    {
                        drawdownPct = (double)((peak - totalEquity) / peak);
                    }
                }
            }

            return new DailySnapshot
            {
                Date = date,
                Currency = currency,
                TotalEquity = totalEquity,
                CashBalance = cashBalance,
                PositionValue = positionValue,
                DailyPnL = dailyPnL,
                DailyReturn = dailyReturn,
                CumulativeReturn = cumulativeReturn,
                PositionsCount = positionsList.Count,
                DrawdownPct = drawdownPct
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshot for account {Account}", accountAlias);
            return null;
        }
    }
}
