using Arch.Core;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using System;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using ECS.StreamableLoading.Common;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.LOD
{
    public class LODAsset : IDisposable
    {
        public byte currentLODLevel; // Only used for sorting during ReEvaluateLODGroup() in SceneLODInfo
        public LOD_STATE State;
        public readonly LODKey LodKey; // Hashmap would probably be better
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> LODPromise;
        public GameObject lodGO;
        public  TextureArraySlot?[] Slots;
        private readonly ILODAssetsPool Pool;
        internal AssetBundleData AssetBundleReference;

        public enum LOD_STATE
        {
            UNINTIALIZED,
            FAILED,
            SUCCESS,
            WAITING_INSTANTIATION,
        }

        public LODAsset(LODKey lodKey, ILODAssetsPool pool)
        {
            LodKey = lodKey;
            Pool = pool;
            Slots = Array.Empty<TextureArraySlot?>();
            AssetBundleReference = null;
            lodGO = null;

            State = LOD_STATE.WAITING_INSTANTIATION;

            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
        }

        public void SetAssetBundleReference(AssetBundleData assetBundleData)
        {
            AssetBundleReference = assetBundleData;
        }

        public void Dispose()
        {
            if (State == LOD_STATE.FAILED) return;

            AssetBundleReference.Dereference();
            AssetBundleReference = null;

            for (int i = 0; i < Slots.Length; i++)
                Slots[i]?.FreeSlot();

            ProfilingCounters.LODAssetAmount.Value--;
            ProfilingCounters.LODInstantiatedInCache.Value--;
        }

        public void EnableAsset()
        {
            if (State == LOD_STATE.FAILED)
                return;

            lodGO?.SetActive(true);

            if (State == LOD_STATE.SUCCESS)
            {
                ProfilingCounters.LODInstantiatedInCache.Value--;
                ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
            }
        }

        public void DisableAsset()
        {
            if (State == LOD_STATE.FAILED)
                return;

            lodGO?.SetActive(false);

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting)
                return;

            if (State == LOD_STATE.SUCCESS)
            {
                ProfilingCounters.LODInstantiatedInCache.Value++;
                ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value--;
            }
        }

        public void Release(World world)
        {
            LODPromise.ForgetLoading(world);
            Pool.Release(LodKey, this);
        }

        public void FinalizeInstantiation(GameObject newRoot, TextureArraySlot?[] slots)
        {
            newRoot.SetActive(true);
            lodGO = newRoot;
            Slots = slots;
            State = LOD_STATE.SUCCESS;
        }
    }
}
