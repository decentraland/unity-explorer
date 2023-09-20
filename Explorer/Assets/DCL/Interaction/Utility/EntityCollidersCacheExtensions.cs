using CRDT;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;

namespace DCL.Interaction.Utility
{
    public static class EntityCollidersCacheExtensions
    {
        /// <summary>
        ///     Asset Must be ready at the moment of calling
        /// </summary>
        public static void Associate(this IEntityCollidersSceneCache cache, in GltfContainerComponent gltfContainerComponent, CRDTEntity sdkEntity)
        {
            GltfContainerAsset asset = gltfContainerComponent.Promise.Result.Value.Asset;

            if (asset.VisibleMeshesColliders != null)
                cache.Associate(asset.VisibleMeshesColliders, new ColliderEntityInfo(sdkEntity, gltfContainerComponent.VisibleMeshesCollisionMask));

            cache.Associate(asset.InvisibleColliders, new ColliderEntityInfo(sdkEntity, gltfContainerComponent.InvisibleMeshesCollisionMask));
        }

        public static void Remove(this IEntityCollidersSceneCache cache, in GltfContainerAsset gltfContainerAsset)
        {
            if (gltfContainerAsset.VisibleMeshesColliders != null)
                cache.Remove(gltfContainerAsset.VisibleMeshesColliders);

            cache.Remove(gltfContainerAsset.InvisibleColliders);
        }
    }
}
