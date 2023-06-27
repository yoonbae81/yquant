using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using yQuant.Domain.Enums;

namespace yQuant.Domain.Entities;

public class Order
{
    public OrderTransaction Transaction { get; init; }
    public OrderType Type { get; init; } = OrderType.Limit;
    public OrderStatus Status { get; set; } = OrderStatus.NotPlaced;
    public string Symbol { get; init; }
    public ushort Quantity { get; init; }
    public decimal Price { get; init; }
    public DateTime CreatedAt { get; } = DateTime.Now;

    private Order(OrderTransaction transaction, OrderType type, string symbol, ushort quantity, decimal price = 0)
    {
        Transaction = transaction;
        Type = type;
        Symbol = symbol;
        Quantity = quantity;
        Price = price;
    }

    public static Order CreateMarketBuyOrder(string symbol, ushort quantity)
    {
        return new Order(
            OrderTransaction.Buy,
            OrderType.Market,
            symbol,
            quantity
            );
    }

    public static Order CreateLimitBuyOrder(string symbol, ushort quantity, decimal price)
    {
        return new Order(
            OrderTransaction.Buy,
            OrderType.Market,
            symbol,
            quantity,
            price
            );
    }
    public static Order CreateMarketSellOrder(string symbol, ushort quantity)
    {
        return new Order(
            OrderTransaction.Sell,
            OrderType.Market,
            symbol,
            quantity
            ); ;
    }

    public static Order CreateLimitSellOrder(string symbol, ushort quantity, decimal price)
    {
        return new Order(
            OrderTransaction.Sell,
            OrderType.Market,
            symbol,
            quantity,
            price
            );
    }
}
