using System;
using UnityEngine;

namespace DCL.MarketplaceCredits.Purchase.TopUp
{
    /// <summary>
    ///     Forwards Unity's static <c>Application.focusChanged</c> event through
    ///     <see cref="IApplicationFocusSource" />. Disposing detaches from the static event.
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
