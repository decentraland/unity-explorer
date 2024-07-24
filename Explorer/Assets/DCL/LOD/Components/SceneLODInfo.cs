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
        //We can represent 8 LODS loaded state with a byte
        public byte LoadedLODs;
        public LODGroup LodGroup;
        private GameObjectPool<LODGroup> lodGroupPool;
        public float fScreenRelativeTransitionHeight;

        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public byte CurrentLODLevelPromise;
        public void Dispose(World world)
        {
            lodGroupPool?.Release(LodGroup);
            lodGroupPool = null;
            LodGroup = null;
        }

        public static SceneLODInfo Create()
        {
            var lodGroup = new GameObject().AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;

            var lod0 = new UnityEngine.LOD();
            lod0.screenRelativeTransitionHeight = 1;
            var lod1 = new UnityEngine.LOD();
            lod1.screenRelativeTransitionHeight = 0.9999f;
            lodGroup.SetLODs(new []
            {
                lod0, lod1
            });
            
            return new SceneLODInfo
            {
                LodGroup = lodGroup,
                lodGroupPool = null, fScreenRelativeTransitionHeight = 0.02f, CurrentLODLevelPromise = byte.MaxValue
            };
        }


        //public Transform CreateLODGroup(GameObjectPool<LODGroup> lodGroupPool, Transform lodTransformParent)
        public LODGroup CreateLODGroup()
        {
            // Create LODGroup
            if (LodGroup == null)
            {
                LodGroup = new GameObject().AddComponent<LODGroup>();
                LodGroup.fadeMode = LODFadeMode.CrossFade;
                LodGroup.animateCrossFading = true;

                var lod0 = new UnityEngine.LOD();
                lod0.screenRelativeTransitionHeight = 1;

                var lod1 = new UnityEngine.LOD();
                lod1.screenRelativeTransitionHeight = 0.9999f;
                LodGroup.SetLODs(new []
                {
                    lod0, lod1
                });

                //LodGroup.transform.SetParent(lodTransformParent); // The parent is the LODs pool parent
            }

            return LodGroup;
        }

        public void ReEvaluateLODGroup(LODAsset lodAsset)
        {
            CurrentLODLevelPromise = byte.MaxValue;
            SetLODLoaded(lodAsset.LodKey.Level);
            
            if (lodAsset.State != LODAsset.LOD_STATE.SUCCESS)
                return;

            //if(LodGroup == null)
            //    LodGroup = CreateLODGroup();


            var lods = LodGroup.GetLODs();
            lods[lodAsset.LodKey.Level].renderers = lodAsset.lodGO.GetComponentsInChildren<Renderer>();
            lodAsset.lodGO.transform.SetParent(LodGroup.transform);
            
            const float distance = 20 * 16;
            if (fScreenRelativeTransitionHeight == 0.02f)
            {
                Renderer[] lodRenderers = lodAsset.lodGO.GetComponentsInChildren<Renderer>();
                if (lodRenderers != null)
                {
                    if (lodRenderers.Length > 0)
                    {
                        Bounds mergedBounds = lodRenderers[0].bounds;

                        // Encapsulate the bounds of the remaining renderers
                        for (int i = 1; i < lodRenderers.Length; i++) { mergedBounds.Encapsulate(lodRenderers[i].bounds); }

                        fScreenRelativeTransitionHeight = Math.Min(0.999f, Math.Max(0.02f, CalculateScreenRelativeTransitionHeight(distance, mergedBounds)));
                    }
                }
            }

            int loadedLODs = CountLoadedLODs();
            if (loadedLODs == 1)
            {
                if (HasLODLoaded(0))
                {
                    lods[0].screenRelativeTransitionHeight = 0.01f;
                    lods[1].screenRelativeTransitionHeight = 0.001f;
                }
                else
                {
                    lods[0].screenRelativeTransitionHeight = 0.99f;
                    lods[1].screenRelativeTransitionHeight = 0.01f;
                }

                //We need to recalculate the bounds only when the first LOD was loaded (hopefully they both have the same bounds)
                //Ideally we would set it from the ABConverter
                LodGroup.RecalculateBounds();
            }
            else if (loadedLODs == 2)
            {
                lods[0].screenRelativeTransitionHeight = 0.5f;
                lods[1].screenRelativeTransitionHeight = 0.01f;
            }

            LodGroup.SetLODs(lods);
        }

        public float CalculateScreenRelativeTransitionHeight(float distance, Bounds rendererBounds)
        {
            float lodBias = QualitySettings.lodBias / 0.8f;
            float objectSize = rendererBounds.extents.y * lodBias;
            float defaultFOV = 60.0f;
            float fov = (Camera.main ? Camera.main.fieldOfView : defaultFOV) * Mathf.Deg2Rad;
            float halfFov = fov / 2.0f;
            float ScreenRelativeTransitionHeight = (objectSize * 0.5f) / (distance * Mathf.Tan(halfFov));
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
