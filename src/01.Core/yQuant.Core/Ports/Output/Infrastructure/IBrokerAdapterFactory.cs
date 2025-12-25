namespace yQuant.Core.Ports.Output.Infrastructure;

public interface IBrokerAdapterFactory
{
    IBrokerAdapter? GetAdapter(string alias);
    IEnumerable<string> GetAvailableAccounts();

}
