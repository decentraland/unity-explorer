using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.CharacterTriggerArea.Components;
using DCL.ECSComponents;
using DCL.Profiles;
using DCL.SDKComponents.AvatarModifierArea.Components;
using DCL.SDKComponents.AvatarModifierArea.Systems;
using DCL.Utilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Entity = Arch.Core.Entity;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.AvatarModifierArea.Tests
{
    public class AvatarModifierAreaHandlerSystemShould : UnitySystemTestBase<AvatarModifierAreaHandlerSystem>
    {
        private Entity entity;
        private Entity fakeAvatarEntity;
        private World globalWorld;
        private Transform fakeAvatarShapeTransform;
        private GameObject fakeAvatarGO;
        private GameObject fakeAvatarBaseGO;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();
            var globalWorldProxy = new WorldProxy();
            globalWorldProxy.SetWorld(globalWorld);
            system = new AvatarModifierAreaHandlerSystem(world, globalWorldProxy);

            fakeAvatarEntity = globalWorld.Create();
            fakeAvatarGO = new GameObject("fake avatar GO");
            fakeAvatarShapeTransform = fakeAvatarGO.transform;
            var fakeAvatarBaseGO = new GameObject("fake avatar BASE");
            AvatarBase fakeAvatarBase = fakeAvatarBaseGO.AddComponent<AvatarBase>();
            fakeAvatarBaseGO.transform.SetParent(fakeAvatarShapeTransform);

            globalWorld.Add(fakeAvatarEntity, fakeAvatarBase, new AvatarShapeComponent(),
                new TransformComponent
                {
                    Transform = fakeAvatarShapeTransform,
                });

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(fakeAvatarGO);
            Object.DestroyImmediate(fakeAvatarBaseGO);
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
        public void SetupAvatarModifierAreaComponentCorrectly()
        {
            var excludedId = "Ia4Ia5Cth0ulhu2Ftaghn2";

            var areaSize = new Vector3
            {
                X = 1.68f,
                Y = 2.96f,
                Z = 8.66f,
            };

            var component = new PBAvatarModifierArea
            {
                Area = areaSize,
                ExcludeIds = { excludedId },
                IsDirty = true,
            };

            world.Add(entity, component);

            system.Update(1);

            Assert.IsTrue(world.TryGet(entity, out AvatarModifierAreaComponent avatarModifierAreaComponent));
            Assert.AreEqual(1, avatarModifierAreaComponent.ExcludedIds.Count);
            Assert.IsTrue(avatarModifierAreaComponent.ExcludedIds.Contains(excludedId.ToLower()));
        }

        [Test]
        public void UpdateAvatarModifierAreaComponentCorrectly()
        {
            var excludedId = "Ia4Ia5Cth0ulhu2Ftaghn2";

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

            Assert.IsTrue(world.TryGet(entity, out AvatarModifierAreaComponent avatarModifierAreaComponent));
            Assert.AreEqual(0, avatarModifierAreaComponent.ExcludedIds.Count);

            component.ExcludeIds.Add(excludedId);
            component.IsDirty = true;
            world.Set(entity, component);
            system.Update(1);

            avatarModifierAreaComponent = world.Get<AvatarModifierAreaComponent>(entity);
            Assert.AreEqual(1, avatarModifierAreaComponent.ExcludedIds.Count);
            Assert.IsTrue(avatarModifierAreaComponent.ExcludedIds.Contains(excludedId.ToLower()));

            component.ExcludeIds.Remove(excludedId);
            component.IsDirty = true;
            world.Set(entity, component);
            system.Update(1);

            Assert.AreEqual(0, world.Get<AvatarModifierAreaComponent>(entity).ExcludedIds.Count);
        }

        [Test]
        public void ToggleHidingFlagCorrectly()
        {
            var excludedIds = new HashSet<string>();

            system.ToggleAvatarHiding(fakeAvatarShapeTransform, excludedIds, true);

            Assert.IsTrue(globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea);

            system.ToggleAvatarHiding(fakeAvatarShapeTransform, excludedIds, false);

            Assert.IsFalse(globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea);
        }

        [Test]
        public void FilterByExcludedIds()
        {
            const string FAKE_USER_ID = "666";

            globalWorld.Add(fakeAvatarEntity, new Profile(FAKE_USER_ID, "fake user", new Avatar(
                BodyShape.MALE,
                WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor())));

            var excludedIds = new HashSet<string>();
            excludedIds.Add(FAKE_USER_ID);

            system.ToggleAvatarHiding(fakeAvatarShapeTransform, excludedIds, true);

            Assert.IsFalse(globalWorld.Get<AvatarShapeComponent>(entity).HiddenByModifierArea);
        }

        [Test]
        public void HandleComponentRemoveCorrectly()
        {
            var component = new PBAvatarModifierArea
            {
                Area = new Vector3
                {
                    X = 1.68f,
                    Y = 2.96f,
                    Z = 8.66f,
                },
                IsDirty = true,
            };

            world.Add(entity, component);
            system.Update(1);

            Assert.IsTrue(world.Has<AvatarModifierAreaComponent>(entity));

            world.Remove<PBAvatarModifierArea>(entity);
            system.Update(1);

            Assert.IsFalse(world.Has<AvatarModifierAreaComponent>(entity));
        }

        [Test]
        public void HandleEntityDestructionCorrectly()
        {
            var component = new PBAvatarModifierArea
            {
                Area = new Vector3
                {
                    X = 1.68f,
                    Y = 2.96f,
                    Z = 8.66f,
                },
                IsDirty = true,
            };

            world.Add(entity, component);
            system.Update(1);

            Assert.IsTrue(world.Has<AvatarModifierAreaComponent>(entity));

            world.Add<DeleteEntityIntention>(entity);
            system.Update(1);

            Assert.IsFalse(world.Has<AvatarModifierAreaComponent>(entity));
        }
    }
}
