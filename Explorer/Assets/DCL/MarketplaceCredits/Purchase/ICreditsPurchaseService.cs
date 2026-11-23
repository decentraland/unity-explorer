using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    public interface ICreditsPurchaseService
    {
        event Action<CreditsPurchaseState>? StateChanged;

        UniTask<CreditsQuoteResult> QuoteAsync(ShopListingDto listing, CancellationToken ct);

        /// <summary>The payment methods the buyer's credits and Polygon MANA support for the quote.</summary>
        UniTask<PaymentOptions> GetPaymentOptionsAsync(CreditsPurchaseQuote quote, CancellationToken ct);

        /// <summary>
        ///     Buys the quoted listing with the given option. Its amounts are what the buyer confirmed: the purchase
        ///     fails with <see cref="CreditsPurchaseError.PriceChanged" /> instead of drawing noticeably more MANA.
        /// </summary>
        UniTask<CreditsPurchaseResult> PurchaseAsync(CreditsPurchaseQuote quote, PaymentOption option, CancellationToken ct);
    }
}
