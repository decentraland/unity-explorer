using Arch.Core;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using System;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using ECS.StreamableLoading.Common;
using UnityEngine;
using UnityEngine.Serialization;
using Utility;

namespace DCL.LOD
{
    public class LODAsset : IDisposable
    {
        public GameObject Root;
        public LOD_STATE State;
        public readonly LODKey LodKey; // Hashmap would probably be better
        public  TextureArraySlot?[] Slots;
        internal AssetBundleData AssetBundleReference;

        public enum LOD_STATE
        {
            UNINTIALIZED,
            FAILED,
            SUCCESS,
            WAITING_INSTANTIATION,
        }

        public LODAsset(LODKey lodKey)
        {
            LodKey = lodKey;
            Slots = Array.Empty<TextureArraySlot?>();
            AssetBundleReference = null;
            Root = null;

            State = LOD_STATE.WAITING_INSTANTIATION;

            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
        }

        public LODAsset(LODKey lodKey, AssetBundleData assetBundleData)
        {
            LodKey = lodKey;
            Slots = Array.Empty<TextureArraySlot?>();
            AssetBundleReference = assetBundleData;

            Root = null;

            State = LOD_STATE.WAITING_INSTANTIATION;

            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
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

            if (Root != null)
                Root.SetActive(true);

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

            if (Root != null)
                Root.SetActive(false);

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting)
                return;

            if (State == LOD_STATE.SUCCESS)
            {
                ProfilingCounters.LODInstantiatedInCache.Value++;
                ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value--;
            }
        }

        public void FinalizeInstantiation(GameObject newRoot, TextureArraySlot?[] slots)
        {
            newRoot.SetActive(true);
            Root = newRoot;
            Slots = slots;
            State = LOD_STATE.SUCCESS;
        }
    }
}
