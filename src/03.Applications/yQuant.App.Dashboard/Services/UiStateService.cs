using System;

namespace yQuant.App.Dashboard.Services
{
    public class UiStateService
    {
        public string? SelectedAccountAlias { get; private set; }

        public event Action? OnChange;

        public void SetAccount(string alias)
        {
            if (SelectedAccountAlias != alias)
            {
                SelectedAccountAlias = alias;
                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
