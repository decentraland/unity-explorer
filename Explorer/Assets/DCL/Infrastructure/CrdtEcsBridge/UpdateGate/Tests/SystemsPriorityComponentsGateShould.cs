using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using NUnit.Framework;

namespace CrdtEcsBridge.UpdateGate.Tests
{
    public class SystemsPriorityComponentsGateShould
    {
        private SystemsPriorityComponentsGate updateGate;

        [SetUp]
        public void SetUp() =>
            updateGate = new SystemsPriorityComponentsGate();

        [TearDown]
        public void TearDown() =>
            updateGate.Dispose();

        [Test]
        public void BeClosedIfNoMatchingComponentRegistered()
        {
            Assert.IsFalse(updateGate.IsOpen<SDKTransform>());

            updateGate.Open<PBTween>();
            Assert.IsFalse(updateGate.IsOpen<SDKTransform>());
        }

        [Test]
        public void BeOpenForRegisteredComponent()
        {
            updateGate.Open<SDKTransform>();
            Assert.IsTrue(updateGate.IsOpen<SDKTransform>());
        }

        [Test]
        public void BeClosedAfterFirstConsumptionOfRegisteredComponent()
        {
            updateGate.Open<SDKTransform>();
            updateGate.IsOpen<SDKTransform>();

            Assert.IsFalse(updateGate.IsOpen<SDKTransform>());
        }
    }
}
