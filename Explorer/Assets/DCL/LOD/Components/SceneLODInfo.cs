using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using System;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Profiling;
using UnityEngine;
using Utility;

namespace DCL.LOD.Components
{
    public struct SceneLODInfo
    {
        public byte CurrentLODLevel;
        public LODAsset CurrentLOD;
        public LODAsset CurrentVisibleLOD;
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public bool IsDirty;
        
        public void Dispose(World world)
        {
            CurrentLODPromise.ForgetLoading(world);
            if (CurrentVisibleLOD != null && !CurrentVisibleLOD.LodKey.Equals(CurrentLOD))
                CurrentVisibleLOD.Release();
            CurrentLOD?.Release();
        }

        public static SceneLODInfo Create() =>
            new()
            {
                CurrentLODLevel = byte.MaxValue
            };

        public void SetCurrentLOD(LODAsset newLod)
        {
            CurrentLOD = newLod;
            UpdateCurrentVisibleLOD();
        }

        public void UpdateCurrentVisibleLOD()
        {
            if (CurrentLOD?.State == LODAsset.LOD_STATE.SUCCESS)
            {
                CurrentVisibleLOD?.Release();
                CurrentVisibleLOD = CurrentLOD;
            }
        }

        public void ResetToCurrentVisibleLOD()
        {
            CurrentLOD = CurrentVisibleLOD;
        }
    }

}
