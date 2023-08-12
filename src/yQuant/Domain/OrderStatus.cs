namespace yQuant.Domain;

public enum OrderStatus
{
    NotPlaced,
    Placed,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected
}