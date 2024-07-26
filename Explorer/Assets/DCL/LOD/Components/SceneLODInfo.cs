using Arch.Core;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using UnityEngine;

namespace DCL.LOD.Components
{
  
    public struct SceneLODInfo
    {
        public Dictionary<string, LODCacheInfo> lodGroupCache;
        public GameObjectPool<LODGroup> lodGroupPool;
        
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public byte CurrentLODLevelPromise;
        
        //We can represent 8 LODS loaded state with a byte
        public byte LoadedLODs;
        public byte FailedLODs;
        public string id;
        public LODGroup LodGroup;
        public float CullRelativeHeight;

        public void Dispose(World world)
        {
            CurrentLODPromise.ForgetLoading(world);
            if (SceneLODInfoUtils.CountLOD(LoadedLODs) > 1
                || SceneLODInfoUtils.CountLOD(FailedLODs) > 1)
            {
                LodGroup.gameObject.SetActive(false);
                lodGroupCache[id] = new LODCacheInfo
                {
                    FailedLODs = FailedLODs, LoadedLODs = LoadedLODs, LodGroup = LodGroup, CullRelativeHeight = CullRelativeHeight
                };
            }
            else
            {
                lodGroupPool.Release(LodGroup);
            }
        }

        public static SceneLODInfo Create()
        {
            return new SceneLODInfo
            {
                CurrentLODLevelPromise = byte.MaxValue
            };
        }


        public void ReEvaluateLODGroup(LODAsset lodAsset, float defaultFOV)
        {
            CurrentLODLevelPromise = byte.MaxValue;
            if (lodAsset.State != LODAsset.LOD_STATE.SUCCESS)
            {
                FailedLODs = SceneLODInfoUtils.SetLODResult(FailedLODs, lodAsset.LodKey.Level);
                return;
            }

            LoadedLODs = SceneLODInfoUtils.SetLODResult(LoadedLODs, lodAsset.LodKey.Level);
            int loadedLODAmount = SceneLODInfoUtils.CountLOD(LoadedLODs);
            var lods = LodGroup.GetLODs();
            
            using (var pooledList = lodAsset.Root.GetComponentsInChildrenIntoPooledList<Renderer>(true))
            {
                //MISHA: Is it possible to avoid the array conversion
                //TODO (Juani) : If it is size 0 it doesnt make sense to go beyond this point
                var renderers = pooledList.Value.ToArray();
                lods[lodAsset.LodKey.Level].renderers = renderers;
                if (loadedLODAmount == 1)
                    CalculateCullRelativeHeight(renderers, defaultFOV);
            }

            lodAsset.Root.transform.SetParent(LodGroup.transform);

            if (loadedLODAmount == 1)
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
            else if (loadedLODAmount == 2)
            {
                lods[0].screenRelativeTransitionHeight = (1 - CullRelativeHeight) / 2 + CullRelativeHeight;
                lods[1].screenRelativeTransitionHeight = CullRelativeHeight;
            }

            LodGroup.SetLODs(lods);
        }



        private void CalculateCullRelativeHeight(Renderer[] lodRenderers, float defaultFOV)
        {
            const float distance = (20 - 1) * 16;
            if (lodRenderers.Length > 0)
            {
                var mergedBounds = lodRenderers[0].bounds;

                // Encapsulate the bounds of the remaining renderers
                for (int i = 1; i < lodRenderers.Length; i++) { mergedBounds.Encapsulate(lodRenderers[i].bounds); }
                //Change to Mathf and Clamp
                CullRelativeHeight = Math.Min(0.999f, Math.Max(0.02f, CalculateScreenRelativeTransitionHeight(defaultFOV, distance, mergedBounds)));
            }
        }

        public float CalculateScreenRelativeTransitionHeight(float defaultFOV, float distance, Bounds rendererBounds)
        {
            //Recalculate distance for every LOD in a menu transition
            //float lodBias = QualitySettings.lodBias;
            float objectSize = Mathf.Max(Mathf.Max(rendererBounds.extents.x, rendererBounds.extents.y), rendererBounds.extents.z) * QualitySettings.lodBias;
            float halfFov = (defaultFOV / 2.0f) * Mathf.Deg2Rad;
            float ScreenRelativeTransitionHeight = objectSize / ((distance + objectSize) * Mathf.Tan(halfFov));
            return ScreenRelativeTransitionHeight;
        }

        public bool HasLODLoaded(byte lodForAcquisition)
        {
            return SceneLODInfoUtils.IsLODLoaded(LoadedLODs, lodForAcquisition) ||
                   SceneLODInfoUtils.IsLODLoaded(FailedLODs, lodForAcquisition) ||
                   CurrentLODLevelPromise == lodForAcquisition;
        }
        

        
 
        

    }
}
