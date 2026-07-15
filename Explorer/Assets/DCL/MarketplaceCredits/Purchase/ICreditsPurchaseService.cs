using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    public interface ICreditsPurchaseService
    {
        Action<CreditsPurchaseState>? StateChanged { get; set; }

        UniTask<CreditsPurchaseResult> PurchaseAsync(string tradeId, int expectedPriceCredits, CancellationToken ct);
    }
}
