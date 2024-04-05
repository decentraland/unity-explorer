using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.LOD
{
    public struct SceneLODInfoDebugContent
    {
        public Color[] OriginalColors;
        public Renderer[] Renderers;
        public GameObject[] FaillingCubesGameObjects;
    }

    public struct SceneLODInfoDebug : IDisposable
    {
        private Dictionary<byte, SceneLODInfoDebugContent> SceneLODInfoDebugContents;
        private Transform MissingSceneParent;
        public byte CurrentLODLevel;

        //This is a sync method, so we can use a shared list
        private static readonly List<Material> TEMP_MATERIALS = new (3);
        
        private void UpdateContent(SceneLODInfoDebugContent newContent, Color debugColor)
        {
            for (int i = 0; i < newContent.Renderers.Length; i++)
            {
                newContent.Renderers[i].SafeGetMaterials(TEMP_MATERIALS);
                foreach (var t in TEMP_MATERIALS)
                    t.color = debugColor;
            }


            foreach (var faillingCubesGameObjects in newContent.FaillingCubesGameObjects)
                faillingCubesGameObjects.gameObject.SetActive(true);
        }

        public void Update(IReadOnlyList<Vector2Int> parcels, LODAsset lodAsset, ILODSettingsAsset lodSettingsAsset)
        {
            if (!lodSettingsAsset.IsColorDebuging)
                return;

            if (!SceneLODInfoDebugContents.TryGetValue(lodAsset.LodKey.Level, out var sceneLODInfoDebugContents))
                sceneLODInfoDebugContents = CreateSceneLODInfoDebugContents(parcels, lodAsset, lodSettingsAsset);
            ClearPreviousContent();
            CurrentLODLevel = lodAsset.LodKey.Level;
            UpdateContent(sceneLODInfoDebugContents, lodSettingsAsset.LODDebugColors[CurrentLODLevel]);
        }

        private void ClearPreviousContent()
        {
            if (CurrentLODLevel != byte.MaxValue)
                ClearContent(SceneLODInfoDebugContents[CurrentLODLevel]);
        }

        private void ClearContent(SceneLODInfoDebugContent debugContent)
        {
            foreach (var faillingCubesGameObject in debugContent.FaillingCubesGameObjects)
                faillingCubesGameObject.gameObject.SetActive(false);
            for (int i = 0; i < debugContent.Renderers.Length; i++)
            {
                debugContent.Renderers[i].SafeGetMaterials(TEMP_MATERIALS);
                for (int j = 0; j < TEMP_MATERIALS.Count; j++)
                {
                    TEMP_MATERIALS[j].color = debugContent.OriginalColors[i + j];
                }
            }

        }

        private SceneLODInfoDebugContent CreateSceneLODInfoDebugContents(IReadOnlyList<Vector2Int> parcels, LODAsset lodAsset, ILODSettingsAsset lodSettingsAsset)
        {
            SceneLODInfoDebugContent sceneLODInfoDebugContents;
            if (lodAsset.LoadingFailed)
            {
                var faillingCubes =  new GameObject[parcels.Count];
                for (int i = 0; i < parcels.Count; i++)
                {
                    faillingCubes[i] = Object.Instantiate(lodSettingsAsset.FaillingCube, ParcelMathHelper.GetPositionByParcelPosition(parcels[i]), Quaternion.identity, MissingSceneParent);
                    faillingCubes[i].gameObject.SetActive(false);
                }

                sceneLODInfoDebugContents  = new SceneLODInfoDebugContent
                {
                    OriginalColors = Array.Empty<Color>(), Renderers = Array.Empty<Renderer>(), FaillingCubesGameObjects = faillingCubes
                };
            }
            else
            {
                var renderers = lodAsset.Root.GetComponentsInChildren<Renderer>();
                var originalColorsList = new List<Color>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].SafeGetMaterials(TEMP_MATERIALS);
                    foreach (var t in TEMP_MATERIALS)
                        originalColorsList.Add(t.color);
                }
                sceneLODInfoDebugContents  = new SceneLODInfoDebugContent
                {
                    OriginalColors = originalColorsList.ToArray(), Renderers = renderers, FaillingCubesGameObjects = Array.Empty<GameObject>()
                };
            }

            SceneLODInfoDebugContents.Add(lodAsset.LodKey.Level, sceneLODInfoDebugContents);
            return sceneLODInfoDebugContents;
        }

        public static SceneLODInfoDebug Create(Transform missingSceneParent)
        {
            return new SceneLODInfoDebug
            {
                SceneLODInfoDebugContents = new Dictionary<byte, SceneLODInfoDebugContent>(), CurrentLODLevel = byte.MaxValue, MissingSceneParent = missingSceneParent
            };
        }

        public void Dispose()
        {
            foreach (var keyValuePair in SceneLODInfoDebugContents)
                ClearContent(keyValuePair.Value);
        }
    }
}