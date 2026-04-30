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
using Unity.PerformanceTesting;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Per-piece breakdown of <c>NearbyAudioPositionSystem</c>'s per-entity hot path. Each test
    /// isolates one managed/Unity-API access pattern that the system performs once per audio
    /// entity per frame. Together with <see cref="NearbyAudioPositionSystemPerformanceTest"/>
    /// (full Update) and <see cref="NearbyAudioSpatialAnglesPerformanceTest"/> (math only),
    /// the slices reveal where the main-thread cost actually lives — informs whether to chase
    /// Burst (math), TransformAccessArray (transform reads/writes), archetype filtering
    /// (range-tag checks), or component caching (head-anchor indirection).
    /// <para>
    /// Test cases mirror the system benchmark (10/50/100) for direct subtraction. Entities are
    /// allocated once per <c>[TestCase]</c> via <c>PopulateN</c>; the <c>Measure.Method</c>
    /// body iterates the prebuilt arrays so the measurement excludes any setup amortization
    /// and reflects steady-state per-entity cost.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class NearbyAudioPositionHotPathPerformanceTest
    {
        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private World world = null!;
        private FakeMuteCache muteCache = null!;
        private NearbyMuteService muteService = null!;
        private readonly List<GameObject> gameObjects = new (256);

        // Parallel-indexed caches populated by PopulateN. Tests iterate these directly so that
        // the measured loop body contains exactly one production-style access per slot.
        private Entity[] avatarEntities = null!;
        private AvatarBase[] avatarBases = null!;
        private LivekitAudioSource[] audioSources = null!;
        private string[] walletIds = null!;

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();
            world = World.Create();

            // Hand-rolled HashSet-backed cache instead of Substitute.For<INearbyMuteCache>():
            // NSubstitute proxies every IsMuted call through argument-matching + call-recording
            // (~20 µs/call + ~10 B GC), which inflated the prior measurement ~13× over production.
            // Repository is irrelevant to the IsMuted hot path — left as a mock for ctor satisfaction.
            muteCache = new FakeMuteCache();
            muteService = new NearbyMuteService(muteCache, Substitute.For<INearbyMuteRepository>());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);
            gameObjects.Clear();

            world?.Dispose();
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        private void PopulateN(int n)
        {
            avatarEntities = new Entity[n];
            avatarBases = new AvatarBase[n];
            audioSources = new LivekitAudioSource[n];
            walletIds = new string[n];

            Random.InitState(42);
            for (int i = 0; i < n; i++)
            {
                string id = $"wallet-perf-{i}";
                walletIds[i] = id;

                GameObject avatarGo = CreateTrackedGameObject($"Avatar_{i}");
                avatarGo.transform.position = Random.insideUnitSphere * 15f;

                AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
                GameObject headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{i}");
                headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
                headAnchorGo.transform.localPosition = new Vector3(0, 1.6f, 0);
                HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);
                avatarBases[i] = avatarBase;

                Entity avatarEntity = world.Create(
                    new Profile(id, id, new Avatar()),
                    avatarBase,
                    new CharacterTransform(avatarGo.transform));

                // Mirrors UpdateWithNParticipants: avatar carries InAudibleRangeTag with
                // IsSuspended=false, so the production TryGet hot-path takes the "active" branch.
                world.Add<InAudibleRangeTag>(avatarEntity);
                avatarEntities[i] = avatarEntity;

                LivekitAudioSource source = LivekitAudioSource.New();
                gameObjects.Add(source.gameObject);
                audioSources[i] = source;

                world.Create(new NearbyAudioSourceComponent(new StreamKey(id, "sid"), avatarEntity, source));
            }
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        // ───── Hot-path slices ─────────────────────────────────────────
        //
        // Subtract each slice from NearbyAudioPositionSystemPerformanceTest.UpdateWithNParticipants
        // medians to attribute system cost. Sum of slices ≠ system total because the system also
        // pays archetype iteration overhead and source-generated query dispatch.

        /// <summary>
        /// `!World.TryGet&lt;InAudibleRangeTag&gt;(e, out var tag) || tag.IsSuspended` — the per-entity
        /// gate the production query evaluates on every audio entity after the 4→2 query merge
        /// folded the suspend tag into a field of <see cref="InAudibleRangeTag"/>. Replaces the
        /// old `Has<>×2` slice; demonstrates whether the single managed-component lookup is cheap
        /// enough that the gate disappears in the noise of archetype iteration.
        /// </summary>
        [Test, Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(1000)]
        public void World_Has_RangeTags(int n)
        {
            PopulateN(n);
            int sink = 0;

            Measure
               .Method(() =>
                {
                    for (int i = 0; i < n; i++)
                    {
                        Entity e = avatarEntities[i];
                        if (!world.TryGet(e, out InAudibleRangeTag tag) || tag.IsSuspended)
                            sink++;
                    }
                })
               .WarmupCount(5).MeasurementCount(50).GC().Run();

            Assert.That(sink, Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>
        /// `World.TryGet&lt;AvatarBase&gt;(e, out _)` — managed-component lookup that the system runs
        /// once per audio entity to obtain the avatar's <c>HeadAnchorPoint</c>. Includes Arch's
        /// archetype dispatch + reference assignment to <c>out</c>.
        /// </summary>
        [Test, Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(1000)]
        public void World_TryGet_AvatarBase(int n)
        {
            PopulateN(n);
            int sink = 0;

            Measure
               .Method(() =>
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (world.TryGet(avatarEntities[i], out AvatarBase? avatar) && avatar != null)
                            sink++;
                    }
                })
               .WarmupCount(5).MeasurementCount(50).GC().Run();

            Assert.That(sink, Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>
        /// `avatarBase.HeadAnchorPoint.position` — chained managed property dereference (auto-property
        /// getter → Transform.position native call). The natural "fix" if this dominates is to cache
        /// the <c>Transform</c> directly inside <c>NearbyAudioSourceComponent</c> at binding time.
        /// </summary>
        [Test, Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(1000)]
        public void HeadAnchorPosition_Reads(int n)
        {
            PopulateN(n);
            Vector3 sink = Vector3.zero;

            Measure
               .Method(() =>
                {
                    for (int i = 0; i < n; i++)
                        sink += avatarBases[i].HeadAnchorPoint.position;
                })
               .WarmupCount(5).MeasurementCount(50).GC().Run();

            Assert.That(float.IsFinite(sink.x + sink.y + sink.z));
        }

        /// <summary>
        /// `src.transform.position = vec` — managed Transform setter (single Unity native call per
        /// entity). The "fix" via TransformAccessArray + IJobParallelForTransform is only justified
        /// if this slice — together with <see cref="HeadAnchorPosition_Reads"/> — dominates total
        /// system cost; otherwise the structural cost of a TransformAccessArray pin/unpin scheme
        /// outweighs its benefit.
        /// </summary>
        [Test, Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(1000)]
        public void TransformPosition_Writes(int n)
        {
            PopulateN(n);
            Vector3 pos = new (1f, 2f, 3f);

            Measure
               .Method(() =>
                {
                    for (int i = 0; i < n; i++)
                        audioSources[i].transform.position = pos;
                })
               .WarmupCount(5).MeasurementCount(50).GC().Run();
        }

        /// <summary>
        /// `src.enabled = !inactive; src.AudioSource.enabled = !inactive; src.AudioSource.mute = false;`
        /// — three managed Unity Behaviour/AudioSource setters. Includes the property dereference
        /// `src.AudioSource` (cached internally per LivekitAudioSource).
        /// </summary>
        [Test, Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(1000)]
        public void AudioSourceFlag_Writes(int n)
        {
            PopulateN(n);

            Measure
               .Method(() =>
                {
                    for (int i = 0; i < n; i++)
                    {
                        LivekitAudioSource src = audioSources[i];
                        src.enabled = true;
                        src.AudioSource.enabled = true;
                        src.AudioSource.mute = false;
                    }
                })
               .WarmupCount(5).MeasurementCount(50).GC().Run();
        }

        /// <summary>
        /// `muteService.IsMuted(walletId)` — HashSet-backed cache lookup (see <see cref="FakeMuteCache"/>),
        /// representative of the production cache's <c>HashSet&lt;string&gt;.Contains</c> hot path. Mixed
        /// hit/miss workload (~half pre-muted) so branch predictor and string-hashing path see realistic
        /// distribution rather than a pure-miss short circuit.
        /// </summary>
        [Test, Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(1000)]
        public void MuteService_Lookups(int n)
        {
            PopulateN(n);
            int sink = 0;

            // Pre-mute ~half the wallets so IsMuted alternates hit/miss instead of always returning false.
            for (int i = 0; i < n; i += 2)
                muteCache.SetMuted(walletIds[i], true);

            Measure
               .Method(() =>
                {
                    for (int i = 0; i < n; i++)
                        if (muteService.IsMuted(walletIds[i])) sink++;
                })
               .WarmupCount(5).MeasurementCount(50).GC().Run();

            Assert.That(sink, Is.GreaterThanOrEqualTo(0));
        }
    }
}
