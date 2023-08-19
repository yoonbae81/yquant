using yQuant.Ports.Output;

namespace yQuant.Adapters.Backtest.Output;
internal class OrderAdapter : OrderPort
{
    public decimal CalcEntryVolume(OrderCommand command)
    {
        throw new NotImplementedException();
    }

    public bool PlaceOrder(OrderCommand command)
    {
        throw new NotImplementedException();
    }
}
