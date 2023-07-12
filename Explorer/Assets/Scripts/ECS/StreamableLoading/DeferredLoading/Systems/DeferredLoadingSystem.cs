using Arch.Core;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.DeferredLoading
{
    public abstract class DeferredLoadingSystem : BaseUnityLoopSystem
    {
        internal unsafe struct IntentionData
        {
            public PartitionComponent PartitionComponent;
            public void* IntentionDataPointer;
            public ComponentHandler Handler;
        }

        /// <summary>
        ///     Strongly typed handler for each component type for which deferred loading is enabled
        /// </summary>
        public abstract class ComponentHandler
        {
            internal abstract unsafe void SetAllowed(void* dataPointer);

            internal abstract void Update(World world, List<IntentionData> loadingIntentions);
        }

        /// <summary>
        ///     It is state-less so we can have a single instance shared between multiple scenes
        /// </summary>
        /// <typeparam name="TAsset"></typeparam>
        /// <typeparam name="TIntention"></typeparam>
        public class ComponentHandler<TAsset, TIntention> : ComponentHandler where TIntention: struct, ILoadingIntention
        {
            private static readonly QueryDescription CREATE_LOADING_REQUEST = new QueryDescription()
                                                                             .WithAll<TIntention, PartitionComponent>()
                                                                             .WithNone<LoadingInProgress, StreamableLoadingResult<TAsset>>();

            internal override unsafe void Update(World world, List<IntentionData> loadingIntentions)
            {
                foreach (ref Chunk chunk in world.Query(in CREATE_LOADING_REQUEST).GetChunkIterator())
                {
                    ref TIntention intentionFirstElement = ref chunk.GetFirst<TIntention>();
                    ref PartitionComponent partitionFirstElement = ref chunk.GetFirst<PartitionComponent>();

                    foreach (int entityIndex in chunk)
                    {
                        ref PartitionComponent partition = ref Unsafe.Add(ref partitionFirstElement, entityIndex);
                        ref TIntention intention = ref Unsafe.Add(ref intentionFirstElement, entityIndex);

                        if (intention.IsAllowed())
                            continue;

                        var intentionData = new IntentionData
                        {
                            PartitionComponent = partition,
                            IntentionDataPointer = UnsafeUtility.AddressOf(ref intention),
                            Handler = this,
                        };

                        loadingIntentions.Add(intentionData);
                    }
                }
            }

            internal override unsafe void SetAllowed(void* dataPointer)
            {
                ref TIntention intentionRef = ref UnsafeUtility.AsRef<TIntention>(dataPointer);
                intentionRef.SetAllowed();
            }
        }

        private readonly IConcurrentBudgetProvider concurrentLoadingBudgetProvider;
        private readonly ComponentHandler[] componentHandlers;

        private readonly List<IntentionData> loadingIntentions;

        protected DeferredLoadingSystem(World world, ComponentHandler[] componentHandlers, IConcurrentBudgetProvider concurrentLoadingBudgetProvider) : base(world)
        {
            this.componentHandlers = componentHandlers;
            this.concurrentLoadingBudgetProvider = concurrentLoadingBudgetProvider;
            loadingIntentions = ListPool<IntentionData>.Get();
        }

        protected override void Update(float t)
        {
            loadingIntentions.Clear();

            // All types of intentions are weighed against each other all together, not each type individually
            foreach (ComponentHandler componentHandler in componentHandlers)
                componentHandler.Update(World, loadingIntentions);

            loadingIntentions.Sort(static (p1, p2) => p1.PartitionComponent.CompareTo(p2.PartitionComponent));
            AnalyzeBudget();
        }

        private unsafe void AnalyzeBudget()
        {
            foreach (IntentionData intentionToAnalyze in loadingIntentions)
            {
                if (!concurrentLoadingBudgetProvider.TrySpendBudget())
                    break;

                intentionToAnalyze.Handler.SetAllowed(intentionToAnalyze.IntentionDataPointer);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            ListPool<IntentionData>.Release(loadingIntentions);
        }
    }
}
