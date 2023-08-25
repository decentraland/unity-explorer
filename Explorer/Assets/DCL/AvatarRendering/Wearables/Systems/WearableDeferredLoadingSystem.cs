using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;

namespace DCL.AvatarRendering.Wearables.Systems
{
    /// <summary>
    ///     Weighs definitions and scenes loading against each other according to their partition
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadWearablesDTOSystem))]
    public partial class WearableDeferredLoadingSystem : DeferredLoadingSystem
    {
        private static readonly QueryDescription[] COMPONENT_HANDLERS;

        static WearableDeferredLoadingSystem()
        {
            COMPONENT_HANDLERS = new[]
            {
                CreateQuery<GetWearableIntention, WearableDTO>(),
                CreateQuery<GetWearableAssetBundleIntention, AssetBundleData>(),
            };
        }

        internal WearableDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider)
            : base(world, COMPONENT_HANDLERS, concurrentLoadingBudgetProvider) { }
    }
}
