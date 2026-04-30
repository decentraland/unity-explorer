using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using DCL.Character.Components;
using DCL.Friends.UserBlocking;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.MutePersistence;
using DCL.VoiceChat.Nearby.Systems;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using NSubstitute;
using NUnit.Framework;
using RichTypes;
using System.Collections.Generic;
using System.Reflection;
using Unity.PerformanceTesting;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Benchmarks the full Nearby audio per-frame chain — Binding → Position → Cleanup —
    /// in steady state with N participants already bound.
    /// <para>
    /// The measured tick exercises the per-entity hot paths of all three systems with no
    /// triggers firing: binding sees idempotent <c>(walletId, sid)</c>, position syncs
    /// transforms + spatial angles + mute, cleanup runs the four-trigger check on every
    /// audio entity. Reports the real per-frame cost a listener pays once the crowd has
    /// settled — the slice <see cref="NearbyAudioPositionSystemPerformanceTest"/> intentionally
    /// skips by handcrafting <see cref="NearbyAudioSourceComponent"/>s.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class NearbyAudioFullCyclePerformanceTest : UnitySystemTestBase<NearbyAudioBindingSystem>
    {
        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly List<GameObject> gameObjects = new (256);

        private FakeStreamRegistry registry;
        private Dictionary<StreamKey, Entity> bindings;
        private NearbyVoiceChatStateModel stateModel;
        private VoiceChatConfiguration configuration;
        private NearbyAudioSourceFactory sourceFactory;
        private NearbyAudioPositionSystem positionSystem;
        private NearbyAudioCleanupSystem cleanupSystem;
        private NearbyLivekitBridgeSystem markerSystem;
        private NearbyAudibleRangeMarkerSystem audibleRangeMarkerSystem;

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            var cameraGo = CreateTrackedGameObject("PerfCamera");
            var camera = cameraGo.AddComponent<Camera>();
            world.Create(new CameraComponent(camera));

            var playerGo = CreateTrackedGameObject("PerfPlayer");
            playerGo.transform.position = new Vector3(0, 1.6f, 0);
            world.Create(new PlayerComponent(playerGo.transform));

            registry = new FakeStreamRegistry();
            bindings = new Dictionary<StreamKey, Entity>();
            IUserBlockingCache userBlockingCache = Substitute.For<IUserBlockingCache>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            configuration = ScriptableObject.CreateInstance<VoiceChatConfiguration>();
            sourceFactory = new NearbyAudioSourceFactory(configuration);

            // FakeMuteCache (HashSet-backed) instead of Substitute.For<INearbyMuteCache>() — substitute
            // proxies every IsMuted call through argument-matching + call-recording (~20 µs/call),
            // which used to dominate the position-system slice of this benchmark and inflated full-cycle
            // per-entity cost ~×7 over real production.
            var muteService = new NearbyMuteService(new FakeMuteCache(), Substitute.For<INearbyMuteRepository>());

            system = new NearbyAudioBindingSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory);
            positionSystem = new NearbyAudioPositionSystem(world, muteService);
            positionSystem.Initialize();
            cleanupSystem = new NearbyAudioCleanupSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory);
            markerSystem = new NearbyLivekitBridgeSystem(world, registry);
            audibleRangeMarkerSystem = new NearbyAudibleRangeMarkerSystem(world);
            audibleRangeMarkerSystem.Initialize();
        }

        protected override void OnTearDown()
        {
            // Dispose cleanup first so it runs TearDownAllAudioSourcesQuery on a still-alive world,
            // routing every source through the factory's Stop → Free → SafeDestroyGameObject path.
            cleanupSystem?.Dispose();
            positionSystem?.Dispose();
            markerSystem?.Dispose();
            audibleRangeMarkerSystem?.Dispose();

            // Defensive: LivekitAudioSource keeps invoking OnAudioFilterRead on the audio thread
            // even after disposal — reap any straggler not caught above to avoid NREs between runs.
            foreach (LivekitAudioSource src in Object.FindObjectsByType<LivekitAudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (src == null) continue;
                src.Stop();
                src.Free();
                Object.DestroyImmediate(src.gameObject);
            }

            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);
            gameObjects.Clear();

            bindings.Clear();
            stateModel.Dispose();

            if (configuration != null) Object.DestroyImmediate(configuration);

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        [Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        public void FullCycleSteadyStateWithNParticipants(int participantCount)
        {
            PopulatePerfWorld(participantCount);

            int rampUpTicks = ComputeRampUpTicks(participantCount);
            for (int t = 0; t < rampUpTicks; t++)
            {
                markerSystem.Update(0);
                audibleRangeMarkerSystem.Update(0);
                system.Update(0);
                positionSystem.Update(0);
                cleanupSystem.Update(0);
            }

            // Full chain WITH markers — compare ms/tick and GC bytes/tick against the
            // pre-F1 baseline captured before marker injection.
            Measure
               .Method(() =>
                {
                    markerSystem.Update(0);
                    audibleRangeMarkerSystem.Update(0);
                    system.Update(0);
                    positionSystem.Update(0);
                    cleanupSystem.Update(0);
                })
               .WarmupCount(5)
               .MeasurementCount(50)
               .GC()
               .Run();
        }

        /// <summary>
        /// A5 canonical-win scenario: 100 avatars present, only a subset publishing audio.
        /// Exercises the archetype filter on Binding (`[All<AvatarBase, StreamingAudioComponent>]`)
        /// and the cleanup short-circuit. The non-streaming avatars must be skipped at chunk-iteration
        /// level by Binding, and Cleanup runs against `streamingParticipants` audio entities only.
        /// <para>
        /// Scenario 2 — 100 / 30: canonical mid-density case.
        /// Scenario 3 — 100 / 0: "empty in steady state" demonstration; full-cycle ms must be near zero.
        /// </para>
        /// </summary>
        [Test]
        [Performance]
        [TestCase(100, 30)]
        [TestCase(100, 0)]
        public void FullCycleSteadyStateWithMixedStreamingState(int totalParticipants, int streamingParticipants)
        {
            PopulatePerfWorldMixed(totalParticipants, streamingParticipants);

            // Ramp-up budget keys off the streaming subset since binding only allocates audio entities
            // for those. Add one extra tick so Bridge's marker propagation settles before measurement.
            int rampUpTicks = ComputeRampUpTicks(streamingParticipants);
            for (int t = 0; t < rampUpTicks; t++)
            {
                markerSystem.Update(0);
                audibleRangeMarkerSystem.Update(0);
                system.Update(0);
                positionSystem.Update(0);
                cleanupSystem.Update(0);
            }

            Measure
               .Method(() =>
                {
                    markerSystem.Update(0);
                    audibleRangeMarkerSystem.Update(0);
                    system.Update(0);
                    positionSystem.Update(0);
                    cleanupSystem.Update(0);
                })
               .WarmupCount(5)
               .MeasurementCount(50)
               .GC()
               .Run();
        }

        /// <summary>
        /// Marker-only overhead in steady state. Queries A and C iterate zero entities
        /// (everything is already tagged), Query B iterates the streaming set (all N) with a
        /// short-circuit return, Query D iterates the speaking subset with a short-circuit
        /// return. This is the per-frame cost A5 / B1 will measure their savings against.
        /// Acceptance budget: ≤ 50 µs/tick at 100 avatars, ≤ 50 B/tick GC (ideally 0).
        /// </summary>
        [Test]
        [Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        public void MarkerOnlySteadyStateWithNParticipants(int participantCount)
        {
            PopulatePerfWorld(participantCount);

            int rampUpTicks = ComputeRampUpTicks(participantCount);
            for (int t = 0; t < rampUpTicks; t++)
            {
                markerSystem.Update(0);
                audibleRangeMarkerSystem.Update(0);
                system.Update(0);
                positionSystem.Update(0);
                cleanupSystem.Update(0);
            }

            Measure
               .Method(() => markerSystem.Update(0))
               .WarmupCount(5)
               .MeasurementCount(50)
               .GC()
               .Run();
        }

        /// <summary>
        /// A1 distance-distribution scenarios at 100 publishers (post-A5 baseline = "all at origin").
        /// Avatars are placed deterministically at fixed radii so steady-state cost is dominated
        /// by archetype membership, not movement churn:
        /// <para>
        /// A1-1 — 100 / 0 / 0: all inside 16 m active band (worst case for A1; pure marker toll).
        /// A1-2 — 30 / 10 / 60: canonical "crowd radius" — 30 active, 10 in suspend band, 60 beyond outer-out.
        /// A1-3 — 0 / 0 / 100: all beyond outer-out; full-cycle should approach sub-200 µs.
        /// </para>
        /// </summary>
        [Test]
        [Performance]
        [TestCase(100, 0, 0)]
        [TestCase(30, 10, 60)]
        [TestCase(0, 0, 100)]
        public void FullCycleSteadyStateWithDistanceDistribution(int activeCount, int suspendCount, int beyondCount)
        {
            PopulatePerfWorldByDistance(activeCount, suspendCount, beyondCount);

            int total = activeCount + suspendCount + beyondCount;
            int rampUpTicks = ComputeRampUpTicks(total);
            for (int t = 0; t < rampUpTicks; t++)
            {
                markerSystem.Update(0);
                audibleRangeMarkerSystem.Update(0);
                system.Update(0);
                positionSystem.Update(0);
                cleanupSystem.Update(0);
            }

            Measure
               .Method(() =>
                {
                    markerSystem.Update(0);
                    audibleRangeMarkerSystem.Update(0);
                    system.Update(0);
                    positionSystem.Update(0);
                    cleanupSystem.Update(0);
                })
               .WarmupCount(5)
               .MeasurementCount(50)
               .GC()
               .Run();
        }

        /// <summary>
        /// A2 Scenario A2-1 — boundary-churn at 30 publishers oscillating across the 22 m audible-range
        /// boundary. Goal: prove that after the pool warms up (~30 entries), subsequent crossings
        /// produce zero `Object.Instantiate` calls — full-cycle ms/tick and GC bytes/tick are the
        /// observable proxies, with `factory.poolCountInactive` checked for stable working-set size.
        /// </summary>
        [Test]
        [Performance]
        public void BoundaryChurnAt30Publishers()
        {
            const int PUBLISHERS = 30;
            const float INSIDE_RADIUS = 15f;  // < 18 m outer-in → gains InAudibleRangeTag
            const float OUTSIDE_RADIUS = 25f; // > 22 m outer-out → loses InAudibleRangeTag

            var avatarTransforms = new List<Transform>(PUBLISHERS);
            for (int i = 0; i < PUBLISHERS; i++)
                avatarTransforms.Add(CreateChurnAvatar($"churn-{i}", i));

            // Warm-up: drive several full inward/outward cycles so the pool fills to its working-set
            // ceiling (~PUBLISHERS entries). Subsequent measurement cycles must allocate zero new
            // GameObject + AudioSource + LivekitAudioSource triples — every Create pops from pool.
            for (int cycle = 0; cycle < 4; cycle++)
            {
                MoveTo(avatarTransforms, INSIDE_RADIUS);
                int rampUpTicks = ComputeRampUpTicks(PUBLISHERS);
                for (int t = 0; t < rampUpTicks; t++)
                    TickFullChain();

                MoveTo(avatarTransforms, OUTSIDE_RADIUS);
                // Cleanup needs at most two ticks: one to flag DeleteEntityIntention, one to tear down.
                TickFullChain();
                TickFullChain();
            }

            int poolWatermark = sourceFactory.poolCountInactive;

            Measure
               .Method(() =>
                {
                    MoveTo(avatarTransforms, INSIDE_RADIUS);
                    int rampUpTicks = ComputeRampUpTicks(PUBLISHERS);
                    for (int t = 0; t < rampUpTicks; t++)
                        TickFullChain();

                    MoveTo(avatarTransforms, OUTSIDE_RADIUS);
                    TickFullChain();
                    TickFullChain();
                })
               .WarmupCount(2)
               .MeasurementCount(20)
               .GC()
               .Run();

            Assert.That(sourceFactory.poolCountInactive, Is.LessThanOrEqualTo(poolWatermark + 5),
                "pool working set must stay flat across boundary cycles — growth indicates new instantiations leaked into measurement");
        }

        private void TickFullChain()
        {
            markerSystem.Update(0);
            audibleRangeMarkerSystem.Update(0);
            system.Update(0);
            positionSystem.Update(0);
            cleanupSystem.Update(0);
        }

        private static void MoveTo(List<Transform> transforms, float radius)
        {
            for (int i = 0; i < transforms.Count; i++)
            {
                float angle = i * 0.137f;
                transforms[i].position = new Vector3(radius * Mathf.Cos(angle), 0f, radius * Mathf.Sin(angle));
            }
        }

        private Transform CreateChurnAvatar(string walletId, int idx)
        {
            float angle = idx * 0.137f;
            Vector3 pos = new (15f * Mathf.Cos(angle), 0f, 15f * Mathf.Sin(angle));

            var avatarGo = CreateTrackedGameObject($"Avatar_{walletId}");
            avatarGo.transform.position = pos;

            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{walletId}");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            headAnchorGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            world.Create(new Profile(walletId, walletId, new Avatar()), avatarBase);
            registry.Add(walletId, "sid");
            return avatarGo.transform;
        }

        private void PopulatePerfWorldByDistance(int activeCount, int suspendCount, int beyondCount)
        {
            // Deterministic placement: x-axis ring at fixed radii. Listener is at origin
            // (PerfPlayer + PerfCamera both at (0,1.6,0)). Distances picked well inside each
            // hysteresis band so float jitter cannot tip an avatar across a boundary.
            const float ACTIVE_RADIUS = 8f;   // < 16 m suspend-in
            const float SUSPEND_RADIUS = 19f; // 17–22 m suspend band
            const float BEYOND_RADIUS = 30f;  // > 22 m outer-out

            int idx = 0;
            for (int i = 0; i < activeCount; i++)
                CreateStreamingAvatarAt($"perf-active-{i}", ACTIVE_RADIUS, idx++);
            for (int i = 0; i < suspendCount; i++)
                CreateStreamingAvatarAt($"perf-suspend-{i}", SUSPEND_RADIUS, idx++);
            for (int i = 0; i < beyondCount; i++)
                CreateStreamingAvatarAt($"perf-beyond-{i}", BEYOND_RADIUS, idx++);
        }

        private void CreateStreamingAvatarAt(string walletId, float radius, int idx)
        {
            // Spread avatars around the ring so transforms are not stacked — keeps the avatar-base
            // / head-anchor reads from hitting the same cache line, closer to a real crowd.
            float angle = idx * 0.137f; // golden-angle-ish, just deterministic spread
            Vector3 pos = new (radius * Mathf.Cos(angle), 0f, radius * Mathf.Sin(angle));

            var avatarGo = CreateTrackedGameObject($"Avatar_{walletId}");
            avatarGo.transform.position = pos;

            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{walletId}");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            headAnchorGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            world.Create(new Profile(walletId, walletId, new Avatar()), avatarBase);
            registry.Add(walletId, "sid");
        }

        private void PopulatePerfWorld(int participantCount)
        {
            // 100 % streaming + 50 % actively speaking — exercises every marker query path
            // in steady state (A & C iterate 0, B iterates all N, D iterates the speaking half).
            for (int i = 0; i < participantCount; i++)
            {
                string wallet = $"wallet-perf-{i}";
                CreateAvatarEntity(wallet);
                registry.Add(wallet, "sid");
                if (i % 2 == 0)
                    registry.MarkAsActiveSpeaker(wallet);
            }
        }

        private void PopulatePerfWorldMixed(int totalParticipants, int streamingParticipants)
        {
            // First N avatars are streaming publishers (registry-backed → Bridge will tag them).
            // The remaining (total - N) avatars exist in the world but never enter the registry —
            // they should be skipped by the archetype filter at chunk-iteration level after A5.1.
            for (int i = 0; i < totalParticipants; i++)
            {
                string wallet = $"wallet-perf-{i}";
                CreateAvatarEntity(wallet);

                if (i < streamingParticipants)
                {
                    registry.Add(wallet, "sid");
                    if (i % 2 == 0)
                        registry.MarkAsActiveSpeaker(wallet);
                }
            }
        }

        private static int ComputeRampUpTicks(int participantCount) =>
            ((participantCount + NearbyAudioBindingSystem.MAX_CREATIONS_PER_FRAME - 1)
             / NearbyAudioBindingSystem.MAX_CREATIONS_PER_FRAME) + 1;

        private Entity CreateAvatarEntity(string walletId)
        {
            var avatarGo = CreateTrackedGameObject($"Avatar_{walletId}");
            avatarGo.transform.position = Random.insideUnitSphere * 15f;

            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{walletId}");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            headAnchorGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            return world.Create(new Profile(walletId, walletId, new Avatar()), avatarBase);
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        // ── B2.1 Scenario: Binding allocation gate ──────────────────

        /// <summary>
        /// B2-1 — Binding zero-alloc steady-state gate. 100 avatars all carrying
        /// <see cref="StreamingAudioComponent"/> + <see cref="InAudibleRangeTag"/>; registry stable
        /// (no FFI events between ticks). Pre-B2 baseline: foreach over a ConcurrentDictionary's
        /// enumerator allocated ~128 B / line / matching avatar → multi-KB per measurement window.
        /// After B2.1 the data path reads sids straight from the entity (string[] foreach lowers
        /// to indexed for-loop in IL), so the measurement window must observe only noise-floor GC.
        /// <para>
        /// Acceptance is read from the perf-runner report (Definition: GC Alloc → Median ≈ 0 B,
        /// budget &lt; 256 B per <c>system.Update(0)</c>). Hard-asserting GC bytes here is unreliable
        /// — Unity's Mono runtime does not expose a stable per-call allocation API in Edit Mode,
        /// and the PerfTesting pipeline is the single source of truth.
        /// </para>
        /// </summary>
        [Test]
        [Performance]
        public void BindingZeroAllocSteadyStateAt100Avatars()
        {
            const int AVATARS = 100;

            for (int i = 0; i < AVATARS; i++)
            {
                string wallet = $"wallet-perf-b2-{i}";
                Entity avatar = CreateAvatarEntity(wallet);
                world.Add(avatar, new StreamingAudioComponent(new[] { "sid" }));
                world.Add<InAudibleRangeTag>(avatar);
                registry.Add(wallet, "sid");
            }

            // Drain creations so steady-state measurement observes idempotent (key, entity) pairs only.
            int rampUp = ((AVATARS + NearbyAudioBindingSystem.MAX_CREATIONS_PER_FRAME - 1)
                          / NearbyAudioBindingSystem.MAX_CREATIONS_PER_FRAME) + 1;
            for (int t = 0; t < rampUp; t++)
                system.Update(0);

            Measure
               .Method(() => system.Update(0))
               .WarmupCount(10)
               .MeasurementCount(100)
               .GC()
               .Run();
        }

        // ── Fake stream registry ────────────────────────────────────
        // Owned<AudioStream>(null) yields a Weak whose Resource.Has is true, so binding actually
        // creates LivekitAudioSource instances through the real factory — we want the integration
        // cost, not a stubbed-out short-circuit.
        // Storage uses copy-on-write string[] semantics matching the production registry: every
        // mutation produces a NEW array reference so reference identity is the version signal.
        private sealed class FakeStreamRegistry : INearbyAudioStreamRegistry
        {
            private readonly Dictionary<string, string[]> sidsByIdentity = new ();
            private readonly Dictionary<StreamKey, Owned<AudioStream>> streamsByKey = new ();
            private readonly HashSet<string> activeSpeakers = new ();

            public void Add(string walletId, string sid)
            {
                if (!sidsByIdentity.TryGetValue(walletId, out string[]? prev))
                    sidsByIdentity[walletId] = new[] { sid };
                else if (System.Array.IndexOf(prev, sid) < 0)
                {
                    string[] next = new string[prev.Length + 1];
                    System.Array.Copy(prev, next, prev.Length);
                    next[prev.Length] = sid;
                    sidsByIdentity[walletId] = next;
                }

                var key = new StreamKey(walletId, sid);
                if (!streamsByKey.ContainsKey(key))
                    streamsByKey[key] = new Owned<AudioStream>(null!);
            }

            public void MarkAsActiveSpeaker(string walletId) => activeSpeakers.Add(walletId);

            public bool HasAudioStream(string walletId) =>
                sidsByIdentity.ContainsKey(walletId);

            public System.ReadOnlySpan<string> GetAudioSids(string walletId) =>
                sidsByIdentity.TryGetValue(walletId, out string[]? arr) ? arr : default;

            public string[]? GetAudioSidsArray(string walletId) =>
                sidsByIdentity.TryGetValue(walletId, out string[]? arr) ? arr : null;

            public Weak<AudioStream> GetActiveStream(StreamKey key) =>
                streamsByKey.TryGetValue(key, out Owned<AudioStream>? owned)
                    ? owned.Downgrade()
                    : Weak<AudioStream>.Null;

            public bool IsStreamGone(StreamKey key)
            {
                if (!sidsByIdentity.TryGetValue(key.identity, out string[]? sids))
                    return true;
                return System.Array.IndexOf(sids, key.sid) < 0;
            }

            public bool IsActiveSpeaker(string walletId) => activeSpeakers.Contains(walletId);

            public void Dispose() { }
        }
    }
}
