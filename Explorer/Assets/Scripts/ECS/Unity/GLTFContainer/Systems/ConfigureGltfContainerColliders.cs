using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.SceneBoundsChecker;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Systems
{
    internal static class ConfigureGltfContainerColliders
    {
        internal static void SetupColliders(ref GltfContainerComponent component, GltfContainerAsset asset)
        {
            SetupVisibleColliders(ref component, asset);
            SetupInvisibleColliders(ref component, asset);
        }

        internal static void SetupInvisibleColliders(ref GltfContainerComponent component, GltfContainerAsset asset)
        {
            // Invisible colliders are contained in the asset by default (not instantiation on demand needed)
            if (component.InvisibleMeshesCollisionMask != ColliderLayer.ClNone)
                EnableColliders(asset.InvisibleColliders, component.InvisibleMeshesCollisionMask);
            else
                DisableColliders(asset.InvisibleColliders);
        }

        internal static void SetupVisibleColliders(ref GltfContainerComponent component, GltfContainerAsset asset)
        {
            if (component.VisibleMeshesCollisionMask != ColliderLayer.ClNone)
            {
                TryInstantiateVisibleMeshesColliders(asset);
                EnableColliders(asset.VisibleMeshesColliders, component.VisibleMeshesCollisionMask);
            }
            else if (asset.VisibleMeshesColliders != null)
                DisableColliders(asset.VisibleMeshesColliders);
        }

        private static void EnableColliders(List<SDKCollider> colliders, ColliderLayer colliderLayer)
        {
            bool hasUnityLayer = PhysicsLayers.TryGetUnityLayerFromSDKLayer(colliderLayer, out int unityLayer);

            for (var i = 0; i < colliders.Count; i++)
            {
                SDKCollider collider = colliders[i];

                collider.IsActiveByEntity = hasUnityLayer;

                if (hasUnityLayer)
                    collider.Collider.gameObject.layer = unityLayer;

                // write the structure back
                colliders[i] = collider;
            }
        }

        private static void DisableColliders(List<SDKCollider> colliders)
        {
            for (var i = 0; i < colliders.Count; i++)
            {
                SDKCollider collider = colliders[i];

                collider.IsActiveByEntity = false;

                // write the structure back
                colliders[i] = collider;
            }
        }

        private static void TryInstantiateVisibleMeshesColliders(GltfContainerAsset asset)
        {
            // They can't change
            if (asset.VisibleMeshesColliders != null)
                return;

            asset.VisibleMeshesColliders = GltfContainerAsset.COLLIDERS_POOL.Get();

            for (var i = 0; i < asset.VisibleColliderMeshes.Count; i++)
            {
                MeshFilter meshFilter = asset.VisibleColliderMeshes[i];
                MeshCollider newCollider = meshFilter.gameObject.AddComponent<MeshCollider>();

                // TODO Jobify: can be invoked from a worker thread
                Physics.BakeMesh(meshFilter.sharedMesh.GetInstanceID(), false);

                newCollider.sharedMesh = meshFilter.sharedMesh;

                asset.VisibleMeshesColliders.Add(new SDKCollider(newCollider));
            }
        }
    }
}
