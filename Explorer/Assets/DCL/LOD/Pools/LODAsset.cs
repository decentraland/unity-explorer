using ECS.StreamableLoading.AssetBundles;
using System;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.LOD.Components;
using UnityEngine;
using Utility;

namespace DCL.LOD
{
    //TODO (Juani) : Interface?
    public class LODAsset : IDisposable
    {
        public GameObject Root;
        public  TextureArraySlot?[] Slots;
        internal AssetBundleData AssetBundleReference;

        internal InitialSceneStateLOD? InitialSceneStateLOD;

        public LODAsset(InitialSceneStateLOD initialSceneStateLOD)
        {
            this.InitialSceneStateLOD = initialSceneStateLOD;
        }

        public LODAsset(GameObject root, AssetBundleData assetBundleData, TextureArraySlot?[] slots)
        {
            Root = root;
            Slots = slots;
            AssetBundleReference = assetBundleData;
        }

        public void Dispose()
        {
            if (InitialSceneStateLOD != null)
            {
                InitialSceneStateLOD.Dispose();
            }
            else
            {
                UnityObjectUtils.SafeDestroy(Root);

                AssetBundleReference.Dereference();
                AssetBundleReference = null;

                for (int i = 0; i < Slots.Length; i++)
                    Slots[i]?.FreeSlot();
            }
        }

    }
}
