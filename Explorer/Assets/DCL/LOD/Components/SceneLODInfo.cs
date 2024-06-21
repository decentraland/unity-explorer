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
        public LODGroup LodGroup;
        public UnityEngine.LOD [] lods;
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
                CurrentLODLevel = byte.MaxValue, LodGroup = new GameObject().AddComponent<LODGroup>(), lods = new UnityEngine.LOD[2]
            };

        public void SetCurrentLOD(LODAsset newLod, Transform lodTransformParent)
        {
            if (!newLod.setup && newLod.Root != null)
            {
                LodGroup.fadeMode = LODFadeMode.CrossFade;
                LodGroup.animateCrossFading = true;
                LodGroup.transform.SetParent(lodTransformParent);
                newLod.Root.transform.SetParent(LodGroup.transform);
                var lod = new UnityEngine.LOD(newLod.LodKey.Level == 0 ? 0.5f : 0.05f,
                    newLod.Root.GetComponentsInChildren<Renderer>());
                lods[newLod.LodKey.Level] = lod;
                LodGroup.SetLODs(lods);
            }

            newLod.setup = true;
            
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
