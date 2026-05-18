using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Friends.UserBlocking;
using DCL.Profiles;
using DCL.SceneBannedUsers;
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using NSubstitute;
using NUnit.Framework;
using RichTypes;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents <see cref="NearbyAudioCleanupSystem"/> contract:
    ///
    /// - Detection marks doomed audio entities with <see cref="DeleteEntityIntention"/>; physical destruction is delegated to
    ///   <see cref="ECS.LifeCycle.Systems.DestroyEntitiesSystem"/> (deliberately not in this test rig).
    /// - Pull-based detection: per tick, four triggers per entity — avatar gone, stream gone, identity blocked, listening gate.
    /// - Teardown reacts to <see cref="DeleteEntityIntention"/>: disposes the <see cref="LivekitAudioSource"/>
    ///   (Stop → Free → SafeDestroyGameObject) and removes the <c>(walletId, sid) → entity</c> binding.
    /// - Tests assert the system's own contribution: the entity is marked + the source is disposed + the binding is removed.
    ///   They deliberately do NOT assert <see cref="World.IsAlive"/>, since this system no longer owns entity destruction.
    /// - <see cref="NearbyAudioCleanupSystem.Dispose"/> disposes any survivors and clears bindings.
    /// </summary>
    public class NearbyAudioCleanupSystemShould : UnitySystemTestBase<NearbyAudioCleanupSystem>
    {
        private const string PARTICIPANT_A = "wallet-alice";
        private const string SID_1 = "sid-1";

        private static readonly QueryDescription LIVE_AUDIO_QUERY =
            new QueryDescription().WithAll<NearbyAudioSourceComponent>().WithNone<DeleteEntityIntention>();

        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private FakeStreamRegistry registry = null!;
        private HashSet<StreamKey> bindings = null!;
        private IUserBlockingCache userBlockingCache = null!;
        private NearbyVoiceChatStateModel stateModel = null!;
        private VoiceChatConfiguration configuration = null!;
        private NearbyAudioSourceFactory sourceFactory = null!;
        private readonly List<GameObject> gameObjects = new (16);

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            registry = new FakeStreamRegistry();
            bindings = new HashSet<StreamKey>();
            userBlockingCache = Substitute.For<IUserBlockingCache>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            configuration = ScriptableObject.CreateInstance<VoiceChatConfiguration>();
            sourceFactory = new NearbyAudioSourceFactory(configuration);

            system = new NearbyAudioCleanupSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory, RoomMetadataCurrentScene.CreateForTest());
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();
            bindings.Clear();
            stateModel.Dispose();

            if (configuration != null) Object.DestroyImmediate(configuration);

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        // ── Trigger #1: avatar gone ─────────────────────────────────

        [Test]
        public void DeadAvatarEntityCausesCleanup()
        {
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Destroy(avatarEntity);

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void AvatarWithDeleteEntityIntentionCausesCleanup()
        {
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Add<DeleteEntityIntention>(avatarEntity);

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        // ── Trigger #2: stream gone ─────────────────────────────────

        [Test]
        public void RegistryMissingWalletCausesCleanup()
        {
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            registry.RemoveAll(PARTICIPANT_A);

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void RegistryMissingSidCausesCleanup()
        {
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            registry.Add(PARTICIPANT_A, "sid-other"); // wallet still in registry, but bound sid is gone
            registry.RemoveSid(PARTICIPANT_A, SID_1);

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        // ── Trigger #3: blocked identity ────────────────────────────

        [Test]
        public void BlockedIdentityCausesCleanup()
        {
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            userBlockingCache.UserIsBlocked(PARTICIPANT_A).Returns(true);

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        // ── Trigger #4: listening gate ──────────────────────────────

        [Test]
        public void SuppressedStateTearsDownAllAudioEntities()
        {
            const int COUNT = 3;
            var seeded = new List<(Entity audioEntity, LivekitAudioSource source, string wallet)>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                seeded.Add((audioEntity, source, $"wallet-{i}"));
            }

            stateModel.Suppress(SuppressionReason.CALL);

            system.Update(0);

            Assert.That(world.CountEntities(in LIVE_AUDIO_QUERY), Is.EqualTo(0), "all audio entities must be marked");
            Assert.That(bindings, Is.Empty);
            foreach ((Entity audioEntity, LivekitAudioSource source, _) in seeded)
            {
                Assert.That(world.Has<DeleteEntityIntention>(audioEntity), Is.True);
                AssertSourceTornDown(source);
            }
        }

        [Test]
        public void DisabledStateTearsDownAllAudioEntities()
        {
            const int COUNT = 3;
            var seeded = new List<(Entity audioEntity, LivekitAudioSource source, string wallet)>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                seeded.Add((audioEntity, source, $"wallet-{i}"));
            }

            stateModel.Disable();

            system.Update(0);

            Assert.That(world.CountEntities(in LIVE_AUDIO_QUERY), Is.EqualTo(0));
            Assert.That(bindings, Is.Empty);
            foreach ((Entity audioEntity, LivekitAudioSource source, _) in seeded)
            {
                Assert.That(world.Has<DeleteEntityIntention>(audioEntity), Is.True);
                AssertSourceTornDown(source);
            }
        }

        // ── Compound ────────────────────────────────────────────────

        [Test]
        public void BothTriggersPresentResultInSingleTeardown()
        {
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Destroy(avatarEntity);
            registry.RemoveAll(PARTICIPANT_A);

            Assert.DoesNotThrow(() => system.Update(0));

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void MassCleanupOnDisconnected()
        {
            const int COUNT = 10;
            var seeded = new List<(Entity audioEntity, LivekitAudioSource source)>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                seeded.Add((audioEntity, source));
            }

            registry.ClearAll();

            system.Update(0);

            Assert.That(world.CountEntities(in LIVE_AUDIO_QUERY), Is.EqualTo(0));
            foreach ((Entity audioEntity, LivekitAudioSource source) in seeded)
            {
                Assert.That(world.Has<DeleteEntityIntention>(audioEntity), Is.True);
                AssertSourceTornDown(source);
            }
        }

        // ── A5.2 / B2.1: archetype short-circuit via StreamingAudioComponent ───

        [Test]
        public void FlagsAudioEntityWhenAvatarLosesStreamingTag()
        {
            // Cheap shortcut — when the avatar is alive, not flagged for deletion, but has lost
            // its StreamingAudioComponent (Bridge dropped it because the registry no longer reports
            // sids for that walletId), the audio entity must be doomed without consulting the
            // registry or the blocking cache.
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<NearbyAudioStreamerComponent>(avatarEntity);

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void KeepsAudioEntityWhenMarkerPresentAndRegistryAlive()
        {
            // Steady state — marker on, registry has the sid, not blocked, avatar alive.
            // The cleanup query must not flag this entity. This is the dominant per-frame path.
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);

            system.Update(0);

            Assert.That(world.Has<DeleteEntityIntention>(audioEntity), Is.False,
                "healthy steady-state entity must not be flagged");
            Assert.That(world.IsAlive(audioEntity), Is.True);
            Assert.That(source == null, Is.False, "LivekitAudioSource must remain alive");
            Assert.That(bindings.Contains(new StreamKey(PARTICIPANT_A, SID_1)), Is.True);
        }

        [Test]
        public void FlagsLosingSidAudioEntityWhenResolverPicksSibling()
        {
            // Ghost-vs-winner — the registry still holds two candidate sids for the wallet (LiveKit
            // dropped an unsubscribe), but the resolver now picks only one. Both audio entities exist
            // as a hangover from before the flip (sid-1 was the previous pick, sid-2 just won).
            // Per-sid doom arrives via !registry.IsActiveSid(comp.Key.identity, comp.Key.sid) — the
            // demoted ghost sid is reaped, the winner survives, the marker stays (it tracks the wallet,
            // not the sid).
            const string SID_2 = "sid-2";
            (Entity audioEntity1, Entity avatarEntity, LivekitAudioSource source1) = SeedBinding(PARTICIPANT_A, SID_1);

            // Sid-2 entity coexists; both audio entities share the same avatar (and its marker).
            var key2 = new StreamKey(PARTICIPANT_A, SID_2);
            LivekitAudioSource source2 = CreateLivekitAudioSource(key2);
            Entity audioEntity2 = world.Create(new NearbyAudioSourceComponent(key2, avatarEntity, source2));
            bindings.Add(key2);

            // Resolver flips to sid-2; sid-1 stays in the registry as a ghost.
            registry.SetActiveSid(PARTICIPANT_A, SID_2);

            system.Update(0);

            AssertCleanedUp(audioEntity1, source1, PARTICIPANT_A, SID_1);
            Assert.That(world.Has<DeleteEntityIntention>(audioEntity2), Is.False,
                "winning sid must remain alive — only the demoted ghost is reaped");
            Assert.That(source2 == null, Is.False);
        }

        [Test]
        public void GhostSidLosingResolverPickCausesCleanup()
        {
            // Test 9 from the spec — the audio entity's sid is no longer the resolver's pick.
            // Distinct from RegistryMissingSidCausesCleanup: here the sid still EXISTS in the registry
            // (HasAudioStream=true), it just lost the active-pick race. The !IsActiveSid predicate
            // must reap it regardless of whether the registry still indexes the sid.
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);

            // Resolver demotes sid-1 in favour of a fresher candidate; HasAudioStream stays true
            // (registry still holds candidates for the identity).
            const string SID_FRESH = "sid-fresh";
            registry.SetActiveSid(PARTICIPANT_A, SID_FRESH);

            // Sanity-check the precondition the test depends on.
            Assert.That(registry.HasAudioStream(PARTICIPANT_A), Is.True,
                "precondition: identity still indexed (only the active pick changed)");
            Assert.That(registry.IsActiveSid(PARTICIPANT_A, SID_1), Is.False,
                "precondition: bound sid is no longer the active one");
            // The Asserts above bumped IsActiveSidCallCount via the precondition; reset before the system tick.
            registry.ResetCallCounters();

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void FlagsAudioEntityWhenAvatarHasDeleteIntentionButMarkerRemains()
        {
            // F1-deliberate invariant: NearbyLivekitBridgeSystem.UpdateStreaming filters with
            // [None<DeleteEntityIntention>], so a doomed avatar keeps its marker until physical
            // destruction. Cleanup must catch this via the World.Has<DeleteEntityIntention>(avatar)
            // clause, not the marker-absence clause.
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Add<DeleteEntityIntention>(avatarEntity);
            // marker intentionally NOT removed — F1 contract

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
            Assert.That(world.Has<NearbyAudioStreamerComponent>(avatarEntity), Is.True,
                "Bridge's [None<DeleteEntityIntention>] filter prevents component removal on a doomed avatar");
        }

        [Test]
        public void DoesNotQueryRegistryWhenMarkerAbsentPathDoomsEntity()
        {
            // Optional sanity — proves the cheap shortcut actually short-circuits the registry call.
            // If the marker-absence clause fires, registry.IsActiveSid must NOT be invoked.
            (_, Entity avatarEntity, _) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<NearbyAudioStreamerComponent>(avatarEntity);

            registry.ResetCallCounters();

            system.Update(0);

            Assert.That(registry.IsActiveSidCallCount, Is.EqualTo(0),
                "marker-absence must short-circuit before the registry lookup");
            Assert.That(userBlockingCache.ReceivedCalls(), Is.Empty,
                "marker-absence must short-circuit before the blocking cache lookup");
        }

        // ── A1: InAudibleRangeTag absence joins the doom shortcut ───

        [Test]
        public void FlagsAudioEntityWhenAvatarLosesAudibleRangeTag()
        {
            // A1 adds a fourth clause to the cheap-shortcut chain — when AudibleRangeMarker drops
            // InAudibleRangeTag (avatar crossed 22 m outward) Cleanup must doom the audio entity
            // in the same frame, before the registry / blocking-cache fallbacks fire.
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<InAudibleRangeTag>(avatarEntity);

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void DoesNotInvokeRegistryWhenAudibleRangeAbsentPathDooms()
        {
            // Cost-shortcut semantics — the marker-absence clause must short-circuit before the
            // registry lookup fires. Mirrors the streaming-tag short-circuit guard from A5.2.
            (_, Entity avatarEntity, _) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<InAudibleRangeTag>(avatarEntity);

            registry.ResetCallCounters();

            system.Update(0);

            Assert.That(registry.IsActiveSidCallCount, Is.EqualTo(0),
                "InAudibleRangeTag absence must short-circuit before the registry lookup");
            Assert.That(userBlockingCache.ReceivedCalls(), Is.Empty,
                "InAudibleRangeTag absence must short-circuit before the blocking cache lookup");
        }

        // ── Idempotency ─────────────────────────────────────────────

        [Test]
        public void IdleTickWithNoTriggersDoesNotMutateWorld()
        {
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);

            system.Update(0);
            system.Update(0);

            Assert.That(world.IsAlive(audioEntity), Is.True);
            Assert.That(world.Has<DeleteEntityIntention>(audioEntity), Is.False);
            Assert.That(source == null, Is.False);
            Assert.That(bindings.Contains(new StreamKey(PARTICIPANT_A, SID_1)), Is.True);
            Assert.That(world.CountEntities(in LIVE_AUDIO_QUERY), Is.EqualTo(1));
        }

        // ── Dispose (world tear-down) ───────────────────────────────

        [Test]
        public void DisposeDestroysAllRemainingSourcesAndClearsBindings()
        {
            const int COUNT = 3;
            var sources = new List<LivekitAudioSource>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (_, _, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                sources.Add(source);
            }

            // Even with all triggers cold, dispose must release every source — covers world tear-down survivors.
            system.Dispose();

            foreach (LivekitAudioSource source in sources)
                AssertSourceTornDown(source);

            Assert.That(bindings, Is.Empty);
        }

        // ── Helpers ─────────────────────────────────────────────────

        private void AssertCleanedUp(Entity audioEntity, LivekitAudioSource source, string walletId, string sid)
        {
            Assert.That(world.IsAlive(audioEntity), Is.True, "entity destruction is delegated to DestroyEntitiesSystem and is out of scope here");
            Assert.That(world.Has<DeleteEntityIntention>(audioEntity), Is.True, "audio entity must be marked for deletion");
            AssertSourceTornDown(source, "LivekitAudioSource must be torn down (destroyed in legacy path, parked inactive in pool path)");
            Assert.That(bindings.Contains(new StreamKey(walletId, sid)), Is.False, "binding must be removed");
        }

        // A2 made source teardown reference-stable: the pool keeps the GO alive after Dispose. Both
        // paths still satisfy "no further audio-thread or main-thread tick" — fold them into one
        // helper so the existing trigger tests do not have to know which path is active. The pool's
        // SetActive(false) is what fires OnDisable on the wrapper (drops the audio-config subscription)
        // and on AudioSource (releases its voice slot); GameObject.activeSelf=false is the single observable.
        private static void AssertSourceTornDown(LivekitAudioSource source, string? message = null)
        {
            if (source == null) return; // legacy path — Object.Destroy ran

            Assert.That(source.gameObject.activeSelf, Is.False, message ?? "pooled source must be inactive");
        }

        private (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) SeedBinding(string walletId, string sid)
        {
            Entity avatarEntity = CreateAvatarEntity(walletId);
            // After A5.2 the cleanup shortcut treats streaming-marker absence as a doom signal;
            // A1 adds InAudibleRangeTag absence as a fourth shortcut clause. Realistic state for
            // a live audio entity is both markers present — Bridge applied StreamingAudioComponent
            // and AudibleRangeMarker applied InAudibleRangeTag before Binding spawned the entity.
            // Pair all three in the seed so existing trigger tests exercise the intended fallbacks
            // (!IsActiveSid / UserIsBlocked / lifecycle), not the marker-absence shortcuts by accident.
            world.Add(avatarEntity, new NearbyAudioStreamerComponent(sid));
            world.Add<InAudibleRangeTag>(avatarEntity);
            registry.Add(walletId, sid);

            var key = new StreamKey(walletId, sid);
            LivekitAudioSource source = CreateLivekitAudioSource(key);
            Entity audioEntity = world.Create(new NearbyAudioSourceComponent(key, avatarEntity, source));
            bindings.Add(key);

            return (audioEntity, avatarEntity, source);
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

        private LivekitAudioSource CreateLivekitAudioSource(StreamKey key)
        {
            LivekitAudioSource source = sourceFactory.Create(key, Weak<AudioStream>.Null);
            gameObjects.Add(source.gameObject);
            return source;
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        // ── Fake stream registry (mutable, for cleanup-trigger driving) ─

        private sealed class FakeStreamRegistry : INearbyAudioStreamRegistry
        {
            // Per-wallet active sid map. Cleanup interrogates the registry via IsActiveSid;
            // the resolver-dedup contract guarantees at most one active sid per identity, so a
            // plain wallet → sid map suffices.
            private readonly Dictionary<string, string> activeSidByIdentity = new ();

            // Per-wallet "registry has at least one sid for this identity" flag, decoupled from
            // active-pick state so tests can model the all-zeros window (HasAudioStream=true,
            // GetActiveSid=null) and ghost-demotion (sid exists in the registry but lost the pick).
            private readonly HashSet<string> hasAudioStreamByIdentity = new ();

            // Call counters for asserting short-circuit behaviour. NSubstitute is overkill here —
            // INearbyAudioStreamRegistry has a hand-rolled fake to drive trigger combinations,
            // and a single counter keeps the cleanup-shortcut test honest.
            public int IsActiveSidCallCount { get; private set; }

            public void ResetCallCounters() => IsActiveSidCallCount = 0;

            public void Add(string walletId, string sid)
            {
                // Mirrors production: an Add publishes a new active pick. Tests that need to model
                // ghost-vs-winner can use SetActiveSid / DemoteToGhost directly.
                hasAudioStreamByIdentity.Add(walletId);
                activeSidByIdentity[walletId] = sid;
            }

            public void RemoveAll(string walletId)
            {
                hasAudioStreamByIdentity.Remove(walletId);
                activeSidByIdentity.Remove(walletId);
            }

            public void RemoveSid(string walletId, string sid)
            {
                // Mirrors production semantics — when the registry drops the sid that was the active
                // pick, the identity has no winner anymore. If it was not the active pick (i.e. a
                // ghost), HasAudioStream stays unchanged.
                if (activeSidByIdentity.TryGetValue(walletId, out string? active) && active == sid)
                {
                    activeSidByIdentity.Remove(walletId);
                    hasAudioStreamByIdentity.Remove(walletId);
                }
            }

            /// <summary>
            /// Promotes a different sid to the active pick for <paramref name="walletId"/>, leaving the
            /// previous one as a "ghost" — it still exists in the registry (HasAudioStream=true) but
            /// <see cref="IsActiveSid"/> returns false for it. Models the resolver flipping winners.
            /// </summary>
            public void SetActiveSid(string walletId, string sid)
            {
                hasAudioStreamByIdentity.Add(walletId);
                activeSidByIdentity[walletId] = sid;
            }

            public void ClearAll()
            {
                hasAudioStreamByIdentity.Clear();
                activeSidByIdentity.Clear();
            }

            public bool HasAudioStream(string walletId) =>
                hasAudioStreamByIdentity.Contains(walletId);

            public Weak<AudioStream> GetActiveStream(StreamKey key) =>
                Weak<AudioStream>.Null;

            public string? GetActiveSid(string walletId) =>
                activeSidByIdentity.TryGetValue(walletId, out string? sid) ? sid : null;

            public bool IsActiveSid(string walletId, string sid)
            {
                IsActiveSidCallCount++;
                return activeSidByIdentity.TryGetValue(walletId, out string? active) && active == sid;
            }

            public bool IsActiveSpeaker(string walletId) => false;

            public int RebuildEpoch => 0;

            public void Dispose() { }
        }
    }
}
