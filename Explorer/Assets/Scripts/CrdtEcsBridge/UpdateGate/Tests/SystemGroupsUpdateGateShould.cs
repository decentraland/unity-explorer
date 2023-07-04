using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.UnityBridge;
using NUnit.Framework;
using System;

namespace CrdtEcsBridge.UpdateGate.Tests
{
    public class SystemGroupsUpdateGateShould
    {
        private SystemGroupsUpdateGate updateGate;

        [SetUp]
        public void SetUp()
        {
            updateGate = new SystemGroupsUpdateGate();
        }

        [Test]
        public void OpenAllSystemGroups()
        {
            updateGate.Open();

            CollectionAssert.AreEquivalent(new[]
            {
                typeof(InitializationSystemGroup),
                typeof(SimulationSystemGroup),
                typeof(PresentationSystemGroup),
                typeof(PhysicsSystemGroup),
                typeof(PostPhysicsSystemGroup),
                typeof(PostRenderingSystemGroup),
            }, updateGate.OpenGroups);
        }

        [Test]
        [TestCase(typeof(InitializationSystemGroup))]
        [TestCase(typeof(SimulationSystemGroup))]
        [TestCase(typeof(PresentationSystemGroup))]
        [TestCase(typeof(PhysicsSystemGroup))]
        [TestCase(typeof(PostPhysicsSystemGroup))]
        [TestCase(typeof(PostRenderingSystemGroup))]
        public void CloseGroupOnInvocation(Type systemGroup)
        {
            updateGate.Open();
            updateGate.ShouldThrottle(systemGroup, new TimeProvider.Info());
            Assert.That(updateGate.OpenGroups, Does.Not.Contains(systemGroup));
        }
    }
}
