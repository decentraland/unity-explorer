using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.MutePersistence;
using DCL.VoiceChat.Nearby.Systems;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using Avatar = DCL.Profiles.Avatar;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Benchmarks <see cref="NearbyAudioPositionSystem.Update"/> with varying participant counts.
    /// Measures main-thread cost of position sync + spatial angle calculation under the new
    /// dedicated audio-source entity layout.
    /// </summary>
    [Category("Performance")]
    public class NearbyAudioPositionSystemPerformanceTest : UnitySystemTestBase<NearbyAudioPositionSystem>
    {
        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly List<GameObject> gameObjects = new (256);

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            var cameraGo = CreateTrackedGameObject("PerfCamera");
            var camera = cameraGo.AddComponent<Camera>();

            var playerGo = CreateTrackedGameObject("PerfPlayer");
            playerGo.transform.position = new Vector3(0, 1.6f, 0);

            // FakeMuteCache (HashSet-backed) instead of Substitute.For<INearbyMuteCache>() — substitute
            // proxies every IsMuted call through argument-matching + call-recording (~20 µs/call),
            // which used to dominate this benchmark and inflated per-entity cost ~×7 over real production.
            var muteService = new NearbyMuteService(new FakeMuteCache(), Substitute.For<INearbyMuteRepository>());

            // PositionSystem reads NearbyListenerState (produced by NearbyAudibleRangeSystem in
            // production). This benchmark exercises PositionSystem in isolation, so we seed the
            // state manually with the player head transform.
            var listenerState = new NearbyListenerState();
            listenerState.BindListener(playerGo.transform, camera.transform);

            system = new NearbyAudioPositionSystem(world, muteService, listenerState);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        [Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(1000)]
        public void UpdateWithNParticipants(int participantCount)
        {
            for (int i = 0; i < participantCount; i++)
                CreateBoundAudioSource(i);

            // Warm one frame so any first-tick branches settle into steady state
            system.Update(0);

            Measure
               .Method(() => system.Update(0))
               .WarmupCount(5)
               .MeasurementCount(50)
               .GC()
               .Run();
        }

        private void CreateBoundAudioSource(int index)
        {
            string id = $"wallet-perf-{index}";

            var avatarGo = CreateTrackedGameObject($"Avatar_{index}");
            avatarGo.transform.position = Random.insideUnitSphere * 15f;

            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{index}");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            headAnchorGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            Entity avatarEntity = world.Create(
                new Profile(id, id, new Avatar()),
                avatarBase,
                new CharacterTransform(avatarGo.transform));

            // A1: PositionSystem skips the spatial pipeline unless the avatar carries
            // InAudibleRangeTag with IsSuspended=false. Default-tag here so the benchmark
            // continues to measure the full spatial-pipeline path, not the inactive-state
            // early-return.
            world.Add<InAudibleRangeTag>(avatarEntity);

            LivekitAudioSource source = LivekitAudioSource.New();
            gameObjects.Add(source.gameObject);

            world.Create(new NearbyAudioSourceComponent(new StreamKey(id, "sid"), avatarEntity, source));
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }
    }
}
