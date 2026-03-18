using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using DCL.SDKComponents.AudioEffectZone.Components;
using DCL.SDKComponents.AudioEffectZone.Systems;
using DCL.SDKEntityTriggerArea.Components;
using DCL.VoiceChat;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NUnit.Framework;
using UnityEngine;
using Entity = Arch.Core.Entity;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.AudioEffectZone.Tests
{
    public class AudioEffectZoneHandlerSystemShould : UnitySystemTestBase<AudioEffectZoneHandlerSystem>
    {
        private Entity triggerAreaEntity;
        private Entity fakeAvatarEntity;
        private World globalWorld;
        private Transform fakeAvatarShapeTransform;
        private Collider fakeAvatarShapeCollider;
        private GameObject fakeAvatarGO;
        private GameObject fakeAvatarBaseGO;
        private GameObject fakeTriggerAreaGO;
        private AudioSource fakeAudioSource;
        private SDKEntityTriggerArea.SDKEntityTriggerArea sdkEntityTriggerArea;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();
            system = new AudioEffectZoneHandlerSystem(world, globalWorld, null);

            fakeTriggerAreaGO = new GameObject("fake trigger area");
            sdkEntityTriggerArea = fakeTriggerAreaGO.AddComponent<SDKEntityTriggerArea.SDKEntityTriggerArea>();

            fakeAvatarGO = new GameObject("fake avatar");
            fakeAvatarShapeTransform = fakeAvatarGO.transform;
            fakeAvatarShapeCollider = fakeAvatarGO.AddComponent<BoxCollider>();
            fakeAudioSource = fakeAvatarGO.AddComponent<AudioSource>();

            fakeAvatarBaseGO = new GameObject("fake avatar BASE");
            AvatarBase fakeAvatarBase = fakeAvatarBaseGO.AddComponent<AvatarBase>();
            fakeAvatarBaseGO.transform.SetParent(fakeAvatarShapeTransform);

            fakeAvatarEntity = globalWorld.Create(
                fakeAvatarBase,
                new AvatarShapeComponent(),
                new TransformComponent { Transform = fakeAvatarShapeTransform },
                new ProximityAudioSourceComponent(fakeAudioSource)
            );

            triggerAreaEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(triggerAreaEntity);
        }

        protected override void OnTearDown()
        {
            Object.DestroyImmediate(fakeAvatarGO);
            Object.DestroyImmediate(fakeAvatarBaseGO);
            Object.DestroyImmediate(fakeTriggerAreaGO);
        }

        [Test]
        public void SetupSDKEntityTriggerAreaCorrectly()
        {
            var areaSize = new Vector3 { X = 4f, Y = 3f, Z = 4f };

            var pbComponent = new PBAudioEffectZone
            {
                Area = areaSize,
                Silence = new PBAudioEffectZone.Types.SilenceEffect(),
                IsDirty = true,
            };

            world.Add(triggerAreaEntity, pbComponent);
            system.Update(0);

            Assert.IsTrue(world.TryGet(triggerAreaEntity, out SDKEntityTriggerAreaComponent triggerAreaComponent));
            Assert.AreEqual(new UnityEngine.Vector3(areaSize.X, areaSize.Y, areaSize.Z), triggerAreaComponent.AreaSize);
            Assert.IsTrue(world.Has<AudioEffectZoneComponent>(triggerAreaEntity));
        }

        [Test]
        public void UpdateAreaSizeOnDirty()
        {
            var areaSize = new Vector3 { X = 4f, Y = 3f, Z = 4f };

            var pbComponent = new PBAudioEffectZone
            {
                Area = areaSize,
                Silence = new PBAudioEffectZone.Types.SilenceEffect(),
                IsDirty = true,
            };

            world.Add(triggerAreaEntity, pbComponent);
            system.Update(0);

            areaSize = new Vector3 { X = 8f, Y = 6f, Z = 8f };
            pbComponent.Area = areaSize;
            pbComponent.IsDirty = true;
            world.Set(triggerAreaEntity, pbComponent);

            system.Update(0);

            Assert.IsTrue(world.TryGet(triggerAreaEntity, out SDKEntityTriggerAreaComponent triggerAreaComponent));
            Assert.AreEqual(new UnityEngine.Vector3(areaSize.X, areaSize.Y, areaSize.Z), triggerAreaComponent.AreaSize);
        }

        [Test]
        public void MuteAvatarOnEnterSilenceZone()
        {
            var pbComponent = new PBAudioEffectZone
            {
                Area = new Vector3 { X = 4f, Y = 3f, Z = 4f },
                Silence = new PBAudioEffectZone.Types.SilenceEffect(),
                IsDirty = true,
            };

            world.Add(triggerAreaEntity, pbComponent);
            system.Update(0);

            Assert.IsFalse(fakeAudioSource.mute);

            sdkEntityTriggerArea.OnTriggerEnter(fakeAvatarShapeCollider);
            SDKEntityTriggerAreaComponent component = world.Get<SDKEntityTriggerAreaComponent>(triggerAreaEntity);
            component.SetMonoBehaviour(sdkEntityTriggerArea);
            world.Set(triggerAreaEntity, component);

            system.Update(0);

            Assert.IsTrue(fakeAudioSource.mute);
        }

        [Test]
        public void UnmuteAvatarOnExitSilenceZone()
        {
            var pbComponent = new PBAudioEffectZone
            {
                Area = new Vector3 { X = 4f, Y = 3f, Z = 4f },
                Silence = new PBAudioEffectZone.Types.SilenceEffect(),
                IsDirty = true,
            };

            world.Add(triggerAreaEntity, pbComponent);
            system.Update(0);

            sdkEntityTriggerArea.OnTriggerEnter(fakeAvatarShapeCollider);
            SDKEntityTriggerAreaComponent component = world.Get<SDKEntityTriggerAreaComponent>(triggerAreaEntity);
            component.SetMonoBehaviour(sdkEntityTriggerArea);
            world.Set(triggerAreaEntity, component);

            system.Update(0);
            Assert.IsTrue(fakeAudioSource.mute);

            sdkEntityTriggerArea.OnTriggerExit(fakeAvatarShapeCollider);
            system.Update(0);

            Assert.IsFalse(fakeAudioSource.mute);
        }

        [Test]
        public void UnmuteOnComponentRemoval()
        {
            var pbComponent = new PBAudioEffectZone
            {
                Area = new Vector3 { X = 4f, Y = 3f, Z = 4f },
                Silence = new PBAudioEffectZone.Types.SilenceEffect(),
                IsDirty = true,
            };

            world.Add(triggerAreaEntity, pbComponent);
            system.Update(0);

            sdkEntityTriggerArea.OnTriggerEnter(fakeAvatarShapeCollider);
            SDKEntityTriggerAreaComponent component = world.Get<SDKEntityTriggerAreaComponent>(triggerAreaEntity);
            component.SetMonoBehaviour(sdkEntityTriggerArea);
            world.Set(triggerAreaEntity, component);

            system.Update(0);
            Assert.IsTrue(fakeAudioSource.mute);

            world.Remove<PBAudioEffectZone>(triggerAreaEntity);
            system.Update(0);

            Assert.IsFalse(fakeAudioSource.mute);
            Assert.IsFalse(world.Has<AudioEffectZoneComponent>(triggerAreaEntity));
        }

        [Test]
        public void UnmuteOnEntityDestruction()
        {
            var pbComponent = new PBAudioEffectZone
            {
                Area = new Vector3 { X = 4f, Y = 3f, Z = 4f },
                Silence = new PBAudioEffectZone.Types.SilenceEffect(),
                IsDirty = true,
            };

            world.Add(triggerAreaEntity, pbComponent);
            system.Update(0);

            sdkEntityTriggerArea.OnTriggerEnter(fakeAvatarShapeCollider);
            SDKEntityTriggerAreaComponent component = world.Get<SDKEntityTriggerAreaComponent>(triggerAreaEntity);
            component.SetMonoBehaviour(sdkEntityTriggerArea);
            world.Set(triggerAreaEntity, component);

            system.Update(0);
            Assert.IsTrue(fakeAudioSource.mute);

            world.Add<DeleteEntityIntention>(triggerAreaEntity);
            system.Update(0);

            Assert.IsFalse(fakeAudioSource.mute);
        }
    }
}
