using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase.Cart
{
    /// <summary>
    ///     Buys a whole cart the way the web shop does: every line is re-read against its live listing, the lines
    ///     are grouped into the transactions that will settle them (one per marketplace contract, one for all
    ///     CollectionStore mints), each group is authorized against a single credit and signed once, and groups
    ///     are settled sequentially.
    /// </summary>
    public interface ICreditsCartCheckoutService : IDisposable
    {
        event Action<CartCheckoutProgress>? StateChanged;

        /// <summary>Raised for every terminal result, bought or not; the shop invalidates its caches when anything was bought.</summary>
        event Action<CartCheckoutResult>? CheckoutCompleted;

        bool IsCheckoutInFlight { get; }
        CartCheckoutProgress CurrentProgress { get; }
        CartCheckoutResult? LastResult { get; }

        /// <summary>Reads only: nothing is reserved. Never throws for a single bad row.</summary>
        UniTask<CartReviewResult> ReviewAsync(IReadOnlyList<ShopCartLine> lines, CancellationToken ct);

        /// <summary>
        ///     uiCt is honoured only while nothing is irreversible (reserving, between groups); once a group is being
        ///     signed it runs to a terminal state on the service's own lifetime.
        /// </summary>
        UniTask<CartCheckoutResult> CheckoutAsync(CartReview review, CancellationToken uiCt);

        void AcknowledgeResult();
    }
}
