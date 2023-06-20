using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Systems
{
    public partial class LoadGltfContainerSystem
    {
        private void SetupColliders(ref GltfContainerComponent component, GltfContainerAsset asset)
        {
            SetupVisibleColliders(ref component, asset);
            SetupInvisibleColliders(ref component, asset);
        }

        private void SetupInvisibleColliders(ref GltfContainerComponent component, GltfContainerAsset asset)
        {
            // Invisible colliders are contained in the asset by default (not instantiation on demand needed)
            if (component.InvisibleMeshesCollisionMask != ColliderLayer.ClNone)
                EnableColliders(asset.InvisibleColliders, component.InvisibleMeshesCollisionMask);
            else
                DisableColliders(asset.InvisibleColliders);
        }

        private void SetupVisibleColliders(ref GltfContainerComponent component, GltfContainerAsset asset)
        {
            if (component.VisibleMeshesCollisionMask != ColliderLayer.ClNone)
            {
                TryInstantiateVisibleMeshesColliders(asset);
                EnableColliders(asset.VisibleMeshesColliders, component.VisibleMeshesCollisionMask);
            }
            else if (asset.VisibleMeshesColliders != null)
                DisableColliders(asset.VisibleMeshesColliders);
        }

        private void EnableColliders(IReadOnlyList<Collider> colliders, ColliderLayer colliderLayer)
        {
            bool hasUnityLayer = PhysicsLayers.TryGetUnityLayerFromSDKLayer(colliderLayer, out int unityLayer);

            for (var i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];

                collider.enabled = hasUnityLayer;

                if (hasUnityLayer)
                    collider.gameObject.layer = unityLayer;
            }
        }

        private void DisableColliders(IReadOnlyList<Collider> colliders)
        {
            for (var i = 0; i < colliders.Count; i++)
                colliders[i].enabled = false;
        }

        private void TryInstantiateVisibleMeshesColliders(GltfContainerAsset asset)
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

                asset.VisibleMeshesColliders.Add(newCollider);
            }
        }
    }
}
