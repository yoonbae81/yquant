using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure
{
    /// <summary>
    /// Port for synchronizing stock master data from external sources.
    /// </summary>
    public interface IMasterDataSyncService
    {
        /// <summary>
        /// Synchronizes master data for a specific country.
        /// </summary>
        /// <param name="country">Country name (e.g., "Korea", "USA")</param>
        /// <param name="exchangeUrls">Dictionary of exchange names to download URLs</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SyncCountryAsync(CountryCode country, Dictionary<string, string> exchangeUrls, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Synchronizes master data for all configured countries.
        /// </summary>
        /// <param name="countries">Dictionary of country names to their exchange configurations</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SyncAllAsync(Dictionary<CountryCode, Dictionary<string, string>> countries, CancellationToken cancellationToken = default);
    }
}
