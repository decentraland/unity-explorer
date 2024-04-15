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

        public void EnableInstationFinalization(string sceneID, Vector2Int parcelCoord, TextureArrayContainer lodTextureArrayContainer)
        {
            this.sceneID = sceneID;
            this.parcelCoord = parcelCoord;
            this.lodTextureArrayContainer = lodTextureArrayContainer;
            AsyncInstantiation.completed += CompletedInstantiation;
            AsyncInstantiation.allowSceneActivation = true;
            State = LOD_STATE.WAITING_FINALIZATION;
        }

        private void CompletedInstantiation(AsyncOperation obj)
        {
            Root = AsyncInstantiation.Result[0];
            Root.gameObject.SetActive(false);
            AsyncInstantiation.completed -= CompletedInstantiation;
        }

        private string sceneID;
        private Vector2Int parcelCoord;
        private TextureArrayContainer lodTextureArrayContainer;


        public void Release()
        {
            Pool.Release(LodKey, this);
        }

        public void CompleteInstantiation(Transform lodsParent)
        {
            if (!LodKey.Level.Equals(0))
                Slots = LODUtils.ApplyTextureArrayToLOD(sceneID,
                    parcelCoord, Root, lodTextureArrayContainer);

            //For some reason, the instantiation async is not holding the LOD parent reference. Maybe a Unity bug
            Root.transform.SetParent(lodsParent);
            Root.gameObject.SetActive(true);
            //ConfigureSceneMaterial.EnableSceneBounds(Root, in SceneCircumscribedPlanes);
            State = LOD_STATE.SUCCESS;
        }
    }
}
