using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Interaction.Utility;
using DCL.LOD.Systems;
using DCL.SceneRunner.Scene;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
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
        private readonly IISSDescriptor? issDescriptor;

        internal CleanUpGltfContainerSystem(World world, IGltfContainerAssetsCache cache, IEntityCollidersSceneCache entityCollidersSceneCache, IPartitionComponent scenePartition, IISSDescriptor? issDescriptor) : base(world)
        {
            this.scenePartition = scenePartition;
            this.cache = cache;
            this.entityCollidersSceneCache = entityCollidersSceneCache;
            this.issDescriptor = issDescriptor;
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

        private void DestroyGLTFContainer(ref GltfContainerComponent component, bool partitionAllowsBridge)
        {
            if (component.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> result) && result.Succeeded)
            {
                entityCollidersSceneCache.Remove(result.Asset);

                // Bridge only if the scene's partition allows it AND the descriptor still has a slot for this hash
                // (capped to the exact number of times it appears in the descriptor — no duplicates).
                // Null descriptor means no ISS for this scene → no bridging.
                bool putInBridge = partitionAllowsBridge
                                   && issDescriptor != null
                                   && issDescriptor.TryReserveBridgeSlot(component.Hash);
                cache.Dereference(component.Hash, result.Asset, putInBridge);
            }

            component.RootGameObject = null;
            component.Promise.ForgetLoading(World);
        }

    }
}
