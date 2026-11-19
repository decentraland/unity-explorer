using Cysharp.Threading.Tasks;
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
        private const string MARKETPLACE = "0x1b67d0e31eeb6b52d8eeed71d3616c2f5b33b8e7";
        private const int PRICE_CENTS = 250;

        private static readonly BigInteger ONE_MANA = BigInteger.Parse("1000000000000000000");
        private static readonly BigInteger TEN_MANA = 10 * ONE_MANA;

        private MarketplaceCreditsAPIClient creditsAPIClient = null!;
        private ManaTokenReader manaTokenReader = null!;
        private ManaUsdRateReader manaUsdRateReader = null!;
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

            provider = new PaymentOptionsProvider(creditsAPIClient, manaTokenReader, manaUsdRateReader);
        }

        [Test]
        public async Task OfferOnlyManaWhenTheCreditsBalanceCannotBeRead()
        {
            // Arrange
            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns<UniTask<UserCreditsResponse>>(_ => throw new InvalidOperationException("credits-server down"));

            // Act
            PaymentOptions options = await provider.ComputeAsync(BUYER, PRICE_CENTS, TEN_MANA, CancellationToken.None);

            // Assert
            Assert.AreEqual(1, options.Options.Count);
            Assert.AreEqual(PaymentMethod.Mana, options.Options[0].Method);
        }

        [Test]
        public async Task OfferOnlyCreditsWhenTheManaBalanceCannotBeRead()
        {
            // Arrange
            manaTokenReader.BalanceOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns<UniTask<BigInteger>>(_ => throw new InvalidOperationException("node down"));

            // Act
            BigInteger? balance = await provider.ManaBalanceAsync(BUYER, CancellationToken.None);
            PaymentOptions options = await provider.ComputeAsync(BUYER, PRICE_CENTS, TEN_MANA, CancellationToken.None);

            // Assert
            Assert.IsNull(balance);
            Assert.AreEqual(1, options.Options.Count);
            Assert.AreEqual(PaymentMethod.Credits, options.Options[0].Method);
            Assert.IsNull(options.ManaShortfall);
        }

        [Test]
        public async Task GiveNoManaPriceWhenTheRateCannotBeRead()
        {
            // Arrange: a USD-pegged trade has no MANA price without the oracle.
            var trade = new TradeDto
            {
                id = "trade-1",
                contract = MARKETPLACE,
                received = new[] { new TradeAssetDto { assetType = CreditsTradeEncoder.ASSET_TYPE_USD_PEGGED_MANA, contractAddress = MARKETPLACE, amount = "2500000000000000000" } },
            };

            CreditsPurchaseQuote quote = CreditsPurchaseQuote.ForTrade(trade, PRICE_CENTS, 25, BigInteger.Zero, false);

            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns<UniTask<ManaUsdRate>>(_ => throw new InvalidOperationException("oracle down"));

            // Act
            BigInteger price = await provider.ManaPriceOfAsync(quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(BigInteger.Zero, price);
        }

        [Test]
        public async Task CarryTheManaPriceOfAMint()
        {
            // Arrange
            CreditsPurchaseQuote quote = CreditsPurchaseQuote.ForMint(new StoreMintTarget(MARKETPLACE, "1", TEN_MANA.ToString()), PRICE_CENTS, 25, TEN_MANA);

            // Act
            BigInteger price = await provider.ManaPriceOfAsync(quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(TEN_MANA, price);
            await manaUsdRateReader.DidNotReceive().ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }
}
