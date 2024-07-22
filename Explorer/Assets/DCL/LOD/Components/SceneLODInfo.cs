using Arch.Core;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace DCL.LOD.Components
{
    public struct SceneLODInfo
    {
        public LODGroup LodGroup;
        public List<LODAsset> LODAssets;
        private GameObjectPool<LODGroup> lodGroupPool;
        private UnityEngine.LOD lod0, lod1;
        public float fScreenRelativeTransitionHeight;

        public void Dispose(World world)
        {
            foreach (var lodVar in LODAssets)
            {
                if(LodGroup != null)
                    lodVar.lodGO?.transform.SetParent(LodGroup.transform.parent);
                lodVar.Release(world);
            }
            LODAssets.Clear();
            lodGroupPool?.Release(LodGroup);
            lodGroupPool = null;
            LodGroup = null;
        }

        public static SceneLODInfo Create()
        {
            return new SceneLODInfo
            {
                LODAssets = new List<LODAsset>(),
                LodGroup = null,
                lodGroupPool = null,
                fScreenRelativeTransitionHeight = 0.02f,
            };
        }

        public bool ArePromisesConsumed()
        {
            foreach (var lodAsset in LODAssets)
            {
                if (!lodAsset.LODPromise.IsConsumed)
                    return false;
            }

            return true;
        }

        public Transform CreateLODGroup(GameObjectPool<LODGroup> lodGroupPool, Transform lodTransformParent)
        {
            // Create LODGroup
            if (LodGroup == null)
            {
                this.lodGroupPool = lodGroupPool;
                LodGroup = lodGroupPool.Get();
                LodGroup.fadeMode = LODFadeMode.CrossFade;
                LodGroup.animateCrossFading = true;
                LodGroup.transform.SetParent(lodTransformParent); // The parent is the LODs pool parent
                lod0 = new UnityEngine.LOD(1.0f, null);
                lod1 = new UnityEngine.LOD(0.999f, null);
                UnityEngine.LOD[] lods = {lod0, lod1};
                LodGroup.SetLODs(lods.ToArray());
            }

            return LodGroup.transform;
        }

        public void ReEvaluateLODGroup(LODAsset lodAsset)
        {
            if (LodGroup == null || LODAssets.Count == 0)
                return;

            if (lodAsset.State != LODAsset.LOD_STATE.SUCCESS)
                return;

            // Ordered sort as the LOD Group expects the screen relative transition heights to be in order
            // and we might not have necessarily loaded them in order.
            LODAssets.Sort((a, b) => a.currentLODLevel.CompareTo(b.currentLODLevel));

            const float distance = 20 * 16;

            if (lodAsset.currentLODLevel == 0)
                lod0.renderers = lodAsset.lodGO.GetComponentsInChildren<Renderer>();
            else if (lodAsset.currentLODLevel == 1)
                lod1.renderers = lodAsset.lodGO.GetComponentsInChildren<Renderer>();

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

            if (LODAssets.Count == 1 && LODAssets[0].currentLODLevel == 0)
            {
                lod0.screenRelativeTransitionHeight = fScreenRelativeTransitionHeight;
                lod1.screenRelativeTransitionHeight = fScreenRelativeTransitionHeight - 0.01f;
                Assert.IsTrue(lod0.screenRelativeTransitionHeight <= 1.0f, $"LODAssets.Count == 1 && LODAssets[0].currentLODLevel == 0 : {lod0.screenRelativeTransitionHeight}, {lod1.screenRelativeTransitionHeight}");
                Assert.IsTrue(lod1.screenRelativeTransitionHeight >= 0.0f, $"LODAssets.Count == 1 && LODAssets[0].currentLODLevel == 0 : {lod0.screenRelativeTransitionHeight}, {lod1.screenRelativeTransitionHeight}");

                Assert.IsTrue(lod0.screenRelativeTransitionHeight > lod1.screenRelativeTransitionHeight,
                    $"LODAssets.Count == 1 && LODAssets[0].currentLODLevel == 0 : {lod0.screenRelativeTransitionHeight}, {lod1.screenRelativeTransitionHeight}");
            }
            else if (LODAssets.Count == 1 && LODAssets[0].currentLODLevel == 1)
            {
                lod0.screenRelativeTransitionHeight = 1.0f;
                lod1.screenRelativeTransitionHeight = fScreenRelativeTransitionHeight;
                Assert.IsTrue(lod0.screenRelativeTransitionHeight <= 1.0f, $"LODAssets.Count == 1 && LODAssets[0].currentLODLevel == 1 : {lod0.screenRelativeTransitionHeight}, {lod1.screenRelativeTransitionHeight}");
                Assert.IsTrue(lod1.screenRelativeTransitionHeight >= 0.0f, $"LODAssets.Count == 1 && LODAssets[0].currentLODLevel == 1 : {lod0.screenRelativeTransitionHeight}, {lod1.screenRelativeTransitionHeight}");

                Assert.IsTrue(lod0.screenRelativeTransitionHeight > lod1.screenRelativeTransitionHeight,
                    $"LODAssets.Count == 1 && LODAssets[0].currentLODLevel == 1 : {lod0.screenRelativeTransitionHeight}, {lod1.screenRelativeTransitionHeight}");
            }
            else if (LODAssets.Count == 2)
            {
                lod0.screenRelativeTransitionHeight = ((1.0f - fScreenRelativeTransitionHeight) * 0.5f) + fScreenRelativeTransitionHeight;
                lod1.screenRelativeTransitionHeight = fScreenRelativeTransitionHeight;
                Assert.IsTrue(lod0.screenRelativeTransitionHeight <= 1.0f, $"LODAssets.Count == 2 : {lod0.screenRelativeTransitionHeight}, {lod1.screenRelativeTransitionHeight}");
                Assert.IsTrue(lod1.screenRelativeTransitionHeight >= 0.0f, $"LODAssets.Count == 2 : {lod0.screenRelativeTransitionHeight}, {lod1.screenRelativeTransitionHeight}");

                Assert.IsTrue(lod0.screenRelativeTransitionHeight > lod1.screenRelativeTransitionHeight,
                    $"LODAssets.Count == 2 : {lod0.screenRelativeTransitionHeight}, {lod1.screenRelativeTransitionHeight}");
            }

            UnityEngine.LOD[] lods = {lod0, lod1 };
            LodGroup.SetLODs(lods);
            LodGroup.RecalculateBounds();
        }

        public float CalculateScreenRelativeTransitionHeight(float distance, Bounds rendererBounds)
        {
            float lodBias = QualitySettings.lodBias / 0.8f;
            float objectSize = rendererBounds.extents.magnitude * lodBias;
            float defaultFOV = 60.0f;
            float fov = (Camera.main ? Camera.main.fieldOfView : defaultFOV) * Mathf.Deg2Rad;
            float halfFov = fov / 2.0f;
            float ScreenRelativeTransitionHeight = (objectSize * 0.5f) / (distance * Mathf.Tan(halfFov));
            return ScreenRelativeTransitionHeight;
        }

        // Quick function to check if LODAsset has already been loaded
        public bool HasLODKey(LODKey lodKey)
        {
            foreach (var lodAsset in LODAssets)
            {
                if (lodKey.Equals(lodAsset.LodKey))
                    return true;
            }

            return false;
        }
    }
}
