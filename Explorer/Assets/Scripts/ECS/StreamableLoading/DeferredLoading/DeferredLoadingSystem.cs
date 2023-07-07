using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Metadata;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace ECS.Prioritization.DeferredLoading
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class DeferredLoadingSystem<TAsset, TIntention> : BaseUnityLoopSystem where TIntention: struct, ILoadingIntention
    {
        private static readonly QueryDescription CREATE_LOADING_REQUEST = new QueryDescription()
                                                                         .WithAll<TIntention>()
                                                                         .WithNone<LoadingInProgress, StreamableLoadingResult<TAsset>>();
        private readonly Query query;

        private readonly IConcurrentBudgetProvider concurrentLoadingBudgetProvider;
        private readonly unsafe List<IntentionData> loadingIntentions;

        public DeferredLoadingSystem(World world, ConcurrentLoadingBudgetProvider concurrentLoadingBudgetProvider) : base(world)
        {
            this.concurrentLoadingBudgetProvider = concurrentLoadingBudgetProvider;
            query = World.Query(in CREATE_LOADING_REQUEST);
            loadingIntentions = new List<IntentionData>();
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
                    void* intentionPointer = UnsafeUtility.AddressOf(ref Unsafe.Add(ref intentionFirstElement, entityIndex));

                    var intentionData = new IntentionData()
                    {
                        PartitionComponent = partition,
                        IntentionDataPointer = intentionPointer,
                    };

                    loadingIntentions.Add(intentionData);
                }
            }

            loadingIntentions.Sort((p1, p2) => p1.PartitionComponent.CompareTo(p2.PartitionComponent));
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

    }

    public unsafe struct IntentionData
    {
        public PartitionComponent PartitionComponent;
        public void* IntentionDataPointer;
    }
}
