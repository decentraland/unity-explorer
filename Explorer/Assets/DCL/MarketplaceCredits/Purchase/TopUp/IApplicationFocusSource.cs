using System;

namespace DCL.MarketplaceCredits.Purchase.TopUp
{
    /// <summary>
    ///     Abstraction over the application's OS focus signal so focus-dependent flows are testable.
    /// </summary>
    public interface IApplicationFocusSource
    {
        /// <summary>
        ///     Raised when the application gains (<c>true</c>) or loses (<c>false</c>) OS focus.
        /// </summary>
        event Action<bool> FocusChanged;
    }
}
