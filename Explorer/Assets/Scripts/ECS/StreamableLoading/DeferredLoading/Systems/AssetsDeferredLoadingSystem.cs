using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.StreamableLoading.Textures;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading
{
    /// <summary>
    ///     Weighs asset bundles and textures against each other according to their partition
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(PrepareAssetBundleLoadingParametersSystem))]
    [UpdateBefore(typeof(LoadTextureSystem))]
    [UpdateBefore(typeof(LoadAssetBundleSystem))]
    public partial class AssetsDeferredLoadingSystem : DeferredLoadingSystem
    {
        private static readonly ComponentHandler[] COMPONENT_HANDLERS =
        {
            new ComponentHandler<AssetBundleData, GetAssetBundleIntention>(),
            new ComponentHandler<Texture2D, GetTextureIntention>(),
        };

        internal AssetsDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider)
            : base(world, COMPONENT_HANDLERS, concurrentLoadingBudgetProvider) { }
    }
}
