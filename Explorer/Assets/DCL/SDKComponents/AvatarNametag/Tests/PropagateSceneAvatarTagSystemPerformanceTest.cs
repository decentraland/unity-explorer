using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using DCL.SDKComponents.AvatarNametag.Systems;
using ECS.TestSuite;
using ECS.Unity.AvatarShape.Components;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using Unity.PerformanceTesting;
using UnityEngine.Profiling;

namespace DCL.SDKComponents.AvatarNametag.Tests
{
    /// <summary>
    ///     Guards the per-frame cost of <see cref="PropagateSceneAvatarTagSystem" />: the steady-state
    ///     update must stay allocation-free, and the dirty-write paths must stay linear.
    /// </summary>
    [Category("Performance")]
    public class PropagateSceneAvatarTagSystemPerformanceTest : UnitySystemTestBase<PropagateSceneAvatarTagSystem>
    {
        private const int NPC_COUNT = 1000;

        // Close to the theoretical maximum: the reserved remote-player range is 32..256.
        private const int PLAYER_COUNT = 200;

        private const int SCENE_LOCAL_ENTITY_BASE = 512;

        private World globalWorld = null!;
        private EntityParticipantTable entityParticipantTable = null!;

        [SetUp]
        public void Setup()
        {
            // Profile.NewRandomProfile validates the generated name against the feature registry.
            EcsTestsUtils.SetUpFeaturesRegistry();

            globalWorld = World.Create();
            entityParticipantTable = new EntityParticipantTable();

            // A real provider instead of an NSubstitute mock: the mock allocates on every
            // IsCurrent read, which would fail the allocation-free assertion below.
            var sceneStateProvider = new SceneStateProvider { IsCurrent = true };

            system = new PropagateSceneAvatarTagSystem(world, sceneStateProvider, entityParticipantTable, globalWorld, globalWorld.Create());
        }

        protected override void OnTearDown()
        {
            globalWorld.Dispose();
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        public void RunTheSteadyStateUpdateAllocationFree()
        {
            // Budgeted GC.Alloc Recorder with a liveness canary: GetAllocatedBytesForCurrentThread is inert
            // on editor Mono, and the strict AllocatingGCMemory constraint trips on noise outside Update.
            const int MEASURED_UPDATES = 1000;
            const int CANARY_ALLOCS = 16;
            const int ALLOC_SAMPLE_BUDGET = 100;

            SpawnTaggedNpcs(NPC_COUNT);

            // Warm-up applies every plate and JITs the query paths outside the measured region.
            for (var i = 0; i < 64; i++)
                system.Update(0);

            Recorder gcAllocRecorder = Recorder.Get("GC.Alloc");
            gcAllocRecorder.FilterToCurrentThread();
            gcAllocRecorder.enabled = false;
            gcAllocRecorder.enabled = true;

            for (var i = 0; i < MEASURED_UPDATES; i++)
                system.Update(0);

            byte[]? canary = null;

            for (var i = 0; i < CANARY_ALLOCS; i++)
                canary = new byte[16];

            gcAllocRecorder.enabled = false;
            int measured = gcAllocRecorder.sampleBlockCount;
            GC.KeepAlive(canary);

            Assert.GreaterOrEqual(measured, CANARY_ALLOCS,
                "GC.Alloc recorder did not observe the deliberate canary allocations — the probe is inert on this runtime.");

            Assert.Less(measured, ALLOC_SAMPLE_BUDGET,
                $"The steady-state update allocated GC memory with {NPC_COUNT} tagged NPCs and nothing dirty ({measured} GC.Alloc samples over {MEASURED_UPDATES} updates).");
        }

        [Test]
        [Performance]
        public void SweepAThousandCleanTagsPerUpdate()
        {
            SpawnTaggedNpcs(NPC_COUNT);
            system.Update(0);

            Measure.Method(() => system.Update(0))
                   .WarmupCount(10)
                   .IterationsPerMeasurement(100)
                   .MeasurementCount(20)
                   .GC()
                   .Run();
        }

        [Test]
        [Performance]
        public void ReapplyAThousandDirtyTagsPerUpdate()
        {
            PBAvatarNametag[] tags = SpawnTaggedNpcs(NPC_COUNT);
            system.Update(0);

            Measure.Method(() =>
                    {
                        for (var i = 0; i < tags.Length; i++)
                            tags[i].IsDirty = true;

                        system.Update(0);
                    })
                   .WarmupCount(5)
                   .IterationsPerMeasurement(10)
                   .MeasurementCount(20)
                   .GC()
                   .Run();
        }

        [Test]
        [Performance]
        public void ScanTwoHundredPlayersForTwoHundredPendingTagsPerUpdate()
        {
            PBAvatarNametag[] tags = SpawnBridgedPlayers(PLAYER_COUNT);
            system.Update(0);

            Measure.Method(() =>
                    {
                        for (var i = 0; i < tags.Length; i++)
                            tags[i].IsDirty = true;

                        system.Update(0);
                    })
                   .WarmupCount(5)
                   .IterationsPerMeasurement(10)
                   .MeasurementCount(20)
                   .GC()
                   .Run();
        }

        private PBAvatarNametag[] SpawnTaggedNpcs(int count)
        {
            var tags = new PBAvatarNametag[count];

            for (var i = 0; i < count; i++)
            {
                tags[i] = new PBAvatarNametag { Label = "Rank 42", IsDirty = true };

                world.Create(new CRDTEntity(SCENE_LOCAL_ENTITY_BASE + i), tags[i],
                    new SDKAvatarShapeComponent(globalWorld.Create()));
            }

            return tags;
        }

        /// <summary>
        ///     Remote players in the bridged shape: the scene writes to one entity, the
        ///     <see cref="SDKProfile" /> lives on another, forcing the NeedsPlayerScan path.
        /// </summary>
        private PBAvatarNametag[] SpawnBridgedPlayers(int count)
        {
            var tags = new PBAvatarNametag[count];

            for (var i = 0; i < count; i++)
            {
                int crdtId = SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + i;

                tags[i] = new PBAvatarNametag { Label = "Rank 42", IsDirty = true };
                world.Create(new CRDTEntity(crdtId), tags[i]);

                var wallet = $"0x{i:x8}";
                entityParticipantTable.Register(wallet, globalWorld.Create(), RoomSource.Island);

                var sdkProfile = new SDKProfile();
                sdkProfile.OverrideWith(Profile.NewRandomProfile(wallet));
                world.Create(new PlayerSceneCRDTEntity(new CRDTEntity(crdtId)), sdkProfile);
            }

            return tags;
        }
    }
}
