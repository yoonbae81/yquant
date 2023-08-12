namespace yQuant.Ports.Input;

internal interface OrderUseCase
{
    public bool RequestOrder(OrderCommand command);
}
