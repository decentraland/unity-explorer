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
    public struct SceneLODInfo
    {
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public byte CurrentLODLevelPromise;

        public string id;
        public LODCacheInfo metadata;
        
        public void Dispose(World world)
        {
            CurrentLODPromise.ForgetLoading(world);
        }

        public static SceneLODInfo Create()
        {
            return new SceneLODInfo
            {
                CurrentLODLevelPromise = byte.MaxValue
            };
        }

        public void AddFailedLOD()
        {
            metadata.FailedLODs = SceneLODInfoUtils.SetLODResult(metadata.FailedLODs, CurrentLODLevelPromise);
            CurrentLODLevelPromise = byte.MaxValue;
        }


        public void AddSuccessLOD(GameObject instantiatedLOD, LODAsset lodAsset, float defaultFOV, float defaultLodBias)
        {
            metadata.SuccessfullLODs = SceneLODInfoUtils.SetLODResult(metadata.SuccessfullLODs, CurrentLODLevelPromise);
            int loadedLODAmount = SceneLODInfoUtils.CountLOD(metadata.SuccessfullLODs);
            var lods = metadata.LodGroup.GetLODs();

            using (var pooledList = instantiatedLOD.GetComponentsInChildrenIntoPooledList<Renderer>(true))
            {
                //MISHA: Is it possible to avoid the array conversion
                //TODO (Juani) : If it is size 0 it doesnt make sense to go beyond this point
                var renderers = pooledList.Value.ToArray();
                lods[CurrentLODLevelPromise].renderers = renderers;
                if (loadedLODAmount == 1)
                    CalculateCullRelativeHeight(renderers, defaultFOV, defaultLodBias);
            }

            instantiatedLOD.transform.SetParent(metadata.LodGroup.transform);

            metadata.LODAssets[CurrentLODLevelPromise] = lodAsset;

            if (loadedLODAmount == 1)
            {
                if (CurrentLODLevelPromise == 0)
                {
                    lods[0].screenRelativeTransitionHeight = metadata.CullRelativeHeight;
                    lods[1].screenRelativeTransitionHeight = metadata.CullRelativeHeight - 0.001f;
                }
                else
                {
                    lods[0].screenRelativeTransitionHeight = 1;
                    lods[1].screenRelativeTransitionHeight = metadata.CullRelativeHeight;
                }
                //We need to recalculate the bounds only when the first LOD was loaded (hopefully they both have the same bounds)
                //Ideally we would set it from the ABConverter
                metadata.LodGroup.RecalculateBounds();
            }
            else if (loadedLODAmount == 2)
            {
                lods[0].screenRelativeTransitionHeight = (1 - metadata.CullRelativeHeight) / 2 + metadata.CullRelativeHeight;
                lods[1].screenRelativeTransitionHeight = metadata.CullRelativeHeight;
            }

            metadata.LodGroup.SetLODs(lods);
            CurrentLODLevelPromise = byte.MaxValue;
        }

        private void CalculateCullRelativeHeight(Renderer[] lodRenderers, float defaultFOV, float defaultLodBias)
        {
            const float distance = (20 - 1) * 16;
            if (lodRenderers.Length > 0)
            {
                var mergedBounds = lodRenderers[0].bounds;

                // Encapsulate the bounds of the remaining renderers
                for (int i = 1; i < lodRenderers.Length; i++) { mergedBounds.Encapsulate(lodRenderers[i].bounds); }

                metadata.CullRelativeHeight = Mathf.Clamp(CalculateScreenRelativeTransitionHeight(defaultFOV, defaultLodBias, distance, mergedBounds), 0.02f, 0.999f);
            }
        }

        public float CalculateScreenRelativeTransitionHeight(float defaultFOV, float defaultLodBias, float distance, Bounds rendererBounds)
        {
            //Recalculate distance for every LOD in a menu transition
            float objectSize = Mathf.Max(Mathf.Max(rendererBounds.extents.x, rendererBounds.extents.y), rendererBounds.extents.z) * defaultLodBias;
            float halfFov = (defaultFOV / 2.0f) * Mathf.Deg2Rad;
            float ScreenRelativeTransitionHeight = objectSize / ((distance + objectSize) * Mathf.Tan(halfFov));
            return ScreenRelativeTransitionHeight;
        }

        public bool HasLOD(byte lodForAcquisition)
        {
            return SceneLODInfoUtils.HasLODResult(metadata.SuccessfullLODs, lodForAcquisition) ||
                   SceneLODInfoUtils.HasLODResult(metadata.FailedLODs, lodForAcquisition) ||
                   CurrentLODLevelPromise == lodForAcquisition;
        }



    }
}
