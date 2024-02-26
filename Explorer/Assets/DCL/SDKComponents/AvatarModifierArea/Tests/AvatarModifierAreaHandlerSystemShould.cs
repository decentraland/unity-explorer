using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterTriggerArea.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.AvatarModifierArea.Systems;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Entity = Arch.Core.Entity;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.AvatarModifierArea.Tests
{
    public class AvatarModifierAreaHandlerSystemShould : UnitySystemTestBase<AvatarModifierAreaHandlerSystem>
    {
        private Entity entity;
        private World globalWorld;
        private Transform fakeAvatarShapeTransform;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();
            var globalWorldProxy = new WorldProxy();
            globalWorldProxy.SetWorld(globalWorld);
            system = new AvatarModifierAreaHandlerSystem(world, globalWorldProxy);

            // TODO: Fake an entity with: AvatarBase + TransformComponent + AvatarShape...
            var fakeAvatarGO = new GameObject("fake avatar GO");
            fakeAvatarShapeTransform = fakeAvatarGO.transform;
            Entity fakeAvatarEntity = globalWorld.Create();

            globalWorld.Add(fakeAvatarEntity, Substitute.For<AvatarBase>(), new TransformComponent
            {
                Transform = fakeAvatarShapeTransform,
            }, new AvatarShapeComponent());

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
        }

        [Test]
        public void SetupCharacterTriggerAreaCorrectly()
        {
            var areaSize = new Vector3
            {
                X = 1.68f,
                Y = 2.96f,
                Z = 8.66f,
            };

            var component = new PBAvatarModifierArea
            {
                Area = areaSize,
                IsDirty = true,
            };

            world.Add(entity, component);

            system.Update(1);

            Assert.IsTrue(world.TryGet(entity, out CharacterTriggerAreaComponent triggerAreaComponent));
            Assert.AreEqual(new UnityEngine.Vector3(areaSize.X, areaSize.Y, areaSize.Z), triggerAreaComponent.AreaSize);
        }

        [Test]
        public void UpdateCharacterTriggerAreaCorrectly()
        {
            var areaSize = new Vector3
            {
                X = 6.18f,
                Y = 9.26f,
                Z = 6.86f,
            };

            var component = new PBAvatarModifierArea
            {
                Area = areaSize,
                IsDirty = true,
            };

            world.Add(entity, component);

            system.Update(1);

            Assert.IsTrue(world.TryGet(entity, out CharacterTriggerAreaComponent triggerAreaComponent));
            Assert.AreEqual(new UnityEngine.Vector3(areaSize.X, areaSize.Y, areaSize.Z), triggerAreaComponent.AreaSize);

            // update component
            areaSize.X *= 2.5f;
            areaSize.Y /= 1.3f;
            areaSize.Z /= 6.6f;
            component.Area = areaSize;
            component.IsDirty = true;
            world.Set(entity, component);

            system.Update(1);

            Assert.IsTrue(world.TryGet(entity, out triggerAreaComponent));
            Assert.AreEqual(new UnityEngine.Vector3(areaSize.X, areaSize.Y, areaSize.Z), triggerAreaComponent.AreaSize);
        }

        [Test]
        public void ToggleHidingFlagCorrectly()
        {
            system.OnEnteredAvatarModifierArea(fakeAvatarShapeTransform);

            system.Update(0);

            Assert.IsTrue(globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea);

            system.OnExitedAvatarModifierArea(fakeAvatarShapeTransform);

            system.Update(0);

            Assert.IsFalse(globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea);
        }
    }
}
