using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using System;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Profiling;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using Utility;

namespace DCL.LOD.Components
{
    public struct SceneLODInfo
    {
        public LODGroup LodGroup;
        public List<LODAsset> LODAssets;

        public void Dispose(World world)
        {
            foreach (var lodVar in LODAssets)
            {
                if(LodGroup != null)
                    lodVar.lodGO?.transform.SetParent(LodGroup.transform.parent);
                lodVar.Release(world);
            }
            LODAssets.Clear();
            if (LodGroup != null)
                UnityObjectUtils.SafeDestroy(LodGroup.gameObject);
        }

        public static SceneLODInfo Create()
        {
            return new SceneLODInfo
            {
                LODAssets = new List<LODAsset>(),
            };
        }

        public bool ArePromisesConsumed()
        {
            foreach (var lodAsset in LODAssets)
            {
                if (!lodAsset.LODPromise.IsConsumed)
                    return false;
            }

            return true;
        }

        public Transform CreateLODGroup(Transform lodTransformParent)
        {
            // Create LODGroup
            if (LodGroup == null)
            {
                LodGroup = new GameObject().AddComponent<LODGroup>();
                LodGroup.fadeMode = LODFadeMode.CrossFade;
                LodGroup.animateCrossFading = true;
                LodGroup.transform.SetParent(lodTransformParent);
            }

            return LodGroup.transform;
        }

        private int AvailableLODAssetCount()
        {
            int nCount = 0;
            foreach (var lodAsset in LODAssets)
            {
                if (lodAsset.State == LODAsset.LOD_STATE.SUCCESS)
                    ++nCount;
            }

            return nCount;
        }

        public void ReEvaluateLODGroup()
        {
            if (LodGroup == null || LODAssets.Count == 0)
                return;

            LODAssets.Sort((a, b) => a.currentLODLevel.CompareTo(b.currentLODLevel));
            UnityEngine.LOD[] lods = new UnityEngine.LOD[AvailableLODAssetCount()];
            int nCount = 0;
            foreach (var lodAsset in LODAssets)
            {
                if (lodAsset.State == LODAsset.LOD_STATE.SUCCESS)
                {
                    Renderer[] lodRenderers = lodAsset.lodGO.GetComponentsInChildren<Renderer>();
                    float screenRelativeTransitionHeight = 0.05f;
                    lods[nCount] = new UnityEngine.LOD(screenRelativeTransitionHeight, lodRenderers);
                    lods[nCount].screenRelativeTransitionHeight = (nCount == 0) ? 0.5f : 0.05f;
                    ++nCount;
                }
            }

            if (lods.Length == 1)
                lods[0].screenRelativeTransitionHeight = 0.05f;

            LodGroup.SetLODs(lods.ToArray());
        }

        public bool HasLODKey(LODKey lodKey)
        {
            foreach (var lodAsset in LODAssets)
            {
                if (lodKey.Equals(lodAsset.LodKey))
                    return true;
            }

            return false;
        }
    }
}
