using System;

namespace yQuant.App.Dashboard.Services
{
    public class UiStateService
    {
        public string? SelectedAccountAlias { get; private set; }
        public PositionSortColumn SortColumn { get; private set; } = PositionSortColumn.PnL;
        public bool SortDescending { get; private set; } = true;

        public event Action? OnChange;

        public void SetAccount(string alias)
        {
            if (SelectedAccountAlias != alias)
            {
                SelectedAccountAlias = alias;
                NotifyStateChanged();
            }
        }

        public void SetPositionSort(PositionSortColumn column, bool descending)
        {
            if (SortColumn != column || SortDescending != descending)
            {
                SortColumn = column;
                SortDescending = descending;
                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }

    public enum PositionSortColumn
    {
        Ticker,
        Name,
        Currency,
        Qty,
        AvgPrice,
        CurrentPrice,
        PnL,
        TotalAmount,
        ReturnRate
    }
}
