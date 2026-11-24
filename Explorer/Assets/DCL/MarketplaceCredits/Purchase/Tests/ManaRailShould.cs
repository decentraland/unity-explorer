using Cysharp.Threading.Tasks;
using DCL.Web3.Chains;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    /// <summary>
    ///     The MANA side of a purchase: when an approval is asked for, and what each relay or settlement outcome
    ///     means for the purchase.
    /// </summary>
    public class ManaRailShould
    {
        private const string BUYER = "0x99995f38fc9d786eab5c3a1b1c4e6ae5f4e99999";
        private const string APPROVE_SELECTOR = "0x095ea7b3";
        private const string TX_HASH = "0x1122";

        private static readonly BigInteger ONE_MANA = BigInteger.Parse("1000000000000000000");

        private ManaTokenReader manaTokenReader = null!;
        private MetaTxRelayer metaTxRelayer = null!;
        private PolygonSettlementPoller settlementPoller = null!;
        private CreditsChainConfig chainConfig = null!;
        private ManaRail manaRail = null!;
        private List<CreditsPurchaseState> recordedStates = null!;

        [SetUp]
        public void SetUp()
        {
            manaTokenReader = Substitute.For<ManaTokenReader>(null, null);
            metaTxRelayer = Substitute.For<MetaTxRelayer>(null, null, null, null);
            settlementPoller = Substitute.For<PolygonSettlementPoller>(null, null);
            chainConfig = new CreditsChainConfig(EthereumNetwork.Sepolia);
            recordedStates = new List<CreditsPurchaseState>();

            metaTxRelayer.RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH, nonce: new BigInteger(4))));

            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Confirmed));

            manaTokenReader.BalanceOfAsync(BUYER, Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult(100 * ONE_MANA));

            manaRail = new ManaRail(manaTokenReader, metaTxRelayer, settlementPoller, chainConfig);
        }

        private void StubAllowance(BigInteger allowance) =>
            manaTokenReader.AllowanceAsync(BUYER, chainConfig.CollectionStoreAddress, Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult(allowance));

        private UniTask<ManaApprovalResult> PrepareStorePaymentAsync() =>
            manaRail.PrepareAsync(BUYER, new[] { new ManaSpend(chainConfig.CollectionStoreAddress, 5 * ONE_MANA) }, 5 * ONE_MANA, recordedStates.Add, CancellationToken.None, CancellationToken.None);

        private UniTask<PurchaseOutcome> BuyFromTheStoreAsync() =>
            manaRail.ExecuteAsync(new ManaPurchaseRequest(BUYER, "mint", chainConfig.CollectionStoreContract, "0xabcdef", BigInteger.MinusOne),
                recordedStates.Add, CancellationToken.None, CancellationToken.None);

        [Test]
        public async Task SkipTheApprovalWhenTheAllowanceCoversThePurchase()
        {
            // Arrange
            StubAllowance(5 * ONE_MANA);

            // Act
            ManaApprovalResult result = await PrepareStorePaymentAsync();

            // Assert
            Assert.IsTrue(result.Success);
            CollectionAssert.IsEmpty(recordedStates);
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ApproveThroughTheManaTokenWhenTheAllowanceIsShort()
        {
            // Arrange: an allowance left over from a cheaper purchase.
            StubAllowance(ONE_MANA);

            // Act
            ManaApprovalResult result = await PrepareStorePaymentAsync();

            // Assert
            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(new[] { CreditsPurchaseState.ApprovingMana, CreditsPurchaseState.ConfirmingManaApproval }, recordedStates);

            await metaTxRelayer.Received(1).RelayAsync(
                Arg.Is<MetaTxContract>(contract => contract.Address == chainConfig.ManaTokenContract.Address && contract.IsClassic),
                BUYER, Arg.Is<string>(calldata => calldata.StartsWith(APPROVE_SELECTOR)), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task CarryTheNonceFromOneApprovalToTheNext()
        {
            // Arrange: neither spender holds an allowance; the first approval is signed with nonce 4.
            manaTokenReader.AllowanceAsync(BUYER, Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult(BigInteger.Zero));

            var spends = new[] { new ManaSpend(chainConfig.CreditsManagerAddress, ONE_MANA), new ManaSpend(chainConfig.CollectionStoreAddress, ONE_MANA) };

            // Act
            ManaApprovalResult result = await manaRail.PrepareAsync(BUYER, spends, 2 * ONE_MANA, recordedStates.Add, CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Success);
            await metaTxRelayer.Received(1).RelayAsync(Arg.Any<MetaTxContract>(), BUYER, Arg.Any<string>(), Arg.Is<BigInteger>(nonce => nonce.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
            await metaTxRelayer.Received(1).RelayAsync(Arg.Any<MetaTxContract>(), BUYER, Arg.Any<string>(), new BigInteger(5), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseAPaymentAboveWhatTheBuyerConfirmed()
        {
            // Act: 5 MANA needed, 4 confirmed.
            ManaApprovalResult result = await manaRail.PrepareAsync(BUYER, new[] { new ManaSpend(chainConfig.CollectionStoreAddress, 5 * ONE_MANA) }, 4 * ONE_MANA,
                recordedStates.Add, CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.PriceChanged, result.Error);
            await manaTokenReader.DidNotReceive().AllowanceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseAPaymentTheBalanceCannotCover()
        {
            // Arrange
            manaTokenReader.BalanceOfAsync(BUYER, Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult(ONE_MANA));

            // Act
            ManaApprovalResult result = await PrepareStorePaymentAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.InsufficientMana, result.Error);
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ContinueWhenTheBalanceCannotBeRead()
        {
            // Arrange
            StubAllowance(5 * ONE_MANA);

            manaTokenReader.BalanceOfAsync(BUYER, Arg.Any<CancellationToken>())
                           .Returns(_ => UniTask.FromException<BigInteger>(new InvalidOperationException("node down")));

            // Act
            ManaApprovalResult result = await PrepareStorePaymentAsync();

            // Assert
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async Task ReportARefusedApprovalSignature()
        {
            // Arrange
            StubAllowance(BigInteger.Zero);

            metaTxRelayer.RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.SignatureRejected, message: "rejected")));

            // Act
            ManaApprovalResult result = await PrepareStorePaymentAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.SignatureRejected, result.Error);
        }

        [TestCase(SettlementOutcome.Reverted)]
        [TestCase(SettlementOutcome.Pending)]
        public async Task FailWhenTheApprovalIsNotSeenConfirmed(SettlementOutcome settlement)
        {
            // Arrange
            StubAllowance(BigInteger.Zero);

            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(settlement));

            // Act
            ManaApprovalResult result = await PrepareStorePaymentAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.ManaApprovalFailed, result.Error);
        }

        [Test]
        public async Task FailWhenTheAllowanceCannotBeRead()
        {
            // Arrange
            manaTokenReader.AllowanceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(_ => UniTask.FromException<BigInteger>(new InvalidOperationException("node down")));

            // Act
            ManaApprovalResult result = await PrepareStorePaymentAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.ManaApprovalFailed, result.Error);
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SettleAPurchaseAgainstItsTarget()
        {
            // Act
            PurchaseOutcome outcome = await BuyFromTheStoreAsync();

            // Assert
            Assert.IsTrue(outcome.Success);
            Assert.IsTrue(outcome.Settled);
            Assert.AreEqual(TX_HASH, outcome.TxHash);
            CollectionAssert.AreEqual(new[] { CreditsPurchaseState.Signing, CreditsPurchaseState.WaitingSettlement, CreditsPurchaseState.Success }, recordedStates);
        }

        [Test]
        public async Task KeepAPurchaseWithAnUnknownBroadcastAsPending()
        {
            // Arrange
            metaTxRelayer.RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.AmbiguousBroadcast, message: "timeout")));

            // Act
            PurchaseOutcome outcome = await BuyFromTheStoreAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.SettlementPending, outcome.Error);
            Assert.IsTrue(outcome.Broadcast);
            Assert.IsFalse(outcome.Settled);
        }

        [Test]
        public async Task ReportARevertedPurchase()
        {
            // Arrange
            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Reverted));

            // Act
            PurchaseOutcome outcome = await BuyFromTheStoreAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.TransactionReverted, outcome.Error);
            Assert.IsTrue(outcome.Reverted);
        }
    }
}
