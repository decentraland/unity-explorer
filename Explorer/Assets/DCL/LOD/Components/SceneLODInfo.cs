using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using System;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Optimization.Pools;
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
        private GameObjectPool<LODGroup> lodGroupPool;

        public void Dispose(World world)
        {
            foreach (var lodVar in LODAssets)
            {
                if(LodGroup != null)
                    lodVar.lodGO?.transform.SetParent(LodGroup.transform.parent);
                lodVar.Release(world);
            }
            LODAssets.Clear();
            lodGroupPool?.Release(LodGroup);
            lodGroupPool = null;
            LodGroup = null;
        }

        public static SceneLODInfo Create()
        {
            return new SceneLODInfo
            {
                LODAssets = new List<LODAsset>(),
                LodGroup = null,
                lodGroupPool = null,
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

        public Transform CreateLODGroup(GameObjectPool<LODGroup> lodGroupPool, Transform lodTransformParent)
        {
            // Create LODGroup
            if (LodGroup == null)
            {
                this.lodGroupPool = lodGroupPool;
                LodGroup = lodGroupPool.Get();
                LodGroup.fadeMode = LODFadeMode.CrossFade;
                LodGroup.animateCrossFading = true;
                LodGroup.transform.SetParent(lodTransformParent); // The parent is the LODs pool parent
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

            // Ordered sort as the LOD Group expects the screen relative transition heights to be in order
            // and we might not have necessarily loaded them in order.
            LODAssets.Sort((a, b) => a.currentLODLevel.CompareTo(b.currentLODLevel));
            int assetCount = AvailableLODAssetCount();
            UnityEngine.LOD[] lods = new UnityEngine.LOD[LODAssets.Count];
            float screenRelativeTransitionHeight = 0.05f;
            int nBitMask = 0;
            for (int i = 0; i < lods.Length; i++)
            {
                if (LODAssets[i].State == LODAsset.LOD_STATE.SUCCESS)
                {
                    Renderer[] lodRenderers = LODAssets[i].lodGO.GetComponentsInChildren<Renderer>();
                    lods[i] = new UnityEngine.LOD(screenRelativeTransitionHeight, lodRenderers);
                    lods[i].screenRelativeTransitionHeight = (i == 0) ? 0.5f : 0.05f; // Not the best options, but without triangle density, it's just guess work really.
                    nBitMask += 1 << i;
                }
                else
                {
                    lods[i] = new UnityEngine.LOD(screenRelativeTransitionHeight, null);
                    lods[i].screenRelativeTransitionHeight = (i == 0) ? 0.5f : 0.05f; // Not the best options, but without triangle density, it's just guess work really.
                }
            }

            LodGroup.SetLODs(lods.ToArray());

            if (nBitMask != 3)
            {
                if ((nBitMask & 1) == 0)
                {
                    LodGroup.ForceLOD(0);
                }
                else if ((nBitMask & 2) == 0)
                {
                    LodGroup.ForceLOD(1);
                }
                else
                {
                    Assert.IsTrue(false); // Shouldn't get here, we have a problem
                }
            }
            else
            {
                LodGroup.ForceLOD(-1);
            }
        }

        // Quick function to check if LODAsset has already been loaded
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
