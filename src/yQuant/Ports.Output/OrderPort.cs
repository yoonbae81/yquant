using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace yQuant.Ports.Output;
public interface OrderPort
{
    public bool PlaceOrder(OrderCommand command);
    public decimal CalcEntryVolume(OrderCommand command);
}
