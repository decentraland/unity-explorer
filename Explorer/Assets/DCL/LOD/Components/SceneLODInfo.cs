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
        internal LODAsset? CurrentLOD;
        internal LODAsset? LastSuccessfulLOD;
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public bool IsDirty;
        
        
        public void Dispose(World world)
        {
            CurrentLODPromise.ForgetLoading(world);
            if (CurrentLOD != null)
            {
                if (CurrentLOD.State == LODAsset.LOD_STATE.FAILED)
                    LastSuccessfulLOD?.Release();
                CurrentLOD?.Release();
                CurrentLOD = null;
            }

        }

        public static SceneLODInfo Create() =>
            new()
            {
                CurrentLODLevel = byte.MaxValue
            };

        public void SetCurrentLOD(LODAsset newLod)
        {
            if (newLod.State == LODAsset.LOD_STATE.FAILED)
                ProfilingCounters.Failling_LOD_Amount.Value++;

            CurrentLOD = newLod;
        }

        public void InstantiateCurrentLOD()
        {
            LastSuccessfulLOD?.Release();
            LastSuccessfulLOD = CurrentLOD;
        }

        public LODAsset? GetCurrentLOD()
        {
            return CurrentLOD;
        }

        public LODAsset? GetCurrentSuccessfulLOD()
        {
            return LastSuccessfulLOD;
        }

        public void ResetToCurrentSuccesfullLOD()
        {
            CurrentLOD = LastSuccessfulLOD;
        }


    }

}
