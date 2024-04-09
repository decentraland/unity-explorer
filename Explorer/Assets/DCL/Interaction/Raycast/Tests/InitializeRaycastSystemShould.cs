using Arch.Core;
using DCL.ECSComponents;
using DCL.Interaction.Raycast.Components;
using DCL.Interaction.Raycast.Systems;
using ECS.TestSuite;
using NUnit.Framework;

namespace DCL.Interaction.Raycast.Tests
{
    public class InitializeRaycastSystemShould : UnitySystemTestBase<InitializeRaycastSystem>
    {

        public void Setup()
        {
            system = new InitializeRaycastSystem(world);
        }


        public void AddRaycastComponent()
        {
            Entity e = world.Create(new PBRaycast());
            AddTransformToEntity(e);

            system.Update(0);

            Assert.That(world.TryGet(e, out RaycastComponent raycastComponent), Is.True);
            Assert.That(raycastComponent.Executed, Is.False);
        }


        public void RelaunchIfChangedToContinuous()
        {
            Entity e = world.Create(new PBRaycast { Continuous = true, IsDirty = true }, new RaycastComponent { Executed = true });
            AddTransformToEntity(e);

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(e).Executed, Is.False);
        }
    }
}
