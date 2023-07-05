namespace yQuant.Domain.Entities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using yQuant.Domain.Enums;

using static yQuant.Domain.Enums.OrderTransaction;
using static yQuant.Domain.Enums.OrderType;
using static yQuant.Domain.Enums.OrderStatus;


public class Order
{
    public OrderTransaction Transaction { get; init; }
    public OrderType Type { get; init; } = Limit;
    public OrderStatus Status { get; set; } = NotPlaced;
    public string Symbol { get; init; }
    public ushort Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; } = DateTime.Now;

    public Order(OrderTransaction transaction, OrderType type, string symbol)
    {
        Transaction = transaction;
        Type = type;
        Symbol = symbol;
    }
}
