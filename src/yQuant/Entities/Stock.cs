using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace yQuant.Entities;

public class Stock
{
    public string Symbol { init; get; }

    public decimal Price { get; }

    public Stock(string symbol)
    {
        Symbol = symbol;
    }
}
