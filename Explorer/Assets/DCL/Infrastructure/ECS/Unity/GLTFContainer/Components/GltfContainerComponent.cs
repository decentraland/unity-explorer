using DCL.Diagnostics;
using System;
using DCL.ECSComponents;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Components
{
    public struct GltfContainerComponent
    {
        public string Hash => Promise.LoadingIntention.Hash;
        public string Name => Promise.LoadingIntention.Name;

        public ColliderLayer VisibleMeshesCollisionMask;
        public ColliderLayer InvisibleMeshesCollisionMask;
        public AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention> Promise;
        public LoadingState State;
        public bool NeedsColliderBoundsCheck;

        public Dictionary<Renderer, Material>? OriginalMaterials;

        /// <summary>
        ///     Reference to the root GameObject of the loaded GLTF asset
        /// </summary>
        public GameObject? RootGameObject;

        public GltfContainerComponent(ColliderLayer visibleMeshesCollisionMask, ColliderLayer invisibleMeshesCollisionMask, AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention> promise)
        {
            VisibleMeshesCollisionMask = visibleMeshesCollisionMask;
            InvisibleMeshesCollisionMask = invisibleMeshesCollisionMask;
            Promise = promise;
            State = LoadingState.Unknown;
            NeedsColliderBoundsCheck = true;
            OriginalMaterials = null;
            RootGameObject = null;
        }

        public static GltfContainerComponent CreateFaulty(ReportData reportData, Exception exception)
        {
            GltfContainerComponent component = new GltfContainerComponent();
            component.SetFaulty(reportData, exception);
            return component;
        }

        public void SetFaulty(ReportData reportData, Exception exception)
        {
            State = LoadingState.FinishedWithError;
            RootGameObject = null;

            Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.CreateFinalized(
                default,
                new StreamableLoadingResult<GltfContainerAsset>(reportData, exception));
        }
    }
}
