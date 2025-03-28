using Arch.Core;
using DCL.ECSComponents;
using ECS.LifeCycle.Systems;
using ECS.TestSuite;
using NUnit.Framework;

namespace ECS.LifeCycle.Tests
{
    public class ResetDirtyFlagSystemShould : UnitySystemTestBase<ResetDirtyFlagSystem<PBGltfContainer>>
    {
        [SetUp]
        public void SetUp()
        {
            system = new ResetDirtyFlagSystem<PBGltfContainer>(world);
        }

        [Test]
        public void ResetIsDirtyWhenDelayDirtyResetIsFalse()
        {
            // Create an entity with a dirty component but DelayDirtyReset is false
            var component = new PBGltfContainer
            {
                IsDirty = true,
                DelayDirtyReset = false
            };

            var entity = world.Create(component);

            // Run the system
            system.Update(0);

            // Check that IsDirty was reset
            component = world.Get<PBGltfContainer>(entity);
            Assert.IsFalse(component.IsDirty);
            Assert.IsFalse(component.DelayDirtyReset);
        }

        [Test]
        public void NotResetIsDirtyWhenDelayDirtyResetIsTrue()
        {
            // Create an entity with a dirty component and DelayDirtyReset is true
            var component = new PBGltfContainer
            {
                IsDirty = true,
                DelayDirtyReset = true
            };

            var entity = world.Create(component);

            // Run the system
            system.Update(0);

            // Check that IsDirty was NOT reset but DelayDirtyReset was
            component = world.Get<PBGltfContainer>(entity);
            Assert.IsTrue(component.IsDirty);  // Should still be dirty
            Assert.IsFalse(component.DelayDirtyReset);  // Should be reset
        }

        [Test]
        public void ResetIsDirtyAfterDelayDirtyResetWasHandled()
        {
            // Create an entity with a dirty component and DelayDirtyReset is true
            var component = new PBGltfContainer
            {
                IsDirty = true,
                DelayDirtyReset = true
            };

            var entity = world.Create(component);

            // First update - should reset DelayDirtyReset but not IsDirty
            system.Update(0);

            component = world.Get<PBGltfContainer>(entity);
            Assert.IsTrue(component.IsDirty);
            Assert.IsFalse(component.DelayDirtyReset);

            // Second update - now IsDirty should be reset
            system.Update(0);

            component = world.Get<PBGltfContainer>(entity);
            Assert.IsFalse(component.IsDirty);
            Assert.IsFalse(component.DelayDirtyReset);
        }
    }
}
