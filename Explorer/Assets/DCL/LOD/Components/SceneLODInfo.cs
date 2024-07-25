using Arch.Core;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.LOD.Components
{
    public enum SCENE_LOD_INFO_STATE
    {
        UNINITIALIZED,
        WAITING_LOD,
        SUCCESS,
        FAILED
    }
    
    public struct SceneLODInfo
    {
        public SCENE_LOD_INFO_STATE State;
        public GameObjectPool<LODGroup> lodGroupPool;
        public Dictionary<string, (LODGroup, byte, float)> lodGroupCache;

        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public byte CurrentLODLevelPromise;
        
        //We can represent 8 LODS loaded state with a byte
        public string id;
        public byte LoadedLODs;
        public LODGroup LodGroup;
        public float CullRelativeHeight;

        public void Dispose(World world)
        {
            if (State is SCENE_LOD_INFO_STATE.SUCCESS)
            {
                LodGroup.gameObject.SetActive(false);
                lodGroupCache[id] = (LodGroup, LoadedLODs, CullRelativeHeight);
            }

            if (State is SCENE_LOD_INFO_STATE.WAITING_LOD)
                lodGroupPool.Release(LodGroup);

            CurrentLODPromise.ForgetLoading(world);
        }

        public static SceneLODInfo Create()
        {
            return new SceneLODInfo
            {
                CurrentLODLevelPromise = byte.MaxValue
            };
        }


        public void ReEvaluateLODGroup(LODAsset lodAsset)
        {
            CurrentLODLevelPromise = byte.MaxValue;
            SetLODLoaded(lodAsset.LodKey.Level);

            //TODO (JUANI) : Maybe only one of the LODs is missing. This considers that for any given missing LOD the whole thing is a failure
            if (lodAsset.State != LODAsset.LOD_STATE.SUCCESS)
            {
                State = SCENE_LOD_INFO_STATE.FAILED;
                lodGroupPool.Release(LodGroup);
                return;
            }

            int loadedLODs = CountLoadedLODs();
            var lods = LodGroup.GetLODs();
            using (var pooledList = lodAsset.Root.GetComponentsInChildrenIntoPooledList<Renderer>(true))
            {
                //MISHA: Is it possible to avoid the array conversion
                //TODO (Juani) : If it is size 0 it doesnt make sense to go beyond this point
                var renderers = pooledList.Value.ToArray();
                lods[lodAsset.LodKey.Level].renderers = renderers;
                if (loadedLODs == 1)
                    CalculateCullRelativeHeight(renderers);
            }

            lodAsset.Root.transform.SetParent(LodGroup.transform);
            

            if (loadedLODs == 1)
            {
                if (lodAsset.LodKey.Level == 0)
                {
                    lods[0].screenRelativeTransitionHeight = CullRelativeHeight;
                    lods[1].screenRelativeTransitionHeight = CullRelativeHeight - 0.001f;
                }
                else
                {
                    lods[0].screenRelativeTransitionHeight = 1;
                    lods[1].screenRelativeTransitionHeight = CullRelativeHeight;
                }
                //We need to recalculate the bounds only when the first LOD was loaded (hopefully they both have the same bounds)
                //Ideally we would set it from the ABConverter
                LodGroup.RecalculateBounds();
            }
            else if (loadedLODs == 2)
            {
                lods[0].screenRelativeTransitionHeight = (1 - CullRelativeHeight) / 2 + CullRelativeHeight;
                lods[1].screenRelativeTransitionHeight = CullRelativeHeight;
            }

            LodGroup.SetLODs(lods);
            State = SCENE_LOD_INFO_STATE.SUCCESS;
        }

        private void CalculateCullRelativeHeight(Renderer[] lodRenderers)
        {
            const float distance = 20 * 16;
            if (lodRenderers.Length > 0)
            {
                var mergedBounds = lodRenderers[0].bounds;

                // Encapsulate the bounds of the remaining renderers
                for (int i = 1; i < lodRenderers.Length; i++) { mergedBounds.Encapsulate(lodRenderers[i].bounds); }

                CullRelativeHeight = Math.Min(0.999f, Math.Max(0.02f, CalculateScreenRelativeTransitionHeight(distance, mergedBounds)));
            }
        }

        public float CalculateScreenRelativeTransitionHeight(float distance, Bounds rendererBounds)
        {
            float lodBias = 1;

            float objectSize = Mathf.Max(Mathf.Max(rendererBounds.extents.x, rendererBounds.extents.y), rendererBounds.extents.z) * lodBias;
            float defaultFOV = 60.0f;
            float fov = (Camera.main ? Camera.main.fieldOfView : defaultFOV) * Mathf.Deg2Rad;
            float halfFov = fov / 2.0f;
            float ScreenRelativeTransitionHeight = objectSize / (distance * Mathf.Tan(halfFov));
            return ScreenRelativeTransitionHeight;
        }

        public bool HasLODLoaded(byte lodForAcquisition)
        {
            return IsLODLoaded(lodForAcquisition) || CurrentLODLevelPromise == lodForAcquisition;
        }
        
        private void SetLODLoaded(int lodLevel)
        {
            LoadedLODs |= (byte)(1 << lodLevel);
        }
        
        private bool IsLODLoaded(int lodLevel)
        {
            return (LoadedLODs & (1 << lodLevel)) != 0;
        }
        
        private int CountLoadedLODs()
        {
            int count = 0;
            byte temp = LoadedLODs;
            while (temp != 0)
            {
                count += temp & 1;
                temp >>= 1;
            }
            return count;
        }
    }
}
