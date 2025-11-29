using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure
{
    /// <summary>
    /// Port for persisting and retrieving stock master data.
    /// </summary>
    public interface IMasterDataRepository
    {
        /// <summary>
        /// Saves a batch of stock master data.
        /// </summary>
        /// <param name="stocks">Collection of stock master data to save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SaveBatchAsync(IEnumerable<StockMaster> stocks, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Retrieves stock master data by ticker symbol.
        /// </summary>
        /// <param name="ticker">Stock ticker symbol</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stock master data if found, null otherwise</returns>
        Task<StockMaster?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default);
    }
}
