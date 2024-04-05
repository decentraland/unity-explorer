using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using System;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Optimization.Pools;
using UnityEngine;
using Utility;

namespace DCL.LOD
{
    public struct LODAsset : IDisposable
    {

        public readonly LODKey LodKey;
        public readonly GameObject Root;
        public AssetBundleData AssetBundleReference;
        public readonly bool LoadingFailed;
        private readonly ILODAssetsPool Pool;
        private readonly TextureArraySlot?[] Slots;

        public LODAsset(LODKey lodKey, GameObject root, AssetBundleData assetBundleReference, ILODAssetsPool pool, TextureArraySlot?[] slots)
        {
            LodKey = lodKey;
            Root = root;
            AssetBundleReference = assetBundleReference;
            LoadingFailed = false;
            Pool = pool;
            Slots = slots;

            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
        }

        //Constructor for LOD_0 which uses the default Scene Material (No texture array)
        public LODAsset(LODKey lodKey, GameObject root, AssetBundleData assetBundleReference, ILODAssetsPool pool)
        {
            LodKey = lodKey;
            Root = root;
            AssetBundleReference = assetBundleReference;
            LoadingFailed = false;
            Pool = pool;
            Slots = Array.Empty<TextureArraySlot?>();

            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
        }

        public LODAsset(LODKey lodKey)
        {
            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.Failling_LOD_Amount.Value++;
            LodKey = lodKey;
            Slots = Array.Empty<TextureArraySlot?>();
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
            {
                for (int i = 0; i < Slots.Length; i++)
                    Slots[i]?.FreeSlot();
            }


            UnityObjectUtils.SafeDestroy(Root);

            ProfilingCounters.LODAssetAmount.Value--;
            ProfilingCounters.LODInstantiatedInCache.Value--;
        }

        public void EnableAsset()
        {
            if (LoadingFailed)
                ProfilingCounters.Failling_LOD_Amount.Value++;

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
