using Arch.Core;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using DCL.VoiceChat.Nearby.Systems;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming.Audio;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Benchmarks <see cref="NearbyAudioPositionSystem.Update"/> with varying participant counts.
    /// Measures main-thread cost of position sync + spatial angle calculation.
    /// </summary>
    [Category("Performance")]
    public class NearbyAudioPositionSystemPerformanceTest : UnitySystemTestBase<NearbyAudioPositionSystem>
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

            system = new NearbyAudioPositionSystem(world, entityParticipantTable, activeAudioSources);
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

            // First update assigns NearbyAudioSourceComponent to all entities
            system.Update(0);

            Measure
               .Method(() => system.Update(0))
               .WarmupCount(5)
               .MeasurementCount(50)
               .GC()
               .Run();
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
