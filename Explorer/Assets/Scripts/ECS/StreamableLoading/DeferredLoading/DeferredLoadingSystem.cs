using Arch.Core;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.Prioritization.DeferredLoading;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.DeferredLoading
{
    public abstract class DeferredLoadingSystem<TAsset, TIntention> : BaseUnityLoopSystem where TIntention: struct, ILoadingIntention
    {
        private unsafe struct IntentionData
        {
            public PartitionComponent PartitionComponent;
            public void* IntentionDataPointer;
        }

        private static readonly QueryDescription CREATE_LOADING_REQUEST = new QueryDescription()
                                                                         .WithAll<TIntention, PartitionComponent>()
                                                                         .WithNone<LoadingInProgress, StreamableLoadingResult<TAsset>>();
        private readonly Query query;

        private readonly IConcurrentBudgetProvider concurrentLoadingBudgetProvider;
        private readonly List<IntentionData> loadingIntentions;

        protected DeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider) : base(world)
        {
            this.concurrentLoadingBudgetProvider = concurrentLoadingBudgetProvider;
            query = World.Query(in CREATE_LOADING_REQUEST);
            loadingIntentions = ListPool<IntentionData>.Get();
        }

        protected override unsafe void Update(float t)
        {
            loadingIntentions.Clear();

            foreach (ref Chunk chunk in query.GetChunkIterator())
            {
                ref TIntention intentionFirstElement = ref chunk.GetFirst<TIntention>();
                ref PartitionComponent partitionFirstElement = ref chunk.GetFirst<PartitionComponent>();

                foreach (int entityIndex in chunk)
                {
                    ref PartitionComponent partition = ref Unsafe.Add(ref partitionFirstElement, entityIndex);
                    ref TIntention intention = ref Unsafe.Add(ref intentionFirstElement, entityIndex);

                    if (intention.IsAllowed())
                        continue;

                    var intentionData = new IntentionData()
                    {
                        PartitionComponent = partition,
                        IntentionDataPointer = UnsafeUtility.AddressOf(ref intention),
                    };

                    loadingIntentions.Add(intentionData);
                }
            }

            loadingIntentions.Sort(static (p1, p2) => p1.PartitionComponent.CompareTo(p2.PartitionComponent));
            AnalyzeBudget();
        }

        private unsafe void AnalyzeBudget()
        {
            foreach (IntentionData intentionToAnalyze in loadingIntentions)
            {
                ref TIntention intentionRef = ref UnsafeUtility.AsRef<TIntention>(intentionToAnalyze.IntentionDataPointer);

                if (intentionRef.IsAllowed())
                    continue;

                if (!concurrentLoadingBudgetProvider.TrySpendBudget())
                    break;

                intentionRef.SetAllowed();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            ListPool<IntentionData>.Release(loadingIntentions);
        }
    }
}
