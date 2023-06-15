using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Cancel promises on the dying entities
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class CleanUpGltfContainerSystem : BaseUnityLoopSystem
    {
        private readonly IStreamableCache<GltfContainerAsset, string> cache;

        internal CleanUpGltfContainerSystem(World world, IStreamableCache<GltfContainerAsset, string> cache) : base(world)
        {
            this.cache = cache;
        }

        protected override void Update(float t)
        {
            ReleaseQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void Release(ref GltfContainerComponent component)
        {
            if (component.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> result) && result.Succeeded)
                cache.Dereference(component.Source, result.Asset);

            component.Promise.ForgetLoading(World);
        }
    }
}
