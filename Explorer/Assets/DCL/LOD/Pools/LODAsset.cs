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
        private static readonly ListObjectPool<Material> MATERIALS_LIST_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);
        
        public LODKey LodKey;
        public GameObject Root;
        public AssetBundleData AssetBundleReference;
        public readonly bool LoadingFailed;
        private readonly ILODAssetsPool Pool;
        private readonly TextureArraySlot[] Slots;
        private readonly IExtendedObjectPool<Material> MaterialPool;

        public LODAsset(LODKey lodKey, GameObject root, AssetBundleData assetBundleReference, ILODAssetsPool pool, TextureArraySlot[] slots, IExtendedObjectPool<Material> materialPool)
        {
            LodKey = lodKey;
            Root = root;
            AssetBundleReference = assetBundleReference;
            LoadingFailed = false;
            Pool = pool;
            Slots = slots;
            MaterialPool = materialPool;

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
            Slots = Array.Empty<TextureArraySlot>();
            ;
            MaterialPool = null;
            
            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
        }

        public LODAsset(LODKey lodKey)
        {
            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.Failling_LOD_Amount.Value++;
            LodKey = lodKey;
            Slots = Array.Empty<TextureArraySlot>();
            LoadingFailed = true;
            Root = null;
            AssetBundleReference = null;
            Pool = null;
            MaterialPool = null;
        }

        public void Dispose()
        {
            if (LoadingFailed) return;

            AssetBundleReference.Dereference();
            AssetBundleReference = null;

            if (!LodKey.Level.Equals(0))
            {
                //TODO: (ASK MISHA) Is this release of the material pool ok?
                using (var pooledList = Root.GetComponentsInChildrenIntoPooledList<Renderer>(true))
                {
                    foreach (var renderer in pooledList.Value)
                    {
                        var materialsToRelease =  MATERIALS_LIST_POOL.Get();
                        renderer.GetMaterials(materialsToRelease);
                        foreach (var rendererMaterial in materialsToRelease)
                            MaterialPool.Release(rendererMaterial);
                        MATERIALS_LIST_POOL.Release(materialsToRelease);
                    }
                }

                for (int i = 0; i < Slots.Length; i++)
                    Slots[i].FreeSlot();
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
