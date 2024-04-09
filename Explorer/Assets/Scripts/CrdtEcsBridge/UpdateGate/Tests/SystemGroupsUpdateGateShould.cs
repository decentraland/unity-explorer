using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.UnityBridge;
using NUnit.Framework;
using System;

namespace CrdtEcsBridge.UpdateGate.Tests
{
    public class SystemGroupsUpdateGateShould
    {
        private SystemGroupsUpdateGate updateGate;


        public void SetUp()
        {
            updateGate = new SystemGroupsUpdateGate();
        }


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








        public void CloseGroupOnInvocation(Type systemGroup)
        {
            updateGate.Open();
            updateGate.ShouldThrottle(systemGroup, new TimeProvider.Info());
            Assert.That(updateGate.OpenGroups, Does.Not.Contains(systemGroup));
        }
    }
}
