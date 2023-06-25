using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using yQuant.Domain.Enums;

namespace yQuant.Domain.Entities;

public class Order {
    public OrderStatus Status { get; }
    public string Symbol { get; }
    public long Quantity { get; }
}
