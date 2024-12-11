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
        public byte CurrentLODLevelPromise;


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

        public void RecalculateLODDistances(float defaultFOV, float defaultLodBias, int loadingDistance, int sceneParcels)
        {
            var lods = metadata.LodGroup.GetLODs();
            SetupLODRelativeHeights(lods, defaultFOV, defaultLodBias, loadingDistance, sceneParcels);
            metadata.LodGroup.SetLODs(lods);
        }

        public void AddFailedLOD()
        {
            metadata.FailedLODs = SceneLODInfoUtils.SetLODResult(metadata.FailedLODs, CurrentLODLevelPromise);
            CurrentLODLevelPromise = byte.MaxValue;
        }

        public void AddSuccessLOD(GameObject instantiatedLOD, LODAsset lodAsset, float defaultFOV, float defaultLodBias, int loadingDistance, int sceneParcels)
        {
            metadata.SuccessfullLODs = SceneLODInfoUtils.SetLODResult(metadata.SuccessfullLODs, CurrentLODLevelPromise);
            metadata.LODAssets[CurrentLODLevelPromise] = lodAsset;
            instantiatedLOD.transform.SetParent(metadata.LodGroup.transform);

            instantiatedLOD.GetComponentsInChildren(true, TEMP_RENDERERS);

            if (TEMP_RENDERERS.Count != 0)
            {
                var lods = metadata.LodGroup.GetLODs();
                SetupRenderers(lods, TEMP_RENDERERS, CurrentLODLevelPromise);
                SetupLODRelativeHeights(lods, defaultFOV, defaultLodBias, loadingDistance, sceneParcels);
                metadata.LodGroup.SetLODs(lods);
            }

            CurrentLODLevelPromise = byte.MaxValue;
        }

        private void SetupLODRelativeHeights(UnityEngine.LOD[] lods, float defaultFOV, float defaultLodBias, int loadingDistance, int sceneParcels)
        {
            int loadedLODAmount = SceneLODInfoUtils.LODCount(metadata.SuccessfullLODs);
            CalculateCullRelativeHeight(defaultFOV, defaultLodBias, loadingDistance);

            if (loadedLODAmount == 1)
            {
                if (SceneLODInfoUtils.HasLODResult(metadata.SuccessfullLODs, 0))
                {
                    //LOD0 is ready to be shown. Therefore, the relative percentage should be the cull percentage
                    lods[0].screenRelativeTransitionHeight = 0.01f;
                    lods[1].screenRelativeTransitionHeight = 0.001f;
                }
            }

        }


        private void SetupRenderers(UnityEngine.LOD[] lods, List<Renderer> renderersToSetup, int lodToSetup)
        {
            var renderers = LODGroupPoolUtils.RENDERER_ARRAY_POOL.Rent(TEMP_RENDERERS.Count);
            renderersToSetup.CopyTo(renderers);
            lods[lodToSetup].renderers = renderers;

            CalculateLODBounds(lods[lodToSetup].renderers, renderersToSetup.Count);
        }

        private void CalculateLODBounds(Renderer[] lodRenderers, int renderersCount)
        {
            var mergedBounds = lodRenderers[0].bounds;

            // Encapsulate the bounds of the remaining renderers
            for (int i = 1; i < renderersCount; i++)
                mergedBounds.Encapsulate(lodRenderers[i].bounds);

            //Object size required to be the largest of the 3 axis
            metadata.LodGroup.size = Mathf.Max(Mathf.Max(mergedBounds.size.x, mergedBounds.size.y), mergedBounds.size.z);
        }

        private void CalculateCullRelativeHeight(float defaultFOV, float defaultLodBias, int loadingDistance)
        {
            //The cull distance is at loading distance - 1 parcel for some space buffer
            //(It should first load, and then cull in)
            float cullDistance = (loadingDistance - 1) * ParcelMathHelper.PARCEL_SIZE;
            float halfFov = defaultFOV / 2.0f * Mathf.Deg2Rad;
            float tanValue = Mathf.Tan(halfFov);
            float size = metadata.LodGroup.size;
            metadata.CullRelativeHeightPercentage = Mathf.Clamp(SceneLODInfoUtils.CalculateScreenRelativeCullHeight(tanValue, cullDistance + size / 2, size / 2, defaultLodBias), 0.02f, 0.999f);
            metadata.LODChangeRelativeDistance = SceneLODInfoUtils.CalculateLODChangeRelativeHeight(metadata.CullRelativeHeightPercentage, tanValue, metadata.LodGroup.size / 2, defaultLodBias);
        }

        public bool HasLOD(byte lodForAcquisition)
        {
            return SceneLODInfoUtils.HasLODResult(metadata.SuccessfullLODs, lodForAcquisition) ||
                   SceneLODInfoUtils.HasLODResult(metadata.FailedLODs, lodForAcquisition) ||
                   CurrentLODLevelPromise == lodForAcquisition;
        }

        public bool IsInitialized()
        {
            return !string.IsNullOrEmpty(id);
        }
    }
}
