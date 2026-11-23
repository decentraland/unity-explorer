using Cysharp.Threading.Tasks;
using DCL.Web3.Chains;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class PaymentOptionsProviderShould
    {
        private const string BUYER = "0x99995f38fc9d786eab5c3a1b1c4e6ae5f4e99999";
        private const string OTHER_CONTRACT = "0x3333333333333333333333333333333333333333";
        private const int PRICE_CENTS = 250;

        private static readonly BigInteger ONE_MANA = BigInteger.Parse("1000000000000000000");
        private static readonly BigInteger TEN_MANA = 10 * ONE_MANA;

        private MarketplaceCreditsAPIClient creditsAPIClient = null!;
        private ManaTokenReader manaTokenReader = null!;
        private ManaUsdRateReader manaUsdRateReader = null!;
        private CreditsChainConfig chainConfig = null!;
        private PaymentOptionsProvider provider = null!;

        [SetUp]
        public void SetUp()
        {
            creditsAPIClient = Substitute.For<MarketplaceCreditsAPIClient>(null, null);
            manaTokenReader = Substitute.For<ManaTokenReader>(null, null);
            manaUsdRateReader = Substitute.For<ManaUsdRateReader>(null, null);

            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(new UserCreditsResponse { usd = new UsdCredits { balanceCents = PRICE_CENTS, credits = 25 } }));

            manaTokenReader.BalanceOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult(100 * ONE_MANA));

            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(UniTask.FromResult(new ManaUsdRate(25_000_000, 8)));

            chainConfig = new CreditsChainConfig(EthereumNetwork.Sepolia);
            provider = new PaymentOptionsProvider(creditsAPIClient, manaTokenReader, manaUsdRateReader, chainConfig, true);
        }

        [Test]
        public async Task OfferOnlyManaWhenTheCreditsBalanceCannotBeRead()
        {
            // Arrange
            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(_ => UniTask.FromException<UserCreditsResponse>(new InvalidOperationException("credits-server down")));

            // Act
            PaymentOptions options = await provider.ComputeAsync(BUYER, PRICE_CENTS, TEN_MANA, true, CancellationToken.None);

            // Assert
            Assert.AreEqual(1, options.Options.Count);
            Assert.AreEqual(PaymentMethod.Mana, options.Options[0].Method);
            Assert.IsNull(options.CreditsBalanceCents);
            Assert.AreEqual(100 * ONE_MANA, options.ManaBalanceWei);
        }

        [Test]
        public async Task CarryThePriceAndBothBalances()
        {
            // Act
            PaymentOptions options = await provider.ComputeAsync(BUYER, PRICE_CENTS, TEN_MANA, true, CancellationToken.None);

            // Assert
            Assert.AreEqual(PRICE_CENTS, options.PriceCents);
            Assert.AreEqual(TEN_MANA, options.PriceManaWei);
            Assert.AreEqual(PRICE_CENTS, options.CreditsBalanceCents);
            Assert.AreEqual(100 * ONE_MANA, options.ManaBalanceWei);
        }

        [Test]
        public async Task ReadTheManaBalanceForAPriceWithoutAManaEquivalent()
        {
            // Act
            PaymentOptions options = await provider.ComputeAsync(BUYER, PRICE_CENTS, BigInteger.Zero, true, CancellationToken.None);

            // Assert
            Assert.AreEqual(1, options.Options.Count);
            Assert.AreEqual(PaymentMethod.Credits, options.Options[0].Method);
            Assert.AreEqual(100 * ONE_MANA, options.ManaBalanceWei);
        }

        [Test]
        public async Task OfferOnlyCreditsAndReadNoManaWhenManaPaymentsAreDisabled()
        {
            // Arrange
            var creditsOnlyProvider = new PaymentOptionsProvider(creditsAPIClient, manaTokenReader, manaUsdRateReader, chainConfig, false);
            CreditsPurchaseQuote mint = CreditsPurchaseQuote.ForMint(new StoreMintTarget(OTHER_CONTRACT, "1", TEN_MANA.ToString()), PRICE_CENTS, 25, TEN_MANA);

            // Act
            BigInteger price = await creditsOnlyProvider.ManaPriceOfAsync(mint, CancellationToken.None);
            PaymentOptions options = await creditsOnlyProvider.ComputeAsync(BUYER, PRICE_CENTS, TEN_MANA, true, CancellationToken.None);

            // Assert
            Assert.AreEqual(BigInteger.Zero, price);
            Assert.AreEqual(1, options.Options.Count);
            Assert.AreEqual(PaymentMethod.Credits, options.Options[0].Method);
            Assert.AreEqual(BigInteger.Zero, options.PriceManaWei);
            Assert.IsNull(options.ManaBalanceWei);
            await manaTokenReader.DidNotReceive().BalanceOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task OfferOnlyCreditsWhenTheManaBalanceCannotBeRead()
        {
            // Arrange
            manaTokenReader.BalanceOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(_ => UniTask.FromException<BigInteger>(new InvalidOperationException("node down")));

            // Act
            PaymentOptions options = await provider.ComputeAsync(BUYER, PRICE_CENTS, TEN_MANA, true, CancellationToken.None);

            // Assert
            Assert.AreEqual(1, options.Options.Count);
            Assert.AreEqual(PaymentMethod.Credits, options.Options[0].Method);
            Assert.IsNull(options.ManaShortfall);
            Assert.IsNull(options.ManaBalanceWei);
        }

        [Test]
        public void RelayManaAloneOnlyFromTheMinimumSaleValue()
        {
            // Arrange: a mint one wei under 1 MANA, one at exactly 1 MANA, and a USD-pegged trade at $2.50.
            BigInteger belowMinimum = ONE_MANA - BigInteger.One;
            CreditsPurchaseQuote cheapMint = CreditsPurchaseQuote.ForMint(new StoreMintTarget(OTHER_CONTRACT, "1", belowMinimum.ToString()), 30, 3, belowMinimum);
            CreditsPurchaseQuote mintAtMinimum = CreditsPurchaseQuote.ForMint(new StoreMintTarget(OTHER_CONTRACT, "1", ONE_MANA.ToString()), 30, 3, ONE_MANA);
            CreditsPurchaseQuote trade = CreditsPurchaseQuote.ForTrade(UsdPeggedTrade(), PRICE_CENTS, 25, BigInteger.Zero, false);

            // Act
            bool cheapMintRelayable = PaymentOptionsProvider.CanRelayManaAlone(cheapMint);
            bool mintAtMinimumRelayable = PaymentOptionsProvider.CanRelayManaAlone(mintAtMinimum);
            bool tradeRelayable = PaymentOptionsProvider.CanRelayManaAlone(trade);

            // Assert
            Assert.IsFalse(cheapMintRelayable);
            Assert.IsTrue(mintAtMinimumRelayable);
            Assert.IsTrue(tradeRelayable);
        }

        [Test]
        public async Task GiveNoManaPriceWhenTheRateCannotBeRead()
        {
            // Arrange: a USD-pegged trade has no MANA price without the oracle.
            TradeDto trade = UsdPeggedTrade();
            CreditsPurchaseQuote quote = CreditsPurchaseQuote.ForTrade(trade, PRICE_CENTS, 25, BigInteger.Zero, false);

            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(_ => UniTask.FromException<ManaUsdRate>(new InvalidOperationException("oracle down")));

            // Act
            BigInteger price = await provider.ManaPriceOfAsync(quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(BigInteger.Zero, price);
        }

        [Test]
        public async Task CarryTheManaPriceOfAMint()
        {
            // Arrange
            CreditsPurchaseQuote quote = CreditsPurchaseQuote.ForMint(new StoreMintTarget(OTHER_CONTRACT, "1", TEN_MANA.ToString()), PRICE_CENTS, 25, TEN_MANA);

            // Act
            BigInteger price = await provider.ManaPriceOfAsync(quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(TEN_MANA, price);
            await manaUsdRateReader.DidNotReceive().ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ConvertAUsdPeggedPriceAtTheOracleRate()
        {
            // Arrange: $2.50 at $0.25 per MANA.
            CreditsPurchaseQuote quote = CreditsPurchaseQuote.ForTrade(UsdPeggedTrade(), PRICE_CENTS, 25, BigInteger.Zero, false);

            // Act
            BigInteger price = await provider.ManaPriceOfAsync(quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(TEN_MANA, price);
        }

        [Test]
        public async Task GiveNoManaPriceToATradePricedInAnotherToken()
        {
            // Arrange: the amount is in a token that is not MANA.
            TradeDto trade = UsdPeggedTrade();
            trade.received[0].assetType = CreditsTradeEncoder.ASSET_TYPE_ERC20;
            trade.received[0].contractAddress = OTHER_CONTRACT;
            CreditsPurchaseQuote quote = CreditsPurchaseQuote.ForTrade(trade, PRICE_CENTS, 25, TEN_MANA, true);

            // Act
            BigInteger price = await provider.ManaPriceOfAsync(quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(BigInteger.Zero, price);
        }

        [Test]
        public async Task GiveNoManaPriceToATradeOnAnUnknownMarketplace()
        {
            // Arrange
            TradeDto trade = UsdPeggedTrade();
            trade.contract = OTHER_CONTRACT;
            CreditsPurchaseQuote quote = CreditsPurchaseQuote.ForTrade(trade, PRICE_CENTS, 25, BigInteger.Zero, false);

            // Act
            BigInteger price = await provider.ManaPriceOfAsync(quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(BigInteger.Zero, price);
            await manaUsdRateReader.DidNotReceive().ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        private TradeDto UsdPeggedTrade() =>
            new ()
            {
                id = "trade-1",
                contract = chainConfig.OffChainMarketplaceAddress,
                received = new[] { new TradeAssetDto { assetType = CreditsTradeEncoder.ASSET_TYPE_USD_PEGGED_MANA, contractAddress = OTHER_CONTRACT, amount = "2500000000000000000" } },
            };
    }
}
