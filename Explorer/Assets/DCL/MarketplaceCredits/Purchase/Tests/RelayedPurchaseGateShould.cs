using NUnit.Framework;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class RelayedPurchaseGateShould
    {
        [Test]
        public void LetOnePurchaseInAtATime()
        {
            // Arrange
            var gate = new RelayedPurchaseGate();

            // Act
            RelayedPurchaseGate.Lease? first = gate.TryEnter();

            // Assert
            Assert.IsNotNull(first);
            Assert.IsNull(gate.TryEnter());
        }

        [Test]
        public void IgnoreALeaseDisposedTwice()
        {
            // Arrange: the first holder leaves and another purchase enters.
            var gate = new RelayedPurchaseGate();
            RelayedPurchaseGate.Lease first = gate.TryEnter()!;
            first.Dispose();
            RelayedPurchaseGate.Lease? second = gate.TryEnter();

            // Act
            first.Dispose();

            // Assert: the second holder still holds the gate.
            Assert.IsNotNull(second);
            Assert.IsNull(gate.TryEnter());
        }
    }
}
