using Arch.Core;
using DCL.ECSComponents;
using DCL.Interaction.Raycast.Components;
using DCL.Interaction.Raycast.Systems;
using DCL.Optimization.Pools;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;

namespace DCL.Interaction.Raycast.Tests
{
    public class InitializeRaycastSystemShould : UnitySystemTestBase<InitializeRaycastSystem>
    {
        [SetUp]
        public void Setup()
        {
            IComponentPool<PBRaycastResult> pbRaycastResultPool = Substitute.For<IComponentPool<PBRaycastResult>>();

            system = new InitializeRaycastSystem(world, pbRaycastResultPool);
        }

        [Test]
        public void AddRaycastComponent()
        {
            Entity e = world.Create(new PBRaycast());
            AddTransformToEntity(e);

            system.Update(0);

            Assert.That(world.TryGet(e, out RaycastComponent raycastComponent), Is.True);
            Assert.That(raycastComponent.Executed, Is.False);
        }

        [Test]
        public void RelaunchIfChangedToContinuous()
        {
            Entity e = world.Create(new PBRaycast { Continuous = true, IsDirty = true }, new RaycastComponent { Executed = true });
            AddTransformToEntity(e);

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(e).Executed, Is.False);
        }
    }
}
