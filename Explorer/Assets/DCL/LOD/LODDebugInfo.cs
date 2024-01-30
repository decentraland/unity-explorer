using UnityEngine;

namespace DCL.LOD
{
    public class LODDebugInfo
    {
        private Color[] OriginalColors;
        private Renderer[] Renderers;
        private ILODSettingsAsset LodSettingsAsset;
        private bool Initialized;

        private void RefreshLODColors(int lodLevelColor)
        {
            if (!Initialized) return;

            if (LodSettingsAsset.IsColorDebuging)
            {
                for (var i = 0; i < Renderers.Length; i++)
                    Renderers[i].material.color = LodSettingsAsset.LODDebugColors[lodLevelColor];
            }
            else
            {
                for (var i = 0; i < Renderers.Length; i++)
                    Renderers[i].material.color = OriginalColors[i];
            }
        }

        public void Update(GameObject root, byte lodKeyLevel, ILODSettingsAsset lodSettingsAsset)
        {
            if (!Initialized)
            {
                LodSettingsAsset = lodSettingsAsset;
                Renderers = root.GetComponentsInChildren<Renderer>();
                OriginalColors = new Color[Renderers.Length];

                for (var i = 0; i < Renderers.Length; i++)
                    OriginalColors[i] = Renderers[i].material.color;

                Initialized = true;
            }

            RefreshLODColors(lodKeyLevel);
        }
    }
}
