using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Numerics;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     Reads what a buyer holds (credits and Polygon MANA) and what a quote costs in MANA. A balance that
    ///     cannot be read is reported as unknown. With MANA payments disabled no price has a MANA equivalent.
    /// </summary>
    public class PaymentOptionsProvider
    {
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly ManaTokenReader manaTokenReader;
        private readonly ManaUsdRateReader manaUsdRateReader;
        private readonly CreditsChainConfig chainConfig;
        private readonly bool isManaEnabled;

        public PaymentOptionsProvider(MarketplaceCreditsAPIClient creditsAPIClient, ManaTokenReader manaTokenReader, ManaUsdRateReader manaUsdRateReader, CreditsChainConfig chainConfig,
            bool isManaEnabled)
        {
            this.chainConfig = chainConfig;
            this.isManaEnabled = isManaEnabled;
            this.creditsAPIClient = creditsAPIClient;
            this.manaTokenReader = manaTokenReader;
            this.manaUsdRateReader = manaUsdRateReader;
        }

        public async UniTask<PaymentOptions> ComputeAsync(string buyer, int priceCents, BigInteger priceManaWei, bool isManaAloneRelayable, CancellationToken ct)
        {
            if (!isManaEnabled)
                return PaymentOptionsCalculator.Compute(priceCents, BigInteger.Zero, await CreditsBalanceCentsAsync(buyer, ct), null, isManaAloneRelayable);

            (long? creditsBalanceCents, BigInteger? manaBalanceWei) = await UniTask.WhenAll(CreditsBalanceCentsAsync(buyer, ct), manaTokenReader.TryBalanceOfAsync(buyer, ct));

            return PaymentOptionsCalculator.Compute(priceCents, priceManaWei, creditsBalanceCents, manaBalanceWei, isManaAloneRelayable);
        }

        /// <summary>
        ///     Whether the relayer takes one unit of the quote paid with MANA alone, which is a sale made straight on
        ///     the marketplace or the CollectionStore: see <see cref="MetaTxRelayer.MIN_DIRECT_SALE_VALUE_WEI" />.
        /// </summary>
        public static bool CanRelayManaAlone(in CreditsPurchaseQuote quote)
        {
            string? amount = quote.Kind == CreditsListingKind.StoreMint
                ? quote.Mint.PriceWei
                : quote.Trade is { received: { Length: > 0 } } trade ? trade.received[0].amount : null;

            return CreditsTradeEncoder.AmountOrZero(amount) >= MetaTxRelayer.MIN_DIRECT_SALE_VALUE_WEI;
        }

        /// <summary>
        ///     The MANA one unit of the quote draws right now: carried by a MANA-priced listing, converted at the
        ///     marketplace's oracle for a USD-pegged one. Zero when MANA payments are disabled, when the rate cannot
        ///     be read, and for a trade that cannot be paid in MANA.
        /// </summary>
        public async UniTask<BigInteger> ManaPriceOfAsync(CreditsPurchaseQuote quote, CancellationToken ct)
        {
            if (!isManaEnabled)
                return BigInteger.Zero;

            if (quote.Trade != null && !chainConfig.CanPayInMana(quote.Trade))
                return BigInteger.Zero;

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

        private async UniTask<long?> CreditsBalanceCentsAsync(string buyer, CancellationToken ct)
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
                return null;
            }
        }
    }
}
