using Arch.Core;
using ECS.Abstract;
using ECS.BudgetProvider;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.DeferredLoading
{
    public abstract class DeferredLoadingSystem : BaseUnityLoopSystem
    {
        private readonly IConcurrentBudgetProvider concurrentLoadingBudgetProvider;

        private readonly List<IntentionData> loadingIntentions;

        private readonly QueryDescription[] sameBoatQueries;

        protected DeferredLoadingSystem(World world, QueryDescription[] sameBoatQueries, IConcurrentBudgetProvider concurrentLoadingBudgetProvider) : base(world)
        {
            this.sameBoatQueries = sameBoatQueries;
            this.concurrentLoadingBudgetProvider = concurrentLoadingBudgetProvider;
            loadingIntentions = ListPool<IntentionData>.Get();
        }

        protected static QueryDescription CreateQuery<TIntention, TAsset>() where TIntention: ILoadingIntention =>
            new QueryDescription()
               .WithAll<TIntention, IPartitionComponent, StreamableLoadingState>()
               .WithNone<StreamableLoadingResult<TAsset>>();

        protected override unsafe void Update(float t)
        {
            loadingIntentions.Clear();

            // All types of intentions are weighed against each other all together, not each type individually
            foreach (QueryDescription query in sameBoatQueries)
            {
                foreach (ref Chunk chunk in World.Query(in query).GetChunkIterator())
                {
                    ref IPartitionComponent partitionFirstElement = ref chunk.GetFirst<IPartitionComponent>();
                    ref StreamableLoadingState stateFirstElement = ref chunk.GetFirst<StreamableLoadingState>();

                    foreach (int entityIndex in chunk)
                    {
                        ref StreamableLoadingState state = ref Unsafe.Add(ref stateFirstElement, entityIndex);
                        ref IPartitionComponent partition = ref Unsafe.Add(ref partitionFirstElement, entityIndex);

                        // Process only not evaluated and explicitly forbidden entities
                        if (state.Value is not (StreamableLoadingState.Status.NotStarted or StreamableLoadingState.Status.Forbidden))
                            continue;

                        var intentionData = new IntentionData
                        {
                            StatePointer = UnsafeUtility.AddressOf(ref state),
                            PartitionComponent = partition,
                        };

                        loadingIntentions.Add(intentionData);
                    }
                }
            }

            if (loadingIntentions.Count == 0) return;

            loadingIntentions.Sort(static (p1, p2) => p1.PartitionComponent.CompareTo(p2.PartitionComponent));
            AnalyzeBudget();
        }

        private unsafe void AnalyzeBudget()
        {
            int i;

            for (i = 0; i < loadingIntentions.Count; i++)
            {
                IntentionData intentionToAnalyze = loadingIntentions[i];

                if (!concurrentLoadingBudgetProvider.TrySpendBudget())
                    break;

                ref StreamableLoadingState state = ref UnsafeUtility.AsRef<StreamableLoadingState>(intentionToAnalyze.StatePointer);
                state.SetAllowed(AcquiredBudget.Create(concurrentLoadingBudgetProvider));
            }

            // Set the rest to forbidden
            for (; i < loadingIntentions.Count; i++)
            {
                IntentionData intentionToAnalyze = loadingIntentions[i];
                ref StreamableLoadingState state = ref UnsafeUtility.AsRef<StreamableLoadingState>(intentionToAnalyze.StatePointer);
                state.Value = StreamableLoadingState.Status.Forbidden;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            ListPool<IntentionData>.Release(loadingIntentions);
        }

        internal unsafe struct IntentionData
        {
            public IPartitionComponent PartitionComponent;
            public void* StatePointer;
        }
    }
}
