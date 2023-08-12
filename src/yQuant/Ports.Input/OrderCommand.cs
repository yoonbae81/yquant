namespace yQuant.Ports.Input;

using yQuant.Domain;

using System;

using static yQuant.Domain.OrderTransaction;
using static yQuant.Domain.OrderType;
using static yQuant.Domain.OrderStatus;

public record OrderCommand
{
    public OrderTransaction Transaction { get; init; }
    public OrderType Type { get; init; } = Limit;
    public OrderStatus Status { get; set; } = NotPlaced;
    public string Symbol { get; init; }
    public ushort Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; } = DateTime.Now;

    public OrderCommand(OrderTransaction transaction, OrderType type, string symbol)
    {
        Transaction = transaction;
        Type = type;
        Symbol = symbol;

        // TODO: Add input validations
    }
}
