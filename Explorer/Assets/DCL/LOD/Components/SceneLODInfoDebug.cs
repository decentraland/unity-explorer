using DCL.LOD.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.LOD
{
    public class SceneLODInfoDebugContent
    {
        public Color[] OriginalColors;
        public Renderer[] Renderers;
        public LODAsset.LOD_STATE LodState;
    }

    public struct SceneLODInfoDebug : IDisposable
    {
        private Dictionary<byte, SceneLODInfoDebugContent> SceneLODInfoDebugContents;
        private ILODSettingsAsset LodSettingsAsset;
        public byte CurrentLODLevel;
        public LODAsset.LOD_STATE CurrentLODState;
        public DebugCube[] DebugCubes;

        //This is a sync method, so we can use a shared list
        private static readonly List<Material> TEMP_MATERIALS = new (3);

        public static SceneLODInfoDebug Create(Transform missingSceneParent, ILODSettingsAsset lodSettingsAsset, IReadOnlyList<Vector2Int> parcels)
        {
            var debugCubes =  new DebugCube[parcels.Count];
            for (int i = 0; i < parcels.Count; i++)
            {
                debugCubes[i] = Object.Instantiate(lodSettingsAsset.DebugCube, ParcelMathHelper.GetPositionByParcelPosition(parcels[i]), Quaternion.identity, missingSceneParent);
                debugCubes[i].gameObject.SetActive(false);
            }

            return new SceneLODInfoDebug
            {
                SceneLODInfoDebugContents = new Dictionary<byte, SceneLODInfoDebugContent>(), CurrentLODLevel = byte.MaxValue, DebugCubes = debugCubes, LodSettingsAsset = lodSettingsAsset
            };
        }

        public void Dispose()
        {
            foreach (var keyValuePair in SceneLODInfoDebugContents)
            {
                ClearContent(keyValuePair.Value);
                foreach (var debugCubes in DebugCubes)
                    UnityObjectUtils.SafeDestroy(debugCubes);
            }
        }

        public void Update(SceneLODInfo sceneLODInfo)
        {
            if (!LodSettingsAsset.IsColorDebugging)
                return;

            foreach (var lodAsset in sceneLODInfo.LODAssets)
            {
                if (!SceneLODInfoDebugContents.TryGetValue(lodAsset.LodKey.Level, out var sceneLODInfoDebugContents))
                {
                    sceneLODInfoDebugContents = CreateSceneLODInfoDebugContents();
                    SceneLODInfoDebugContents.Add(lodAsset.LodKey.Level, sceneLODInfoDebugContents);
                }

                ClearPreviousContent();
                UpdateContent(sceneLODInfoDebugContents, lodAsset);
            }
        }

        private SceneLODInfoDebugContent CreateSceneLODInfoDebugContents()
        {
            var sceneLODInfoDebugContents = new SceneLODInfoDebugContent
            {
                OriginalColors = Array.Empty<Color>(), Renderers = Array.Empty<Renderer>(), LodState = LODAsset.LOD_STATE.UNINTIALIZED
            };
            return sceneLODInfoDebugContents;
        }

        private void ClearPreviousContent()
        {
            if (CurrentLODLevel != byte.MaxValue)
                ClearContent(SceneLODInfoDebugContents[CurrentLODLevel]);
        }

        private void ClearContent(SceneLODInfoDebugContent debugContent)
        {
            foreach (var debugCubes in DebugCubes)
                debugCubes.gameObject.SetActive(false);
            for (int i = 0; i < debugContent.Renderers.Length; i++)
            {
                debugContent.Renderers[i].SafeGetMaterials(TEMP_MATERIALS);
                for (int j = 0; j < TEMP_MATERIALS.Count; j++)
                    TEMP_MATERIALS[j].color = debugContent.OriginalColors[i + j];
            }
        }

        private void UpdateContent(SceneLODInfoDebugContent debugContent, LODAsset lodAsset)
        {
            if (debugContent.LodState != lodAsset.State)
                UpdateState(ref debugContent, lodAsset);

            CurrentLODState = debugContent.LodState;
            CurrentLODLevel = lodAsset.LodKey.Level;

            if (debugContent.LodState == LODAsset.LOD_STATE.SUCCESS)
            {
                for (int i = 0; i < debugContent.Renderers.Length; i++)
                {
                    debugContent.Renderers[i].SafeGetMaterials(TEMP_MATERIALS);
                    foreach (var t in TEMP_MATERIALS)
                        t.color = LodSettingsAsset.LODDebugColors[CurrentLODLevel];
                }
            }
            else
            {
                var debugColor = GetDebugColor(debugContent);
                foreach (var debugCube in DebugCubes)
                {
                    debugCube.failingLODCubeMeshRenderer.material.color = debugColor;
                    debugCube.gameObject.SetActive(true);
                }
            }
        }

        private void UpdateState(ref SceneLODInfoDebugContent debugContent, LODAsset lodAsset)
        {
            if (lodAsset.State == LODAsset.LOD_STATE.SUCCESS)
            {
                var renderers = lodAsset.lodGO.GetComponentsInChildren<Renderer>();
                var originalColorsList = new List<Color>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].SafeGetMaterials(TEMP_MATERIALS);
                    foreach (var t in TEMP_MATERIALS)
                        originalColorsList.Add(t.color);
                }

                debugContent.OriginalColors = originalColorsList.ToArray();
                debugContent.Renderers = renderers;
            }

            debugContent.LodState = lodAsset.State;
        }

        private Color GetDebugColor(SceneLODInfoDebugContent debugAsset)
        {
            if (debugAsset.LodState == LODAsset.LOD_STATE.FAILED)
                return LodSettingsAsset.LODDebugColors[CurrentLODLevel];

            //Still in loading state
            return Color.magenta;
        }
    }
}
