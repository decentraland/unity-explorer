using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    public interface ICreditsPurchaseService
    {
        event Action<CreditsPurchaseState>? StateChanged;

        UniTask<CreditsQuoteResult> QuoteAsync(ShopListingDto listing, CancellationToken ct);

        UniTask<CreditsPurchaseResult> PurchaseAsync(CreditsPurchaseQuote quote, CancellationToken ct);
    }
}
