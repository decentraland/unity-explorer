using DCL.LOD.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.LOD
{
    public struct SceneLODInfoDebug
    {
        //This is a sync method, so we can use a shared list
        private static readonly List<Material> TEMP_MATERIALS = new (3);
        private Dictionary<int, Color[]> OriginalColors;
        private Dictionary<int, DebugCube[]> FailedLODs;
        private ILODSettingsAsset LodSettingsAsset;

        private int currentLODCount;

        public static SceneLODInfoDebug Create(ILODSettingsAsset lodSettingsAsset)
        {
            return new SceneLODInfoDebug
            {
                OriginalColors = new Dictionary<int, Color[]>(), FailedLODs = new Dictionary<int, DebugCube[]>(), LodSettingsAsset = lodSettingsAsset
            };
        }

        public void Dispose(SceneLODInfo sceneLODInfo)
        {
            //Not initialized
            if (string.IsNullOrEmpty(sceneLODInfo.id))
                return;

            var lods = sceneLODInfo.metadata.LodGroup.GetLODs();
            foreach (var pair in OriginalColors)
            {
                var lodAsset = lods[pair.Key];
                for (int j = 0; j < lodAsset.renderers.Length; j++)
                {
                    var lodAssetRenderer = lodAsset.renderers[j];
                    lodAssetRenderer.SafeGetMaterials(TEMP_MATERIALS);
                    foreach (var t in TEMP_MATERIALS)
                    {
                        if (pair.Value != null)
                            t.color = pair.Value[j];
                    }
                }
            }

            foreach (var pair in FailedLODs)
            {
                foreach (var t in pair.Value)
                    UnityObjectUtils.SafeDestroy(t.gameObject);
                lods[pair.Key].renderers = Array.Empty<Renderer>();
            }

            sceneLODInfo.metadata.LodGroup.SetLODs(lods);
        }

        public void Update(SceneLODInfo sceneLODInfo, IReadOnlyList<Vector2Int> parcels, Material[] failedMaterials)
        {
            //Not initialized
            if (string.IsNullOrEmpty(sceneLODInfo.id))
                return;

            //Still no LODs available
            if (currentLODCount == sceneLODInfo.metadata.LODLoadedCount())
                return;

            var lods = sceneLODInfo.metadata.LodGroup.GetLODs();
            for (int lodLevel = 0; lodLevel < lods.Length; lodLevel++)
            {
                if (SceneLODInfoUtils.HasLODResult(sceneLODInfo.metadata.SuccessfullLODs, lodLevel))
                    TintSuccessfullLOD(lods[lodLevel], lodLevel);
                else if (SceneLODInfoUtils.HasLODResult(sceneLODInfo.metadata.FailedLODs, lodLevel))
                {
                    lods[lodLevel].renderers = DoFailedCubes(sceneLODInfo.metadata.LodGroup.transform, lods[lodLevel], lodLevel, parcels, failedMaterials);
                    //We will modify the screenRelativeTransitionHeight to show the cube
                    lods[lodLevel].screenRelativeTransitionHeight = (1 - lodLevel) * 0.5f;
                }
            }

            sceneLODInfo.metadata.LodGroup.RecalculateBounds();
            sceneLODInfo.metadata.LodGroup.SetLODs(lods);

            currentLODCount = sceneLODInfo.metadata.LODLoadedCount();
        }

        private Renderer[] DoFailedCubes(Transform lodGroupTransform, UnityEngine.LOD lod, int lodLevel, IReadOnlyList<Vector2Int> parcels, Material[] failedMaterials)
        {
            //It has already been created
            if (FailedLODs.ContainsKey(lodLevel))
                return lod.renderers;

            FailedLODs[lodLevel] =  new DebugCube[parcels.Count];
            var renderers = new Renderer[parcels.Count];
            for (int i = 0; i < parcels.Count; i++)
            {
                FailedLODs[lodLevel][i] = Object.Instantiate(LodSettingsAsset.DebugCube, ParcelMathHelper.GetPositionByParcelPosition(parcels[i]), Quaternion.identity, lodGroupTransform);
                FailedLODs[lodLevel][i].failingLODCubeMeshRenderer.sharedMaterial = failedMaterials[lodLevel];
                renderers[i] = FailedLODs[lodLevel][i].failingLODCubeMeshRenderer;
            }

            return renderers;
        }

        private void TintSuccessfullLOD(UnityEngine.LOD lod, int lodLevel)
        {
            //It has already been tinted
            if (OriginalColors.ContainsKey(lodLevel))
                return;

            OriginalColors[lodLevel] = new Color[lod.renderers.Length];
            for (int j = 0; j < lod.renderers.Length; j++)
            {
                var lodAssetRenderer = lod.renderers[j];
                lodAssetRenderer.SafeGetMaterials(TEMP_MATERIALS);
                foreach (var t in TEMP_MATERIALS)
                {
                    OriginalColors[lodLevel][j] = t.color;
                    t.color = LodSettingsAsset.LODDebugColors[lodLevel];
                }
            }
        }
    }
}
