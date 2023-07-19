using Arch.Core;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.BudgetProvider;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace ECS.Unity.GLTFContainer.Asset.DeferredInstantiating
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateBefore(typeof(CreateGltfAssetFromAssetBundleSystem))]
    public partial class DeferredInstantiatingSystem : BaseUnityLoopSystem
    {

        protected static QueryDescription query =
            new QueryDescription()
               .WithAll<IPartitionComponent, StreamableInstantiatingState>()
               .WithNone<StreamableLoadingResult<GltfContainerAsset>>();

        private readonly List<IntentionData> loadingIntentions;
        private readonly IConcurrentBudgetProvider concurrentInstantionProvider;

        public DeferredInstantiatingSystem(World world, IConcurrentBudgetProvider budgetProvider) : base(world)
        {
            this.concurrentInstantionProvider = budgetProvider;
            loadingIntentions = new List<IntentionData>();
        }


        protected override unsafe void Update(float t)
        {
            loadingIntentions.Clear();

            // All types of intentions are weighed against each other all together, not each type individually
            foreach (ref Chunk chunk in World.Query(in query).GetChunkIterator())
            {
                ref IPartitionComponent partitionFirstElement = ref chunk.GetFirst<IPartitionComponent>();
                ref StreamableInstantiatingState stateFirstElement = ref chunk.GetFirst<StreamableInstantiatingState>();

                foreach (int entityIndex in chunk)
                {
                    ref StreamableInstantiatingState state = ref Unsafe.Add(ref stateFirstElement, entityIndex);
                    ref IPartitionComponent partition = ref Unsafe.Add(ref partitionFirstElement, entityIndex);

                    // Process only not evaluated and explicitly forbidden entities
                    if (state.Value is not (StreamableInstantiatingState.Status.Forbidden))
                        continue;

                    var intentionData = new IntentionData
                    {
                        StatePointer = UnsafeUtility.AddressOf(ref state),
                        PartitionComponent = partition
                    };

                    loadingIntentions.Add(intentionData);
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
                ref StreamableInstantiatingState state = ref UnsafeUtility.AsRef<StreamableInstantiatingState>(loadingIntentions[i].StatePointer);

                if (!concurrentInstantionProvider.TrySpendBudget(state.InstantiationCost))
                    break;

                state.SetAllowed(AcquiredBudget.Create(concurrentInstantionProvider, state.InstantiationCost));
            }
        }

        internal unsafe struct IntentionData
        {
            public IPartitionComponent PartitionComponent;
            public void* StatePointer;
        }
    }
}

