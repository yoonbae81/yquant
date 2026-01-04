using System;
using yQuant.Core.Models;

namespace yQuant.App.Dashboard.Services
{
    public class RealtimeEventService
    {
        public event Action<OrderResult>? OnOrderExecutionReceived;

        public void NotifyOrderExecution(OrderResult result)
        {
            OnOrderExecutionReceived?.Invoke(result);
        }
    }
}
