using System;

using yQuant.Domain;
using static yQuant.Domain.OrderType;
using static yQuant.Domain.OrderStatus;

namespace yQuant.Ports.Output;

public record OrderCommand
{
    public OrderPosition Position { get; init; }
    public OrderType Type { get; init; } = Limit;
    public OrderStatus Status { get; set; } = NotPlaced;
    public string Symbol { get; init; }
    public ushort Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; } = DateTime.Now;

    public OrderCommand(OrderPosition position, OrderType type, string symbol)
    {
        Position = position;
        Type = type;
        Symbol = symbol;

        // TODO: Add input validations
    }
}
