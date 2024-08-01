using System.Buffers;
using System.Collections.Generic;
using Arch.Core;
using DCL.LOD.Systems;
using DCL.Optimization.Pools;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using UnityEngine;
using Utility;

namespace DCL.LOD.Components
{
    public struct SceneLODInfo
    {
        //This is a sync method, so we can use a shared list
        private static readonly List<Renderer> TEMP_RENDERERS = new (3);
        
        public string id;
        public LODCacheInfo metadata;

        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public byte CurrentLODLevelPromise { get; private set; }
        
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


        public void AddSuccessLOD(GameObject instantiatedLOD, LODAsset lodAsset, float defaultFOV, float defaultLodBias, int loadingDistance)
        {
            metadata.SuccessfullLODs = SceneLODInfoUtils.SetLODResult(metadata.SuccessfullLODs, CurrentLODLevelPromise);
            metadata.LODAssets[CurrentLODLevelPromise] = lodAsset;
            instantiatedLOD.transform.SetParent(metadata.LodGroup.transform);

            instantiatedLOD.GetComponentsInChildren(true, TEMP_RENDERERS);
            
            if (TEMP_RENDERERS.Count != 0)
                RecalculateLODValues(defaultFOV, defaultLodBias, loadingDistance);

            CurrentLODLevelPromise = byte.MaxValue;
        }

        private void RecalculateLODValues(float defaultFOV, float defaultLodBias, int loadingDistance)
        {
            int loadedLODAmount = SceneLODInfoUtils.LODCount(metadata.SuccessfullLODs);
            var lods = metadata.LodGroup.GetLODs();

            var renderers = LODGroupPoolUtils.RENDERER_ARRAY_POOL.Rent(TEMP_RENDERERS.Count);
            TEMP_RENDERERS.CopyTo(renderers);
            lods[CurrentLODLevelPromise].renderers = renderers;
            
            if (loadedLODAmount == 1)
            {
                //We only have to make this calculations for the first LOD (assuming they have the same bounds)
                CalculateCullRelativeHeight(lods[CurrentLODLevelPromise].renderers, TEMP_RENDERERS.Count, defaultFOV, defaultLodBias, loadingDistance);
                
                if (CurrentLODLevelPromise == 0)
                {
                    //LOD0 is ready to be shown. Therefore, the relative percentage should be the cull percentage
                    lods[0].screenRelativeTransitionHeight = metadata.CullRelativeHeightPercentage;
                    lods[1].screenRelativeTransitionHeight = metadata.CullRelativeHeightPercentage - 0.001f;
                }
                else
                {
                    //LOD1 is ready to be shown. Therefore, the relative percentage of LOD1 should be the cull percentage, while LOD0 percentage should remain at 100% of the screen
                    lods[0].screenRelativeTransitionHeight = 1;
                    lods[1].screenRelativeTransitionHeight = metadata.CullRelativeHeightPercentage;
                }
            }
            else if (loadedLODAmount == 2)
            {
                //If both LODs are loaded, we set the cull percentage for LOD1 and then assign the mid point between cull and 100% for LOD0
                lods[0].screenRelativeTransitionHeight = (1 - metadata.CullRelativeHeightPercentage) / 2 + metadata.CullRelativeHeightPercentage;
                lods[1].screenRelativeTransitionHeight = metadata.CullRelativeHeightPercentage;
            }
            metadata.LodGroup.SetLODs(lods);
        }

        private void CalculateCullRelativeHeight(Renderer[] lodRenderers, int renderersLength, float defaultFOV, float defaultLodBias, int loadingDistance)
        {
            var mergedBounds = lodRenderers[0].bounds;

            // Encapsulate the bounds of the remaining renderers
            for (int i = 1; i < renderersLength; i++)
            {
                mergedBounds.Encapsulate(lodRenderers[i].bounds);
            }

            //The cull distance is at loading distance - 1 parcel for some space buffer
            //(It should first load, and then cull in)
            float cullDistance = (loadingDistance - 1) * ParcelMathHelper.PARCEL_SIZE;

            //Object size required to be the largest of the 3 axis
            float maxExtents = Mathf.Max(Mathf.Max(mergedBounds.extents.x, mergedBounds.extents.y), mergedBounds.extents.z);
            float maxExtentsWithLODBias = maxExtents * defaultLodBias;
            //We set the bounds of the LODGroup
            metadata.LodGroup.size = maxExtents;

            float halfFov = defaultFOV / 2.0f * Mathf.Deg2Rad;
            float tanValue = Mathf.Tan(halfFov);
            metadata.CullRelativeHeightPercentage = Mathf.Clamp(CalculateScreenRelativeCullHeight(tanValue, cullDistance, maxExtentsWithLODBias), 0.02f, 0.999f);
            metadata.LODChangeRelativeDistance = CalculateLODChangeRelativeHeight(tanValue, maxExtentsWithLODBias);
        }

        //This will give us the percent of the screen in which the object will be culled when being at (unloadingDistance - 1) parcel
        private float CalculateScreenRelativeCullHeight(float tanValue, float distance, float objectSize)
        {
            return objectSize / ((distance + objectSize) * tanValue);
        }

        //This will give us the distance at which the LOD change should occur if we consider the percentage at the middle between 
        //cull distance and 100% of the screen
        private float CalculateLODChangeRelativeHeight(float tanValue, float objectSize)
        {
            float halfDistancePercentage = (1 - metadata.CullRelativeHeightPercentage) / 2 + metadata.CullRelativeHeightPercentage;
            return objectSize / (halfDistancePercentage * tanValue) - objectSize;
        }

        public bool HasLOD(byte lodForAcquisition)
        {
            return SceneLODInfoUtils.HasLODResult(metadata.SuccessfullLODs, lodForAcquisition) ||
                   SceneLODInfoUtils.HasLODResult(metadata.FailedLODs, lodForAcquisition) ||
                   CurrentLODLevelPromise == lodForAcquisition;
        }

        public void SetCurrentLODPromise(AssetPromise<AssetBundleData, GetAssetBundleIntention> promise, byte lodLevel)
        {
            //CurrentLODPromise = promise;
            CurrentLODLevelPromise = lodLevel;
        }

    }
}
