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
        public  TextureArraySlot?[] Slots;
        internal AssetBundleData AssetBundleReference;

        public LODAsset(GameObject root, AssetBundleData assetBundleData, TextureArraySlot?[] slots)
        {
            Root = root;
            Slots = slots;
            AssetBundleReference = assetBundleData;
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroy(Root);
            
            AssetBundleReference.Dereference();
            AssetBundleReference = null;

            for (int i = 0; i < Slots.Length; i++)
                Slots[i]?.FreeSlot();
        }

    }
}
