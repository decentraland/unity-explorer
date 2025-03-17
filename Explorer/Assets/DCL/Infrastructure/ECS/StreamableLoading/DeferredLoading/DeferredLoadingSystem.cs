using Arch.Core;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine.Pool;
using static ECS.StreamableLoading.Common.Components.StreamableLoadingState;

namespace ECS.StreamableLoading.DeferredLoading
{
    public abstract class DeferredLoadingSystem : BaseUnityLoopSystem
    {
        // There is suspicion that one of these operations take too long
        private static readonly ProfilerMarker<int> SORT_PROFILER_MARKER = new ($"{nameof(DeferredLoadingSystem)}.Sort", "Intentions Count");
        private static readonly ProfilerMarker<int> ANALYZE_BUDGET_PROFILER_MARKER = new ($"{nameof(DeferredLoadingSystem)}.AnalyzeBudget", "Intentions Count");
        private readonly IReleasablePerformanceBudget releasablePerformanceLoadingBudget;

        private readonly List<IntentionData> loadingIntentions;
        private readonly IPerformanceBudget memoryBudget;

        protected QueryDescription[] sameBoatQueries;

        protected DeferredLoadingSystem(World world, QueryDescription[] sameBoatQueries, IReleasablePerformanceBudget releasablePerformanceLoadingBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.sameBoatQueries = sameBoatQueries;
            this.releasablePerformanceLoadingBudget = releasablePerformanceLoadingBudget;
            this.memoryBudget = memoryBudget;
            loadingIntentions = ListPool<IntentionData>.Get()!;
        }

        protected static QueryDescription CreateQuery<TIntention, TAsset>() where TIntention: ILoadingIntention =>
            new QueryDescription()
               .WithAll<TIntention, IPartitionComponent, StreamableLoadingState>()
               .WithNone<StreamableLoadingResult<TAsset>>();

        protected override void Update(float t)
        {
            loadingIntentions.Clear();

            // All types of intentions are weighed against each other all together, not each type individually
            foreach (QueryDescription query in sameBoatQueries)
            {
                foreach (ref Chunk chunk in World!.Query(in query).GetChunkIterator())
                {
                    ref IPartitionComponent partitionFirstElement = ref chunk.GetFirst<IPartitionComponent>();
                    ref StreamableLoadingState stateFirstElement = ref chunk.GetFirst<StreamableLoadingState>();

                    foreach (int entityIndex in chunk)
                    {
                        ref StreamableLoadingState state = ref Unsafe.Add(ref stateFirstElement, entityIndex);
                        ref IPartitionComponent partition = ref Unsafe.Add(ref partitionFirstElement, entityIndex)!;

                        // Process only not evaluated and explicitly forbidden entities
                        if (state.Value is not (Status.NotStarted or Status.Forbidden))
                            continue;

                        var intentionData = new IntentionData
                        {
                            State = state,
                            PartitionComponent = partition,
                        };

                        loadingIntentions.Add(intentionData);
                    }
                }
            }

            if (loadingIntentions.Count == 0) return;

            using (SORT_PROFILER_MARKER.Auto(loadingIntentions.Count))
                loadingIntentions.Sort(static (p1, p2) => BucketBasedComparer.INSTANCE.Compare(p1.PartitionComponent, p2.PartitionComponent));

            using (ANALYZE_BUDGET_PROFILER_MARKER.Auto(loadingIntentions.Count))
                AnalyzeBudget();
        }

        private void AnalyzeBudget()
        {
            int i;

            for (i = 0; i < loadingIntentions.Count; i++)
            {
                if (!memoryBudget.TrySpendBudget()) break;
                if (!releasablePerformanceLoadingBudget.TrySpendBudget(out IAcquiredBudget acquiredBudget)) break;

                loadingIntentions[i].State.SetAllowed(acquiredBudget);
            }

            // Set the rest to forbidden
            for (; i < loadingIntentions.Count; i++)
                loadingIntentions[i].State.Forbid();
        }

        protected override void OnDispose()
        {
            ListPool<IntentionData>.Release(loadingIntentions);
        }

        internal struct IntentionData
        {
            public IPartitionComponent PartitionComponent;
            public StreamableLoadingState State;
        }
    }
}
