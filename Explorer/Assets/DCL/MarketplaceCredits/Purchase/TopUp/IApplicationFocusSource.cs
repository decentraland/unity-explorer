using System;

namespace DCL.MarketplaceCredits.Purchase.TopUp
{
    /// <summary>
    /// Abstracts Unity's <c>Application.focusChanged</c> for testability.
    /// </summary>
    public interface IApplicationFocusSource
    {
        /// <summary>
        /// Raised when the application gains or loses OS focus.
        /// <c>true</c> = focus gained, <c>false</c> = focus lost.
        /// </summary>
        event Action<bool> FocusChanged;
    }
}
