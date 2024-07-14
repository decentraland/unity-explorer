using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.SceneBoundsChecker;
using System;
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
                EnableColliders(asset.DecodedVisibleSDKColliders!, component.VisibleMeshesCollisionMask);
            }
            else if (asset.DecodedVisibleSDKColliders != null)
                DisableColliders(asset.DecodedVisibleSDKColliders);
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
            if (asset.DecodedVisibleSDKColliders != null)
                return;

            asset.DecodedVisibleSDKColliders = GltfContainerAsset.COLLIDERS_POOL.Get();

            for (var i = 0; i < asset.VisibleColliderMeshes.Count; i++)
            {
                GltfContainerAsset.VisibleMeshCollider visibleMeshCollider = asset.VisibleColliderMeshes[i];

                try
                {
                    Mesh mesh = visibleMeshCollider.Mesh;
                    GameObject go = visibleMeshCollider.GameObject;
                    MeshCollider newCollider = go.AddComponent<MeshCollider>();

                    // TODO Jobify: can be invoked from a worker thread
                    Physics.BakeMesh(mesh.GetInstanceID(), false);

                    newCollider.sharedMesh = mesh;

                    asset.DecodedVisibleSDKColliders.Add(new SDKCollider(newCollider));
                }
                catch (Exception e)
                {
                    throw new Exception($"Error adding collider to mesh {visibleMeshCollider.GameObject.name}", e);
                }
            }
        }
    }
}
