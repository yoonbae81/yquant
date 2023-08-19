using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace yQuant.Domains;

public enum OrderType
{
    Market,
    Limit
    // Stop
    // StopLimit,
    // TrailingStop
}

public enum OrderPosition
{
    Entry,
    Exit
}

public enum OrderStatus
{
    NotPlaced,
    Placed,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected
}
