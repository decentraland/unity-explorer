using Arch.Core;
using Arch.SystemGroups;
using DCL.PerformanceBudgeting.BudgetProvider;
using ECS.StreamableLoading.AssetBundles;
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
        private static readonly QueryDescription[] COMPONENT_HANDLERS;

        static AssetsDeferredLoadingSystem()
        {
            COMPONENT_HANDLERS = new[]
            {
                CreateQuery<GetAssetBundleIntention, AssetBundleData>(),
                CreateQuery<GetTextureIntention, Texture2D>(),
            };
        }

        internal AssetsDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider)
            : base(world, COMPONENT_HANDLERS, concurrentLoadingBudgetProvider) { }
    }
}
