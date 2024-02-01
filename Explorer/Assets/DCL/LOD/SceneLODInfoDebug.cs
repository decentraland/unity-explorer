using System;
using System.Collections.Generic;
using NBitcoin.Scripting;
using UnityEngine;

namespace DCL.LOD
{
    public struct SceneLODInfoDebug : IDisposable
    {
        private Dictionary<string, Color[]> OriginalColors;
        private Dictionary<string, Renderer[]> Renderers;
        public LODKey? CurrentLODKey;

        private void RefreshLODColors(Renderer[] renderers, Color[] originalColors, LODKey newKey, ILODSettingsAsset lodSettingsAsset)
        {
            if (lodSettingsAsset.IsColorDebuging)
            {
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].material.color = lodSettingsAsset.LODDebugColors[newKey.Level];
            }
            else
            {
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].material.color = originalColors[i];
            }

            CurrentLODKey = newKey;
        }

        public void Update(LODAsset lodAsset, ILODSettingsAsset lodSettingsAsset)
        {
            string lodKey = lodAsset.LodKey.ToString();
            if (!OriginalColors.ContainsKey(lodKey))
            {
                var renderers = lodAsset.Root.GetComponentsInChildren<Renderer>();
                var originalColors =  new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                    originalColors[i] = renderers[i].material.color;

                OriginalColors[lodKey] = originalColors;
                Renderers[lodKey] = renderers;

                RefreshLODColors(renderers, originalColors, lodAsset.LodKey, lodSettingsAsset);
            }
            else
            {
                RefreshLODColors(Renderers[lodKey], OriginalColors[lodKey], lodAsset.LodKey, lodSettingsAsset);
            }
        }

        public static SceneLODInfoDebug Create()
        {
            return new SceneLODInfoDebug
            {
                OriginalColors = new Dictionary<string, Color[]>(), Renderers = new Dictionary<string, Renderer[]>(), CurrentLODKey = null
            };
        }

        public void Dispose()
        {
            foreach (var keyValuePair in Renderers)
            {
                for (int i = 0; i < keyValuePair.Value.Length; i++)
                {
                    // A renderer in the pool may have been deleted
                    if (keyValuePair.Value[i] == null)
                        continue;

                    keyValuePair.Value[i].material.color = OriginalColors[keyValuePair.Key][i];
                }
            }
        }
    }
}