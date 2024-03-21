using DCL.AssetsProvision;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using System;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.Optimization.Pools;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Utility;

namespace DCL.LOD
{
    public struct LODAsset : IDisposable
    {
        public LODKey LodKey;
        public GameObject Root;
        public AssetBundleData AssetBundleReference;
        public readonly bool LoadingFailed;
        private readonly ILODAssetsPool Pool;
        private TextureArraySlot[] slots;

        public LODAsset(LODKey lodKey, GameObject root, AssetBundleData assetBundleReference, ILODAssetsPool pool, TextureArraySlot[] slots)
        {
            LodKey = lodKey;
            Root = root;
            AssetBundleReference = assetBundleReference;
            LoadingFailed = false;
            Pool = pool;
            this.slots = slots;

            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
        }
        
        public LODAsset(LODKey lodKey, GameObject root, AssetBundleData assetBundleReference, ILODAssetsPool pool)
        {
            LodKey = lodKey;
            Root = root;
            AssetBundleReference = assetBundleReference;
            LoadingFailed = false;
            Pool = pool;
            this.slots = Array.Empty<TextureArraySlot>();;

            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
        }

        public LODAsset(LODKey lodKey)
        {
            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.Failling_LOD_Amount.Value++;
            LodKey = lodKey;
            this.slots = Array.Empty<TextureArraySlot>();
            LoadingFailed = true;
            Root = null;
            AssetBundleReference = null;
            Pool = null;
        }

        public void Dispose()
        {
            if (LoadingFailed) return;

            AssetBundleReference.Dereference();
            AssetBundleReference = null;
            
            if (!LodKey.Level.Equals(0))
                for (var i = 0; i < slots.Length; i++)
                    slots[i].FreeSlot();

            UnityObjectUtils.SafeDestroy(Root);

            ProfilingCounters.LODAssetAmount.Value--;
            ProfilingCounters.LODInstantiatedInCache.Value--;
        }

        public void EnableAsset()
        {
            if (LoadingFailed)
            {
                ProfilingCounters.Failling_LOD_Amount.Value++;
                return;
            }

            ProfilingCounters.LODInstantiatedInCache.Value--;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
            Root.SetActive(true);
        }

        public void DisableAsset()
        {
            if (LoadingFailed) return;

            ProfilingCounters.LODInstantiatedInCache.Value++;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value--;

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return;

            Root.SetActive(false);
        }

        public void Release()
        {
            if (LoadingFailed)
                ProfilingCounters.Failling_LOD_Amount.Value--;
            else
                Pool.Release(LodKey, this);
        }
    }
}
