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

        public void Dispose(World world)
        {
            CurrentLODPromise.ForgetLoading(world);

            CurrentLOD?.Release();
            CurrentLOD = null;
        }

        public static SceneLODInfo Create() =>
            new()
            {
                CurrentLODLevel = byte.MaxValue
            };
    }

}
