using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.MutePersistence;
using DCL.VoiceChat.Nearby.Systems;
using ECS.TestSuite;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.PerformanceTesting;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Benchmarks <see cref="NearbyVoiceChatNametagSystem.Update"/> on a mixed steady-state
    /// avatar set: half are active speakers carrying a settled <see cref="VoiceChatNametagComponent"/>
    /// (exercises the Update-existing query, no mutation), the other half are non-speakers without
    /// the component (exercises the Add-missing query, <c>Resolve</c> returns null → no add).
    /// Reports the per-frame iteration cost of both reconciliation passes.
    /// </summary>
    [Category("Performance")]
    public class NearbyVoiceChatNametagSystemPerformanceTest : UnitySystemTestBase<NearbyVoiceChatNametagSystem>
    {
        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly List<GameObject> gameObjects = new (256);

        private FakeActiveSpeakers activeSpeakers;
        private NearbyVoiceChatStateModel stateModel;

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            activeSpeakers = new FakeActiveSpeakers();
            IRoom islandRoom = Substitute.For<IRoom>();
            islandRoom.ActiveSpeakers.Returns(activeSpeakers);

            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            var muteService = new NearbyMuteService(Substitute.For<INearbyMuteCache>(), Substitute.For<INearbyMuteRepository>());

            Entity playerEntity = world.Create();

            system = new NearbyVoiceChatNametagSystem(world, playerEntity, islandRoom, stateModel, muteService);
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);
            gameObjects.Clear();

            stateModel.Dispose();
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        [Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        public void UpdateMixedAvatarsWithNAvatars(int avatarCount)
        {
            int half = avatarCount / 2;
            for (int i = 0; i < avatarCount; i++)
            {
                string wallet = $"wallet-perf-{i}";
                Entity e = CreateAvatarEntity(wallet);

                if (i < half)
                {
                    activeSpeakers.Add(wallet);
                    world.Add(e, new VoiceChatNametagComponent(isSpeaking: true, type: VoiceChatType.NEARBY));
                }
            }

            // Warm one frame so any first-tick component-add work settles into steady state
            // before measurement (and so subsequent ticks observe an idempotent population).
            system.Update(0);

            Measure
               .Method(() => system.Update(0))
               .WarmupCount(5)
               .MeasurementCount(50)
               .GC()
               .Run();
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

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        private sealed class FakeActiveSpeakers : IActiveSpeakers
        {
            private readonly HashSet<string> set = new ();
            public event Action Updated = delegate { };

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
