using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Profiles;
using DCL.SDKComponents.VoiceChatModifierArea.Systems;
using DCL.SDKEntityTriggerArea.Components;
using DCL.SDKEntityTriggerArea.Systems;
using DCL.VoiceChat.Nearby;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.VoiceChatModifierArea.Tests
{
    public class VoiceChatModifierAreaHandlerSystemShould : UnitySystemTestBase<VoiceChatModifierAreaHandlerSystem>
    {
        private const string USER_ID = "0xspeaker";

        private World globalWorld = null!;
        private Entity triggerAreaEntity;
        private Entity avatarEntity;
        private GameObject avatarGO = null!;
        private GameObject triggerAreaGO = null!;
        private Collider avatarCollider = null!;
        private SDKEntityTriggerArea.SDKEntityTriggerArea sdkEntityTriggerArea = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            system = new VoiceChatModifierAreaHandlerSystem(world, globalWorld);

            triggerAreaGO = new GameObject("voice area trigger");
            sdkEntityTriggerArea = triggerAreaGO.AddComponent<SDKEntityTriggerArea.SDKEntityTriggerArea>();

            avatarGO = new GameObject("avatar");
            avatarCollider = avatarGO.AddComponent<BoxCollider>();
            var avatarBaseGO = new GameObject("avatar base");
            avatarBaseGO.transform.SetParent(avatarGO.transform);
            AvatarBase avatarBase = avatarBaseGO.AddComponent<AvatarBase>();

            avatarEntity = globalWorld.Create(avatarBase, new Profile(UserId.New(USER_ID).Unwrap(), USER_ID, new Avatar()));
            triggerAreaEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(triggerAreaEntity);
        }

        protected override void OnTearDown()
        {
            Object.DestroyImmediate(avatarGO);
            Object.DestroyImmediate(triggerAreaGO);
            World.Destroy(globalWorld);
        }

        [Test]
        public void SetUpTheTriggerArea()
        {
            world.Add(triggerAreaEntity, Area());

            system.Update(0);

            Assert.IsTrue(world.TryGet(triggerAreaEntity, out SDKEntityTriggerAreaComponent triggerArea));
            Assert.AreEqual(new UnityEngine.Vector3(4, 3, 4), triggerArea.AreaSize);
        }

        [Test]
        public void ApplyTheAreaToAnAvatarThatEnters()
        {
            AddAreaAndEnter(Area(volumeScale: 0.5f, maxDistance: 3f, mute: true, isolate: true));

            VoiceChatAreaModifier modifier = globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity).Modifiers[0];
            Assert.AreEqual(0.5f, modifier.VolumeScale);
            Assert.AreEqual(3f, modifier.MaxDistance);
            Assert.IsTrue(modifier.Mute);
            Assert.IsTrue(modifier.Isolate);
        }

        [Test]
        public void ApplyDefaultsForUnsetFields()
        {
            AddAreaAndEnter(Area());

            VoiceChatAreaModifier modifier = globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity).Modifiers[0];
            Assert.AreEqual(1f, modifier.VolumeScale);
            Assert.AreEqual(float.PositiveInfinity, modifier.MaxDistance);
            Assert.IsFalse(modifier.Mute);
            Assert.IsFalse(modifier.Isolate);
        }

        [Test]
        public void KeepTheMaxDistanceAboveZero()
        {
            AddAreaAndEnter(Area(maxDistance: 0f));

            Assert.Greater(globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity).Modifiers[0].MaxDistance, 0f);
        }

        [Test]
        public void RemoveTheAreaWhenTheAvatarExits()
        {
            AddAreaAndEnter(Area(mute: true));

            sdkEntityTriggerArea.OnTriggerExit(avatarCollider);
            system.Update(0);

            Assert.IsEmpty(globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity).Modifiers);
        }

        [Test]
        public void SkipExcludedAvatars()
        {
            PBVoiceChatModifierArea area = Area(mute: true);
            area.ExcludeIds.Add(USER_ID.ToUpper());

            AddAreaAndEnter(area);

            Assert.IsFalse(globalWorld.Has<VoiceChatAreaModifiersComponent>(avatarEntity));
        }

        [Test]
        public void ReapplyTheAreaWhenItChanges()
        {
            PBVoiceChatModifierArea area = Area(volumeScale: 0.5f);
            AddAreaAndEnter(area);

            area.VolumeScale = 0.2f;
            area.IsDirty = true;
            system.Update(0);

            VoiceChatAreaModifiersComponent component = globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity);
            Assert.AreEqual(1, component.Modifiers.Count);
            Assert.AreEqual(0.2f, component.Modifiers[0].VolumeScale);
        }

        [Test]
        public void RemoveTheAreaWhenTheAreaEntityIsDestroyed()
        {
            AddAreaAndEnter(Area(mute: true));

            world.Add<DeleteEntityIntention>(triggerAreaEntity);
            system.Update(0);

            Assert.IsEmpty(globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity).Modifiers);
        }

        [Test]
        public void RemoveTheAreaWhenTheComponentIsRemoved()
        {
            AddAreaAndEnter(Area(mute: true));

            world.Remove<PBVoiceChatModifierArea>(triggerAreaEntity);
            system.Update(0);

            Assert.IsEmpty(globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity).Modifiers);
        }

        [Test]
        public void RemoveTheAreaWhenTheSceneWorldIsFinalized()
        {
            AddAreaAndEnter(Area(mute: true));

            system.FinalizeComponents(world.Query(new QueryDescription()));

            Assert.IsEmpty(globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity).Modifiers);
        }

        [Test]
        public void KeepTheTriggerAreaThroughCleanupWhileTheAreaIsActive()
        {
            AddAreaAndEnter(Area(mute: true));
            var cleanupSystem = new SDKEntityTriggerAreaCleanupSystem(world, Substitute.For<IComponentPool<SDKEntityTriggerArea.SDKEntityTriggerArea>>());

            cleanupSystem.Update(0);

            Assert.IsTrue(world.Has<SDKEntityTriggerAreaComponent>(triggerAreaEntity));
            Assert.AreEqual(1, globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity).Modifiers.Count);
        }

        [Test]
        public void ReleaseTheTriggerAreaOnlyAfterTheAreaIsRemoved()
        {
            AddAreaAndEnter(Area(mute: true));
            var cleanupSystem = new SDKEntityTriggerAreaCleanupSystem(world, Substitute.For<IComponentPool<SDKEntityTriggerArea.SDKEntityTriggerArea>>());
            world.Remove<PBVoiceChatModifierArea>(triggerAreaEntity);

            cleanupSystem.Update(0);
            Assert.IsTrue(world.Has<SDKEntityTriggerAreaComponent>(triggerAreaEntity));

            system.Update(0);
            cleanupSystem.Update(0);

            Assert.IsEmpty(globalWorld.Get<VoiceChatAreaModifiersComponent>(avatarEntity).Modifiers);
            Assert.IsFalse(world.Has<SDKEntityTriggerAreaComponent>(triggerAreaEntity));
        }

        private void AddAreaAndEnter(PBVoiceChatModifierArea area)
        {
            world.Add(triggerAreaEntity, area);
            system.Update(0);

            ref SDKEntityTriggerAreaComponent triggerArea = ref world.Get<SDKEntityTriggerAreaComponent>(triggerAreaEntity);
            triggerArea.SetMonoBehaviour(sdkEntityTriggerArea);

            sdkEntityTriggerArea.OnTriggerEnter(avatarCollider);
            system.Update(0);
        }

        private static PBVoiceChatModifierArea Area(float? volumeScale = null, float? maxDistance = null, bool? mute = null, bool? isolate = null)
        {
            var area = new PBVoiceChatModifierArea { Area = new Vector3 { X = 4, Y = 3, Z = 4 }, IsDirty = true };

            if (volumeScale.HasValue) area.VolumeScale = volumeScale.Value;
            if (maxDistance.HasValue) area.MaxDistance = maxDistance.Value;
            if (mute.HasValue) area.Mute = mute.Value;
            if (isolate.HasValue) area.Isolate = isolate.Value;

            return area;
        }
    }
}
