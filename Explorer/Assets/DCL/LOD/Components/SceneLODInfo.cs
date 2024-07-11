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
        public GameObject Root;
        public byte CurrentLODLevel;
        public List<LODAsset> LODAssets;

        public void Dispose(World world)
        {
            foreach (var lodVar in LODAssets)
            {
                lodVar?.Release(world);
            }
            UnityObjectUtils.SafeDestroy(Root);
            //CurrentLODPromise.ForgetLoading(world);
            // if (CurrentVisibleLOD != null && !CurrentVisibleLOD.LodKey.Equals(CurrentLOD))
            //     CurrentVisibleLOD.Release();
            //CurrentLOD?.Release();
        }

        public static SceneLODInfo Create()
        {
            GameObject root = new GameObject();
            LODGroup lodGroup = root.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            //lodGroup.ForceLOD(0);
            return new SceneLODInfo
            {
                CurrentLODLevel = byte.MaxValue,
                LODAssets = new List<LODAsset>(),
                Root = root,
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

        public void ReEvaluateLODGroup(Transform lodTransformParent)
        {
            LODAssets.Sort((a, b) => a.currentLODLevel.CompareTo(b.currentLODLevel));
            UnityEngine.LOD[] lods = new UnityEngine.LOD[LODAssets.Count];
            for (int i = 0; i < LODAssets.Count; ++i)
            {
                lods[i] = LODAssets[i].lod;
                lods[i].screenRelativeTransitionHeight = (i == 0) ? 0.5f : 0.05f;
            }

            if (lods.Length == 1)
                lods[0].screenRelativeTransitionHeight = 0.05f;

            LODGroup lodGroup = Root.GetComponent<LODGroup>();
            lodGroup.transform.SetParent(lodTransformParent);
            lodGroup.SetLODs(lods.ToArray());
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
