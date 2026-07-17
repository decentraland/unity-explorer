using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    public interface ICreditsPurchaseService
    {
        event Action<CreditsPurchaseState>? StateChanged;

        UniTask<CreditsPurchaseResult> PurchaseAsync(string tradeId, int expectedPriceCredits, CancellationToken ct);
    }
}
