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

        public enum LOD_STATE
        {
            UNINTIALIZED,
            FAILED,
            SUCCESS,
            WAITING_INSTANTIATION,
            WAITING_FINALIZATION
        }


        public LODAsset(LODKey lodKey, ILODAssetsPool pool, AssetBundleData assetBundleData, AsyncInstantiateOperation<GameObject> asyncInstantiation)
        {
            LodKey = lodKey;
            Pool = pool;
            Root = null;
            Slots = Array.Empty<TextureArraySlot?>();
            AsyncInstantiation = asyncInstantiation;
            asyncInstantiation.allowSceneActivation = false;
            AssetBundleReference = assetBundleData;

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

            if (!LodKey.Level.Equals(0))
            {
                for (int i = 0; i < Slots.Length; i++)
                    Slots[i]?.FreeSlot();
            }

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

        public void EnableInstationFinalization()
        {
            AsyncInstantiation.allowSceneActivation = true;
            State = LOD_STATE.WAITING_FINALIZATION;
        }

        public bool TryFinalizeInstantiation(string sceneID, Vector2Int parcelCoord, TextureArrayContainer lodTextureArrayContainer, Transform lodTransformParent)
        {
            if (!AsyncInstantiation.isDone)
                return false;

            Root = AsyncInstantiation.Result[0];
            //NOTE: For some reason, the parent is lost on the async instantiation. Looks like a Unity bug
            Root.transform.SetParent(lodTransformParent);
            if (!LodKey.Level.Equals(0))
                Slots = LODUtils.ApplyTextureArrayToLOD(sceneID,
                    parcelCoord, Root, lodTextureArrayContainer);
            //ConfigureSceneMaterial.EnableSceneBounds(Root, in SceneCircumscribedPlanes);
            State = LOD_STATE.SUCCESS;
            return true;
        }

        public void Release()
        {
            Pool.Release(LodKey, this);
        }

    }
}
