using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Interaction.Utility;
using DCL.LOD.Systems;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Cancel promises on the dying entities
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CleanUpGltfContainerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private IPartitionComponent scenePartition;
        private IGltfContainerAssetsCache cache;
        private IEntityCollidersSceneCache entityCollidersSceneCache;

        internal CleanUpGltfContainerSystem(World world, IGltfContainerAssetsCache cache, IEntityCollidersSceneCache entityCollidersSceneCache, IPartitionComponent scenePartition) : base(world)
        {
            this.scenePartition = scenePartition;
            this.cache = cache;
            this.entityCollidersSceneCache = entityCollidersSceneCache;
        }

        protected override void Update(float t)
        {
            FinalizeGLTFContainerQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void FinalizeGLTFContainer(ref GltfContainerComponent component)
        {
            DestroyGLTFContainer(ref component, false);
        }

        [Query]
        [All(typeof(GltfContainerComponent))]
        private void DestroyWithScenePartition(ref GltfContainerComponent component)
        {
            DestroyGLTFContainer(ref component, LODUtils.ShouldGoToTheBridge(scenePartition));
        }

        public void FinalizeComponents(in Query query)
        {
            DestroyWithScenePartitionQuery(World);
        }

        private void DestroyGLTFContainer(ref GltfContainerComponent component, bool putInBridge)
        {
            if (component.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> result) && result.Succeeded)
            {
                entityCollidersSceneCache.Remove(result.Asset);

                // Raw GLTFs use NoCache so the underlying data is not shared — dispose directly
                // instead of returning to the cache (which would later double-dispose)
                if (result.Asset.AssetData is GLTFData)
                    result.Asset.Dispose();
                else
                    cache.Dereference(component.Hash, result.Asset, putInBridge && result.Asset.IsISS);
            }

            component.RootGameObject = null;
            component.Promise.ForgetLoading(World);
        }

    }
}
