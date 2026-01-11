using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure;

public interface IStockCatalogRepository
{
    Task SaveBatchAsync(IEnumerable<Stock> stocks, CancellationToken cancellationToken = default);
    Task<Stock?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default);
    Task LoadAllToMemoryAsync(bool force = false, string? countryCode = null, CancellationToken cancellationToken = default);
    Task LoadCountryToMemoryAsync(string countryCode, CancellationToken cancellationToken = default);
    Task SetLastSyncDateAsync(CountryCode country, DateTime date);
    Task<DateTime?> GetLastSyncDateAsync(CountryCode country);
    Task<string[]> GetActiveCountriesAsync();
    Task<IEnumerable<Stock>> GetByTickersAsync(IEnumerable<string> tickers, CancellationToken cancellationToken = default);
}
