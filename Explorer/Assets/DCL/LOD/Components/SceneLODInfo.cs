using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using System;
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
                if (CurrentLOD.Value.LoadingFailed)
                    LastSuccessfulLOD?.Release();
                else
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
            if (newLod.LoadingFailed)
                ProfilingCounters.Failling_LOD_Amount.Value++;
            else
            {
                if (!newLod.LodKey.Equals(LastSuccessfulLOD))
                {
                    LastSuccessfulLOD?.Release();
                    LastSuccessfulLOD = newLod;
                }
            }

            CurrentLOD = newLod;
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
