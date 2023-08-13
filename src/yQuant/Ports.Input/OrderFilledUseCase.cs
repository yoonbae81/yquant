namespace yQuant.Ports.Input;

public interface OrderFilledUseCase
{
    public void OrderFilled(string symbol, decimal price, ushort quantity);
}
