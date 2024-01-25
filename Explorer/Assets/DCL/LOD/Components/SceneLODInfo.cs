using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using System;
using UnityEngine;
using Utility;

namespace DCL.LOD.Components
{
    public struct SceneLODInfo
    {
        public byte CurrentLODLevel;
        public LODAsset? CurrentLOD;
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public bool IsDirty;

        public void Dispose(World world, ILODAssetsPool lodAssetsPool)
        {
            CurrentLODPromise.ForgetLoading(world);

            if (CurrentLOD != null)
                CurrentLOD.TryRelease(lodAssetsPool);

            CurrentLOD = null;
        }

        public void ToggleDebugColors()
        {
            CurrentLOD?.ToggleDebugColors();
        }
    }

}
