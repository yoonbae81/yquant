using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure
{
    public interface IMasterDataLoader
    {
        Task<IEnumerable<StockMaster>> LoadMasterDataAsync(string exchange, string url);
    }
}
