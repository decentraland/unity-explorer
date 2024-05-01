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
        private Entity triggerAreaEntity;
        private Entity fakeAvatarEntity;
        private World globalWorld;
        private Transform fakeAvatarShapeTransform;
        private Collider fakeAvatarShapeCollider;
        private GameObject fakeAvatarGO;
        private GameObject fakeAvatarBaseGO;
        private GameObject fakeTriggerAreaGO;
        private CharacterTriggerArea.CharacterTriggerArea characterTriggerArea;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();
            var globalWorldProxy = new ObjectProxy<World>();
            globalWorldProxy.SetObject(globalWorld);
            system = new AvatarModifierAreaHandlerSystem(world, globalWorldProxy);

            fakeTriggerAreaGO = new GameObject("fake character area trigger");
            characterTriggerArea = fakeTriggerAreaGO.AddComponent<CharacterTriggerArea.CharacterTriggerArea>();

            fakeAvatarEntity = globalWorld.Create();
            fakeAvatarGO = new GameObject("fake avatar");
            fakeAvatarShapeTransform = fakeAvatarGO.transform;
            fakeAvatarShapeCollider = fakeAvatarGO.AddComponent<BoxCollider>();
            fakeAvatarBaseGO = new GameObject("fake avatar BASE");
            AvatarBase fakeAvatarBase = fakeAvatarBaseGO.AddComponent<AvatarBase>();
            fakeAvatarBaseGO.transform.SetParent(fakeAvatarShapeTransform);

            globalWorld.Add(fakeAvatarEntity, fakeAvatarBase, new AvatarShapeComponent(),
                new TransformComponent
                {
                    Transform = fakeAvatarShapeTransform,
                });

            triggerAreaEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(triggerAreaEntity);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(fakeAvatarGO);
            Object.DestroyImmediate(fakeAvatarBaseGO);
            Object.DestroyImmediate(fakeTriggerAreaGO);
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

            world.Add(triggerAreaEntity, component);

            system.Update(0);

            Assert.IsTrue(world.TryGet(triggerAreaEntity, out CharacterTriggerAreaComponent triggerAreaComponent));
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

            world.Add(triggerAreaEntity, component);

            system.Update(0);

            Assert.IsTrue(world.TryGet(triggerAreaEntity, out CharacterTriggerAreaComponent triggerAreaComponent));
            Assert.AreEqual(new UnityEngine.Vector3(areaSize.X, areaSize.Y, areaSize.Z), triggerAreaComponent.AreaSize);

            // update component
            areaSize.X *= 2.5f;
            areaSize.Y /= 1.3f;
            areaSize.Z /= 6.6f;
            component.Area = areaSize;
            component.IsDirty = true;
            world.Set(triggerAreaEntity, component);

            system.Update(0);

            Assert.IsTrue(world.TryGet(triggerAreaEntity, out triggerAreaComponent));
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

            world.Add(triggerAreaEntity, component);

            system.Update(0);

            Assert.IsTrue(world.TryGet(triggerAreaEntity, out AvatarModifierAreaComponent avatarModifierAreaComponent));
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

            world.Add(triggerAreaEntity, component);
            system.Update(0);

            Assert.IsTrue(world.TryGet(triggerAreaEntity, out AvatarModifierAreaComponent avatarModifierAreaComponent));
            Assert.AreEqual(0, avatarModifierAreaComponent.ExcludedIds.Count);

            component.ExcludeIds.Add(excludedId);
            component.IsDirty = true;
            world.Set(triggerAreaEntity, component);
            system.Update(0);

            avatarModifierAreaComponent = world.Get<AvatarModifierAreaComponent>(triggerAreaEntity);
            Assert.AreEqual(1, avatarModifierAreaComponent.ExcludedIds.Count);
            Assert.IsTrue(avatarModifierAreaComponent.ExcludedIds.Contains(excludedId.ToLower()));

            component.ExcludeIds.Remove(excludedId);
            component.IsDirty = true;
            world.Set(triggerAreaEntity, component);
            system.Update(0);

            Assert.AreEqual(0, world.Get<AvatarModifierAreaComponent>(triggerAreaEntity).ExcludedIds.Count);
        }

        [Test]
        public void ToggleHidingFlagCorrectly()
        {
            var excludedIds = new HashSet<string>();

            system.ToggleAvatarHiding(fakeAvatarShapeTransform, true, excludedIds);

            Assert.IsTrue(globalWorld.Get<AvatarShapeComponent>(triggerAreaEntity).HiddenByModifierArea);

            system.ToggleAvatarHiding(fakeAvatarShapeTransform, false, excludedIds);

            Assert.IsFalse(globalWorld.Get<AvatarShapeComponent>(triggerAreaEntity).HiddenByModifierArea);
        }

        [Test]
        public void FilterByExcludedIds()
        {
            const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";

            globalWorld.Add(fakeAvatarEntity, new Profile(FAKE_USER_ID, "fake user", new Avatar(
                BodyShape.MALE,
                WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor())));

            var excludedIds = new HashSet<string>();
            excludedIds.Add(FAKE_USER_ID);

            system.ToggleAvatarHiding(fakeAvatarShapeTransform, true, excludedIds);

            Assert.IsFalse(globalWorld.Get<AvatarShapeComponent>(triggerAreaEntity).HiddenByModifierArea);
        }

        [Test]
        public void HandleExcludedIdsUpdateCorrectly()
        {
            var avatar1ExcludedId = "Ia4Ia5Cth0ulhu2Ftaghn2";

            globalWorld.Add(fakeAvatarEntity, new Profile(avatar1ExcludedId.ToLower(), "fake user", new Avatar(
                BodyShape.MALE,
                WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor())));

            var avatar2ExcludedId = "Y-oG9so7Th6oH";
            Entity fakeAvatar2Entity = globalWorld.Create();
            var fakeAvatar2GO = new GameObject("fake avatar");
            Transform fakeAvatar2ShapeTransform = fakeAvatar2GO.transform;
            BoxCollider fakeAvatar2ShapeCollider = fakeAvatar2GO.AddComponent<BoxCollider>();
            var fakeAvatar2BaseGO = new GameObject("fake avatar BASE");
            AvatarBase fakeAvatar2Base = fakeAvatar2BaseGO.AddComponent<AvatarBase>();
            fakeAvatar2BaseGO.transform.SetParent(fakeAvatar2ShapeTransform);

            globalWorld.Add(fakeAvatar2Entity, fakeAvatar2Base, new AvatarShapeComponent(),
                new TransformComponent
                {
                    Transform = fakeAvatar2ShapeTransform,
                },
                new Profile(avatar2ExcludedId.ToLower(), "fake user", new Avatar(
                    BodyShape.MALE,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor())));

            var pbComponent = new PBAvatarModifierArea
            {
                Area = new Vector3
                {
                    X = 1.68f,
                    Y = 2.96f,
                    Z = 8.66f,
                },
                ExcludeIds = { avatar1ExcludedId },
                IsDirty = true,
            };

            world.Add(triggerAreaEntity, pbComponent);
            system.Update(0);

            Assert.IsTrue(world.Has<AvatarModifierAreaComponent>(triggerAreaEntity));

            // "Enter" Avatar-1 in trigger area
            characterTriggerArea.OnTriggerEnter(fakeAvatarShapeCollider);

            // "Enter" Avatar-2 in trigger area
            characterTriggerArea.OnTriggerEnter(fakeAvatar2ShapeCollider);

            CharacterTriggerAreaComponent component = world.Get<CharacterTriggerAreaComponent>(triggerAreaEntity);
            component.ForceAssignArea(characterTriggerArea);
            world.Set(triggerAreaEntity, component);

            system.Update(0);

            Assert.IsFalse(globalWorld.Get<AvatarShapeComponent>(fakeAvatarEntity).HiddenByModifierArea);
            Assert.IsTrue(globalWorld.Get<AvatarShapeComponent>(fakeAvatar2Entity).HiddenByModifierArea);

            // Update component excluded ids to remove avatar 1 and add avatar 2 exclusion
            pbComponent = new PBAvatarModifierArea
            {
                Area = new Vector3
                {
                    X = 1.68f,
                    Y = 2.96f,
                    Z = 8.66f,
                },
                ExcludeIds = { avatar2ExcludedId },
                IsDirty = true,
            };

            world.Set(triggerAreaEntity, pbComponent);

            system.Update(0);

            // Check now avatar 1 is shown and avatar 2 is hidden
            Assert.IsTrue(globalWorld.Get<AvatarShapeComponent>(fakeAvatarEntity).HiddenByModifierArea);
            Assert.IsFalse(globalWorld.Get<AvatarShapeComponent>(fakeAvatar2Entity).HiddenByModifierArea);

            // Cleanup
            Object.DestroyImmediate(fakeAvatar2GO);
            Object.DestroyImmediate(fakeAvatar2BaseGO);
        }

        // TODO: leeaving scene ???

        [Test]
        public void HandleComponentRemoveCorrectly()
        {
            var pbComponent = new PBAvatarModifierArea
            {
                Area = new Vector3
                {
                    X = 1.68f,
                    Y = 2.96f,
                    Z = 8.66f,
                },
                IsDirty = true,
            };

            world.Add(triggerAreaEntity, pbComponent);
            system.Update(0);

            Assert.IsTrue(world.Has<AvatarModifierAreaComponent>(triggerAreaEntity));

            // "Enter" trigger area
            characterTriggerArea.OnTriggerEnter(fakeAvatarShapeCollider);
            CharacterTriggerAreaComponent component = world.Get<CharacterTriggerAreaComponent>(triggerAreaEntity);
            component.ForceAssignArea(characterTriggerArea);
            world.Set(triggerAreaEntity, component);

            system.Update(0);

            Assert.IsTrue(globalWorld.Get<AvatarShapeComponent>(fakeAvatarEntity).HiddenByModifierArea);

            // Remove component
            world.Remove<PBAvatarModifierArea>(triggerAreaEntity);
            system.Update(0);

            // Check area effect is reset
            Assert.IsFalse(globalWorld.Get<AvatarShapeComponent>(fakeAvatarEntity).HiddenByModifierArea);

            Assert.IsFalse(world.Has<AvatarModifierAreaComponent>(triggerAreaEntity));
        }

        [Test]
        public void HandleEntityDestructionCorrectly()
        {
            var pbComponent = new PBAvatarModifierArea
            {
                Area = new Vector3
                {
                    X = 1.68f,
                    Y = 2.96f,
                    Z = 8.66f,
                },
                IsDirty = true,
            };

            world.Add(triggerAreaEntity, pbComponent);
            system.Update(0);

            Assert.IsTrue(world.Has<AvatarModifierAreaComponent>(triggerAreaEntity));

            // "Enter" trigger area
            characterTriggerArea.OnTriggerEnter(fakeAvatarShapeCollider);
            CharacterTriggerAreaComponent component = world.Get<CharacterTriggerAreaComponent>(triggerAreaEntity);
            component.ForceAssignArea(characterTriggerArea);
            world.Set(triggerAreaEntity, component);

            system.Update(0);

            Assert.IsTrue(globalWorld.Get<AvatarShapeComponent>(fakeAvatarEntity).HiddenByModifierArea);

            // Flag entity for destruction
            world.Add<DeleteEntityIntention>(triggerAreaEntity);
            system.Update(0);

            // Check area effect is reset
            Assert.IsFalse(globalWorld.Get<AvatarShapeComponent>(fakeAvatarEntity).HiddenByModifierArea);
        }
    }
}
