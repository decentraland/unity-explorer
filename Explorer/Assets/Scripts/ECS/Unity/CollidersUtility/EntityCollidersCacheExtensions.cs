using Arch.Core;
using CRDT;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.SceneBoundsChecker;
using System.Collections.Generic;

namespace DCL.Interaction.Utility
{
    public static class EntityCollidersCacheExtensions
    {
        /// <summary>
        ///     Asset Must be ready at the moment of calling
        /// </summary>
        public static void Associate(this IEntityCollidersSceneCache cache, in GltfContainerComponent gltfContainerComponent, EntityReference entityReference, CRDTEntity sdkEntity)
        {
            GltfContainerAsset asset = gltfContainerComponent.Promise.Result.Value.Asset;

            if (asset.DecodedVisibleSDKColliders != null)
                cache.Associate(asset.DecodedVisibleSDKColliders, new ColliderSceneEntityInfo(entityReference, sdkEntity, gltfContainerComponent.VisibleMeshesCollisionMask));

            cache.Associate(asset.InvisibleColliders, new ColliderSceneEntityInfo(entityReference, sdkEntity, gltfContainerComponent.InvisibleMeshesCollisionMask));
        }

        public static void Associate(this IEntityCollidersSceneCache cache, IReadOnlyList<SDKCollider> colliders, ColliderSceneEntityInfo sceneEntityInfo)
        {
            for (var i = 0; i < colliders.Count; i++)
                cache.Associate(colliders[i].Collider, sceneEntityInfo);
        }

        public static void Remove(this IEntityCollidersSceneCache cache, IReadOnlyList<SDKCollider> colliders)
        {
            for (var i = 0; i < colliders.Count; i++)
                cache.Remove(colliders[i].Collider);
        }

        public static void Remove(this IEntityCollidersSceneCache cache, in GltfContainerAsset gltfContainerAsset)
        {
            if (gltfContainerAsset.DecodedVisibleSDKColliders != null)
                cache.Remove(gltfContainerAsset.DecodedVisibleSDKColliders);

            cache.Remove(gltfContainerAsset.InvisibleColliders);
        }
    }
}
