namespace yQuant.Ports.Input;
public interface TickDeliveredUseCase {
    public void TickDelivered(string symbol, decimal price, ushort quantity);
}
