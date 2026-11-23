using Cysharp.Threading.Tasks;
using DCL.Web3;
using DCL.Web3.Chains;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class ManaTokenReaderShould
    {
        private const string BUYER = "0x99995f38fc9d786eab5c3a1b1c4e6ae5f4e99999";

        private IEthereumApi ethereumApi = null!;
        private CreditsChainConfig chainConfig = null!;
        private ManaTokenReader reader = null!;

        [SetUp]
        public void SetUp()
        {
            ethereumApi = Substitute.For<IEthereumApi>();
            chainConfig = new CreditsChainConfig(EthereumNetwork.Sepolia);
            reader = new ManaTokenReader(ethereumApi, chainConfig);
        }

        private void StubResult(object? result) =>
            ethereumApi.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(UniTask.FromResult(new EthApiResponse { result = result }));

        [Test]
        public async Task ReadTheBalanceFromTheManaTokenOfTheChain()
        {
            // Arrange: 5 MANA.
            StubResult("0x0000000000000000000000000000000000000000000000004563918244f40000");

            // Act
            BigInteger balance = await reader.BalanceOfAsync(BUYER, CancellationToken.None);

            // Assert
            Assert.AreEqual(BigInteger.Parse("5000000000000000000"), balance);

            await ethereumApi.Received(1).SendAsync(
                Arg.Is<EthApiRequest>(request => request.method == "eth_call" && request.readonlyNetwork == chainConfig.ReadonlyNetwork
                                                 && request.@params[0].ToString().Contains(chainConfig.ManaTokenContract.Address)),
                Web3RequestSource.Internal, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReadAnEmptyResultAsZero()
        {
            // Arrange
            StubResult("0x");

            // Act
            BigInteger allowance = await reader.AllowanceAsync(BUYER, chainConfig.CreditsManagerAddress, CancellationToken.None);

            // Assert
            Assert.AreEqual(BigInteger.Zero, allowance);
        }

        [Test]
        public async Task KeepAnUnlimitedAllowancePositive()
        {
            // Arrange: 2^256 - 1 has its top bit set.
            StubResult($"0x{new string('f', 64)}");

            // Act
            BigInteger allowance = await reader.AllowanceAsync(BUYER, chainConfig.CreditsManagerAddress, CancellationToken.None);

            // Assert
            Assert.AreEqual(BigInteger.Pow(2, 256) - BigInteger.One, allowance);
        }

        [Test]
        public void ThrowWhenTheNodeGivesNoResult()
        {
            // Arrange
            StubResult(null);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.BalanceOfAsync(BUYER, CancellationToken.None));
        }

        [Test]
        public async Task ReportAnUnreadableBalanceAsUnknown()
        {
            // Arrange
            StubResult(null);

            // Act
            BigInteger? balance = await reader.TryBalanceOfAsync(BUYER, CancellationToken.None);

            // Assert
            Assert.IsNull(balance);
        }
    }
}
