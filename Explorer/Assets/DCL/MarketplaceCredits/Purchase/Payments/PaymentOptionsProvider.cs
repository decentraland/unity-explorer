using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Numerics;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     Reads what a buyer holds (credits and Polygon MANA) and what a quote costs in MANA, which is everything
    ///     <see cref="PaymentOptionsCalculator" /> needs. A read that fails is reported as nothing held, so the
    ///     method depending on it is not offered.
    /// </summary>
    public class PaymentOptionsProvider
    {
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly ManaTokenReader manaTokenReader;
        private readonly ManaUsdRateReader manaUsdRateReader;

        public PaymentOptionsProvider(MarketplaceCreditsAPIClient creditsAPIClient, ManaTokenReader manaTokenReader, ManaUsdRateReader manaUsdRateReader)
        {
            this.creditsAPIClient = creditsAPIClient;
            this.manaTokenReader = manaTokenReader;
            this.manaUsdRateReader = manaUsdRateReader;
        }

        public async UniTask<PaymentOptions> ComputeAsync(string buyer, int priceCents, BigInteger priceManaWei, CancellationToken ct)
        {
            long balanceCents = await CreditsBalanceCentsAsync(buyer, ct);
            BigInteger manaBalanceWei = priceManaWei.Sign > 0 ? await ManaBalanceAsync(buyer, ct) ?? BigInteger.Zero : BigInteger.Zero;

            return PaymentOptionsCalculator.Compute(priceCents, priceManaWei, balanceCents, manaBalanceWei);
        }

        /// <summary>
        ///     The MANA one unit of the quote draws right now: carried by a MANA-priced listing, converted at the
        ///     marketplace's oracle for a USD-pegged one. Zero when the rate cannot be read.
        /// </summary>
        public async UniTask<BigInteger> ManaPriceOfAsync(CreditsPurchaseQuote quote, CancellationToken ct)
        {
            if (quote.IsLiveRatePrice || quote.Trade == null)
                return quote.RequiredManaWei;

            try
            {
                ManaUsdRate rate = await manaUsdRateReader.ReadAsync(quote.Trade.contract, ct);
                return CreditsTradeEncoder.UsdWeiToManaWei(quote.Trade.received[0].amount, rate);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA price of trade {quote.Trade.id} unavailable: {e.Message}");
                return BigInteger.Zero;
            }
        }

        public async UniTask<long> CreditsBalanceCentsAsync(string buyer, CancellationToken ct)
        {
            try
            {
                UserCreditsResponse credits = await creditsAPIClient.GetUserCreditsAsync(buyer, ct);
                return credits.usd.balanceCents;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Credits balance unavailable: {e.Message}");
                return 0;
            }
        }

        /// <summary>Null when the balance could not be read.</summary>
        public async UniTask<BigInteger?> ManaBalanceAsync(string buyer, CancellationToken ct)
        {
            try { return await manaTokenReader.BalanceOfAsync(buyer, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA balance unavailable: {e.Message}");
                return null;
            }
        }
    }
}
