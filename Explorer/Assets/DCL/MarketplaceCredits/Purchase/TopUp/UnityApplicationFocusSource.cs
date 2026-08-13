using System;
using UnityEngine;

namespace DCL.MarketplaceCredits.Purchase.TopUp
{
    /// <summary>
    /// Forwards Unity's <c>Application.focusChanged</c> event through <see cref="IApplicationFocusSource"/>.
    /// Subscribe via the interface; dispose to unsubscribe from the Unity event.
    /// </summary>
    public class UnityApplicationFocusSource : IApplicationFocusSource, IDisposable
    {
        public event Action<bool>? FocusChanged;

        public UnityApplicationFocusSource()
        {
            Application.focusChanged += OnFocusChanged;
        }

        public void Dispose()
        {
            Application.focusChanged -= OnFocusChanged;
        }

        private void OnFocusChanged(bool hasFocus) =>
            FocusChanged?.Invoke(hasFocus);
    }
}
