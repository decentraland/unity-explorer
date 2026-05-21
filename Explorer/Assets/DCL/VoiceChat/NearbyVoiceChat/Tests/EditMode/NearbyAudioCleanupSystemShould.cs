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
    /// Documents <see cref="NearbyAudioCleanupSystem"/> contract (slice 4 — co-located component):
    ///
    /// - Detection collects doomed avatars (lost streamer marker / out of range / sid demoted / blocked /
    ///   scene-banned), disposes their <see cref="LivekitAudioSource"/>, then removes the
    ///   <see cref="NearbyAudioSourceComponent"/> from the avatar entity (avatar itself stays alive).
    /// - Listening-gate / device-change paths bulk-remove the component from every live entity.
    /// - Avatars carrying <see cref="DeleteEntityIntention"/> get only their source disposed — the
    ///   component goes away with the entity when DestroyEntitiesSystem runs.
    /// - Tests assert the system's own contribution: component removed (live triggers) / source disposed.
    ///   They do NOT assert avatar-entity destruction (out of scope).
    /// - <see cref="NearbyAudioCleanupSystem.Dispose"/> disposes any survivors.
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
            userBlockingCache = Substitute.For<IUserBlockingCache>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            configuration = ScriptableObject.CreateInstance<VoiceChatConfiguration>();
            sourceFactory = new NearbyAudioSourceFactory(configuration);

            system = new NearbyAudioCleanupSystem(world, registry, userBlockingCache, stateModel, sourceFactory, RoomMetadataCurrentScene.CreateForTest());
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();
            stateModel.Dispose();

            if (configuration != null) Object.DestroyImmediate(configuration);

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        // ── Trigger #7: avatar dying ────────────────────────────────
        // Slice 4 collapsed the audio entity onto the avatar. The "hard-destroy the avatar"
        // premise from the old test is no longer reachable in practice (avatar destruction
        // is gated through DeleteEntityIntention → DestroyEntitiesSystem) — and the legacy
        // separate-audio-entity test would have nothing to clean up either. The
        // DeleteEntityIntention path below is the only meaningful "avatar gone" scenario now.

        [Test]
        public void AvatarWithDeleteEntityIntentionDisposesSource()
        {
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Add<DeleteEntityIntention>(avatarEntity);

            system.Update(0);

            // Trigger #7 disposes the source but does NOT World.Remove the component — the dying avatar
            // will take it down on physical destruction.
            AssertSourceTornDown(source);
        }

        // ── Trigger #2: stream gone ─────────────────────────────────

        [Test]
        public void RegistryMissingWalletCausesCleanup()
        {
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            registry.RemoveAll(PARTICIPANT_A);

            system.Update(0);

            AssertCleanedUp(avatarEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void RegistryMissingSidCausesCleanup()
        {
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            registry.Add(PARTICIPANT_A, "sid-other"); // wallet still in registry, but bound sid is gone
            registry.RemoveSid(PARTICIPANT_A, SID_1);

            system.Update(0);

            AssertCleanedUp(avatarEntity, source, PARTICIPANT_A, SID_1);
        }

        // ── Trigger #3: blocked identity ────────────────────────────

        [Test]
        public void BlockedIdentityCausesCleanup()
        {
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            userBlockingCache.UserIsBlocked(PARTICIPANT_A).Returns(true);

            system.Update(0);

            AssertCleanedUp(avatarEntity, source, PARTICIPANT_A, SID_1);
        }

        // ── Trigger #4: listening gate ──────────────────────────────

        [Test]
        public void SuppressedStateTearsDownAllAudioSources()
        {
            const int COUNT = 3;
            var seeded = new List<(Entity avatarEntity, LivekitAudioSource source, string wallet)>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (Entity avatarEntity, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                seeded.Add((avatarEntity, source, $"wallet-{i}"));
            }

            stateModel.Suppress(SuppressionReason.CALL);

            system.Update(0);

            Assert.That(world.CountEntities(in LIVE_AUDIO_QUERY), Is.EqualTo(0), "all audio components must be removed");
            foreach ((Entity avatarEntity, LivekitAudioSource source, _) in seeded)
            {
                Assert.That(world.Has<NearbyAudioSourceComponent>(avatarEntity), Is.False,
                    "listening-gate bulk path removes the component from every live entity");
                Assert.That(world.IsAlive(avatarEntity), Is.True, "avatar itself must stay alive");
                AssertSourceTornDown(source);
            }
        }

        [Test]
        public void DisabledStateTearsDownAllAudioSources()
        {
            const int COUNT = 3;
            var seeded = new List<(Entity avatarEntity, LivekitAudioSource source, string wallet)>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (Entity avatarEntity, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                seeded.Add((avatarEntity, source, $"wallet-{i}"));
            }

            stateModel.Disable();

            system.Update(0);

            Assert.That(world.CountEntities(in LIVE_AUDIO_QUERY), Is.EqualTo(0));
            foreach ((Entity avatarEntity, LivekitAudioSource source, _) in seeded)
            {
                Assert.That(world.Has<NearbyAudioSourceComponent>(avatarEntity), Is.False);
                Assert.That(world.IsAlive(avatarEntity), Is.True);
                AssertSourceTornDown(source);
            }
        }

        // ── Compound ────────────────────────────────────────────────

        [Test]
        public void BothTriggersPresentResultInSingleTeardown()
        {
            // Compound trigger — registry drops the identity AND user gets blocked in the same frame.
            // Both clauses dock onto the same per-entity collect pass; the result is one teardown, not two.
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            registry.RemoveAll(PARTICIPANT_A);
            userBlockingCache.UserIsBlocked(PARTICIPANT_A).Returns(true);

            Assert.DoesNotThrow(() => system.Update(0));

            AssertCleanedUp(avatarEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void MassCleanupOnDisconnected()
        {
            const int COUNT = 10;
            var seeded = new List<(Entity avatarEntity, LivekitAudioSource source)>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (Entity avatarEntity, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                seeded.Add((avatarEntity, source));
            }

            registry.ClearAll();

            system.Update(0);

            Assert.That(world.CountEntities(in LIVE_AUDIO_QUERY), Is.EqualTo(0));
            foreach ((Entity avatarEntity, LivekitAudioSource source) in seeded)
            {
                Assert.That(world.Has<NearbyAudioSourceComponent>(avatarEntity), Is.False);
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
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<NearbyAudioStreamerComponent>(avatarEntity);

            system.Update(0);

            AssertCleanedUp(avatarEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void KeepsAudioComponentWhenMarkerPresentAndRegistryAlive()
        {
            // Steady state — marker on, registry has the sid, not blocked, avatar alive.
            // The cleanup query must not touch this entity. This is the dominant per-frame path.
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);

            system.Update(0);

            Assert.That(world.Has<NearbyAudioSourceComponent>(avatarEntity), Is.True,
                "healthy steady-state entity must keep its audio-source component");
            Assert.That(world.IsAlive(avatarEntity), Is.True);
            Assert.That(source == null, Is.False, "LivekitAudioSource must remain alive");
        }

        // FlagsLosingSidAudioEntityWhenResolverPicksSibling — DELETED.
        // Premise required two NearbyAudioSourceComponent instances on the same avatar (one per sid).
        // After slice-4 co-location, the component lives on the avatar entity and Arch allows at most one
        // instance of a given component type per entity. The resolver-dedup contract collapsed multi-sid
        // state into a single CurrentSid, and GhostSidLosingResolverPickCausesCleanup below covers the
        // single-sid ghost-demotion case.

        [Test]
        public void GhostSidLosingResolverPickCausesCleanup()
        {
            // Test 9 from the spec — the audio entity's sid is no longer the resolver's pick.
            // Distinct from RegistryMissingSidCausesCleanup: here the sid still EXISTS in the registry
            // (HasAudioStream=true), it just lost the active-pick race. The !IsActiveSid predicate
            // must reap it regardless of whether the registry still indexes the sid.
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);

            // Resolver demotes sid-1 in favour of a fresher candidate; HasAudioStream stays true
            // (registry still holds candidates for the identity).
            const string SID_FRESH = "sid-fresh";
            registry.SetActiveSid(PARTICIPANT_A, SID_FRESH);

            // Sanity-check the precondition the test depends on.
            Assert.That(registry.HasAudioStream(PARTICIPANT_A), Is.True,
                "precondition: identity still indexed (only the active pick changed)");
            Assert.That(registry.IsActiveSid(new StreamKey(PARTICIPANT_A, SID_1)), Is.False,
                "precondition: bound sid is no longer the active one");
            // The Asserts above bumped IsActiveSidCallCount via the precondition; reset before the system tick.
            registry.ResetCallCounters();

            system.Update(0);

            AssertCleanedUp(avatarEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void DisposesSourceWhenAvatarHasDeleteIntentionButMarkerRemains()
        {
            // F1-deliberate invariant: NearbyLivekitBridgeSystem.UpdateStreaming filters with
            // [None<DeleteEntityIntention>], so a doomed avatar keeps its marker until physical
            // destruction. The dying-avatar trigger #7 must dispose the source regardless of marker state;
            // it does NOT World.Remove the audio-source component (the entity itself is on its way out).
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Add<DeleteEntityIntention>(avatarEntity);
            // marker intentionally NOT removed — F1 contract

            system.Update(0);

            AssertSourceTornDown(source);
            Assert.That(world.Has<NearbyAudioStreamerComponent>(avatarEntity), Is.True,
                "Bridge's [None<DeleteEntityIntention>] filter prevents component removal on a doomed avatar");
        }

        [Test]
        public void DoesNotQueryRegistryWhenMarkerAbsentPathDoomsEntity()
        {
            // Optional sanity — proves the cheap shortcut actually short-circuits the registry call.
            // If the marker-absence clause fires, registry.IsActiveSid must NOT be invoked.
            (Entity avatarEntity, _) = SeedBinding(PARTICIPANT_A, SID_1);
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
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<InAudibleRangeTag>(avatarEntity);

            system.Update(0);

            AssertCleanedUp(avatarEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void DoesNotInvokeRegistryWhenAudibleRangeAbsentPathDooms()
        {
            // Cost-shortcut semantics — the marker-absence clause must short-circuit before the
            // registry lookup fires. Mirrors the streaming-tag short-circuit guard from A5.2.
            (Entity avatarEntity, _) = SeedBinding(PARTICIPANT_A, SID_1);
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
            (Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);

            system.Update(0);
            system.Update(0);

            Assert.That(world.IsAlive(avatarEntity), Is.True);
            Assert.That(world.Has<NearbyAudioSourceComponent>(avatarEntity), Is.True);
            Assert.That(source == null, Is.False);
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
                (_, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                sources.Add(source);
            }

            // Even with all triggers cold, dispose must release every source — covers world tear-down survivors.
            system.Dispose();

            foreach (LivekitAudioSource source in sources)
                AssertSourceTornDown(source);
        }

        // ── Helpers ─────────────────────────────────────────────────

        // Slice 4: cleanup contract for the LIVE doom path — component removed from the avatar entity,
        // source torn down. The avatar entity itself stays alive (it's the avatar; it just no longer
        // carries an audio-source pair).
        private void AssertCleanedUp(Entity avatarEntity, LivekitAudioSource source, string walletId, string sid)
        {
            Assert.That(world.IsAlive(avatarEntity), Is.True, "avatar entity destruction is out of scope here");
            Assert.That(world.Has<NearbyAudioSourceComponent>(avatarEntity), Is.False,
                "audio-source component must be removed from the avatar");
            AssertSourceTornDown(source, "LivekitAudioSource must be torn down (destroyed in legacy path, parked inactive in pool path)");
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

        // Slice 4: audio-source component is co-located on the avatar — there is no separate audio entity.
        // Callers that previously needed an "audioEntity" alias should just reuse avatarEntity; AssertCleanedUp
        // takes the same entity for both the liveness check and the component-removed check.
        private (Entity avatarEntity, LivekitAudioSource source) SeedBinding(string walletId, string sid)
        {
            Entity avatarEntity = CreateAvatarEntity(walletId);
            // Realistic state for a live audio component: streamer marker + audible range tag both present.
            // Trigger tests that want the !IsActiveSid / UserIsBlocked / lifecycle fallbacks rely on this baseline.
            world.Add(avatarEntity, new NearbyAudioStreamerComponent(sid));
            world.Add<InAudibleRangeTag>(avatarEntity);
            registry.Add(walletId, sid);

            var key = new StreamKey(walletId, sid);
            LivekitAudioSource source = CreateLivekitAudioSource(key);
            world.Add(avatarEntity, new NearbyAudioSourceComponent(key, source));

            return (avatarEntity, source);
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

            public bool IsActiveSid(StreamKey key)
            {
                IsActiveSidCallCount++;
                return activeSidByIdentity.TryGetValue(key.identity, out string? active) && active == key.sid;
            }

            public bool IsActiveSpeaker(string walletId) => false;

            public int RebuildEpoch => 0;

            public void Dispose() { }
        }
    }
}
