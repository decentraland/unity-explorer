using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Friends.UserBlocking;
using DCL.Profiles;
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
        private Dictionary<StreamKey, Entity> bindings = null!;
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
            bindings = new Dictionary<StreamKey, Entity>();
            userBlockingCache = Substitute.For<IUserBlockingCache>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            configuration = ScriptableObject.CreateInstance<VoiceChatConfiguration>();
            sourceFactory = new NearbyAudioSourceFactory(configuration);

            system = new NearbyAudioCleanupSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory);
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

        // ── archetype short-circuit via StreamingAudioComponent ─────

        [Test]
        public void FlagsAudioEntityWhenAvatarLosesStreamingComponent()
        {
            // Cheap shortcut — when the avatar is alive, not flagged for deletion, but has lost
            // its StreamingAudioComponent (Bridge dropped it because the registry no longer reports
            // sids for that walletId), the audio entity must be doomed without consulting the
            // registry or the blocking cache.
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<StreamingAudioComponent>(avatarEntity);

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void KeepsAudioEntityWhenComponentPresentAndRegistryAlive()
        {
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);

            system.Update(0);

            Assert.That(world.Has<DeleteEntityIntention>(audioEntity), Is.False,
                "healthy steady-state entity must not be flagged");
            Assert.That(world.IsAlive(audioEntity), Is.True);
            Assert.That(source == null, Is.False, "LivekitAudioSource must remain alive");
            Assert.That(bindings.ContainsKey(new StreamKey(PARTICIPANT_A, SID_1)), Is.True);
        }

        [Test]
        public void FlagsAudioEntityWhenOneOfNSidsGoneButComponentPresent()
        {
            // Multi-sid granularity — the component is per-walletId, so removing one sid leaves
            // the component (with the surviving sids) and the other sid's audio entity in place.
            // Per-sid doom must arrive through registry.IsStreamGone(comp.Key), not the
            // marker-absence shortcut.
            const string SID_2 = "sid-2";
            (Entity audioEntity1, Entity avatarEntity, LivekitAudioSource source1) = SeedBinding(PARTICIPANT_A, SID_1);
            registry.Add(PARTICIPANT_A, SID_2);

            // Sid-2 entity coexists; both audio entities share the same avatar (and its component).
            LivekitAudioSource source2 = CreateLivekitAudioSource();
            var key2 = new StreamKey(PARTICIPANT_A, SID_2);
            Entity audioEntity2 = world.Create(new NearbyAudioSourceComponent(key2, avatarEntity, source2));
            bindings.TryAdd(key2, audioEntity2);

            // Drop only sid-1 from the registry. Component stays (sid-2 still present).
            registry.RemoveSid(PARTICIPANT_A, SID_1);

            system.Update(0);

            AssertCleanedUp(audioEntity1, source1, PARTICIPANT_A, SID_1);
            Assert.That(world.Has<DeleteEntityIntention>(audioEntity2), Is.False,
                "sibling sid must remain alive — multi-sid is the case the registry fallback exists for");
            Assert.That(source2 == null, Is.False);
        }

        [Test]
        public void FlagsAudioEntityWhenAvatarHasDeleteIntentionButComponentRemains()
        {
            // Bridge.UpdateStreaming filters with [None<DeleteEntityIntention>], so a doomed avatar
            // keeps its StreamingAudioComponent until physical destruction. Cleanup must catch this
            // via the World.Has<DeleteEntityIntention>(avatar) clause, not the component-absence clause.
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Add<DeleteEntityIntention>(avatarEntity);
            // component intentionally NOT removed — Bridge's [None<DeleteEntityIntention>] filter prevents that

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
            Assert.That(world.Has<StreamingAudioComponent>(avatarEntity), Is.True,
                "Bridge's [None<DeleteEntityIntention>] filter prevents component removal on a doomed avatar");
        }

        [Test]
        public void DoesNotQueryRegistryWhenComponentAbsentPathDoomsEntity()
        {
            // Cheap shortcut — proves the marker-absence clause short-circuits the registry call.
            (_, Entity avatarEntity, _) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<StreamingAudioComponent>(avatarEntity);

            registry.ResetCallCounters();

            system.Update(0);

            Assert.That(registry.IsStreamGoneCallCount, Is.EqualTo(0),
                "component-absence must short-circuit before the registry lookup");
            Assert.That(userBlockingCache.ReceivedCalls(), Is.Empty,
                "component-absence must short-circuit before the blocking cache lookup");
        }

        [Test]
        public void FlagsAudioEntityWhenAvatarLosesAudibleRangeTag()
        {
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<InAudibleRangeTag>(avatarEntity);

            system.Update(0);

            AssertCleanedUp(audioEntity, source, PARTICIPANT_A, SID_1);
        }

        [Test]
        public void DoesNotInvokeRegistryWhenAudibleRangeAbsentPathDooms()
        {
            (_, Entity avatarEntity, _) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<InAudibleRangeTag>(avatarEntity);

            registry.ResetCallCounters();

            system.Update(0);

            Assert.That(registry.IsStreamGoneCallCount, Is.EqualTo(0),
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
            Assert.That(bindings.ContainsKey(new StreamKey(PARTICIPANT_A, SID_1)), Is.True);
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
            Assert.That(bindings.ContainsKey(new StreamKey(walletId, sid)), Is.False, "binding must be removed");
        }

        private static void AssertSourceTornDown(LivekitAudioSource source, string? message = null)
        {
            if (source == null) return; // legacy path — Object.Destroy ran

            Assert.That(source.gameObject.activeSelf, Is.False, message ?? "pooled source must be inactive");
            Assert.That(source.enabled, Is.False, message ?? "pooled source's wrapper must be disabled");
        }

        private (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) SeedBinding(string walletId, string sid)
        {
            Entity avatarEntity = CreateAvatarEntity(walletId);
            // Realistic state for a live audio entity: StreamingAudioComponent (with its sid)
            // + InAudibleRangeTag both present — Bridge applied the component and AudibleRangeMarker
            // applied the range tag before Binding spawned the entity. Pair both in the seed so the
            // existing trigger tests exercise the intended fallbacks (IsStreamGone / UserIsBlocked /
            // lifecycle), not the component-absence shortcut by accident.
            world.Add(avatarEntity, new StreamingAudioComponent(new[] { sid }));
            world.Add<InAudibleRangeTag>(avatarEntity);
            registry.Add(walletId, sid);

            LivekitAudioSource source = CreateLivekitAudioSource();
            var key = new StreamKey(walletId, sid);
            Entity audioEntity = world.Create(new NearbyAudioSourceComponent(key, avatarEntity, source));
            bindings.TryAdd(key, audioEntity);

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

        private LivekitAudioSource CreateLivekitAudioSource()
        {
            LivekitAudioSource source = LivekitAudioSource.New();
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
            private readonly Dictionary<string, string[]> sidsByIdentity = new ();

            // Call counters for asserting short-circuit behaviour.
            public int IsStreamGoneCallCount { get; private set; }

            public void ResetCallCounters() => IsStreamGoneCallCount = 0;

            public void Add(string walletId, string sid)
            {
                if (!sidsByIdentity.TryGetValue(walletId, out string[]? prev))
                    sidsByIdentity[walletId] = new[] { sid };
                else if (Array.IndexOf(prev, sid) < 0)
                {
                    string[] next = new string[prev.Length + 1];
                    Array.Copy(prev, next, prev.Length);
                    next[prev.Length] = sid;
                    sidsByIdentity[walletId] = next;
                }
            }

            public void RemoveAll(string walletId) =>
                sidsByIdentity.Remove(walletId);

            public void RemoveSid(string walletId, string sid)
            {
                if (!sidsByIdentity.TryGetValue(walletId, out string[]? prev)) return;
                int idx = Array.IndexOf(prev, sid);
                if (idx < 0) return;

                if (prev.Length == 1)
                {
                    sidsByIdentity.Remove(walletId);
                    return;
                }

                string[] next = new string[prev.Length - 1];
                for (int i = 0, j = 0; i < prev.Length; i++)
                {
                    if (i == idx) continue;
                    next[j++] = prev[i];
                }
                sidsByIdentity[walletId] = next;
            }

            public void ClearAll() =>
                sidsByIdentity.Clear();

            public bool HasAudioStream(string walletId) =>
                sidsByIdentity.ContainsKey(walletId);

            public ReadOnlySpan<string> GetAudioSids(string walletId) =>
                sidsByIdentity.TryGetValue(walletId, out string[]? arr) ? arr : default;

            public string[]? GetAudioSidsArray(string walletId) =>
                sidsByIdentity.TryGetValue(walletId, out string[]? arr) ? arr : null;

            public Weak<AudioStream> GetActiveStream(StreamKey key) =>
                Weak<AudioStream>.Null;

            public bool IsStreamGone(StreamKey key)
            {
                IsStreamGoneCallCount++;
                if (!sidsByIdentity.TryGetValue(key.identity, out string[]? sids))
                    return true;
                return Array.IndexOf(sids, key.sid) < 0;
            }

            public bool IsActiveSpeaker(string walletId) => false;

            public void Dispose() { }
        }
    }
}
