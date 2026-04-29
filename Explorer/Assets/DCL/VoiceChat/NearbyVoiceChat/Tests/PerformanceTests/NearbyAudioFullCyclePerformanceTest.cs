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
using System;
using System.Collections.Concurrent;
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

            var muteService = new NearbyMuteService(Substitute.For<INearbyMuteCache>(), Substitute.For<INearbyMuteRepository>());

            system = new NearbyAudioBindingSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory);
            positionSystem = new NearbyAudioPositionSystem(world, muteService);
            positionSystem.Initialize();
            cleanupSystem = new NearbyAudioCleanupSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory);
        }

        protected override void OnTearDown()
        {
            // Dispose cleanup first so it runs TearDownAllAudioSourcesQuery on a still-alive world,
            // routing every source through the factory's Stop → Free → SafeDestroyGameObject path.
            cleanupSystem?.Dispose();
            positionSystem?.Dispose();

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
            for (int i = 0; i < participantCount; i++)
            {
                string wallet = $"wallet-perf-{i}";
                CreateAvatarEntity(wallet);
                registry.Add(wallet, "sid");
            }

            // Drain the throttled binding ramp-up — binding caps at MAX_CREATIONS_PER_FRAME per tick,
            // plus one extra tick so the warmed-up state observes idempotent (no-op) binding.
            int rampUpTicks = ((participantCount + NearbyAudioBindingSystem.MAX_CREATIONS_PER_FRAME - 1)
                               / NearbyAudioBindingSystem.MAX_CREATIONS_PER_FRAME) + 1;
            for (int t = 0; t < rampUpTicks; t++)
            {
                system.Update(0);
                positionSystem.Update(0);
                cleanupSystem.Update(0);
            }

            Measure
               .Method(() =>
                {
                    system.Update(0);
                    positionSystem.Update(0);
                    cleanupSystem.Update(0);
                })
               .WarmupCount(5)
               .MeasurementCount(50)
               .GC()
               .Run();
        }

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

        // ── Fake stream registry ────────────────────────────────────
        // Mirrors NearbyAudioBindingSystemShould's fake: Owned<AudioStream>(null) yields a Weak
        // whose Resource.Has is true, so binding actually creates LivekitAudioSource instances
        // through the real factory — we want the integration cost, not a stubbed-out short-circuit.
        private sealed class FakeStreamRegistry : INearbyAudioStreamRegistry
        {
            private readonly Dictionary<string, ConcurrentDictionary<string, byte>> sidsByIdentity = new ();
            private readonly Dictionary<StreamKey, Owned<AudioStream>> streamsByKey = new ();

            public void Add(string walletId, string sid)
            {
                if (!sidsByIdentity.TryGetValue(walletId, out var sids))
                {
                    sids = new ConcurrentDictionary<string, byte>();
                    sidsByIdentity[walletId] = sids;
                }

                sids.TryAdd(sid, 0);

                var key = new StreamKey(walletId, sid);
                if (!streamsByKey.ContainsKey(key))
                    streamsByKey[key] = new Owned<AudioStream>(null!);
            }

            public ConcurrentDictionary<string, byte>? GetAudioSids(string walletId) =>
                sidsByIdentity.TryGetValue(walletId, out var sids) ? sids : null;

            public Weak<AudioStream> GetActiveStream(StreamKey key) =>
                streamsByKey.TryGetValue(key, out Owned<AudioStream>? owned)
                    ? owned.Downgrade()
                    : Weak<AudioStream>.Null;

            public bool IsStreamGone(StreamKey key)
            {
                ConcurrentDictionary<string, byte>? sids = GetAudioSids(key.identity);
                return sids == null || !sids.ContainsKey(key.sid);
            }

            public bool IsActiveSpeaker(string walletId) =>
                throw new NotImplementedException();

            public void Dispose() { }
        }
    }
}
