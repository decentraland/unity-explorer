using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using System;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Optimization.Pools;
using ECS.Unity.SceneBoundsChecker;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Networking;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.LOD
{
    public class LODAsset : IDisposable
    {
        public LOD_STATE State;
        public readonly LODKey LodKey;

        public GameObject Root;
        public AsyncInstantiateOperation<GameObject> AsyncInstantiation;
        public  TextureArraySlot?[] Slots;
        
        private readonly ILODAssetsPool Pool;
        internal AssetBundleData AssetBundleReference;

        private readonly string SceneID;
        private readonly Vector2Int ParcelCoord;
        private readonly TextureArrayContainer LodTextureArrayContainer;

        public enum LOD_STATE
        {
            UNINTIALIZED,
            FAILED,
            SUCCESS,
            WAITING_INSTANTIATION,
            WAITING_FINALIZATION
        }


        public LODAsset(LODKey lodKey, ILODAssetsPool pool, AssetBundleData assetBundleData,
            Transform lodsTransformParent, Vector3 baseParcelPosition,
            Vector2Int parcelCoord, TextureArrayContainer lodTextureArrayContainer)
        {
            LodKey = lodKey;
            Pool = pool;
            Slots = Array.Empty<TextureArraySlot?>();
            AssetBundleReference = assetBundleData;
            SceneID = LodKey.Hash;
            ParcelCoord = parcelCoord;
            LodTextureArrayContainer = lodTextureArrayContainer;

            AsyncInstantiation =
                Object.InstantiateAsync(AssetBundleReference.GetMainAsset<GameObject>(),
                    lodsTransformParent, baseParcelPosition, Quaternion.identity);
            AsyncInstantiation.allowSceneActivation = false;

            Root = null;

            State = LOD_STATE.WAITING_INSTANTIATION;
            
            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
        }

        public LODAsset(LODKey lodKey, ILODAssetsPool pool)
        {
            State = LOD_STATE.FAILED;
            LodKey = lodKey;
            Pool = pool;

            Slots = Array.Empty<TextureArraySlot?>();
            Root = null;
            AsyncInstantiation = null;
            AssetBundleReference = null;

            ProfilingCounters.LODAssetAmount.Value++;
            ProfilingCounters.Failling_LOD_Amount.Value++;
        }

        public void Dispose()
        {
            if (State == LOD_STATE.FAILED) return;

            AssetBundleReference.Dereference();
            AssetBundleReference = null;

            for (int i = 0; i < Slots.Length; i++)
                Slots[i]?.FreeSlot();

            AsyncInstantiation.Cancel();
            if (State == LOD_STATE.SUCCESS)
                UnityObjectUtils.SafeDestroy(Root);

            ProfilingCounters.LODAssetAmount.Value--;
            ProfilingCounters.LODInstantiatedInCache.Value--;
        }

        public void EnableAsset()
        {
            if (State == LOD_STATE.FAILED)
                return;

            if (State == LOD_STATE.SUCCESS)
            {
                Root.SetActive(true);
                ProfilingCounters.LODInstantiatedInCache.Value--;
                ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value++;
            }
        }

        public void DisableAsset()
        {
            if (State == LOD_STATE.FAILED)
                return;
            
            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return;

            if (State == LOD_STATE.SUCCESS)
            {
                Root.SetActive(false);
                ProfilingCounters.LODInstantiatedInCache.Value++;
                ProfilingCounters.LOD_Per_Level_Values[LodKey.Level].Value--;
            }
        }

        public void FinalizeInstantiation()
        {
            AsyncInstantiation.allowSceneActivation = true;

            AsyncInstantiation.WaitForCompletion();

            Root = AsyncInstantiation.Result[0];
            if (!LodKey.Level.Equals(0))
                Slots = LODUtils.ApplyTextureArrayToLOD(SceneID,
                    ParcelCoord, Root, LodTextureArrayContainer);

            State = LOD_STATE.SUCCESS;
        }

        public void Release()
        {
            Pool.Release(LodKey, this);
        }
    }
}
