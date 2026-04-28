using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.MutePersistence;
using DCL.VoiceChat.Nearby.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents the contract of <see cref="NearbyVoiceChatNametagSystem"/>:
    /// presence/state of <see cref="VoiceChatNametagComponent"/> on avatar entities is reconciled
    /// once per tick from the current external state (state model, ActiveSpeakers, mute service).
    /// </summary>
    public class NearbyVoiceChatNametagSystemShould : UnitySystemTestBase<NearbyVoiceChatNametagSystem>
    {
        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private IRoom islandRoom;
        private FakeActiveSpeakers activeSpeakers;
        private NearbyVoiceChatStateModel stateModel;
        private INearbyMuteCache muteCache;
        private NearbyMuteService muteService;
        private Entity playerEntity;

        private readonly List<GameObject> gameObjects = new (16);

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            activeSpeakers = new FakeActiveSpeakers();

            islandRoom = Substitute.For<IRoom>();
            islandRoom.ActiveSpeakers.Returns(activeSpeakers);

            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);

            muteCache = Substitute.For<INearbyMuteCache>();
            muteService = new NearbyMuteService(muteCache, Substitute.For<INearbyMuteRepository>());

            playerEntity = world.Create();

            system = new NearbyVoiceChatNametagSystem(world, playerEntity, islandRoom, stateModel, muteService);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);
            gameObjects.Clear();

            stateModel.Dispose();
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        // ── Listening-gate (bulk teardown) ──────────────────────────

        [Test]
        public void SuppressedStateFlagsAllNearbyComponentsForRemoval()
        {
            Entity a = CreateNametaggedAvatarEntity("wallet-a", new VoiceChatNametagComponent(true, VoiceChatType.NEARBY));
            Entity b = CreateNametaggedAvatarEntity("wallet-b", new VoiceChatNametagComponent(true, VoiceChatType.NEARBY));
            Entity c = CreateNametaggedAvatarEntity("wallet-c", new VoiceChatNametagComponent(false, VoiceChatType.NEARBY));

            stateModel.Suppress(SuppressionReason.CALL);
            system.Update(0);

            AssertIsRemoving(a);
            AssertIsRemoving(b);
            AssertIsRemoving(c);
        }

        [Test]
        public void DisabledStateFlagsAllNearbyComponentsForRemoval()
        {
            Entity a = CreateNametaggedAvatarEntity("wallet-a", new VoiceChatNametagComponent(true, VoiceChatType.NEARBY));

            stateModel.Disable();
            system.Update(0);

            AssertIsRemoving(a);
        }

        [Test]
        public void SuppressedStateLeavesNonNearbyComponentsAlone()
        {
            Entity community = CreateNametaggedAvatarEntity("wallet-community",
                new VoiceChatNametagComponent(true, VoiceChatType.COMMUNITY));

            stateModel.Suppress(SuppressionReason.CALL);
            system.Update(0);

            ref var c = ref world.Get<VoiceChatNametagComponent>(community);
            Assert.That(c.Type, Is.EqualTo(VoiceChatType.COMMUNITY));
            Assert.That(c.IsRemoving, Is.False, "community/private components belong to a different handler");
        }

        [Test]
        public void SuppressedStateSkipsAvatarPass()
        {
            // Avatar present, ActiveSpeakers reports them speaking, but listening gate is closed.
            // The bulk teardown rewrites existing components (none here). The avatar pass must not run,
            // so no component is added.
            CreateAvatarEntity("wallet-a");
            activeSpeakers.Add("wallet-a");

            stateModel.Suppress(SuppressionReason.CALL);
            system.Update(0);

            int count = 0;
            world.Query(in new QueryDescription().WithAll<VoiceChatNametagComponent>(), (Entity _) => count++);
            Assert.That(count, Is.EqualTo(0), "suppressed gate must not run the add-missing pass");
        }

        // ── Update-existing (ref mutate) ────────────────────────────

        [Test]
        public void RemoteStoppedSpeakingFlagsForRemoval()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateNametaggedAvatarEntity(WALLET,
                new VoiceChatNametagComponent(isSpeaking: true, type: VoiceChatType.NEARBY));
            // ActiveSpeakers does NOT contain WALLET → predicate flips to "should not show".

            system.Update(0);

            AssertIsRemoving(e);
        }

        [Test]
        public void RemoteHushFlipUpdatesIsHushed()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateNametaggedAvatarEntity(WALLET,
                new VoiceChatNametagComponent(isSpeaking: true, type: VoiceChatType.NEARBY, isHushed: false)
                    { IsDirty = false });
            activeSpeakers.Add(WALLET);
            muteCache.IsMuted(WALLET).Returns(true);

            system.Update(0);

            ref var c = ref world.Get<VoiceChatNametagComponent>(e);
            Assert.That(c.IsHushed, Is.True);
            Assert.That(c.IsSpeaking, Is.True);
            Assert.That(c.IsDirty, Is.True, "consumer must observe a state change");
            Assert.That(c.IsRemoving, Is.False);
        }

        [Test]
        public void LocalOpenMicSpeakingFlipUpdatesIsSpeaking()
        {
            const string LOCAL = "wallet-local";
            // Replace the empty playerEntity with a full avatar entity, and re-create the system to capture it.
            world.Destroy(playerEntity);
            playerEntity = CreateAvatarEntity(LOCAL);
            world.Add(playerEntity, new VoiceChatNametagComponent(isSpeaking: false, type: VoiceChatType.NEARBY)
                { IsDirty = false });

            system = new NearbyVoiceChatNametagSystem(world, playerEntity, islandRoom, stateModel, muteService);

            // The state model is constructed at IDLE in SetUp; StartSpeaking() promotes IDLE → OPEN_MIC.
            stateModel.StartSpeaking();

            activeSpeakers.Add(LOCAL);
            system.Update(0);

            ref var c = ref world.Get<VoiceChatNametagComponent>(playerEntity);
            Assert.That(c.IsSpeaking, Is.True);
            Assert.That(c.IsDirty, Is.True);
        }

        [Test]
        public void IdempotencyNoMutationWhenSteadyState()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateNametaggedAvatarEntity(WALLET,
                new VoiceChatNametagComponent(isSpeaking: true, type: VoiceChatType.NEARBY, isHushed: false)
                    { IsDirty = false });
            activeSpeakers.Add(WALLET);
            muteCache.IsMuted(WALLET).Returns(false);

            system.Update(0); // first reconcile may dirty if any field differed
            // Now "consumer" clears the dirty flag.
            ref var c1 = ref world.Get<VoiceChatNametagComponent>(e);
            c1.IsDirty = false;

            system.Update(0); // second tick, no external change

            ref var c2 = ref world.Get<VoiceChatNametagComponent>(e);
            Assert.That(c2.IsDirty, Is.False, "steady state must not re-dirty the consumer");
            Assert.That(c2.IsRemoving, Is.False);
        }

        [Test]
        public void NonNearbyComponentNotTouched()
        {
            const string WALLET = "wallet-community";
            Entity e = CreateNametaggedAvatarEntity(WALLET,
                new VoiceChatNametagComponent(isSpeaking: true, type: VoiceChatType.COMMUNITY)
                    { IsDirty = false });
            activeSpeakers.Add(WALLET);
            muteCache.IsMuted(WALLET).Returns(true); // would otherwise flip IsHushed

            system.Update(0);

            ref var c = ref world.Get<VoiceChatNametagComponent>(e);
            Assert.That(c.Type, Is.EqualTo(VoiceChatType.COMMUNITY));
            Assert.That(c.IsHushed, Is.False, "non-nearby component must not be touched by this system");
            Assert.That(c.IsDirty, Is.False);
            Assert.That(c.IsRemoving, Is.False);
        }

        // ── Add-missing (per-avatar pass) ───────────────────────────

        [Test]
        public void RemoteSpeakerWithoutComponentGetsComponentAdded()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            activeSpeakers.Add(WALLET);

            system.Update(0);

            ref var c = ref world.Get<VoiceChatNametagComponent>(e);
            Assert.That(c.Type, Is.EqualTo(VoiceChatType.NEARBY));
            Assert.That(c.IsSpeaking, Is.True);
            Assert.That(c.IsHushed, Is.False);
            Assert.That(c.IsRemoving, Is.False);
        }

        [Test]
        public void RemoteSpeakerMutedGetsHushedComponent()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            activeSpeakers.Add(WALLET);
            muteCache.IsMuted(WALLET).Returns(true);

            system.Update(0);

            ref var c = ref world.Get<VoiceChatNametagComponent>(e);
            Assert.That(c.IsHushed, Is.True);
        }

        [Test]
        public void RemoteNotSpeakingGetsNoComponent()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            // ActiveSpeakers empty.

            system.Update(0);

            Assert.That(world.Has<VoiceChatNametagComponent>(e), Is.False);
        }

        [Test]
        public void LocalPlayerOpenMicGetsComponentAdded()
        {
            const string LOCAL = "wallet-local";
            world.Destroy(playerEntity);
            playerEntity = CreateAvatarEntity(LOCAL);
            system = new NearbyVoiceChatNametagSystem(world, playerEntity, islandRoom, stateModel, muteService);

            stateModel.StartSpeaking();
            // ActiveSpeakers empty.

            system.Update(0);

            ref var c = ref world.Get<VoiceChatNametagComponent>(playerEntity);
            Assert.That(c.Type, Is.EqualTo(VoiceChatType.NEARBY));
            Assert.That(c.IsSpeaking, Is.False);
        }

        [Test]
        public void LocalPlayerOpenMicSpeakingGetsIsSpeakingTrue()
        {
            const string LOCAL = "wallet-local";
            world.Destroy(playerEntity);
            playerEntity = CreateAvatarEntity(LOCAL);
            system = new NearbyVoiceChatNametagSystem(world, playerEntity, islandRoom, stateModel, muteService);

            stateModel.StartSpeaking();
            activeSpeakers.Add(LOCAL);

            system.Update(0);

            ref var c = ref world.Get<VoiceChatNametagComponent>(playerEntity);
            Assert.That(c.IsSpeaking, Is.True);
        }

        [Test]
        public void LocalPlayerIdleGetsNoComponent()
        {
            const string LOCAL = "wallet-local";
            world.Destroy(playerEntity);
            playerEntity = CreateAvatarEntity(LOCAL);
            system = new NearbyVoiceChatNametagSystem(world, playerEntity, islandRoom, stateModel, muteService);

            // State stays IDLE.
            activeSpeakers.Add(LOCAL); // even speaking — local must be silent in IDLE

            system.Update(0);

            Assert.That(world.Has<VoiceChatNametagComponent>(playerEntity), Is.False);
        }

        [Test]
        public void AvatarWithoutAvatarBaseSkipped()
        {
            const string WALLET = "wallet-a";
            Entity e = world.Create(new Profile(WALLET, WALLET, new Avatar()));
            activeSpeakers.Add(WALLET);

            system.Update(0);

            Assert.That(world.Has<VoiceChatNametagComponent>(e), Is.False,
                "no AvatarBase = no nametag (mirrors audio binding system semantics)");
        }

        [Test]
        public void AvatarWithDeleteEntityIntentionSkipped()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            world.Add<DeleteEntityIntention>(e);
            activeSpeakers.Add(WALLET);

            system.Update(0);

            Assert.That(world.Has<VoiceChatNametagComponent>(e), Is.False);
        }

        [Test]
        public void EmptyWalletIdSkipped()
        {
            var avatarGo = CreateTrackedGameObject("Avatar_empty");
            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var anchorGo = CreateTrackedGameObject("HeadAnchor_empty");
            anchorGo.transform.SetParent(avatarGo.transform);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, anchorGo.transform);

            Entity e = world.Create(new Profile("", "", new Avatar()), avatarBase);

            system.Update(0);

            Assert.That(world.Has<VoiceChatNametagComponent>(e), Is.False);
        }

        // ── Resurrection symmetry ──────────────────────────────────

        [Test]
        public void ResumeFromSuppressedReAddsComponentForActiveSpeaker()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            activeSpeakers.Add(WALLET);

            // 1. Tick under SUPPRESSED — bulk teardown runs but there's nothing to teardown yet
            //    (component was never created since suppressed gate skips add-missing).
            stateModel.Suppress(SuppressionReason.CALL);
            system.Update(0);
            Assert.That(world.Has<VoiceChatNametagComponent>(e), Is.False);

            // 2. Resume → IDLE, tick again — add-missing must rehydrate.
            stateModel.Resume(SuppressionReason.CALL);
            system.Update(0);

            Assert.That(world.Has<VoiceChatNametagComponent>(e), Is.True);
            ref var c = ref world.Get<VoiceChatNametagComponent>(e);
            Assert.That(c.IsSpeaking, Is.True);
            Assert.That(c.IsRemoving, Is.False);
        }

        // ── Helpers ─────────────────────────────────────────────────

        private void AssertIsRemoving(Entity e)
        {
            ref var c = ref world.Get<VoiceChatNametagComponent>(e);
            Assert.That(c.IsRemoving, Is.True);
            Assert.That(c.IsDirty, Is.True);
        }

        private Entity CreateAvatarEntity(string walletId)
        {
            var avatarGo = CreateTrackedGameObject($"Avatar_{walletId}");
            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{walletId}");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            return world.Create(new Profile(walletId, walletId, new Avatar()), avatarBase);
        }

        private Entity CreateNametaggedAvatarEntity(string walletId, VoiceChatNametagComponent nametag)
        {
            Entity e = CreateAvatarEntity(walletId);
            world.Add(e, nametag);
            return e;
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        // ── Fake IActiveSpeakers ────────────────────────────────────

        private sealed class FakeActiveSpeakers : IActiveSpeakers
        {
            private readonly HashSet<string> set = new ();
            public event System.Action Updated = delegate { };

            public int Count => set.Count;
            public IEnumerator<string> GetEnumerator() => set.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public void Add(string id)
            {
                set.Add(id);
                Updated.Invoke();
            }

            public void Remove(string id)
            {
                set.Remove(id);
                Updated.Invoke();
            }

            public void Clear()
            {
                set.Clear();
                Updated.Invoke();
            }
        }
    }
}
