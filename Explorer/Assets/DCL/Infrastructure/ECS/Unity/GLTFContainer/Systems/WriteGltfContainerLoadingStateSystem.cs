using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.GLTFContainer.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using Utility;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Utility to write Gltf Container Loading State to CRDT (propagate it back to the scene)
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(FinalizeGltfContainerLoadingSystem))]
    public partial class WriteGltfContainerLoadingStateSystem : BaseUnityLoopSystem
    {
        /// <summary>
        ///     Golden-capture determinism: loading states reach the scene only on scene-tick bucket
        ///     boundaries (10 s of scene clock), in entity-id order. Real load completions land
        ///     on wall-clock-dependent ticks, so any scene behaviour keyed to them (chained tweens,
        ///     on-ready animation starts) diverges per boot and per platform; batching every
        ///     completion of a bucket onto one canonical tick makes the scene's subsequent evolution
        ///     a pure function of the deterministic clock. The bucket must divide the frozen scene
        ///     clock ceiling with room for several reaction cycles: with the phase-locked clock,
        ///     everything requested during bootstrap flushes on the first resumed tick and chained
        ///     reactions get one more boundary before the ceiling.
        /// </summary>
        // Post-initial chains load in wall time (~1-10 s) while ticks run at up to 40 Hz, so the
        // window between canonical flushes must exceed the worst chain load at that rate or the
        // delivery races the boundary; 900 ticks puts the one chain flush at tick 2400.
        private const uint QUANTIZE_BUCKET_TICKS = 1800;

        private static readonly bool QUANTIZE_TO_SCENE_CLOCK =
            System.Environment.GetEnvironmentVariable("DCL_PLAZABENCH_DETERM_CLOCK") == "1";

        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        private readonly EntityEventBuffer<GltfContainerComponent> changedGltfs;
        private readonly EntityEventBuffer<GltfContainerComponent>.ForEachDelegate eventHandler;

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly SortedList<CRDTEntity, LoadingState> quantizedPending = new ();
        private uint lastFlushedBucket;

        // Timing diagnostics for the harness: every flush appends when it ran, how many states it
        // carried, and when the newest of them was buffered — all on the shared process-start
        // epoch — so the real-time hold gates are set from measured load tails, not guesses.
        private static readonly string TIMING_FILE = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"golden-loadstates-{System.Diagnostics.Process.GetCurrentProcess().Id}.txt");
        private double lastBufferedAt;

        public WriteGltfContainerLoadingStateSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, EntityEventBuffer<GltfContainerComponent> changedGltfs,
            ISceneStateProvider sceneStateProvider)
            : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.changedGltfs = changedGltfs;
            this.sceneStateProvider = sceneStateProvider;
            eventHandler = PropagateChangedState;
        }

        protected override void Update(float t)
        {
            changedGltfs.ForEach(eventHandler);

            if (QUANTIZE_TO_SCENE_CLOCK)
                FlushOnBucketBoundary();
        }

        private void PropagateChangedState(Entity entity, GltfContainerComponent component)
        {
            if (!World.TryGet(entity, out CRDTEntity sdkEntity)) return;

            if (QUANTIZE_TO_SCENE_CLOCK)
            {
                // Last state wins within a bucket; SortedList keeps the canonical flush order.
                quantizedPending[sdkEntity] = component.State;
                lastBufferedAt = (System.DateTime.UtcNow - ProcessEpoch.StartUtc).TotalSeconds;
                return;
            }

            Write(sdkEntity, component.State);
        }

        /// <summary>
        ///     This system's group is synced with the scene loop, so while the phase hold parks a
        ///     scene nothing here runs at all: the FINISHED backlog can only drain across the
        ///     resumed ticks, taking a wall-dependent number of them (~35 s measured worst).
        ///     Delivering at a fixed dispatched tick past that drain makes the arrival canonical:
        ///     every machine's scene observes the complete initial world on the same tick.
        /// </summary>
        private const uint INITIAL_FLUSH_TICK = 1500;

        private bool initialFlushDone;

        private void FlushOnBucketBoundary()
        {
            if (!initialFlushDone)
            {
                if (sceneStateProvider.TickNumber < INITIAL_FLUSH_TICK) return;

                initialFlushDone = true;
                lastFlushedBucket = sceneStateProvider.TickNumber / QUANTIZE_BUCKET_TICKS;
                Flush();
                return;
            }

            uint bucket = sceneStateProvider.TickNumber / QUANTIZE_BUCKET_TICKS;

            if (bucket == lastFlushedBucket) return;

            lastFlushedBucket = bucket;
            Flush();
        }

        private void Flush()
        {
            if (quantizedPending.Count == 0) return;

            for (var i = 0; i < quantizedPending.Count; i++)
                Write(quantizedPending.Keys[i], quantizedPending.Values[i]);

            try
            {
                System.IO.File.AppendAllText(TIMING_FILE,
                    $"flush t={(System.DateTime.UtcNow - ProcessEpoch.StartUtc).TotalSeconds:F1} tick={sceneStateProvider.TickNumber} count={quantizedPending.Count} lastBuffered={lastBufferedAt:F1}\n");
            }
            catch (System.Exception) { /* diagnostics only */ }

            quantizedPending.Clear();
        }

        private void Write(CRDTEntity sdkEntity, LoadingState state)
        {
            ecsToCRDTWriter.PutMessage<PBGltfContainerLoadingState, LoadingState>(
                static (component, loadingState) => component.CurrentState = loadingState,
                sdkEntity,
                state
            );
        }
    }
}
