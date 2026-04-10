using Arch.Core;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using DCL.VoiceChat.Proximity.Systems;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming.Audio;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;

namespace DCL.VoiceChat.Proximity
{
    /// <summary>
    /// Benchmarks <see cref="ProximityAudioPositionSystem.Update"/> with varying participant counts.
    /// Measures main-thread cost of position sync + spatial angle calculation.
    /// </summary>
    [Category("Performance")]
    public class ProximityAudioPositionSystemPerformanceTest : UnitySystemTestBase<ProximityAudioPositionSystem>
    {
        private IReadOnlyEntityParticipantTable entityParticipantTable;
        private ConcurrentDictionary<string, LivekitAudioSource> activeAudioSources;
        private readonly List<GameObject> gameObjects = new (128);

        [SetUp]
        public void SetUp()
        {
            entityParticipantTable = Substitute.For<IReadOnlyEntityParticipantTable>();
            activeAudioSources = new ConcurrentDictionary<string, LivekitAudioSource>();

            var cameraGo = CreateTrackedGameObject("PerfCamera");
            var camera = cameraGo.AddComponent<Camera>();
            world.Create(new CameraComponent(camera));

            var playerGo = CreateTrackedGameObject("PerfPlayer");
            playerGo.transform.position = new Vector3(0, 1.75f, 0);
            world.Create(new PlayerComponent(playerGo.transform));

            system = new ProximityAudioPositionSystem(world, entityParticipantTable, activeAudioSources);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();
        }

        [Test]
        [Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        public void UpdateWithNParticipants(int participantCount)
        {
            SetupParticipants(participantCount);

            // First update assigns ProximityAudioSourceComponent to all entities
            system.Update(0);

            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            Measure
               .Method(() => system.Update(0))
               .WarmupCount(5)
               .MeasurementCount(50)
               .GC()
               .Run();

            long gcBytes = gcAlloc.LastValue;
            gcAlloc.Dispose();

            Debug.Log($"[ProximityAudioPositionSystem] {participantCount} participants — GC.Alloc last: {gcBytes} bytes");
            Assert.That(gcBytes, Is.EqualTo(0), $"System.Update must be allocation-free, but allocated {gcBytes} bytes with {participantCount} participants");
        }

        private void SetupParticipants(int count)
        {
            for (int i = 0; i < count; i++)
            {
                string id = $"wallet-perf-{i}";

                var remoteGo = CreateTrackedGameObject($"Remote_{i}");
                remoteGo.transform.position = Random.insideUnitSphere * 15f;
                Entity entity = world.Create(new CharacterTransform(remoteGo.transform));

                LivekitAudioSource source = LivekitAudioSource.New();
                gameObjects.Add(source.gameObject);

                var entry = new IReadOnlyEntityParticipantTable.Entry(id, entity, RoomSource.ISLAND);

                entityParticipantTable
                   .TryGet(id, out Arg.Any<IReadOnlyEntityParticipantTable.Entry>())
                   .Returns(callInfo =>
                    {
                        callInfo[1] = entry;
                        return true;
                    });

                activeAudioSources[id] = source;
            }
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }
    }
}
