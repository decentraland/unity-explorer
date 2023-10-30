using Arch.Core;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Pool;
using Utility;

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

        protected override void Update(float t)
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
                            StatePointer = new ManagedTypePointer<StreamableLoadingState>(ref state),
                            PartitionComponent = partition,
                        };

                        loadingIntentions.Add(intentionData);
                    }
                }
            }

            if (loadingIntentions.Count == 0) return;

            loadingIntentions.Sort(static (p1, p2) => BucketBasedComparer.INSTANCE.Compare(p1.PartitionComponent, p2.PartitionComponent));
            AnalyzeBudget();
        }

        private void AnalyzeBudget()
        {
            int i;

            for (i = 0; i < loadingIntentions.Count; i++)
            {
                IntentionData intentionToAnalyze = loadingIntentions[i];

                if (!concurrentLoadingBudgetProvider.TrySpendBudget())
                    break;

                ref StreamableLoadingState state = ref intentionToAnalyze.StatePointer.Value;
                state.SetAllowed(AcquiredBudget.Create(concurrentLoadingBudgetProvider));
            }

            // Set the rest to forbidden
            for (; i < loadingIntentions.Count; i++)
            {
                IntentionData intentionToAnalyze = loadingIntentions[i];
                ref StreamableLoadingState state = ref intentionToAnalyze.StatePointer.Value;
                state.Value = StreamableLoadingState.Status.Forbidden;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            ListPool<IntentionData>.Release(loadingIntentions);
        }

        internal struct IntentionData
        {
            public IPartitionComponent PartitionComponent;
            public ManagedTypePointer<StreamableLoadingState> StatePointer;
        }
    }
}
